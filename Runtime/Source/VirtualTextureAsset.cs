﻿using System;
using UnityEngine;
using Unity.Mathematics;
using Unity.Collections;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;
using System.Runtime.InteropServices;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine.Experimental.Rendering;

namespace Landscape.RuntimeVirtualTexture
{
    public enum EBorder
    {
        X0 = 0,
        X1 = 1,
        X2 = 2,
        X4 = 4
    }

    [CreateAssetMenu(menuName = "Landscape/VirtualTextureAsset")]
    public unsafe class VirtualTextureAsset : ScriptableObject, IDisposable
    {
        [Range(8, 16)]
        public int tileNum = 16;
        [Range(64, 512)]
        public int tileSize = 256;
        public EBorder tileBorder = EBorder.X2;
        [Range(256, 1024)]
        public int pageSize = 256;

        public int NumMip { get { return (int)math.log2(pageSize) + 1; } }
        public int TileSizePadding { get { return tileSize + (int)tileBorder * 2; } }
        public int QuadTileSizePadding { get { return TileSizePadding / 4; } }

        [HideInInspector]
        public ComputeShader m_Shader;

        internal FLruCache* lruCache;
        internal RenderTexture renderTextureA;
        internal RenderTexture renderTextureB;
        internal RenderTexture tileTextureA;
        internal RenderTexture tileTextureB;
        internal RenderTexture compressTextureA;
        internal RenderTexture compressTextureB;
        internal Texture2D physicsTextureA;
        internal Texture2D physicsTextureB;
        internal Texture2D decodeTextureA;
        internal Texture2D decodeTextureB;
        internal RenderTexture pageTableTexture;
        internal RenderTargetIdentifier[] colorBuffers;
        internal int TextureSize { get { return tileNum * TileSizePadding; } }

        public VirtualTextureAsset()
        {

        }

        public void Initialize()
        {
            lruCache = (FLruCache*)UnsafeUtility.Malloc(Marshal.SizeOf(typeof(FLruCache)) * 1, 64, Allocator.Persistent);
            FLruCache.BuildLruCache(ref lruCache[0], tileNum * tileNum);

            RenderTextureDescriptor textureDesctiptor = new RenderTextureDescriptor { width = TileSizePadding, height = TileSizePadding, volumeDepth = 1, dimension = TextureDimension.Tex2D, colorFormat = RenderTextureFormat.ARGB32, depthBufferBits = 0, mipCount = -1, useMipMap = false, autoGenerateMips = false, bindMS = false, msaaSamples = 1 };

            //rende texture
            renderTextureA = new RenderTexture(textureDesctiptor);
            renderTextureA.name = "RenderTextureA";
            renderTextureA.filterMode = FilterMode.Bilinear;
            renderTextureA.wrapMode = TextureWrapMode.Clamp;

            renderTextureB = new RenderTexture(textureDesctiptor);
            renderTextureB.name = "RenderTextureB";
            renderTextureB.filterMode = FilterMode.Bilinear;
            renderTextureB.wrapMode = TextureWrapMode.Clamp;

            //tile texture
            tileTextureA = new RenderTexture(textureDesctiptor);
            tileTextureA.name = "TileTextureA";
            tileTextureA.filterMode = FilterMode.Bilinear;
            tileTextureA.wrapMode = TextureWrapMode.Clamp;

            tileTextureB = new RenderTexture(textureDesctiptor);
            tileTextureB.name = "TileTextureB";
            tileTextureB.filterMode = FilterMode.Bilinear;
            tileTextureB.wrapMode = TextureWrapMode.Clamp;

            //compress texture
            compressTextureA = new RenderTexture(QuadTileSizePadding, QuadTileSizePadding, 0)
            {
                name = "CompressTextureA",
                graphicsFormat = GraphicsFormat.R32G32B32A32_UInt,
                enableRandomWrite = true,
            };
            compressTextureA.Create();

            compressTextureB = new RenderTexture(QuadTileSizePadding, QuadTileSizePadding, 0)
            {
                name = "CompressTextureB",
                graphicsFormat = GraphicsFormat.R32G32B32A32_UInt,
                enableRandomWrite = true,
            };
            compressTextureB.Create();


            TextureFormat textureFormat;
            #if UNITY_ANDROID && !UNITY_EDITOR
                    textureFormat = TextureFormat.ETC2_RGBA8;
                    m_Shader.DisableKeyword("_COMPRESS_BC3");
                    m_Shader.EnableKeyword("_COMPRESS_ETC2");
            #else
                    textureFormat = TextureFormat.DXT5;
                    m_Shader.DisableKeyword("_COMPRESS_ETC2");
                    m_Shader.EnableKeyword("_COMPRESS_BC3");
            #endif

            //decode texture
            decodeTextureA = new Texture2D(TileSizePadding, TileSizePadding, textureFormat, false, true);
            decodeTextureA.Apply(false, true);
            decodeTextureA.name = "DecodeTextureA";
            decodeTextureA.filterMode = FilterMode.Bilinear;
            decodeTextureA.wrapMode = TextureWrapMode.Clamp;

            decodeTextureB = new Texture2D(TileSizePadding, TileSizePadding, textureFormat, false, true);
            decodeTextureB.Apply(false, true);
            decodeTextureB.name = "DecodeTextureB";
            decodeTextureB.filterMode = FilterMode.Bilinear;
            decodeTextureB.wrapMode = TextureWrapMode.Clamp;

            //physics texture
            physicsTextureA = new Texture2D(TextureSize, TextureSize, textureFormat, false, true);
            physicsTextureA.Apply(false, true);
            physicsTextureA.name = "PhyscisTextureA";
            physicsTextureA.filterMode = FilterMode.Bilinear;
            physicsTextureA.wrapMode = TextureWrapMode.Clamp;
            //physicsTextureA.anisoLevel = 8;

            physicsTextureB = new Texture2D(TextureSize, TextureSize, textureFormat, false, true);
            physicsTextureB.Apply(false, true);
            physicsTextureB.name = "PhyscisTextureB";
            physicsTextureB.filterMode = FilterMode.Bilinear;
            physicsTextureB.wrapMode = TextureWrapMode.Clamp;
            //physicsTextureB.anisoLevel = 8;

            Resources.UnloadUnusedAssets();
            GC.Collect();
            GC.WaitForPendingFinalizers();

            pageTableTexture = new RenderTexture(pageSize, pageSize, 0, GraphicsFormat.R8G8B8A8_UNorm);
            pageTableTexture.name = "PageTableTexture";
            pageTableTexture.filterMode = FilterMode.Point;
            pageTableTexture.wrapMode = TextureWrapMode.Clamp;

            colorBuffers = new RenderTargetIdentifier[2];
            colorBuffers[0] = new RenderTargetIdentifier(renderTextureA);
            colorBuffers[1] = new RenderTargetIdentifier(renderTextureB);

            // 设置Shader参数
            // x: padding 偏移量
            // y: tile 有效区域的尺寸
            // zw: 1/区域尺寸
            Shader.SetGlobalTexture("_PhyscisAlbedo", physicsTextureA);
            Shader.SetGlobalTexture("_PhyscisNormal", physicsTextureB);
            Shader.SetGlobalTexture("_PageTableTexture", pageTableTexture);
            Shader.SetGlobalVector("_VTPageParams", new Vector4(pageSize, 1 / pageSize, NumMip - 1, 0));
            Shader.SetGlobalVector("_VTPageTileParams", new Vector4((float)tileBorder, (float)tileSize, TextureSize, TextureSize));
        }

        public void Dispose()
        {
            lruCache[0].Dispose();
            UnsafeUtility.Free((void*)lruCache, Allocator.Persistent);

            tileTextureA.Release();
            tileTextureB.Release();
            renderTextureA.Release();
            renderTextureB.Release();
            compressTextureA.Release();
            compressTextureB.Release();
            pageTableTexture.Release();

            Object.DestroyImmediate(tileTextureA);
            Object.DestroyImmediate(tileTextureB);
            Object.DestroyImmediate(physicsTextureA);
            Object.DestroyImmediate(physicsTextureB);
            Object.DestroyImmediate(compressTextureA);
            Object.DestroyImmediate(compressTextureB);
            Object.DestroyImmediate(pageTableTexture);
        }
    }
}