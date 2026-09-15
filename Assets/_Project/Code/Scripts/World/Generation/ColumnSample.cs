namespace Project.World.Generation
{
    /// <summary>
    /// Per-column data shared between generation stages. One entry per (x, z) of a chunk,
    /// indexed by <see cref="ColumnIndex"/>.
    /// </summary>
    public struct ColumnSample
    {
        public float Continent;
        public float Temperature;
        public float Moisture;
        public float MountainMask;
        public float Height;

        public int SurfaceY;

        /// <summary>Highest solid voxel after hydrology carving, or <see cref="SurfaceY"/> when untouched.</summary>
        public int GroundTopY;

        /// <summary>Top water voxel, or -1 when the column is dry.</summary>
        public int WaterY;

        public byte Biome;
        public byte Climate;
        public byte Ring;
        public byte SurfaceMaterial;
        public byte SubsurfaceMaterial;
        public byte IsOcean;
        public byte IsCarved;

        public static int ColumnIndex(int x, int z)
        {
            return z * WorldMetrics.ChunkSize + x;
        }

        public const int ColumnCount = WorldMetrics.ChunkSize * WorldMetrics.ChunkSize;
    }
}
