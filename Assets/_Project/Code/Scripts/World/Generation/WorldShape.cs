using System;
using Unity.Collections;
using Unity.Mathematics;

namespace Project.World.Generation
{
    public struct WormSegment
    {
        public float3 A;
        public float3 B;
        public float Radius;
    }

    /// <summary>
    /// World-scale features resolved once from the seed: the lake-to-ocean river polyline and the
    /// cave worms. Kept in native memory so stages can hand them straight to Burst jobs.
    /// </summary>
    public sealed class WorldShape : IDisposable
    {
        private const int MaxRiverPoints = 256;
        private const float RiverStep = 20f;

        /// <summary>Deepest the channel is allowed to sit below the surrounding ground.</summary>
        private const float RiverMaxCut = 16f;

        public NativeArray<float2> RiverPoints;
        public NativeArray<float> RiverLevels;
        public NativeArray<WormSegment> Worms;

        public int RiverPointCount { get; private set; }
        public bool RiverReachedOcean { get; private set; }
        public float3 CaveEntrance { get; private set; }
        public int3 PoiSocket { get; private set; }
        public float3 SpawnPoint { get; private set; }

        public WorldShape(in WorldGenParams p)
        {
            BuildRiver(p);
            BuildWorms(p);
            BuildLandmarks(p);
        }

        private void BuildRiver(in WorldGenParams p)
        {
            var points = new NativeArray<float2>(MaxRiverPoints, Allocator.Persistent);
            var levels = new NativeArray<float>(MaxRiverPoints, Allocator.Persistent);

            float2 cursor = p.LakeCenter;
            float2 outward = math.lengthsq(p.LakeCenter) > 1e-3f
                ? math.normalize(p.LakeCenter)
                : new float2(1f, 0f);

            float oceanRadius = p.WorldRadius * 0.92f;

            int count = 0;
            float level = p.LakeLevel;

            while (count < MaxRiverPoints)
            {
                points[count] = cursor;
                levels[count] = level;
                count++;

                if (math.length(cursor) >= oceanRadius)
                {
                    RiverReachedOcean = true;
                    break;
                }

                // Follow the terrain downhill inside a cone pointing at the ocean, so the channel
                // runs along valleys instead of slicing a canyon straight through a mountain.
                float baseAngle = math.atan2(outward.y, outward.x);
                float2 bestDirection = outward;
                float bestScore = float.MaxValue;

                for (int candidate = 0; candidate < 9; candidate++)
                {
                    float offset = (candidate / 8f - 0.5f) * (math.PI * 0.72f);
                    float angle = baseAngle + offset;
                    float2 direction = new float2(math.cos(angle), math.sin(angle));

                    float2 probe = cursor + direction * RiverStep;
                    float height = WorldSampler.Height(probe, p);

                    // Height dominates; the outward term only breaks ties so we keep making progress.
                    float score = height - math.dot(direction, outward) * 6f;
                    if (score < bestScore)
                    {
                        bestScore = score;
                        bestDirection = direction;
                    }
                }

                cursor += bestDirection * RiverStep;
                outward = math.lengthsq(cursor) > 1e-3f ? math.normalize(cursor) : outward;

                // Water flows down and never below the sea, but where a ridge blocks the way the
                // surface is allowed to climb so the channel crosses it as a gorge, not a canyon.
                float terrain = WorldSampler.Height(cursor, p);
                level = math.max(p.SeaLevel, math.min(level, terrain));
                level = math.max(level, math.min(terrain, terrain - RiverMaxCut));
            }

            RiverPoints = points;
            RiverLevels = levels;
            RiverPointCount = count;
        }

        private void BuildWorms(in WorldGenParams p)
        {
            var random = new Unity.Mathematics.Random(math.max(1u, (uint)(p.Seed * 1664525u + 1013904223u)));
            int wormCount = random.NextInt(1, 3);

            var segments = new NativeList<WormSegment>(256, Allocator.Temp);
            bool entranceRecorded = false;

            for (int w = 0; w < wormCount; w++)
            {
                float angle = random.NextFloat(0f, 2f * math.PI);
                float distance = p.WorldRadius * random.NextFloat(0.06f, 0.24f);
                float2 entranceXz = new float2(math.cos(angle), math.sin(angle)) * distance;

                float surface = WorldSampler.Height(entranceXz, p);
                if (surface <= p.SeaLevel + 2f)
                {
                    // Entrance would open underwater; nudge it inland and retry once.
                    entranceXz *= 0.5f;
                    surface = WorldSampler.Height(entranceXz, p);
                }

                float shaftTop = surface + 1f;
                float shaftBottom = math.max(6f, surface - 12f);

                segments.Add(new WormSegment
                {
                    A = new float3(entranceXz.x, shaftTop, entranceXz.y),
                    B = new float3(entranceXz.x, shaftBottom, entranceXz.y),
                    Radius = 2.8f
                });

                if (!entranceRecorded)
                {
                    CaveEntrance = new float3(entranceXz.x, shaftTop, entranceXz.y);
                    entranceRecorded = true;
                }

                float3 head = new float3(entranceXz.x, shaftBottom, entranceXz.y);
                float heading = random.NextFloat(0f, 2f * math.PI);
                float pitch = -0.28f;
                int steps = random.NextInt(34, 52);

                for (int i = 0; i < steps; i++)
                {
                    heading += random.NextFloat(-0.42f, 0.42f);
                    pitch = math.clamp(pitch + random.NextFloat(-0.16f, 0.14f), -0.55f, 0.22f);

                    float3 direction = math.normalize(new float3(
                        math.cos(heading) * math.cos(pitch),
                        math.sin(pitch),
                        math.sin(heading) * math.cos(pitch)));

                    float3 next = head + direction * 6f;
                    next.y = math.clamp(next.y, 5f, p.SeaLevel + 6f);

                    segments.Add(new WormSegment
                    {
                        A = head,
                        B = next,
                        Radius = random.NextFloat(1.9f, 2.9f)
                    });

                    head = next;
                }

                // Terminal chamber so the tunnel ends in something readable.
                segments.Add(new WormSegment
                {
                    A = head,
                    B = head + new float3(0f, 1.5f, 0f),
                    Radius = 6.5f
                });
            }

            Worms = new NativeArray<WormSegment>(segments.Length, Allocator.Persistent);
            for (int i = 0; i < segments.Length; i++)
            {
                Worms[i] = segments[i];
            }

            segments.Dispose();
        }

        private void BuildLandmarks(in WorldGenParams p)
        {
            var random = new Unity.Mathematics.Random(math.max(1u, (uint)(p.Seed * 22695477u + 1u)));

            float angle = random.NextFloat(0f, 2f * math.PI);
            float2 socketXz = new float2(math.cos(angle), math.sin(angle)) * (p.WorldRadius * 0.11f);
            float socketHeight = WorldSampler.Height(socketXz, p);
            PoiSocket = new int3((int)math.round(socketXz.x), (int)math.round(socketHeight) + 1, (int)math.round(socketXz.y));

            // Spawn on dry land near the world centre, above the water table.
            float2 spawnXz = float2.zero;
            for (int i = 0; i < 24; i++)
            {
                float a = i * 0.7f;
                float2 candidate = new float2(math.cos(a), math.sin(a)) * (i * 9f);
                if (WorldSampler.Height(candidate, p) > p.SeaLevel + 3f)
                {
                    spawnXz = candidate;
                    break;
                }
            }

            SpawnPoint = new float3(spawnXz.x, WorldSampler.Height(spawnXz, p) + 3f, spawnXz.y);
        }

        public void Dispose()
        {
            if (RiverPoints.IsCreated)
            {
                RiverPoints.Dispose();
            }

            if (RiverLevels.IsCreated)
            {
                RiverLevels.Dispose();
            }

            if (Worms.IsCreated)
            {
                Worms.Dispose();
            }
        }
    }
}
