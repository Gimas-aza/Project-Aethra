using Unity.Mathematics;

namespace Project.World.Generation
{
    /// <summary>
    /// Emits greybox props (rocks, fallen trees, dummy trees) and the world's single empty POI
    /// socket. Writes no voxels, so scatter never takes part in the collapse flood fill.
    /// </summary>
    public sealed class ScatterStage : IWorldGenStage
    {
        private readonly WorldGenScratch _scratch;

        public ScatterStage(WorldGenScratch scratch)
        {
            _scratch = scratch;
        }

        public string Name => "Scatter";

        public void Apply(Chunk chunk, in WorldGenContext context)
        {
            int3 origin = chunk.Key.OriginVoxel;
            int minY = origin.y;
            int maxY = origin.y + WorldMetrics.ChunkSize;
            int seed = _scratch.Params.Seed;

            for (int z = 0; z < WorldMetrics.ChunkSize; z++)
            {
                for (int x = 0; x < WorldMetrics.ChunkSize; x++)
                {
                    ColumnSample column = _scratch.Columns[ColumnSample.ColumnIndex(x, z)];

                    int standY = column.GroundTopY + 1;
                    if (standY < minY || standY >= maxY)
                    {
                        continue;
                    }

                    if (column.WaterY >= 0 || column.GroundTopY <= _scratch.Params.SeaLevel)
                    {
                        continue;
                    }

                    int wx = origin.x + x;
                    int wz = origin.z + z;

                    float roll = WorldNoise.Hash01(wx, 0, wz, seed + 5501);
                    float density = DensityFor((BiomeId)column.Biome);
                    if (roll > density)
                    {
                        continue;
                    }

                    float pick = WorldNoise.Hash01(wx, 1, wz, seed + 7717);
                    ScatterKind kind = PickKind((BiomeId)column.Biome, pick);

                    _scratch.Placements.Add(new ScatterPlacement
                    {
                        Voxel = new int3(wx, standY, wz),
                        Kind = (byte)kind,
                        Yaw = WorldNoise.Hash01(wx, 2, wz, seed + 3313) * 360f,
                        Scale = 0.75f + WorldNoise.Hash01(wx, 3, wz, seed + 8821) * 0.7f
                    });
                }
            }

            AddPoiSocketIfInside(origin, minY, maxY);
        }

        private void AddPoiSocketIfInside(int3 origin, int minY, int maxY)
        {
            int3 socket = _scratch.Shape.PoiSocket;
            if (socket.x < origin.x || socket.x >= origin.x + WorldMetrics.ChunkSize)
            {
                return;
            }

            if (socket.z < origin.z || socket.z >= origin.z + WorldMetrics.ChunkSize)
            {
                return;
            }

            int localX = socket.x - origin.x;
            int localZ = socket.z - origin.z;
            ColumnSample column = _scratch.Columns[ColumnSample.ColumnIndex(localX, localZ)];
            int standY = column.GroundTopY + 1;

            if (standY < minY || standY >= maxY)
            {
                return;
            }

            _scratch.Placements.Add(new ScatterPlacement
            {
                Voxel = new int3(socket.x, standY, socket.z),
                Kind = (byte)ScatterKind.PoiSocket,
                Yaw = 0f,
                Scale = 1f
            });
        }

        private static float DensityFor(BiomeId biome)
        {
            switch (biome)
            {
                case BiomeId.DarkForest:
                    return 0.010f;
                case BiomeId.Forest:
                    return 0.006f;
                case BiomeId.Meadow:
                    return 0.0022f;
                case BiomeId.SnowMountains:
                    return 0.0016f;
                default:
                    return 0.0008f;
            }
        }

        private static ScatterKind PickKind(BiomeId biome, float roll)
        {
            if (biome == BiomeId.Forest || biome == BiomeId.DarkForest)
            {
                if (roll < 0.62f)
                {
                    return ScatterKind.Tree;
                }

                return roll < 0.82f ? ScatterKind.FallenTree : ScatterKind.Rock;
            }

            return roll < 0.75f ? ScatterKind.Rock : ScatterKind.FallenTree;
        }
    }
}
