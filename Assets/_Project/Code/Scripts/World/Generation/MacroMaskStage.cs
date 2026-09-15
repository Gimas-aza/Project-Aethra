using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace Project.World.Generation
{
    /// <summary>Disk falloff, continent noise and the radial ring index.</summary>
    public sealed class MacroMaskStage : IWorldGenColumnStage
    {
        private readonly WorldGenScratch _scratch;

        public MacroMaskStage(WorldGenScratch scratch)
        {
            _scratch = scratch;
        }

        public string Name => "MacroMask";

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
                column.Continent = WorldSampler.Continent(xz, Params);
                column.MountainMask = WorldSampler.MountainMask(xz, Params);
                column.Ring = WorldSampler.Ring(xz, Params);
                column.IsOcean = (byte)(column.Continent < 0.06f ? 1 : 0);
                Columns[index] = column;
            }
        }
    }
}
