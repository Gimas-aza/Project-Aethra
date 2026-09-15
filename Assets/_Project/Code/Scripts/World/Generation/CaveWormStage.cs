using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace Project.World.Generation
{
    /// <summary>
    /// Carves the worm tunnels from <see cref="WorldShape"/>. Only segments whose bounds touch the
    /// chunk are handed to the job, so most chunks cost a bounds test and nothing else.
    /// </summary>
    public sealed class CaveWormStage : IWorldGenStage
    {
        private readonly WorldGenScratch _scratch;

        public CaveWormStage(WorldGenScratch scratch)
        {
            _scratch = scratch;
        }

        public string Name => "CaveWorm";

        public void Apply(Chunk chunk, in WorldGenContext context)
        {
            int3 origin = chunk.Key.OriginVoxel;
            float3 chunkMin = origin;
            float3 chunkMax = origin + WorldMetrics.ChunkSize;

            NativeList<WormSegment> active = _scratch.ActiveWorms;
            active.Clear();

            NativeArray<WormSegment> worms = _scratch.Shape.Worms;
            for (int i = 0; i < worms.Length; i++)
            {
                WormSegment segment = worms[i];
                float radius = segment.Radius + 1f;
                float3 min = math.min(segment.A, segment.B) - radius;
                float3 max = math.max(segment.A, segment.B) + radius;

                if (math.all(max >= chunkMin) && math.all(min <= chunkMax))
                {
                    active.Add(segment);
                }
            }

            if (active.Length == 0)
            {
                return;
            }

            NativeArray<Voxel> voxels = chunk.Voxels;

            var job = new Job
            {
                Origin = origin,
                Segments = active.AsArray(),
                Columns = _scratch.Columns,
                Voxels = voxels
            };

            job.Schedule(WorldMetrics.ChunkVolume, 128).Complete();
        }

        [BurstCompile]
        private struct Job : IJobParallelFor
        {
            public int3 Origin;
            [ReadOnly] public NativeArray<WormSegment> Segments;
            [ReadOnly] public NativeArray<ColumnSample> Columns;
            public NativeArray<Voxel> Voxels;

            public void Execute(int index)
            {
                Voxel voxel = Voxels[index];
                if (voxel.IsAir)
                {
                    return;
                }

                int y = index % WorldMetrics.ChunkSize;
                int x = index / WorldMetrics.ChunkSize % WorldMetrics.ChunkSize;
                int z = index / (WorldMetrics.ChunkSize * WorldMetrics.ChunkSize);

                int wy = Origin.y + y;
                if (wy <= 0)
                {
                    return;
                }

                // Never open a tunnel into the underside of standing water: without flow simulation
                // that leaves a hole with water floating over it.
                ColumnSample column = Columns[ColumnSample.ColumnIndex(x, z)];
                if (column.WaterY >= 0 && wy > column.GroundTopY - 3)
                {
                    return;
                }

                float3 p = new float3(Origin.x + x + 0.5f, wy + 0.5f, Origin.z + z + 0.5f);

                for (int i = 0; i < Segments.Length; i++)
                {
                    WormSegment segment = Segments[i];
                    float distanceSq = WorldNoise.DistanceToSegmentSq(p, segment.A, segment.B, out _);
                    if (distanceSq <= segment.Radius * segment.Radius)
                    {
                        Voxels[index] = Voxel.Air;
                        return;
                    }
                }
            }
        }
    }
}
