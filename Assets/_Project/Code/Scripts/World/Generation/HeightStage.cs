using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace Project.World.Generation
{
    /// <summary>FBM base relief plus ridged mountains, resolved to an integer surface voxel.</summary>
    public sealed class HeightStage : IWorldGenColumnStage
    {
        private readonly WorldGenScratch _scratch;

        public HeightStage(WorldGenScratch scratch)
        {
            _scratch = scratch;
        }

        public string Name => "Height";

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
                column.Height = WorldSampler.HeightFrom(xz, Params, column.Continent, column.MountainMask);
                column.SurfaceY = (int)math.floor(column.Height);
                column.GroundTopY = column.SurfaceY;
                column.WaterY = -1;
                column.IsCarved = 0;
                Columns[index] = column;
            }
        }
    }
}
