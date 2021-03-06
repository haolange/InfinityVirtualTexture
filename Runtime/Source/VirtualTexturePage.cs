using System;
using Unity.Mathematics;
using Unity.Collections;
using System.Diagnostics;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Unity.Collections.LowLevel.Unsafe;

namespace Landscape.RuntimeVirtualTexture
{
    internal struct FPage
    {
        public bool isNull;
        public int mipLevel;
        public FRectInt rect;
        public FPagePayload payload;

        public FPage(int x, int y, int width, int height, int mipLevel, bool isNull = false)
        {
            this.rect = new FRectInt(x, y, width, height);
            this.isNull = isNull;
            this.mipLevel = mipLevel;
            this.payload = new FPagePayload();
            this.payload.pageCoord = new int2(-1, -1);
            this.payload.notLoading = true;
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
            int hash = NativeExtensions.Combine(rect.GetHashCode(), payload.GetHashCode());
            hash = NativeExtensions.Combine(hash, mipLevel.GetHashCode());
            hash += (isNull ? 0 : 1);
            return hash;
        }
    }

    internal struct FPagePayload
    {
        internal int activeFrame;
        internal bool notLoading;
        internal int2 pageCoord;
        private static readonly int2 s_InvalidTileIndex = new int2(-1, -1);
        internal bool isReady { get { return (!pageCoord.Equals(s_InvalidTileIndex)); } }


        public void ResetTileIndex()
        {
            pageCoord = s_InvalidTileIndex;
        }

        public bool Equals(in FPagePayload target)
        {
            return isReady.Equals(target.isReady) && activeFrame.Equals(target.activeFrame) && pageCoord.Equals(target.pageCoord) && notLoading.Equals(target.notLoading);
        }

        public override bool Equals(object target)
        {
            return Equals((FPagePayload)target);
        }

        public override int GetHashCode()
        {
            int hash = NativeExtensions.Combine(pageCoord.GetHashCode(), activeFrame.GetHashCode());
            hash = NativeExtensions.Combine(hash, notLoading.GetHashCode());
            hash += (isReady ? 0 : 1);
            return hash;
        }
    }

    internal struct FPageLoadInfo : IComparable<FPageLoadInfo>
    {
        internal int x;
        internal int y;
        internal int mipLevel;

        public FPageLoadInfo(in int x, in int y, in int mipLevel)
        {
            this.x = x;
            this.y = y;
            this.mipLevel = mipLevel;
        }

        public bool Equals(in FPageLoadInfo target)
        {
            return target.x == x && target.y == y && target.mipLevel == mipLevel;
        }

        public bool NotEquals(FPageLoadInfo target)
        {
            return target.x != x || target.y != y || target.mipLevel != mipLevel;
        }

        public override bool Equals(object target)
        {
            return Equals((FPageLoadInfo)target);
        }

        public override int GetHashCode()
        {
            return x.GetHashCode() + y.GetHashCode() + mipLevel.GetHashCode();
        }

        public int CompareTo(FPageLoadInfo target)
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
        [NativeDisableUnsafePtrRestriction]
        internal FPage* pageBuffer;

        public FPageTable(in int mipLevel, in int tableSize)
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
