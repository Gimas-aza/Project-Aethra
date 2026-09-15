using System;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace Project.World.Meshing
{
    /// <summary>
    /// Catalog data flattened into Burst-readable lookup tables, shared by the chunk mesher and the
    /// falling-debris mesher.
    /// </summary>
    public sealed class VoxelMaterialTables : IDisposable
    {
        public const int Size = 256;

        private NativeArray<byte> _solidFlags;
        private NativeArray<byte> _liquidFlags;
        private NativeArray<float4> _colors;

        public VoxelMaterialTables(VoxelMaterialCatalog catalog)
        {
            _solidFlags = new NativeArray<byte>(Size, Allocator.Persistent);
            _liquidFlags = new NativeArray<byte>(Size, Allocator.Persistent);
            _colors = new NativeArray<float4>(Size, Allocator.Persistent);

            for (int id = 0; id < Size; id++)
            {
                VoxelMaterial material = catalog != null ? catalog.Get((byte)id) : null;
                bool liquid = material != null && material.IsLiquid;
                bool solid = id != VoxelIds.Air && material != null && material.SupportsTerrain;

                _solidFlags[id] = (byte)(solid ? 1 : 0);
                _liquidFlags[id] = (byte)(liquid ? 1 : 0);

                Color color = material != null ? material.Color : Color.magenta;
                _colors[id] = new float4(color.r, color.g, color.b, color.a);
            }
        }

        public NativeArray<byte> SolidFlags => _solidFlags;
        public NativeArray<byte> LiquidFlags => _liquidFlags;
        public NativeArray<float4> Colors => _colors;

        public void Dispose()
        {
            if (_solidFlags.IsCreated)
            {
                _solidFlags.Dispose();
            }

            if (_liquidFlags.IsCreated)
            {
                _liquidFlags.Dispose();
            }

            if (_colors.IsCreated)
            {
                _colors.Dispose();
            }
        }
    }
}
