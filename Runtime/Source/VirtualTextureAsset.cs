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
        internal RenderTexture physicsTextureA;
        internal RenderTexture physicsTextureB;
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

            RenderTextureDescriptor textureDesctiptor = new RenderTextureDescriptor { width = TextureSize, height = TextureSize, volumeDepth = 1, dimension = TextureDimension.Tex2D, colorFormat = RenderTextureFormat.RGB565, depthBufferBits = 0, mipCount = -1, useMipMap = false, autoGenerateMips = false, bindMS = false, msaaSamples = 1 };

            //physics texture
            physicsTextureA = new RenderTexture(textureDesctiptor);
            physicsTextureA.name = "PhysicsTextureA";
            physicsTextureA.filterMode = FilterMode.Bilinear;
            physicsTextureA.wrapMode = TextureWrapMode.Clamp;

            physicsTextureB = new RenderTexture(textureDesctiptor);
            physicsTextureB.name = "PhysicsTextureB";
            physicsTextureB.filterMode = FilterMode.Bilinear;
            physicsTextureB.wrapMode = TextureWrapMode.Clamp;

            colorBuffers = new RenderTargetIdentifier[2];
            colorBuffers[0] = new RenderTargetIdentifier(physicsTextureA);
            colorBuffers[1] = new RenderTargetIdentifier(physicsTextureB);

            pageTableTexture = new RenderTexture(pageSize, pageSize, 0, GraphicsFormat.R8G8B8A8_UNorm);
            pageTableTexture.name = "PageTableTexture";
            pageTableTexture.filterMode = FilterMode.Point;
            pageTableTexture.wrapMode = TextureWrapMode.Clamp;

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

            physicsTextureA.Release();
            physicsTextureB.Release();
            pageTableTexture.Release();
            Object.DestroyImmediate(physicsTextureA);
            Object.DestroyImmediate(physicsTextureB);
            Object.DestroyImmediate(pageTableTexture);
        }
    }
}