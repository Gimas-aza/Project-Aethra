using System;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace Project.World.Meshing
{
    /// <summary>
    /// Schedules a batch of chunk mesh jobs, then applies the results. Sample fields are gathered on
    /// the main thread; Surface Nets and the water surface run on worker threads.
    /// <para>Call order per batch: <see cref="Schedule"/>*, <see cref="CompleteAll"/>,
    /// <see cref="Apply"/>*, <see cref="Reset"/>.</para>
    /// </summary>
    public sealed class ChunkMesher : IDisposable
    {
        private readonly VoxelMaterialTables _tables;
        private readonly Slot[] _slots;

        private int _scheduled;

        public ChunkMesher(VoxelMaterialTables tables, int batchSize)
        {
            _tables = tables;
            _slots = new Slot[math.max(1, batchSize)];

            for (int i = 0; i < _slots.Length; i++)
            {
                _slots[i] = new Slot();
            }
        }

        public int Capacity => _slots.Length;
        public int ScheduledCount => _scheduled;
        public bool HasRoom => _scheduled < _slots.Length;

        public ChunkKey KeyAt(int index)
        {
            return _slots[index].Key;
        }

        public bool Schedule(ChunkKey key, ChunkStore store)
        {
            if (!HasRoom)
            {
                return false;
            }

            Slot slot = _slots[_scheduled];
            slot.Key = key;

            ChunkFieldGather.Gather(key, store, slot.Field);

            var surfaceNets = new SurfaceNetsJob
            {
                Size = new int3(WorldMetrics.ChunkSize),
                Field = slot.Field,
                SolidFlags = _tables.SolidFlags,
                MaterialColors = _tables.Colors,
                CellVertex = slot.CellVertex,
                Vertices = slot.Vertices,
                Normals = slot.Normals,
                Colors = slot.Colors,
                Indices = slot.Indices
            };

            var water = new WaterSurfaceJob
            {
                Field = slot.Field,
                SolidFlags = _tables.SolidFlags,
                LiquidFlags = _tables.LiquidFlags,
                Vertices = slot.WaterVertices,
                Indices = slot.WaterIndices
            };

            slot.Handle = JobHandle.CombineDependencies(surfaceNets.Schedule(), water.Schedule());

            _scheduled++;
            return true;
        }

        public void CompleteAll()
        {
            for (int i = 0; i < _scheduled; i++)
            {
                _slots[i].Handle.Complete();
            }
        }

        public void Apply(int index, Mesh terrainMesh, Mesh waterMesh, out bool hasTerrain, out bool hasWater)
        {
            Slot slot = _slots[index];

            hasTerrain = MeshWriter.Write(terrainMesh, slot.Vertices, slot.Normals, slot.Colors, slot.Indices);
            hasWater = MeshWriter.Write(waterMesh, slot.WaterVertices, default, default, slot.WaterIndices);
        }

        public void Reset()
        {
            _scheduled = 0;
        }

        public void Dispose()
        {
            for (int i = 0; i < _slots.Length; i++)
            {
                _slots[i].Handle.Complete();
                _slots[i].Dispose();
            }
        }

        private sealed class Slot : IDisposable
        {
            public readonly NativeArray<byte> Field;
            public readonly NativeArray<int> CellVertex;
            public readonly NativeList<float3> Vertices;
            public readonly NativeList<float3> Normals;
            public readonly NativeList<float4> Colors;
            public readonly NativeList<int> Indices;
            public readonly NativeList<float3> WaterVertices;
            public readonly NativeList<int> WaterIndices;

            public JobHandle Handle;
            public ChunkKey Key;

            public Slot()
            {
                Field = new NativeArray<byte>(MeshConstants.FieldVolume, Allocator.Persistent);
                CellVertex = new NativeArray<int>(MeshConstants.CellVolume, Allocator.Persistent);
                Vertices = new NativeList<float3>(2048, Allocator.Persistent);
                Normals = new NativeList<float3>(2048, Allocator.Persistent);
                Colors = new NativeList<float4>(2048, Allocator.Persistent);
                Indices = new NativeList<int>(6144, Allocator.Persistent);
                WaterVertices = new NativeList<float3>(256, Allocator.Persistent);
                WaterIndices = new NativeList<int>(384, Allocator.Persistent);
            }

            public void Dispose()
            {
                if (Field.IsCreated)
                {
                    Field.Dispose();
                }

                if (CellVertex.IsCreated)
                {
                    CellVertex.Dispose();
                }

                if (Vertices.IsCreated)
                {
                    Vertices.Dispose();
                }

                if (Normals.IsCreated)
                {
                    Normals.Dispose();
                }

                if (Colors.IsCreated)
                {
                    Colors.Dispose();
                }

                if (Indices.IsCreated)
                {
                    Indices.Dispose();
                }

                if (WaterVertices.IsCreated)
                {
                    WaterVertices.Dispose();
                }

                if (WaterIndices.IsCreated)
                {
                    WaterIndices.Dispose();
                }
            }
        }
    }
}
