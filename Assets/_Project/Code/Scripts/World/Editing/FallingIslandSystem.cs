using System;
using System.Collections.Generic;
using Project.World.Meshing;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace Project.World.Editing
{
    /// <summary>
    /// Turns an unsupported voxel component into a pooled rigidbody with a greedy box compound
    /// collider, then stamps it back into the grid once it has rested.
    /// </summary>
    public sealed class FallingIslandSystem : IDisposable
    {
        private const float MassPerVoxel = 0.6f;
        private const int MaxStampLift = 48;
        private const float DespawnBelowY = -12f;
        private const float MaxFallSeconds = 25f;

        private readonly VoxelEditService _edit;
        private readonly VoxelMaterialCatalog _catalog;
        private readonly VoxelMaterialTables _tables;
        private readonly Transform _root;
        private readonly Material _material;
        private readonly int _layer;
        private readonly float _restDelay;

        private readonly Stack<FallingIsland> _pool = new Stack<FallingIsland>();
        private readonly List<FallingIsland> _active = new List<FallingIsland>();
        private readonly List<FallingIsland.BoxBounds> _boxes = new List<FallingIsland.BoxBounds>();

        public FallingIslandSystem(
            VoxelEditService edit,
            VoxelMaterialCatalog catalog,
            VoxelMaterialTables tables,
            Transform root,
            Material material,
            int layer,
            float restDelay)
        {
            _edit = edit;
            _catalog = catalog;
            _tables = tables;
            _root = root;
            _material = material;
            _layer = layer;
            _restDelay = restDelay;
        }

        public int ActiveCount => _active.Count;

        public bool Spawn(List<int3> voxels)
        {
            if (voxels == null || voxels.Count == 0)
            {
                return false;
            }

            int3 min = voxels[0];
            int3 max = voxels[0];
            for (int i = 1; i < voxels.Count; i++)
            {
                min = math.min(min, voxels[i]);
                max = math.max(max, voxels[i]);
            }

            int3 size = max - min + 1;
            int volume = size.x * size.y * size.z;
            var materials = new byte[volume];

            for (int i = 0; i < voxels.Count; i++)
            {
                if (!_edit.TryGet(voxels[i], out Voxel voxel) || voxel.IsAir)
                {
                    continue;
                }

                int3 local = voxels[i] - min;
                materials[LocalIndex(local, size)] = voxel.MaterialId;
            }

            // Cut the lump out of the world before it starts falling.
            for (int i = 0; i < voxels.Count; i++)
            {
                _edit.Set(voxels[i], Voxel.Air);
            }

            _edit.FlushRemesh();

            BuildBoxes(materials, size);
            if (_boxes.Count == 0)
            {
                return false;
            }

            FallingIsland island = Rent();
            BuildMesh(island.Mesh, materials, size);
            island.Activate(min, size, materials, _boxes, math.max(1f, voxels.Count * MassPerVoxel), _restDelay);
            _active.Add(island);
            return true;
        }

        public void Tick(float deltaTime)
        {
            for (int i = _active.Count - 1; i >= 0; i--)
            {
                FallingIsland island = _active[i];
                island.TickRest(deltaTime);

                // Debris that drops out of the world (or into a region whose colliders have been
                // streamed out) is discarded rather than stamped somewhere arbitrary.
                bool lost = island.transform.position.y < DespawnBelowY || island.Age > MaxFallSeconds;

                if (!island.HasSettled && !lost)
                {
                    continue;
                }

                if (!lost)
                {
                    Stamp(island);
                }

                _active.RemoveAt(i);
                island.Recycle();
                _pool.Push(island);
            }
        }

        private void Stamp(FallingIsland island)
        {
            int3 size = island.Size;
            byte[] materials = island.Materials;
            Transform transform = island.transform;

            for (int z = 0; z < size.z; z++)
            {
                for (int y = 0; y < size.y; y++)
                {
                    for (int x = 0; x < size.x; x++)
                    {
                        byte material = materials[LocalIndex(new int3(x, y, z), size)];
                        if (material == VoxelIds.Air)
                        {
                            continue;
                        }

                        Vector3 world = transform.TransformPoint(new Vector3(x + 0.5f, y + 0.5f, z + 0.5f));
                        var target = new int3(
                            Mathf.FloorToInt(world.x),
                            math.max(1, Mathf.FloorToInt(world.y)),
                            Mathf.FloorToInt(world.z));

                        for (int lift = 0; lift < MaxStampLift; lift++)
                        {
                            if (!_edit.TryGet(target, out Voxel existing))
                            {
                                break;
                            }

                            bool displaceable = existing.IsAir ||
                                                (_catalog != null && _catalog.IsLiquid(existing.MaterialId));

                            if (displaceable)
                            {
                                _edit.Set(target, new Voxel { MaterialId = material });
                                break;
                            }

                            target.y++;
                        }
                    }
                }
            }

            _edit.FlushRemesh();
        }

        private FallingIsland Rent()
        {
            if (_pool.Count > 0)
            {
                return _pool.Pop();
            }

            return FallingIsland.Create(_root, _material, _layer);
        }

        /// <summary>Merges the occupied voxels into as few axis-aligned boxes as possible.</summary>
        private void BuildBoxes(byte[] materials, int3 size)
        {
            _boxes.Clear();

            var used = new bool[materials.Length];

            for (int z = 0; z < size.z; z++)
            {
                for (int y = 0; y < size.y; y++)
                {
                    for (int x = 0; x < size.x; x++)
                    {
                        int index = LocalIndex(new int3(x, y, z), size);
                        if (used[index] || materials[index] == VoxelIds.Air)
                        {
                            continue;
                        }

                        int width = 1;
                        while (x + width < size.x && IsFree(materials, used, size, x + width, y, z))
                        {
                            width++;
                        }

                        int height = 1;
                        while (y + height < size.y && RowFree(materials, used, size, x, width, y + height, z))
                        {
                            height++;
                        }

                        int depth = 1;
                        while (z + depth < size.z && SlabFree(materials, used, size, x, width, y, height, z + depth))
                        {
                            depth++;
                        }

                        for (int dz = 0; dz < depth; dz++)
                        {
                            for (int dy = 0; dy < height; dy++)
                            {
                                for (int dx = 0; dx < width; dx++)
                                {
                                    used[LocalIndex(new int3(x + dx, y + dy, z + dz), size)] = true;
                                }
                            }
                        }

                        _boxes.Add(new FallingIsland.BoxBounds
                        {
                            Min = new int3(x, y, z),
                            Size = new int3(width, height, depth)
                        });
                    }
                }
            }
        }

        private void BuildMesh(Mesh mesh, byte[] materials, int3 size)
        {
            int3 fieldExtent = Meshing.SurfaceNetsJob.FieldExtent(size);
            int3 cellExtent = Meshing.SurfaceNetsJob.CellExtent(size);

            var field = new NativeArray<byte>(fieldExtent.x * fieldExtent.y * fieldExtent.z, Allocator.TempJob);
            var cellVertex = new NativeArray<int>(cellExtent.x * cellExtent.y * cellExtent.z, Allocator.TempJob);
            var vertices = new NativeList<float3>(512, Allocator.TempJob);
            var normals = new NativeList<float3>(512, Allocator.TempJob);
            var colors = new NativeList<float4>(512, Allocator.TempJob);
            var indices = new NativeList<int>(1536, Allocator.TempJob);

            for (int z = 0; z < size.z; z++)
            {
                for (int y = 0; y < size.y; y++)
                {
                    for (int x = 0; x < size.x; x++)
                    {
                        int fieldIndex = x + 1 + fieldExtent.x * (y + 1 + fieldExtent.y * (z + 1));
                        field[fieldIndex] = materials[LocalIndex(new int3(x, y, z), size)];
                    }
                }
            }

            var job = new Meshing.SurfaceNetsJob
            {
                Size = size,
                Field = field,
                SolidFlags = _tables.SolidFlags,
                MaterialColors = _tables.Colors,
                CellVertex = cellVertex,
                Vertices = vertices,
                Normals = normals,
                Colors = colors,
                Indices = indices
            };

            job.Schedule().Complete();
            MeshWriter.Write(mesh, vertices, normals, colors, indices);

            field.Dispose();
            cellVertex.Dispose();
            vertices.Dispose();
            normals.Dispose();
            colors.Dispose();
            indices.Dispose();
        }

        private static bool IsFree(byte[] materials, bool[] used, int3 size, int x, int y, int z)
        {
            int index = LocalIndex(new int3(x, y, z), size);
            return !used[index] && materials[index] != VoxelIds.Air;
        }

        private static bool RowFree(byte[] materials, bool[] used, int3 size, int x, int width, int y, int z)
        {
            for (int i = 0; i < width; i++)
            {
                if (!IsFree(materials, used, size, x + i, y, z))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool SlabFree(byte[] materials, bool[] used, int3 size, int x, int width, int y, int height, int z)
        {
            for (int j = 0; j < height; j++)
            {
                if (!RowFree(materials, used, size, x, width, y + j, z))
                {
                    return false;
                }
            }

            return true;
        }

        private static int LocalIndex(int3 local, int3 size)
        {
            return local.x + size.x * (local.y + size.y * local.z);
        }

        public void Dispose()
        {
            _active.Clear();
            _pool.Clear();
        }
    }
}
