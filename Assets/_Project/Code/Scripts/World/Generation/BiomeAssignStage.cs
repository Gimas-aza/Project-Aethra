using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace Project.World.Generation
{
    /// <summary>
    /// Climate + height to biome, and biome to surface materials. Thresholds are dithered with a
    /// high-frequency noise so biome joints break up instead of following a hard contour.
    /// </summary>
    public sealed class BiomeAssignStage : IWorldGenColumnStage
    {
        private readonly WorldGenScratch _scratch;

        public BiomeAssignStage(WorldGenScratch scratch)
        {
            _scratch = scratch;
        }

        public string Name => "BiomeAssign";

        public void Apply(Chunk chunk, in WorldGenContext context)
        {
            int3 origin = chunk.Key.OriginVoxel;

            var job = new Job
            {
                Params = _scratch.Params,
                OriginXz = new int2(origin.x, origin.z),
                Columns = _scratch.Columns
            };

            job.Schedule(ColumnSample.ColumnCount, 32).Complete();
        }

        [BurstCompile]
        private struct Job : IJobParallelFor
        {
            public WorldGenParams Params;
            public int2 OriginXz;
            public NativeArray<ColumnSample> Columns;

            public void Execute(int index)
            {
                int x = index % WorldMetrics.ChunkSize;
                int z = index / WorldMetrics.ChunkSize;
                float2 xz = new float2(OriginXz.x + x, OriginXz.y + z);

                ColumnSample column = Columns[index];

                float temperature = WorldSampler.CoolByAltitude(column.Temperature, column.Height, Params);
                ClimateBand band = WorldSampler.BandOf(temperature);
                column.Temperature = temperature;
                column.Climate = (byte)band;

                // Dither the thresholds so borders interlock instead of forming a clean arc.
                float jitter = WorldNoise.Fbm(xz * 0.075f, 2, 2f, 0.5f) * 0.055f;
                float moisture = math.saturate(column.Moisture + jitter);
                float above = column.Height - Params.SeaLevel;
                bool cold = band == ClimateBand.Cold;

                BiomeId biome;
                byte surface;
                byte subsurface;

                if (column.Height <= Params.SeaLevel)
                {
                    biome = BiomeId.Ocean;
                    surface = column.Height > Params.SeaLevel - 6f ? VoxelIds.Sand : VoxelIds.Dirt;
                    subsurface = VoxelIds.Dirt;
                }
                else if (above <= 2.2f + jitter * 8f)
                {
                    biome = BiomeId.Beach;
                    surface = VoxelIds.Sand;
                    subsurface = VoxelIds.Sand;
                }
                else if (above > 44f)
                {
                    biome = BiomeId.SnowMountains;
                    surface = cold || above > 58f ? VoxelIds.Snow : VoxelIds.Stone;
                    subsurface = VoxelIds.Stone;
                }
                else if (above > 30f)
                {
                    biome = BiomeId.SnowMountains;
                    surface = cold ? VoxelIds.Snow : VoxelIds.Stone;
                    subsurface = VoxelIds.Stone;
                }
                else
                {
                    if (moisture > 0.62f)
                    {
                        biome = BiomeId.DarkForest;
                    }
                    else if (moisture > 0.42f)
                    {
                        biome = BiomeId.Forest;
                    }
                    else
                    {
                        biome = BiomeId.Meadow;
                    }

                    surface = cold ? VoxelIds.Snow : VoxelIds.Grass;
                    subsurface = VoxelIds.Dirt;
                }

                column.Biome = (byte)biome;
                column.SurfaceMaterial = surface;
                column.SubsurfaceMaterial = subsurface;
                Columns[index] = column;
            }
        }
    }
}
