using System;
using System.Collections.Generic;
using Unity.Mathematics;

namespace Project.World.Editing
{
    /// <summary>
    /// The single write path into voxel data. Keeps the dirty set, and queues every chunk whose mesh
    /// depends on a changed voxel, including the neighbours a border voxel bleeds into.
    /// </summary>
    public sealed class VoxelEditService
    {
        private readonly ChunkStore _store;
        private readonly VoxelMaterialCatalog _catalog;
        private readonly Action<ChunkKey> _remesh;
        private readonly HashSet<ChunkKey> _touched = new HashSet<ChunkKey>();

        public VoxelEditService(ChunkStore store, VoxelMaterialCatalog catalog, Action<ChunkKey> remesh)
        {
            _store = store ?? throw new ArgumentNullException(nameof(store));
            _catalog = catalog;
            _remesh = remesh;
        }

        public bool TryGet(int3 world, out Voxel voxel)
        {
            ChunkKey key = ChunkKey.FromWorldVoxel(world.x, world.y, world.z);
            if (!_store.TryGet(key, out Chunk chunk))
            {
                voxel = Voxel.Air;
                return false;
            }

            int3 origin = key.OriginVoxel;
            voxel = chunk.Get(world.x - origin.x, world.y - origin.y, world.z - origin.z);
            return true;
        }

        /// <summary>True when the voxel is loaded terrain that can hold weight (water does not).</summary>
        public bool IsSupporting(int3 world)
        {
            return TryGet(world, out Voxel voxel) && _catalog != null && _catalog.SupportsTerrain(voxel.MaterialId);
        }

        public bool IsLoaded(int3 world)
        {
            return _store.TryGet(ChunkKey.FromWorldVoxel(world.x, world.y, world.z), out _);
        }

        public bool Set(int3 world, Voxel voxel)
        {
            ChunkKey key = ChunkKey.FromWorldVoxel(world.x, world.y, world.z);
            if (!_store.TryGet(key, out Chunk chunk))
            {
                return false;
            }

            int3 origin = key.OriginVoxel;
            int lx = world.x - origin.x;
            int ly = world.y - origin.y;
            int lz = world.z - origin.z;

            chunk.Set(lx, ly, lz, voxel);
            _store.MarkDirty(key);
            MarkNeighbourhood(key, lx, ly, lz);
            return true;
        }

        /// <summary>
        /// Damages every solid voxel in a sphere; voxels past their material's damage limit are
        /// removed and appended to <paramref name="removed"/>.
        /// </summary>
        public int Dig(int3 center, int radius, int damage, List<int3> removed)
        {
            removed.Clear();
            int radiusSq = radius * radius;

            for (int dz = -radius; dz <= radius; dz++)
            {
                for (int dy = -radius; dy <= radius; dy++)
                {
                    for (int dx = -radius; dx <= radius; dx++)
                    {
                        if (dx * dx + dy * dy + dz * dz > radiusSq)
                        {
                            continue;
                        }

                        var position = new int3(center.x + dx, center.y + dy, center.z + dz);
                        if (position.y <= 0)
                        {
                            continue;
                        }

                        if (!TryGet(position, out Voxel voxel) || voxel.IsAir)
                        {
                            continue;
                        }

                        VoxelMaterial material = _catalog != null ? _catalog.Get(voxel.MaterialId) : null;
                        if (material == null || !material.SupportsTerrain)
                        {
                            continue;
                        }

                        int next = voxel.Damage + damage;
                        if (next >= math.max(1, material.MaxDamage))
                        {
                            Set(position, Voxel.Air);
                            removed.Add(position);
                        }
                        else
                        {
                            voxel.Damage = (byte)next;
                            Set(position, voxel);
                        }
                    }
                }
            }

            return removed.Count;
        }

        /// <summary>Fills air and water inside a sphere with <paramref name="materialId"/>.</summary>
        public int Place(int3 center, int radius, byte materialId)
        {
            int radiusSq = radius * radius;
            int placed = 0;

            for (int dz = -radius; dz <= radius; dz++)
            {
                for (int dy = -radius; dy <= radius; dy++)
                {
                    for (int dx = -radius; dx <= radius; dx++)
                    {
                        if (dx * dx + dy * dy + dz * dz > radiusSq)
                        {
                            continue;
                        }

                        var position = new int3(center.x + dx, center.y + dy, center.z + dz);
                        if (!TryGet(position, out Voxel voxel))
                        {
                            continue;
                        }

                        bool replaceable = voxel.IsAir || (_catalog != null && _catalog.IsLiquid(voxel.MaterialId));
                        if (!replaceable)
                        {
                            continue;
                        }

                        Set(position, new Voxel { MaterialId = materialId });
                        placed++;
                    }
                }
            }

            return placed;
        }

        /// <summary>Pushes every queued chunk into the streamer. Call once per edit batch.</summary>
        public void FlushRemesh()
        {
            if (_remesh != null)
            {
                foreach (ChunkKey key in _touched)
                {
                    _remesh(key);
                }
            }

            _touched.Clear();
        }

        private void MarkNeighbourhood(ChunkKey key, int lx, int ly, int lz)
        {
            int last = WorldMetrics.ChunkSize - 1;

            int minX = lx == 0 ? -1 : 0;
            int maxX = lx == last ? 1 : 0;
            int minY = ly == 0 ? -1 : 0;
            int maxY = ly == last ? 1 : 0;
            int minZ = lz == 0 ? -1 : 0;
            int maxZ = lz == last ? 1 : 0;

            for (int dz = minZ; dz <= maxZ; dz++)
            {
                for (int dy = minY; dy <= maxY; dy++)
                {
                    for (int dx = minX; dx <= maxX; dx++)
                    {
                        _touched.Add(new ChunkKey(key.X + dx, key.Y + dy, key.Z + dz));
                    }
                }
            }
        }
    }
}
