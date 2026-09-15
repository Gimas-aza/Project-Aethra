namespace Project.World.Meshing
{
    /// <summary>
    /// Layout of the padded sample field a chunk mesh is built from.
    /// Samples cover voxels -1..16, cells cover -1..15, so every chunk owns each surface edge
    /// exactly once and neighbouring chunks meet without seams or duplicated geometry.
    /// </summary>
    public static class MeshConstants
    {
        public const int FieldSize = WorldMetrics.ChunkSize + 2;
        public const int FieldVolume = FieldSize * FieldSize * FieldSize;

        public const int CellSize = WorldMetrics.ChunkSize + 1;
        public const int CellVolume = CellSize * CellSize * CellSize;

        /// <summary>Water surface sits just under the voxel top so shorelines do not z-fight.</summary>
        public const float WaterSurfaceOffset = 0.88f;

        public static int FieldIndex(int x, int y, int z)
        {
            return x + 1 + FieldSize * (y + 1 + FieldSize * (z + 1));
        }

        public static int CellIndex(int x, int y, int z)
        {
            return x + 1 + CellSize * (y + 1 + CellSize * (z + 1));
        }
    }
}
