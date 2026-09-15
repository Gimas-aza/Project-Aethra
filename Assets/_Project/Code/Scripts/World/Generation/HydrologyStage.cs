using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace Project.World.Generation
{
    /// <summary>
    /// Ocean fill, the central lake basin and the lake-to-ocean river (carve then fill).
    /// Water is a plain material: no flow simulation, only a per-column surface level.
    /// </summary>
    public sealed class HydrologyStage : IWorldGenStage
    {
        /// <summary>Rise per metre away from the channel when shaving the banks into a ramp.</summary>
        private const float BankSlope = 1.35f;

        /// <summary>Above this drop the channel is abandoned rather than cut through a ridge.</summary>
        private const float MaxRiverCut = 20f;

        private readonly WorldGenScratch _scratch;
        private int2 _cachedColumnKey = new int2(int.MinValue, int.MinValue);
        private bool _hasCache;

        public HydrologyStage(WorldGenScratch scratch)
        {
            _scratch = scratch;
        }

        public string Name => "Hydrology";

        public void Apply(Chunk chunk, in WorldGenContext context)
        {
            int3 origin = chunk.Key.OriginVoxel;
            int2 columnKey = new int2(chunk.Key.X, chunk.Key.Z);

            if (!_hasCache || !columnKey.Equals(_cachedColumnKey))
            {
                WorldShape shape = _scratch.Shape;

                var columnJob = new ColumnJob
                {
                    Params = _scratch.Params,
                    OriginXz = new int2(origin.x, origin.z),
                    RiverPoints = shape.RiverPoints,
                    RiverLevels = shape.RiverLevels,
                    RiverCount = shape.RiverPointCount,
                    Columns = _scratch.Columns
                };

                columnJob.Schedule(ColumnSample.ColumnCount, 32).Complete();

                _cachedColumnKey = columnKey;
                _hasCache = true;
            }

            NativeArray<Voxel> voxels = chunk.Voxels;

            var fillJob = new FillJob
            {
                Params = _scratch.Params,
                Origin = origin,
                Columns = _scratch.Columns,
                Voxels = voxels
            };

            fillJob.Schedule(WorldMetrics.ChunkVolume, 128).Complete();
        }

        /// <summary>Called by the orchestrator when the cached column data is thrown away.</summary>
        public void InvalidateColumnCache()
        {
            _hasCache = false;
        }

        [BurstCompile]
        private struct ColumnJob : IJobParallelFor
        {
            public WorldGenParams Params;
            public int2 OriginXz;
            [ReadOnly] public NativeArray<float2> RiverPoints;
            [ReadOnly] public NativeArray<float> RiverLevels;
            public int RiverCount;
            public NativeArray<ColumnSample> Columns;

            public void Execute(int index)
            {
                int x = index % WorldMetrics.ChunkSize;
                int z = index / WorldMetrics.ChunkSize;
                float2 xz = new float2(OriginXz.x + x, OriginXz.y + z);

                ColumnSample column = Columns[index];
                column.GroundTopY = column.SurfaceY;
                column.WaterY = -1;
                column.IsCarved = 0;

                // 1. Ocean.
                if (column.SurfaceY < Params.SeaLevel)
                {
                    column.WaterY = Params.SeaLevel;
                }

                // 2. Lake basin: always carved, so the world has standing water even if the river fails.
                // The radius and floor are both noised, otherwise the basin reads as a drawn circle
                // with concentric terraces.
                float shoreWobble = WorldNoise.Fbm(xz * 0.018f, 3, 2f, 0.5f);
                float lakeRadius = Params.LakeRadius * (1f + shoreWobble * 0.30f);
                float lakeDistance = math.length(xz - Params.LakeCenter);

                if (lakeDistance < lakeRadius)
                {
                    float normalized = lakeDistance / lakeRadius;
                    float floorNoise = WorldNoise.Fbm(xz * 0.055f, 2, 2f, 0.5f) * 1.8f;
                    float bowl = Params.LakeLevel - (10f + shoreWobble * 4f) * (1f - normalized * normalized) + floorNoise;

                    // Fade the carve out at the rim so the basin blends into the surrounding ground.
                    bowl = math.lerp(bowl, column.SurfaceY, math.smoothstep(0.74f, 1f, normalized));
                    int bowlY = (int)math.floor(bowl);

                    if (column.GroundTopY > bowlY)
                    {
                        column.GroundTopY = bowlY;
                        column.IsCarved = 1;
                    }

                    if (column.GroundTopY < Params.LakeLevel)
                    {
                        column.WaterY = math.max(column.WaterY, Params.LakeLevel);
                    }
                }

                // 3. River: carve a channel along the polyline and fill it to the local level.
                float bestDistanceSq = float.MaxValue;
                float bestLevel = 0f;

                for (int i = 0; i < RiverCount - 1; i++)
                {
                    float distanceSq = WorldNoise.DistanceToSegmentSq(xz, RiverPoints[i], RiverPoints[i + 1], out float t);
                    if (distanceSq < bestDistanceSq)
                    {
                        bestDistanceSq = distanceSq;
                        bestLevel = math.lerp(RiverLevels[i], RiverLevels[i + 1], t);
                    }
                }

                float bankWidth = Params.RiverWidth;
                float distance = math.sqrt(bestDistanceSq);
                float cut = column.SurfaceY - bestLevel;

                // Banks are shaved into a ramp, so a deeper channel needs a wider footprint.
                float outerWidth = bankWidth + math.clamp(cut, 0f, MaxRiverCut) / BankSlope + 1f;

                if (RiverCount > 1 && distance < outerWidth)
                {
                    // Skip where the ground already sits well below the channel (the river would
                    // hang in the air) or far above it (we would slice a canyon through a ridge).
                    if (cut > -7f && cut < MaxRiverCut)
                    {
                        int targetY;
                        if (distance <= bankWidth)
                        {
                            float w = distance / bankWidth;
                            targetY = (int)math.floor(bestLevel - Params.RiverDepth * (1f - w * w));
                        }
                        else
                        {
                            // Shave the banks into a ramp instead of leaving a vertical wall.
                            targetY = (int)math.floor(bestLevel + (distance - bankWidth) * BankSlope);
                        }

                        if (column.GroundTopY > targetY)
                        {
                            column.GroundTopY = targetY;
                            column.IsCarved = 1;
                        }

                        int level = (int)math.floor(bestLevel);
                        if (distance <= bankWidth && column.GroundTopY < level)
                        {
                            column.WaterY = math.max(column.WaterY, level);
                        }
                    }
                }

                column.GroundTopY = math.clamp(column.GroundTopY, 0, Params.WorldHeight - 1);
                if (column.WaterY >= 0)
                {
                    column.WaterY = math.min(column.WaterY, Params.WorldHeight - 1);
                    if (column.WaterY <= column.GroundTopY)
                    {
                        column.WaterY = -1;
                    }
                }

                Columns[index] = column;
            }
        }

        [BurstCompile]
        private struct FillJob : IJobParallelFor
        {
            public WorldGenParams Params;
            public int3 Origin;
            [ReadOnly] public NativeArray<ColumnSample> Columns;
            public NativeArray<Voxel> Voxels;

            public void Execute(int index)
            {
                int y = index % WorldMetrics.ChunkSize;
                int x = index / WorldMetrics.ChunkSize % WorldMetrics.ChunkSize;
                int z = index / (WorldMetrics.ChunkSize * WorldMetrics.ChunkSize);

                int wy = Origin.y + y;
                if (wy <= 0 || wy >= Params.WorldHeight)
                {
                    return;
                }

                ColumnSample column = Columns[ColumnSample.ColumnIndex(x, z)];

                if (wy <= column.GroundTopY)
                {
                    // Freshly carved banks read as sand instead of whatever stratum was exposed.
                    if (column.IsCarved == 1 && wy == column.GroundTopY)
                    {
                        Voxels[index] = new Voxel { MaterialId = VoxelIds.Sand };
                    }

                    return;
                }

                if (column.WaterY >= 0 && wy <= column.WaterY)
                {
                    bool freezes = column.Climate == (byte)ClimateBand.Cold && wy == column.WaterY;
                    Voxels[index] = new Voxel { MaterialId = freezes ? VoxelIds.Ice : VoxelIds.Water };
                    return;
                }

                Voxels[index] = Voxel.Air;
            }
        }
    }
}
