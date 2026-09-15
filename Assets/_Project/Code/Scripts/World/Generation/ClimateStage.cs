using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace Project.World.Generation
{
    /// <summary>Temperature and moisture fields. Altitude cooling is applied later, once height exists.</summary>
    public sealed class ClimateStage : IWorldGenColumnStage
    {
        private readonly WorldGenScratch _scratch;

        public ClimateStage(WorldGenScratch scratch)
        {
            _scratch = scratch;
        }

        public string Name => "Climate";

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
                column.Temperature = WorldSampler.Temperature(xz, Params);
                column.Moisture = WorldSampler.Moisture(xz, Params);
                column.Climate = (byte)WorldSampler.BandOf(column.Temperature);
                Columns[index] = column;
            }
        }
    }
}
