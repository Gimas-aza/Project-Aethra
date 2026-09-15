using Unity.Burst;
using Unity.Mathematics;

namespace Project.World.Generation
{
    /// <summary>
    /// Continuous world field sampled by several stages and by <see cref="WorldShape"/>.
    /// Pure functions of position + seed so every caller agrees on the same terrain.
    /// </summary>
    [BurstCompile]
    public static class WorldSampler
    {
        /// <summary>0 = open ocean, 1 = deep inland. Drives the disk falloff.</summary>
        public static float Continent(float2 xz, in WorldGenParams p)
        {
            float r = math.length(xz) / math.max(1f, p.WorldRadius);
            float land = 1f - math.smoothstep(0.80f, 1f, r);
            float n = WorldNoise.Fbm01((xz + p.OffsetContinent) * 0.0009f, 4);
            return math.saturate(land * (0.45f + 0.75f * n));
        }

        public static float MountainMask(float2 xz, in WorldGenParams p)
        {
            float m = WorldNoise.Fbm01((xz + p.OffsetMountain) * 0.0014f, 3);
            return math.saturate((m - 0.52f) * 2.6f);
        }

        public static float Height(float2 xz, in WorldGenParams p, out float continent, out float mountainMask)
        {
            continent = Continent(xz, p);
            mountainMask = MountainMask(xz, p);
            return HeightFrom(xz, p, continent, mountainMask);
        }

        /// <summary>Height from already-sampled macro fields, so stages do not pay for them twice.</summary>
        public static float HeightFrom(float2 xz, in WorldGenParams p, float continent, float mountainMask)
        {
            float landBase = p.SeaLevel + 3f + continent * 24f;
            float detail = WorldNoise.Fbm((xz + p.OffsetDetail) * 0.011f, 4, 2f, 0.5f) * 4.5f * continent;
            float ridge = WorldNoise.Ridge((xz + p.OffsetMountain) * 0.0042f, 4);
            float mountain = ridge * ridge * ridge * mountainMask * 46f * continent;

            // High-frequency grain. On a 1 m binary grid a perfectly smooth height field quantises
            // into concentric terraces; this dithers the contours into a rougher surface.
            float grain = WorldNoise.Fbm((xz + p.OffsetDetail) * 0.085f, 2, 2f, 0.5f) * 1.15f;

            float oceanFloor = p.SeaLevel - 5f - (1f - continent) * 16f;
            float land = landBase + detail + mountain + grain;

            float h = math.lerp(oceanFloor, land, math.smoothstep(0.02f, 0.30f, continent));
            return math.clamp(h, 1.5f, p.WorldHeight - 3f);
        }

        public static float Height(float2 xz, in WorldGenParams p)
        {
            return Height(xz, p, out _, out _);
        }

        /// <summary>0 = freezing, 1 = hot. Rim of the disk is colder than the centre.</summary>
        public static float Temperature(float2 xz, in WorldGenParams p)
        {
            float r = math.length(xz) / math.max(1f, p.WorldRadius);
            float n = WorldNoise.Fbm01((xz + p.OffsetTemperature) * 0.0007f, 3);
            return math.saturate(0.30f + n * 0.75f - r * 0.35f);
        }

        public static float Moisture(float2 xz, in WorldGenParams p)
        {
            return WorldNoise.Fbm01((xz + p.OffsetMoisture) * 0.0011f, 4);
        }

        /// <summary>Radial band index used by the GDD ring layout. Outer rings exist as IDs only for now.</summary>
        public static byte Ring(float2 xz, in WorldGenParams p)
        {
            float r = math.length(xz) / math.max(1f, p.WorldRadius);
            return (byte)math.clamp((int)(r * 3f), 0, 2);
        }

        /// <summary>Altitude cooling applied after the height field is known.</summary>
        public static float CoolByAltitude(float temperature, float height, in WorldGenParams p)
        {
            float above = math.max(0f, height - p.SeaLevel);
            return math.saturate(temperature - math.saturate(above / 70f) * 0.40f);
        }

        public static ClimateBand BandOf(float temperature)
        {
            if (temperature < 0.30f)
            {
                return ClimateBand.Cold;
            }

            return temperature < 0.66f ? ClimateBand.Temperate : ClimateBand.Warm;
        }
    }
}
