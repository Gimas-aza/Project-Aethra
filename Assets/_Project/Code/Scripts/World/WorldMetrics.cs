namespace Project.World
{
    public static class WorldMetrics
    {
        public const int ChunkSize = 16;
        public const int ChunkVolume = ChunkSize * ChunkSize * ChunkSize;
        public const float VoxelSize = 1f;

        public static int Index(int x, int y, int z)
        {
            return y + ChunkSize * (x + ChunkSize * z);
        }

        public static bool InChunk(int x, int y, int z)
        {
            return (uint)x < ChunkSize && (uint)y < ChunkSize && (uint)z < ChunkSize;
        }
    }
}
