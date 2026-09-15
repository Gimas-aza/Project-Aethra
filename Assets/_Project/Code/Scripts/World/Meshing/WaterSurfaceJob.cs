using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace Project.World.Meshing
{
    /// <summary>
    /// Greedy top-face quads for liquid voxels. Water has no sides or bottom: it is read from above
    /// and from the shore, which is all the slice needs.
    /// </summary>
    [BurstCompile]
    public struct WaterSurfaceJob : IJob
    {
        [ReadOnly] public NativeArray<byte> Field;
        [ReadOnly] public NativeArray<byte> SolidFlags;
        [ReadOnly] public NativeArray<byte> LiquidFlags;

        public NativeList<float3> Vertices;
        public NativeList<int> Indices;

        public void Execute()
        {
            Vertices.Clear();
            Indices.Clear();

            const int size = WorldMetrics.ChunkSize;
            var mask = new NativeArray<byte>(size * size, Allocator.Temp);

            for (int y = 0; y < size; y++)
            {
                for (int z = 0; z < size; z++)
                {
                    for (int x = 0; x < size; x++)
                    {
                        byte here = Field[MeshConstants.FieldIndex(x, y, z)];
                        if (LiquidFlags[here] == 0)
                        {
                            mask[z * size + x] = 0;
                            continue;
                        }

                        byte above = Field[MeshConstants.FieldIndex(x, y + 1, z)];
                        bool covered = LiquidFlags[above] != 0 || SolidFlags[above] != 0;
                        mask[z * size + x] = (byte)(covered ? 0 : 1);
                    }
                }

                GreedyLayer(mask, y);
            }

            mask.Dispose();
        }

        private void GreedyLayer(NativeArray<byte> mask, int y)
        {
            const int size = WorldMetrics.ChunkSize;
            float top = y + MeshConstants.WaterSurfaceOffset;

            for (int z = 0; z < size; z++)
            {
                for (int x = 0; x < size; x++)
                {
                    if (mask[z * size + x] == 0)
                    {
                        continue;
                    }

                    int width = 1;
                    while (x + width < size && mask[z * size + x + width] != 0)
                    {
                        width++;
                    }

                    int depth = 1;
                    bool canGrow = true;
                    while (z + depth < size && canGrow)
                    {
                        for (int k = 0; k < width; k++)
                        {
                            if (mask[(z + depth) * size + x + k] == 0)
                            {
                                canGrow = false;
                                break;
                            }
                        }

                        if (canGrow)
                        {
                            depth++;
                        }
                    }

                    for (int dz = 0; dz < depth; dz++)
                    {
                        for (int dx = 0; dx < width; dx++)
                        {
                            mask[(z + dz) * size + x + dx] = 0;
                        }
                    }

                    int baseIndex = Vertices.Length;
                    Vertices.Add(new float3(x, top, z));
                    Vertices.Add(new float3(x, top, z + depth));
                    Vertices.Add(new float3(x + width, top, z + depth));
                    Vertices.Add(new float3(x + width, top, z));

                    Indices.Add(baseIndex);
                    Indices.Add(baseIndex + 1);
                    Indices.Add(baseIndex + 2);
                    Indices.Add(baseIndex);
                    Indices.Add(baseIndex + 2);
                    Indices.Add(baseIndex + 3);
                }
            }
        }
    }
}
