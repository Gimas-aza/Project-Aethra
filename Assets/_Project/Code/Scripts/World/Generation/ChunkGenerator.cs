using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace Project.World.Generation
{
    /// <summary>
    /// Runs the stage list for one chunk. Column stages are cached per (chunkX, chunkZ), so a whole
    /// vertical stack pays for the macro fields once.
    /// </summary>
    public sealed class ChunkGenerator : IDisposable
    {
        private readonly WorldGenContext _context;
        private readonly WorldGenScratch _scratch;
        private readonly IWorldGenStage[] _columnStages;
        private readonly IWorldGenStage[] _voxelStages;
        private readonly HydrologyStage _hydrology;

        private int2 _columnKey = new int2(int.MinValue, int.MinValue);
        private bool _columnsValid;

        public ChunkGenerator(WorldSession session, VoxelMaterialCatalog catalog)
        {
            if (session == null)
            {
                throw new ArgumentNullException(nameof(session));
            }

            WorldGenParams parameters = WorldGenParams.FromSettings(session.Settings);
            Shape = new WorldShape(parameters);

            _scratch = new WorldGenScratch(parameters, Shape);
            _context = new WorldGenContext(session, catalog);

            _columnStages = new IWorldGenStage[]
            {
                new MacroMaskStage(_scratch),
                new ClimateStage(_scratch),
                new HeightStage(_scratch),
                new BiomeAssignStage(_scratch)
            };

            _hydrology = new HydrologyStage(_scratch);

            _voxelStages = new IWorldGenStage[]
            {
                new StrataFillStage(_scratch),
                _hydrology,
                new CaveWormStage(_scratch),
                new ScatterStage(_scratch)
            };
        }

        public WorldShape Shape { get; }
        public WorldGenParams Params => _scratch.Params;

        /// <summary>Props emitted by the most recent <see cref="Generate"/> call.</summary>
        public NativeList<ScatterPlacement> LastPlacements => _scratch.Placements;

        public void Generate(Chunk chunk)
        {
            _scratch.BeginChunk(chunk.Key);

            var columnKey = new int2(chunk.Key.X, chunk.Key.Z);
            if (!_columnsValid || !columnKey.Equals(_columnKey))
            {
                for (int i = 0; i < _columnStages.Length; i++)
                {
                    _columnStages[i].Apply(chunk, _context);
                }

                _columnKey = columnKey;
                _columnsValid = true;
                _scratch.MarkColumnsValid();
            }

            for (int i = 0; i < _voxelStages.Length; i++)
            {
                _voxelStages[i].Apply(chunk, _context);
            }

            Finalize(chunk);
        }

        private void Finalize(Chunk chunk)
        {
            NativeArray<Voxel> voxels = chunk.Voxels;

            var summary = new SolidCountJob
            {
                Voxels = voxels,
                Result = _scratch.SolidCounter
            };

            summary.Schedule().Complete();

            if (_scratch.SolidCounter[0] > 0)
            {
                chunk.MarkNotUniformAir();
            }

            // Generated content is reproducible from the seed; only player edits count as dirty.
            chunk.IsDirty = false;
        }

        public void Dispose()
        {
            _hydrology.InvalidateColumnCache();
            _scratch.Dispose();
            Shape.Dispose();
        }

        [BurstCompile]
        private struct SolidCountJob : IJob
        {
            [ReadOnly] public NativeArray<Voxel> Voxels;
            public NativeArray<int> Result;

            public void Execute()
            {
                int count = 0;
                for (int i = 0; i < Voxels.Length; i++)
                {
                    if (Voxels[i].MaterialId != VoxelIds.Air)
                    {
                        count++;
                    }
                }

                Result[0] = count;
            }
        }
    }
}
