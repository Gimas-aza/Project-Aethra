namespace Project.World.Generation
{
    public readonly struct WorldGenContext
    {
        public WorldGenContext(WorldSession session, VoxelMaterialCatalog catalog)
        {
            Session = session;
            Catalog = catalog;
        }

        public WorldSession Session { get; }
        public VoxelMaterialCatalog Catalog { get; }
        public WorldGenSettings Settings => Session.Settings;
        public int Seed => Session.Seed;
    }
}
