using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Project.World.Streaming
{
    /// <summary>
    /// Persists only chunks the player has changed. Everything else is reproducible from the seed,
    /// so it is regenerated instead of stored. One file per chunk, no region packing.
    /// </summary>
    public sealed class WorldSaveService
    {
        private const int Magic = 0x41455658; // 'A','E','V','X'
        private const int Version = 1;
        private const int HeaderBytes = 8 + 12;
        private const int PayloadBytes = WorldMetrics.ChunkVolume * 2;

        private readonly byte[] _buffer = new byte[HeaderBytes + PayloadBytes];
        private readonly List<ChunkKey> _scratch = new List<ChunkKey>(64);

        public WorldSaveService(string worldId)
        {
            Root = Path.Combine(Application.persistentDataPath, "Aethra", worldId);
        }

        public string Root { get; }

        public bool TryLoad(Chunk chunk)
        {
            string path = PathFor(chunk.Key);
            if (!File.Exists(path))
            {
                return false;
            }

            try
            {
                using (FileStream stream = File.OpenRead(path))
                {
                    int read = 0;
                    while (read < _buffer.Length)
                    {
                        int step = stream.Read(_buffer, read, _buffer.Length - read);
                        if (step <= 0)
                        {
                            break;
                        }

                        read += step;
                    }

                    if (read < _buffer.Length || ReadInt(0) != Magic || ReadInt(4) != Version)
                    {
                        Debug.LogWarning($"Discarding malformed chunk save at {path}.");
                        return false;
                    }
                }
            }
            catch (IOException exception)
            {
                Debug.LogWarning($"Failed to read chunk save {path}: {exception.Message}");
                return false;
            }

            var voxels = chunk.Voxels;
            int offset = HeaderBytes;
            bool anySolid = false;

            for (int i = 0; i < WorldMetrics.ChunkVolume; i++)
            {
                byte material = _buffer[offset++];
                byte damage = _buffer[offset++];
                voxels[i] = new Voxel { MaterialId = material, Damage = damage };
                anySolid |= material != VoxelIds.Air;
            }

            if (anySolid)
            {
                chunk.MarkNotUniformAir();
            }

            return true;
        }

        public void Save(Chunk chunk)
        {
            Directory.CreateDirectory(Root);

            WriteInt(0, Magic);
            WriteInt(4, Version);
            WriteInt(8, chunk.Key.X);
            WriteInt(12, chunk.Key.Y);
            WriteInt(16, chunk.Key.Z);

            var voxels = chunk.Voxels;
            int offset = HeaderBytes;

            for (int i = 0; i < WorldMetrics.ChunkVolume; i++)
            {
                Voxel voxel = voxels[i];
                _buffer[offset++] = voxel.MaterialId;
                _buffer[offset++] = voxel.Damage;
            }

            try
            {
                File.WriteAllBytes(PathFor(chunk.Key), _buffer);
            }
            catch (IOException exception)
            {
                Debug.LogError($"Failed to write chunk save {PathFor(chunk.Key)}: {exception.Message}");
            }
        }

        /// <summary>Writes every loaded chunk the player has touched. Returns how many were saved.</summary>
        public int SaveDirty(ChunkStore store)
        {
            _scratch.Clear();

            foreach (KeyValuePair<ChunkKey, Chunk> entry in store.Loaded)
            {
                if (entry.Value.IsDirty)
                {
                    _scratch.Add(entry.Key);
                }
            }

            for (int i = 0; i < _scratch.Count; i++)
            {
                if (store.TryGet(_scratch[i], out Chunk chunk))
                {
                    Save(chunk);
                }
            }

            return _scratch.Count;
        }

        private string PathFor(ChunkKey key)
        {
            return Path.Combine(Root, $"c_{key.X}_{key.Y}_{key.Z}.bin");
        }

        private void WriteInt(int offset, int value)
        {
            _buffer[offset] = (byte)value;
            _buffer[offset + 1] = (byte)(value >> 8);
            _buffer[offset + 2] = (byte)(value >> 16);
            _buffer[offset + 3] = (byte)(value >> 24);
        }

        private int ReadInt(int offset)
        {
            return _buffer[offset]
                   | (_buffer[offset + 1] << 8)
                   | (_buffer[offset + 2] << 16)
                   | (_buffer[offset + 3] << 24);
        }
    }
}
