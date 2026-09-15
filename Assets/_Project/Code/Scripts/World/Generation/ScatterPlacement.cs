using Unity.Mathematics;

namespace Project.World.Generation
{
    public enum ScatterKind : byte
    {
        Rock = 0,
        FallenTree = 1,
        Tree = 2,
        PoiSocket = 3
    }

    /// <summary>A greybox prop the streamer should instantiate under the chunk that owns it.</summary>
    public struct ScatterPlacement
    {
        public int3 Voxel;
        public byte Kind;
        public float Yaw;
        public float Scale;
    }
}
