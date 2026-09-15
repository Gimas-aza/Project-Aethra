namespace Project.World.Generation
{
    public interface IWorldGenStage
    {
        string Name { get; }

        void Apply(Chunk chunk, in WorldGenContext context);
    }
}
