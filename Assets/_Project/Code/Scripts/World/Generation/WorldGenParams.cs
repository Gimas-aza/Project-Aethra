using Unity.Mathematics;

namespace Project.World.Generation
{
    /// <summary>
    /// Blittable snapshot of <see cref="WorldGenSettings"/> plus derived world-scale features,
    /// safe to copy into Burst jobs.
    /// </summary>
    public struct WorldGenParams
    {
        public int Seed;
        public int WorldRadius;
        public int WorldHeight;
        public int SeaLevel;

        public float2 LakeCenter;
        public float LakeRadius;
        public int LakeLevel;

        public float RiverWidth;
        public int RiverDepth;

        public float2 OffsetContinent;
        public float2 OffsetTemperature;
        public float2 OffsetMoisture;
        public float2 OffsetMountain;
        public float2 OffsetDetail;

        public static WorldGenParams FromSettings(WorldGenSettings settings)
        {
            var random = new Random((uint)math.max(1, settings.Seed * 747796405 + 2891336453));

            int seaLevel = settings.SeaLevel;
            float lakeAngle = random.NextFloat(0f, 2f * math.PI);
            float lakeDistance = settings.WorldRadius * 0.16f;

            return new WorldGenParams
            {
                Seed = settings.Seed,
                WorldRadius = settings.WorldRadius,
                WorldHeight = settings.WorldHeight,
                SeaLevel = seaLevel,
                LakeCenter = new float2(math.cos(lakeAngle), math.sin(lakeAngle)) * lakeDistance,
                LakeRadius = 46f,
                LakeLevel = seaLevel + 14,
                RiverWidth = 5.5f,
                RiverDepth = 3,
                OffsetContinent = random.NextFloat2(-4000f, 4000f),
                OffsetTemperature = random.NextFloat2(-4000f, 4000f),
                OffsetMoisture = random.NextFloat2(-4000f, 4000f),
                OffsetMountain = random.NextFloat2(-4000f, 4000f),
                OffsetDetail = random.NextFloat2(-4000f, 4000f)
            };
        }
    }
}
