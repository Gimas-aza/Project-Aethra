using System;
using Unity.Collections;

namespace Project.World
{
    public sealed class Chunk : IDisposable
    {
        private NativeArray<Voxel> _voxels;

        public ChunkKey Key { get; }
        public NativeArray<Voxel> Voxels => _voxels;
        public bool IsDirty { get; set; }
        public bool IsUniformAir { get; private set; }

        public Chunk(ChunkKey key)
        {
            Key = key;
            _voxels = new NativeArray<Voxel>(WorldMetrics.ChunkVolume, Allocator.Persistent);
            IsUniformAir = true;
        }

        public Voxel Get(int x, int y, int z)
        {
            return _voxels[WorldMetrics.Index(x, y, z)];
        }

        public void Set(int x, int y, int z, Voxel voxel)
        {
            int index = WorldMetrics.Index(x, y, z);
            _voxels[index] = voxel;
            IsDirty = true;
            if (!voxel.IsAir)
            {
                IsUniformAir = false;
            }
        }

        public void MarkNotUniformAir()
        {
            IsUniformAir = false;
        }

        public void Dispose()
        {
            if (_voxels.IsCreated)
            {
                _voxels.Dispose();
            }
        }
    }
}
