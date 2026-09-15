using Unity.Collections;
using Unity.Mathematics;

namespace Project.World.Meshing
{
    /// <summary>
    /// Copies a chunk and its 26 neighbours into the padded sample field the mesh jobs read.
    /// Done as 27 block copies rather than per-sample lookups.
    /// </summary>
    public static class ChunkFieldGather
    {
        public static void Gather(ChunkKey key, ChunkStore store, NativeArray<byte> field)
        {
            const int size = WorldMetrics.ChunkSize;

            for (int dz = -1; dz <= 1; dz++)
            {
                int lz0 = math.max(-1, dz * size);
                int lz1 = math.min(size, dz * size + size - 1);
                if (lz0 > lz1)
                {
                    continue;
                }

                for (int dy = -1; dy <= 1; dy++)
                {
                    int ly0 = math.max(-1, dy * size);
                    int ly1 = math.min(size, dy * size + size - 1);
                    if (ly0 > ly1)
                    {
                        continue;
                    }

                    for (int dx = -1; dx <= 1; dx++)
                    {
                        int lx0 = math.max(-1, dx * size);
                        int lx1 = math.min(size, dx * size + size - 1);
                        if (lx0 > lx1)
                        {
                            continue;
                        }

                        var neighbourKey = new ChunkKey(key.X + dx, key.Y + dy, key.Z + dz);
                        bool loaded = store.TryGet(neighbourKey, out Chunk neighbour);

                        // Below the world the ground is closed off with bedrock; above it, open air.
                        byte fallback = !loaded && neighbourKey.Y < 0 ? VoxelIds.Granite : VoxelIds.Air;
                        NativeArray<Voxel> source = loaded ? neighbour.Voxels : default;

                        for (int lz = lz0; lz <= lz1; lz++)
                        {
                            for (int ly = ly0; ly <= ly1; ly++)
                            {
                                for (int lx = lx0; lx <= lx1; lx++)
                                {
                                    byte material = fallback;
                                    if (loaded)
                                    {
                                        material = source[WorldMetrics.Index(
                                            lx - dx * size,
                                            ly - dy * size,
                                            lz - dz * size)].MaterialId;
                                    }

                                    field[MeshConstants.FieldIndex(lx, ly, lz)] = material;
                                }
                            }
                        }
                    }
                }
            }
        }
    }
}
