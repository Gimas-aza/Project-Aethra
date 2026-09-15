using System;
using System.Collections.Generic;
using Project.World.Generation;
using Project.World.Meshing;
using Unity.Mathematics;
using UnityEngine;

namespace Project.World.Streaming
{
    /// <summary>
    /// Keeps chunks around the player generated, meshed and collidable, and drops the ones behind.
    /// Generation works a whole vertical column at a time so the macro noise is sampled once per
    /// (x, z) instead of once per chunk.
    /// </summary>
    public sealed class ChunkStreamer : IDisposable
    {
        private readonly ChunkStore _store;
        private readonly ChunkGenerator _generator;
        private readonly ChunkMesher _mesher;
        private readonly WorldSaveService _save;
        private readonly ScatterSpawner _scatter;
        private readonly Transform _root;
        private readonly Material _terrainMaterial;
        private readonly Material _waterMaterial;

        private readonly int _verticalChunks;
        private readonly float _streamRadius;
        private readonly float _generateRadius;
        private readonly float _unloadRadius;
        private readonly float _worldRadius;
        private readonly int _terrainLayer;

        private readonly Dictionary<ChunkKey, ChunkView> _views = new Dictionary<ChunkKey, ChunkView>();
        private readonly Stack<ChunkView> _viewPool = new Stack<ChunkView>();
        private readonly HashSet<int2> _completeColumns = new HashSet<int2>();
        private readonly HashSet<int2> _queuedColumns = new HashSet<int2>();
        private readonly List<int2> _pendingColumns = new List<int2>();
        private readonly List<int2> _columnScratch = new List<int2>();
        private readonly HashSet<ChunkKey> _meshed = new HashSet<ChunkKey>();
        private readonly HashSet<ChunkKey> _meshQueued = new HashSet<ChunkKey>();
        private readonly List<ChunkKey> _meshQueue = new List<ChunkKey>();
        private readonly Dictionary<ChunkKey, List<ScatterPlacement>> _scatterByChunk =
            new Dictionary<ChunkKey, List<ScatterPlacement>>();

        private readonly Comparison<int2> _farthestFirst;
        private float2 _sortOrigin;
        private int2 _lastPlayerColumn;
        private bool _hasScanned;

        public ChunkStreamer(
            ChunkStore store,
            ChunkGenerator generator,
            ChunkMesher mesher,
            WorldSaveService save,
            ScatterSpawner scatter,
            WorldGenSettings settings,
            Transform root,
            Material terrainMaterial,
            Material waterMaterial,
            int terrainLayer)
        {
            _store = store;
            _generator = generator;
            _mesher = mesher;
            _save = save;
            _scatter = scatter;
            _root = root;
            _terrainMaterial = terrainMaterial;
            _waterMaterial = waterMaterial;
            _terrainLayer = terrainLayer;

            _verticalChunks = settings.VerticalChunkCount;
            _streamRadius = settings.StreamRadius;
            _generateRadius = settings.StreamRadius + WorldMetrics.ChunkSize * 1.5f;
            _unloadRadius = settings.StreamRadius + settings.UnloadHysteresis + WorldMetrics.ChunkSize;
            _worldRadius = settings.WorldRadius;

            _farthestFirst = CompareFarthestFirst;
        }

        public int ColumnsPerTick { get; set; } = 2;
        public int LoadedChunkCount => _store.Loaded.Count;
        public int PendingColumnCount => _pendingColumns.Count;
        public bool IsWarmedUp => _pendingColumns.Count == 0 && _meshQueue.Count == 0;

        public void Tick(Vector3 playerPosition)
        {
            int2 playerColumn = ColumnOf(playerPosition);
            if (!_hasScanned || !playerColumn.Equals(_lastPlayerColumn))
            {
                Rescan(playerPosition);
                _lastPlayerColumn = playerColumn;
                _hasScanned = true;
            }

            GenerateStep(playerPosition);
            MeshStep();
        }

        /// <summary>Queues a chunk for an immediate rebuild after a voxel edit.</summary>
        public void MarkForRemesh(ChunkKey key)
        {
            _meshed.Remove(key);
            if (_meshQueued.Add(key))
            {
                _meshQueue.Add(key);
            }
        }

        private void Rescan(Vector3 playerPosition)
        {
            var playerXz = new float2(playerPosition.x, playerPosition.z);
            int reach = Mathf.CeilToInt(_generateRadius / WorldMetrics.ChunkSize) + 1;
            int2 center = ColumnOf(playerPosition);

            for (int dz = -reach; dz <= reach; dz++)
            {
                for (int dx = -reach; dx <= reach; dx++)
                {
                    var column = new int2(center.x + dx, center.y + dz);
                    float2 columnCenter = ColumnCenter(column);

                    if (math.length(columnCenter) > _worldRadius)
                    {
                        continue;
                    }

                    float distance = math.distance(columnCenter, playerXz);
                    if (distance > _generateRadius)
                    {
                        continue;
                    }

                    if (_completeColumns.Contains(column))
                    {
                        TryEnqueueMeshAround(column, playerXz);
                        continue;
                    }

                    if (_queuedColumns.Add(column))
                    {
                        _pendingColumns.Add(column);
                    }
                }
            }

            _sortOrigin = playerXz;
            _pendingColumns.Sort(_farthestFirst);

            UnloadFar(playerXz);
        }

        private void GenerateStep(Vector3 playerPosition)
        {
            var playerXz = new float2(playerPosition.x, playerPosition.z);

            for (int i = 0; i < ColumnsPerTick && _pendingColumns.Count > 0; i++)
            {
                int last = _pendingColumns.Count - 1;
                int2 column = _pendingColumns[last];
                _pendingColumns.RemoveAt(last);
                _queuedColumns.Remove(column);

                GenerateColumn(column);
                TryEnqueueMeshAround(column, playerXz);
            }
        }

        private void GenerateColumn(int2 column)
        {
            for (int y = 0; y < _verticalChunks; y++)
            {
                var key = new ChunkKey(column.x, y, column.y);
                if (_store.TryGet(key, out _))
                {
                    continue;
                }

                Chunk chunk = _store.GetOrCreate(key);
                _generator.Generate(chunk);
                CachePlacements(key);

                if (_save.TryLoad(chunk))
                {
                    chunk.IsDirty = true;
                    _store.MarkDirty(key);
                }
            }

            _completeColumns.Add(column);
        }

        private void CachePlacements(ChunkKey key)
        {
            var placements = _generator.LastPlacements;
            if (placements.Length == 0)
            {
                return;
            }

            if (!_scatterByChunk.TryGetValue(key, out List<ScatterPlacement> list))
            {
                list = new List<ScatterPlacement>(placements.Length);
                _scatterByChunk.Add(key, list);
            }

            list.Clear();
            for (int i = 0; i < placements.Length; i++)
            {
                list.Add(placements[i]);
            }
        }

        private void TryEnqueueMeshAround(int2 column, float2 playerXz)
        {
            for (int dz = -1; dz <= 1; dz++)
            {
                for (int dx = -1; dx <= 1; dx++)
                {
                    var candidate = new int2(column.x + dx, column.y + dz);
                    if (!_completeColumns.Contains(candidate))
                    {
                        continue;
                    }

                    if (math.distance(ColumnCenter(candidate), playerXz) > _streamRadius)
                    {
                        continue;
                    }

                    if (!NeighboursComplete(candidate))
                    {
                        continue;
                    }

                    EnqueueColumnMesh(candidate);
                }
            }
        }

        private bool NeighboursComplete(int2 column)
        {
            for (int dz = -1; dz <= 1; dz++)
            {
                for (int dx = -1; dx <= 1; dx++)
                {
                    var neighbour = new int2(column.x + dx, column.y + dz);
                    if (_completeColumns.Contains(neighbour))
                    {
                        continue;
                    }

                    // Columns outside the disk never generate; treat them as settled water/air.
                    if (math.length(ColumnCenter(neighbour)) > _worldRadius)
                    {
                        continue;
                    }

                    return false;
                }
            }

            return true;
        }

        private void EnqueueColumnMesh(int2 column)
        {
            for (int y = 0; y < _verticalChunks; y++)
            {
                var key = new ChunkKey(column.x, y, column.y);
                if (_meshed.Contains(key) || _meshQueued.Contains(key))
                {
                    continue;
                }

                _meshQueued.Add(key);
                _meshQueue.Add(key);
            }
        }

        private void MeshStep()
        {
            int scheduled = 0;

            while (_meshQueue.Count > 0 && _mesher.HasRoom)
            {
                int last = _meshQueue.Count - 1;
                ChunkKey key = _meshQueue[last];
                _meshQueue.RemoveAt(last);
                _meshQueued.Remove(key);

                if (!_store.TryGet(key, out Chunk chunk))
                {
                    continue;
                }

                if (chunk.IsUniformAir && NeighbourhoodIsAir(key))
                {
                    _meshed.Add(key);
                    continue;
                }

                if (!_mesher.Schedule(key, _store))
                {
                    break;
                }

                scheduled++;
            }

            if (scheduled == 0)
            {
                _mesher.Reset();
                return;
            }

            _mesher.CompleteAll();

            for (int i = 0; i < scheduled; i++)
            {
                ChunkKey key = _mesher.KeyAt(i);
                ChunkView view = GetOrCreateView(key);

                _mesher.Apply(i, view.TerrainMesh, view.WaterMesh, out bool hasTerrain, out bool hasWater);

                bool hasScatter = _scatterByChunk.TryGetValue(key, out List<ScatterPlacement> placements)
                                  && placements.Count > 0;

                if (!hasTerrain && !hasWater && !hasScatter)
                {
                    RecycleView(key);
                }
                else
                {
                    view.RefreshParts(hasTerrain, hasWater);
                    if (hasScatter && view.ScatterRoot.childCount == 0)
                    {
                        _scatter.Spawn(placements, view.ScatterRoot);
                    }
                }

                _meshed.Add(key);
            }

            _mesher.Reset();
        }

        private bool NeighbourhoodIsAir(ChunkKey key)
        {
            for (int dz = -1; dz <= 1; dz++)
            {
                for (int dy = -1; dy <= 1; dy++)
                {
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        var neighbour = new ChunkKey(key.X + dx, key.Y + dy, key.Z + dz);
                        if (neighbour.Y < 0)
                        {
                            return false;
                        }

                        if (_store.TryGet(neighbour, out Chunk chunk) && !chunk.IsUniformAir)
                        {
                            return false;
                        }
                    }
                }
            }

            return true;
        }

        private void UnloadFar(float2 playerXz)
        {
            _columnScratch.Clear();

            foreach (int2 column in _completeColumns)
            {
                if (math.distance(ColumnCenter(column), playerXz) > _unloadRadius)
                {
                    _columnScratch.Add(column);
                }
            }

            for (int i = 0; i < _columnScratch.Count; i++)
            {
                int2 column = _columnScratch[i];
                bool keepData = false;

                for (int y = 0; y < _verticalChunks; y++)
                {
                    var key = new ChunkKey(column.x, y, column.y);
                    RecycleView(key);
                    _meshed.Remove(key);

                    if (_meshQueued.Remove(key))
                    {
                        _meshQueue.Remove(key);
                    }

                    if (_store.TryGet(key, out Chunk chunk) && chunk.IsDirty)
                    {
                        keepData = true;
                    }
                }

                if (keepData)
                {
                    continue;
                }

                for (int y = 0; y < _verticalChunks; y++)
                {
                    var key = new ChunkKey(column.x, y, column.y);
                    _store.Unload(key);
                    _scatterByChunk.Remove(key);
                }

                _completeColumns.Remove(column);
            }
        }

        private ChunkView GetOrCreateView(ChunkKey key)
        {
            if (_views.TryGetValue(key, out ChunkView view))
            {
                return view;
            }

            view = _viewPool.Count > 0
                ? _viewPool.Pop()
                : ChunkView.Create(_root, _terrainMaterial, _waterMaterial, _terrainLayer);

            view.Bind(key);
            _views.Add(key, view);
            return view;
        }

        private void RecycleView(ChunkKey key)
        {
            if (!_views.TryGetValue(key, out ChunkView view))
            {
                return;
            }

            _scatter.Recycle(view.ScatterRoot);
            view.Recycle();
            _views.Remove(key);
            _viewPool.Push(view);
        }

        private static int2 ColumnOf(Vector3 position)
        {
            return new int2(
                Mathf.FloorToInt(position.x / WorldMetrics.ChunkSize),
                Mathf.FloorToInt(position.z / WorldMetrics.ChunkSize));
        }

        private static float2 ColumnCenter(int2 column)
        {
            const float half = WorldMetrics.ChunkSize * 0.5f;
            return new float2(
                column.x * WorldMetrics.ChunkSize + half,
                column.y * WorldMetrics.ChunkSize + half);
        }

        private int CompareFarthestFirst(int2 a, int2 b)
        {
            float da = math.distancesq(ColumnCenter(a), _sortOrigin);
            float db = math.distancesq(ColumnCenter(b), _sortOrigin);
            return db.CompareTo(da);
        }

        public void Dispose()
        {
            _views.Clear();
            _viewPool.Clear();
            _scatterByChunk.Clear();
        }
    }
}
