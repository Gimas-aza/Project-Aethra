using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace Project.World.Generation
{
    /// <summary>
    /// Fills solid voxels: topsoil from the biome, then warped stone bands, bedrock at the floor.
    /// Copper seeds near the surface of dark forest.
    /// </summary>
    public sealed class StrataFillStage : IWorldGenStage
    {
        private readonly WorldGenScratch _scratch;

        public StrataFillStage(WorldGenScratch scratch)
        {
            _scratch = scratch;
        }

        public string Name => "StrataFill";

        public void Apply(Chunk chunk, in WorldGenContext context)
        {
            NativeArray<Voxel> voxels = chunk.Voxels;

            var job = new Job
            {
                Params = _scratch.Params,
                Origin = chunk.Key.OriginVoxel,
                Columns = _scratch.Columns,
                Voxels = voxels
            };

            job.Schedule(WorldMetrics.ChunkVolume, 128).Complete();
        }

        [BurstCompile]
        private struct Job : IJobParallelFor
        {
            public WorldGenParams Params;
            public int3 Origin;
            [ReadOnly] public NativeArray<ColumnSample> Columns;
            public NativeArray<Voxel> Voxels;

            public void Execute(int index)
            {
                // WorldMetrics.Index(x, y, z) == y + 16 * (x + 16 * z)
                int y = index % WorldMetrics.ChunkSize;
                int x = index / WorldMetrics.ChunkSize % WorldMetrics.ChunkSize;
                int z = index / (WorldMetrics.ChunkSize * WorldMetrics.ChunkSize);

                int wy = Origin.y + y;
                if (wy >= Params.WorldHeight)
                {
                    Voxels[index] = Voxel.Air;
                    return;
                }

                if (wy <= 0)
                {
                    Voxels[index] = new Voxel { MaterialId = VoxelIds.Granite };
                    return;
                }

                ColumnSample column = Columns[ColumnSample.ColumnIndex(x, z)];
                if (wy > column.SurfaceY)
                {
                    Voxels[index] = Voxel.Air;
                    return;
                }

                int wx = Origin.x + x;
                int wz = Origin.z + z;
                int depth = column.SurfaceY - wy;

                byte material;
                if (depth == 0)
                {
                    material = column.SurfaceMaterial;
                }
                else if (depth <= 3)
                {
                    material = column.SubsurfaceMaterial;
                }
                else
                {
                    float warp = WorldNoise.Fbm(new float3(wx, wy * 1.6f, wz) * 0.028f, 3, 2f, 0.5f) * 7f;
                    float band = wy + warp;

                    if (band < 10f)
                    {
                        material = VoxelIds.Granite;
                    }
                    else if (band < 24f)
                    {
                        material = VoxelIds.Slate;
                    }
                    else
                    {
                        material = VoxelIds.Stone;
                    }

                    if (column.Biome == (byte)BiomeId.DarkForest && depth <= 10 &&
                        WorldNoise.Hash01(wx, wy, wz, Params.Seed + 91) > 0.968f)
                    {
                        material = VoxelIds.CopperOre;
                    }
                }

                Voxels[index] = new Voxel { MaterialId = material };
            }
        }
    }
}
