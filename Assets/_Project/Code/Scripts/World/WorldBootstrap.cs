using System.Collections.Generic;
using Project.World.Editing;
using Project.World.Generation;
using Project.World.Meshing;
using Project.World.Streaming;
using Unity.Mathematics;
using UnityEngine;

namespace Project.World
{
    /// <summary>
    /// Composition root for the voxel world. Owns the session, the chunk store and every runtime
    /// system, and is the only place scene references are wired.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class WorldBootstrap : MonoBehaviour
    {
        [Header("Data")]
        [SerializeField] private WorldGenSettings _settings;
        [SerializeField] private VoxelMaterialCatalog _catalog;

        [Header("Rendering")]
        [SerializeField] private Material _terrainMaterial;
        [SerializeField] private Material _waterMaterial;

        [Header("Scatter prefabs")]
        [SerializeField] private GameObject _rockPrefab;
        [SerializeField] private GameObject _fallenTreePrefab;
        [SerializeField] private GameObject _treePrefab;
        [SerializeField] private GameObject _poiSocketPrefab;

        [Header("Streaming")]
        [SerializeField] private Transform _streamTarget;
        [SerializeField] [Range(1, 8)] private int _columnsPerTick = 2;
        [SerializeField] [Range(1, 16)] private int _meshBatch = 8;

        private readonly List<int3> _removed = new List<int3>(256);
        private readonly List<List<int3>> _islandsFound = new List<List<int3>>(4);

        private VoxelMaterialTables _tables;
        private ChunkGenerator _generator;
        private ChunkMesher _mesher;
        private WorldSaveService _save;
        private ScatterSpawner _scatter;
        private ChunkStreamer _streamer;
        private VoxelEditService _edit;
        private ConnectivitySystem _connectivity;
        private FallingIslandSystem _islands;

        private Transform _chunkRoot;
        private Transform _debrisRoot;
        private Transform _scatterPool;
        private float _autoSaveTimer;
        private bool _quitting;

        public WorldSession Session { get; private set; }
        public ChunkStore Store { get; private set; }
        public VoxelMaterialCatalog Catalog => _catalog;
        public WorldGenSettings Settings => _settings;

        public bool IsReady => _streamer != null;
        public bool IsWarmedUp => _streamer != null && _streamer.IsWarmedUp;
        public int ActiveDebrisCount => _islands?.ActiveCount ?? 0;

        public Vector3 SpawnPoint => _generator == null
            ? transform.position
            : new Vector3(_generator.Shape.SpawnPoint.x, _generator.Shape.SpawnPoint.y, _generator.Shape.SpawnPoint.z);

        private void Awake()
        {
            if (_settings == null || _catalog == null)
            {
                Debug.LogError("WorldBootstrap needs both WorldGenSettings and VoxelMaterialCatalog.", this);
                enabled = false;
                return;
            }

            Session = new WorldSession(_settings);
            Store = new ChunkStore();

            _chunkRoot = CreateRoot("Chunks");
            _debrisRoot = CreateRoot("Debris");
            _scatterPool = CreateRoot("ScatterPool");
            _scatterPool.gameObject.SetActive(false);

            _tables = new VoxelMaterialTables(_catalog);
            _generator = new ChunkGenerator(Session, _catalog);
            _mesher = new ChunkMesher(_tables, _meshBatch);
            _save = new WorldSaveService(Session.WorldId);
            _scatter = new ScatterSpawner(_scatterPool, _rockPrefab, _fallenTreePrefab, _treePrefab, _poiSocketPrefab);

            _streamer = new ChunkStreamer(
                Store,
                _generator,
                _mesher,
                _save,
                _scatter,
                _settings,
                _chunkRoot,
                _terrainMaterial,
                _waterMaterial,
                WorldPhysicsLayers.Terrain)
            {
                ColumnsPerTick = _columnsPerTick
            };

            _edit = new VoxelEditService(Store, _catalog, _streamer.MarkForRemesh);
            _connectivity = new ConnectivitySystem(_edit, _settings.CollapseVoxelThreshold);
            _islands = new FallingIslandSystem(
                _edit,
                _catalog,
                _tables,
                _debrisRoot,
                _terrainMaterial,
                WorldPhysicsLayers.FallingTerrain,
                _settings.CollapseRestDelay);

            ConfigureLayerCollisions();
        }

        private void Update()
        {
            if (!IsReady)
            {
                return;
            }

            Vector3 focus = _streamTarget != null ? _streamTarget.position : transform.position;
            _streamer.Tick(focus);
            _islands.Tick(Time.deltaTime);

            _autoSaveTimer += Time.deltaTime;
            if (_autoSaveTimer >= _settings.AutoSaveIntervalSeconds)
            {
                _autoSaveTimer = 0f;
                SaveNow();
            }
        }

        /// <summary>Damages a sphere of voxels and collapses whatever that leaves unsupported.</summary>
        public bool Dig(int3 center, int radius, int damage)
        {
            if (!IsReady)
            {
                return false;
            }

            _edit.Dig(center, radius, damage, _removed);
            _edit.FlushRemesh();

            if (_removed.Count == 0)
            {
                return false;
            }

            _islandsFound.Clear();
            _connectivity.FindUnsupported(_removed, _islandsFound);

            for (int i = 0; i < _islandsFound.Count; i++)
            {
                _islands.Spawn(_islandsFound[i]);
            }

            return true;
        }

        public bool Place(int3 center, int radius, byte materialId)
        {
            if (!IsReady)
            {
                return false;
            }

            int placed = _edit.Place(center, radius, materialId);
            _edit.FlushRemesh();
            return placed > 0;
        }

        public bool RaycastVoxel(Vector3 origin, Vector3 direction, float maxDistance, out int3 hit, out int3 placement)
        {
            if (!IsReady)
            {
                hit = default;
                placement = default;
                return false;
            }

            return VoxelRaycaster.Raycast(_edit, _catalog, origin, direction, maxDistance, out hit, out placement);
        }

        public int SaveNow()
        {
            if (_save == null || Store == null)
            {
                return 0;
            }

            return _save.SaveDirty(Store);
        }

        private Transform CreateRoot(string name)
        {
            var root = new GameObject(name);
            root.transform.SetParent(transform, false);
            return root.transform;
        }

        private static void ConfigureLayerCollisions()
        {
            int falling = WorldPhysicsLayers.FallingTerrain;
            int player = WorldPhysicsLayers.Player;

            // Collapsing terrain is scenery, not a hazard: it must not shove or hurt the player.
            if (falling >= 0 && player >= 0)
            {
                Physics.IgnoreLayerCollision(falling, player, true);
            }
        }

        private void OnApplicationQuit()
        {
            _quitting = true;
            SaveNow();
        }

        private void OnDestroy()
        {
            if (!_quitting)
            {
                SaveNow();
            }

            _islands?.Dispose();
            _streamer?.Dispose();
            _mesher?.Dispose();
            _generator?.Dispose();
            _tables?.Dispose();
            Store?.Dispose();

            _islands = null;
            _streamer = null;
            _mesher = null;
            _generator = null;
            _tables = null;
            Store = null;
        }
    }
}
