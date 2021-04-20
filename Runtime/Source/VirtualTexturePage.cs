using System;
using UnityEngine;
using Unity.Mathematics;
using Unity.Collections;
using System.Runtime.InteropServices;
using Unity.Collections.LowLevel.Unsafe;
using System.Collections.Generic;
using System.Diagnostics;

namespace Landscape.RuntimeVirtualTexture
{
    internal struct FPage
    {
        internal RectInt rect;
        internal int mipLevel;
        internal bool isNull;
        internal FPagePayload payload;

        public FPage(int x, int y, int width, int height, int mipLevel, bool isNull = false)
        {
            this.rect = new RectInt(x, y, width, height);
            this.mipLevel = mipLevel;
            this.isNull = isNull;
            this.payload = new FPagePayload();
            this.payload.tileIndex = new int2(-1, -1);
            this.payload.pageRequestInfo = new FPageRequestInfo(0, 0, 0, true);
        }

        public static void BuildPage(ref FPage page, int x, int y, int width, int height, int mipLevel, bool isNull = false)
        {
            page.rect = new RectInt(x, y, width, height);
            page.mipLevel = mipLevel;
            page.isNull = isNull;
            page.payload = new FPagePayload();
            page.payload.tileIndex = new int2(-1, -1);
            page.payload.pageRequestInfo = new FPageRequestInfo(0, 0, 0, true);
        }

        public bool Equals(in FPage Target)
        {
            return rect.Equals(Target.rect) && payload.Equals(Target.payload) && mipLevel.Equals(Target.mipLevel) && isNull.Equals(Target.isNull);
        }

        public override bool Equals(object obj)
        {
            return Equals((FPage)obj);
        }

        public override int GetHashCode()
        {
            return rect.GetHashCode() + payload.GetHashCode() + mipLevel.GetHashCode() + (isNull ? 0 : 1);
        }
    }

    internal struct FPagePayload
    {
        internal int2 tileIndex;
        internal int activeFrame;
        internal FPageRequestInfo pageRequestInfo;
        private static readonly int2 s_InvalidTileIndex = new int2(-1, -1);
        internal bool isReady { get { return (!tileIndex.Equals(s_InvalidTileIndex)); } }

        public void ResetTileIndex()
        {
            tileIndex = s_InvalidTileIndex;
        }

        public bool Equals(in FPagePayload target)
        {
            return isReady.Equals(target.isReady) && activeFrame.Equals(target.activeFrame) && tileIndex.Equals(target.tileIndex) && pageRequestInfo.Equals(target.pageRequestInfo);
        }

        public override bool Equals(object target)
        {
            return Equals((FPagePayload)target);
        }

        public override int GetHashCode()
        {
            return tileIndex.GetHashCode() + activeFrame.GetHashCode() + pageRequestInfo.GetHashCode() + (isReady ? 0 : 1);
        }
    }

    internal struct FPageRequestInfo : IComparable<FPageRequestInfo>
    {
        internal int pageX;
        internal int pageY;
        internal int mipLevel;
        internal bool isNull;

        public FPageRequestInfo(in int pageX, in int pageY, in int mipLevel, in bool isNull = false)
        {
            this.pageX = pageX;
            this.pageY = pageY;
            this.isNull = isNull;
            this.mipLevel = mipLevel;
        }

        public bool Equals(in FPageRequestInfo obj)
        {
            return obj.pageX == pageX && obj.pageY == pageY && obj.mipLevel == mipLevel && obj.isNull == isNull;
        }

        public bool NotEquals(FPageRequestInfo obj)
        {
            return obj.pageX != pageX || obj.pageY != pageY || obj.mipLevel != mipLevel && obj.isNull != isNull;
        }

        public override bool Equals(object obj)
        {
            return Equals((FPageRequestInfo)obj);
        }

        public override int GetHashCode()
        {
            return pageX.GetHashCode() + pageY.GetHashCode() + mipLevel.GetHashCode() + (isNull ? 0 : 1);
        }

        public int CompareTo(FPageRequestInfo target)
        {
            return mipLevel.CompareTo(target.mipLevel);
        }
    }

#if UNITY_EDITOR
    internal unsafe sealed class FPageTableDebugView
    {
        FPageTable m_Target;

        public FPageTableDebugView(FPageTable target)
        {
            m_Target = target;
        }

        public int mipLevel
        {
            get
            {
                return m_Target.mipLevel;
            }
        }

        public int cellSize
        {
            get
            {
                return m_Target.cellSize;
            }
        }

        public int cellCount
        {
            get
            {
                return m_Target.cellCount;
            }
        }

        public List<FPage> pageBuffer
        {
            get
            {
                var result = new List<FPage>();
                for (int i = 0; i < m_Target.cellCount * m_Target.cellCount; ++i)
                {
                    result.Add(m_Target.pageBuffer[i]);
                }
                return result;
            }
        }
    }

    [DebuggerTypeProxy(typeof(FPageTableDebugView))]
#endif
    internal unsafe struct FPageTable : IDisposable
    {
        internal int mipLevel;
        internal int cellSize;
        internal int cellCount;
        internal FPage* pageBuffer;

        internal FPageTable(in int mipLevel, in int tableSize)
        {
            this.mipLevel = mipLevel;
            this.cellSize = (int)math.pow(2, mipLevel);
            this.cellCount = tableSize / cellSize;
            this.pageBuffer = (FPage*)UnsafeUtility.Malloc(Marshal.SizeOf(typeof(FPage)) * (cellCount * cellCount), 64, Allocator.Persistent);

            for (int i = 0; i < cellCount; ++i)
            {
                for (int j = 0; j < cellCount; ++j)
                {
                    this.pageBuffer[i * cellCount + j] = new FPage(i * cellSize, j * cellSize, cellSize, cellSize, mipLevel);
                }
            }
        }

        public ref FPage GetPage(in int x, in int y)
        {
            int2 uv = new int2((x / cellSize) % cellCount, (y / cellSize) % cellCount);
            return ref pageBuffer[uv.x * cellCount + uv.y];
        }

        public void Dispose()
        {
            UnsafeUtility.Free((void*)pageBuffer, Allocator.Persistent);
        }
    }
}