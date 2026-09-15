using Unity.Burst;
using Unity.Mathematics;

namespace Project.World.Generation
{
    /// <summary>Burst-friendly noise helpers shared by every generation stage.</summary>
    [BurstCompile]
    public static class WorldNoise
    {
        public static float Fbm(float2 p, int octaves, float lacunarity, float gain)
        {
            float amplitude = 1f;
            float frequency = 1f;
            float sum = 0f;
            float norm = 0f;

            for (int i = 0; i < octaves; i++)
            {
                sum += noise.snoise(p * frequency) * amplitude;
                norm += amplitude;
                frequency *= lacunarity;
                amplitude *= gain;
            }

            return norm > 0f ? sum / norm : 0f;
        }

        public static float Fbm(float3 p, int octaves, float lacunarity, float gain)
        {
            float amplitude = 1f;
            float frequency = 1f;
            float sum = 0f;
            float norm = 0f;

            for (int i = 0; i < octaves; i++)
            {
                sum += noise.snoise(p * frequency) * amplitude;
                norm += amplitude;
                frequency *= lacunarity;
                amplitude *= gain;
            }

            return norm > 0f ? sum / norm : 0f;
        }

        /// <summary>Signed FBM remapped to 0..1.</summary>
        public static float Fbm01(float2 p, int octaves)
        {
            return math.saturate(Fbm(p, octaves, 2f, 0.5f) * 0.5f + 0.5f);
        }

        /// <summary>Ridged noise: peaks where the signed noise crosses zero.</summary>
        public static float Ridge(float2 p, int octaves)
        {
            return math.saturate(1f - math.abs(Fbm(p, octaves, 2.1f, 0.5f)));
        }

        public static uint Hash(int x, int y, int z, int seed)
        {
            uint h = (uint)(x * 73856093) ^ (uint)(y * 19349663) ^ (uint)(z * 83492791) ^ (uint)(seed * 2654435761u);
            h ^= h >> 15;
            h *= 2246822519u;
            h ^= h >> 13;
            h *= 3266489917u;
            h ^= h >> 16;
            return h;
        }

        public static float Hash01(int x, int y, int z, int seed)
        {
            return Hash(x, y, z, seed) * (1f / 4294967295f);
        }

        /// <summary>Squared distance from <paramref name="p"/> to segment [a, b], plus the clamped parameter.</summary>
        public static float DistanceToSegmentSq(float2 p, float2 a, float2 b, out float t)
        {
            float2 ab = b - a;
            float lengthSq = math.lengthsq(ab);
            t = lengthSq > 1e-6f ? math.saturate(math.dot(p - a, ab) / lengthSq) : 0f;
            float2 closest = a + ab * t;
            return math.lengthsq(p - closest);
        }

        public static float DistanceToSegmentSq(float3 p, float3 a, float3 b, out float t)
        {
            float3 ab = b - a;
            float lengthSq = math.lengthsq(ab);
            t = lengthSq > 1e-6f ? math.saturate(math.dot(p - a, ab) / lengthSq) : 0f;
            float3 closest = a + ab * t;
            return math.lengthsq(p - closest);
        }
    }
}
