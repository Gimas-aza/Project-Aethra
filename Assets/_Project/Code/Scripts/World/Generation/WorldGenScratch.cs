using System;
using Unity.Collections;
using Unity.Mathematics;

namespace Project.World.Generation
{
    /// <summary>
    /// Buffers shared by every stage of a single generation pass. Column data is cached per
    /// (chunkX, chunkZ) so the vertical stack of chunks pays for the macro fields only once.
    /// </summary>
    public sealed class WorldGenScratch : IDisposable
    {
        private int2 _columnKey;
        private bool _columnsValid;

        public WorldGenScratch(in WorldGenParams parameters, WorldShape shape)
        {
            Params = parameters;
            Shape = shape;
            Columns = new NativeArray<ColumnSample>(ColumnSample.ColumnCount, Allocator.Persistent);
            Placements = new NativeList<ScatterPlacement>(32, Allocator.Persistent);
            ActiveWorms = new NativeList<WormSegment>(64, Allocator.Persistent);
            SolidCounter = new NativeArray<int>(1, Allocator.Persistent);
        }

        public WorldGenParams Params { get; }
        public WorldShape Shape { get; }

        public NativeArray<ColumnSample> Columns;
        public NativeList<ScatterPlacement> Placements;
        public NativeList<WormSegment> ActiveWorms;
        public NativeArray<int> SolidCounter;

        public ChunkKey Key { get; private set; }
        public int3 OriginVoxel { get; private set; }

        /// <summary>True when the cached column data already matches this chunk's (x, z).</summary>
        public bool ColumnsValid => _columnsValid;

        public void BeginChunk(ChunkKey key)
        {
            Key = key;
            OriginVoxel = key.OriginVoxel;

            int2 columnKey = new int2(key.X, key.Z);
            if (!_columnsValid || !columnKey.Equals(_columnKey))
            {
                _columnKey = columnKey;
                _columnsValid = false;
            }

            Placements.Clear();
            SolidCounter[0] = 0;
        }

        public void MarkColumnsValid()
        {
            _columnsValid = true;
        }

        public void InvalidateColumns()
        {
            _columnsValid = false;
        }

        public void Dispose()
        {
            if (Columns.IsCreated)
            {
                Columns.Dispose();
            }

            if (Placements.IsCreated)
            {
                Placements.Dispose();
            }

            if (ActiveWorms.IsCreated)
            {
                ActiveWorms.Dispose();
            }

            if (SolidCounter.IsCreated)
            {
                SolidCounter.Dispose();
            }
        }
    }
}
