using System;
using System.Collections.Generic;

namespace Project.World
{
    public sealed class ChunkStore : IDisposable
    {
        private readonly Dictionary<ChunkKey, Chunk> _loaded = new Dictionary<ChunkKey, Chunk>();
        private readonly HashSet<ChunkKey> _dirty = new HashSet<ChunkKey>();

        public IReadOnlyDictionary<ChunkKey, Chunk> Loaded => _loaded;

        public bool TryGet(ChunkKey key, out Chunk chunk)
        {
            return _loaded.TryGetValue(key, out chunk);
        }

        public Chunk GetOrCreate(ChunkKey key)
        {
            if (_loaded.TryGetValue(key, out Chunk chunk))
            {
                return chunk;
            }

            chunk = new Chunk(key);
            _loaded.Add(key, chunk);
            return chunk;
        }

        public void MarkDirty(ChunkKey key)
        {
            if (_loaded.TryGetValue(key, out Chunk chunk))
            {
                chunk.IsDirty = true;
            }

            _dirty.Add(key);
        }

        public bool IsDirty(ChunkKey key)
        {
            return _dirty.Contains(key);
        }

        public void ClearDirty(ChunkKey key)
        {
            _dirty.Remove(key);
            if (_loaded.TryGetValue(key, out Chunk chunk))
            {
                chunk.IsDirty = false;
            }
        }

        public bool Unload(ChunkKey key)
        {
            if (!_loaded.TryGetValue(key, out Chunk chunk))
            {
                return false;
            }

            if (_dirty.Contains(key))
            {
                return false;
            }

            chunk.Dispose();
            _loaded.Remove(key);
            return true;
        }

        public void Dispose()
        {
            foreach (Chunk chunk in _loaded.Values)
            {
                chunk.Dispose();
            }

            _loaded.Clear();
            _dirty.Clear();
        }
    }
}
