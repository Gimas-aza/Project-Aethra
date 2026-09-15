namespace Project.World.Generation
{
    /// <summary>
    /// Marks a stage that only writes <see cref="WorldGenScratch.Columns"/>. The orchestrator runs
    /// these once per (chunkX, chunkZ) instead of once per chunk.
    /// </summary>
    public interface IWorldGenColumnStage : IWorldGenStage
    {
    }
}
