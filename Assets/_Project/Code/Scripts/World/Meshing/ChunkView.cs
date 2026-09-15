using UnityEngine;

namespace Project.World.Meshing
{
    /// <summary>
    /// Runtime presentation of one chunk: opaque Surface Nets mesh with a static non-convex
    /// collider, a transparent water child, and a parent for scattered props.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ChunkView : MonoBehaviour
    {
        private MeshFilter _filter;
        private MeshRenderer _renderer;
        private MeshCollider _collider;
        private MeshFilter _waterFilter;
        private MeshRenderer _waterRenderer;
        private GameObject _waterObject;

        public Mesh TerrainMesh { get; private set; }
        public Mesh WaterMesh { get; private set; }
        public Transform ScatterRoot { get; private set; }
        public ChunkKey Key { get; private set; }

        public static ChunkView Create(Transform parent, Material terrainMaterial, Material waterMaterial, int terrainLayer)
        {
            var root = new GameObject("Chunk");
            root.transform.SetParent(parent, false);

            ChunkView view = root.AddComponent<ChunkView>();
            view.Build(terrainMaterial, waterMaterial, terrainLayer);
            return view;
        }

        private void Build(Material terrainMaterial, Material waterMaterial, int terrainLayer)
        {
            if (terrainLayer >= 0)
            {
                gameObject.layer = terrainLayer;
            }

            TerrainMesh = new Mesh { name = "ChunkTerrain", hideFlags = HideFlags.DontSave };
            TerrainMesh.MarkDynamic();

            WaterMesh = new Mesh { name = "ChunkWater", hideFlags = HideFlags.DontSave };
            WaterMesh.MarkDynamic();

            _filter = gameObject.AddComponent<MeshFilter>();
            _filter.sharedMesh = TerrainMesh;

            _renderer = gameObject.AddComponent<MeshRenderer>();
            _renderer.sharedMaterial = terrainMaterial;
            _renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;

            _collider = gameObject.AddComponent<MeshCollider>();
            _collider.convex = false;
            _collider.sharedMesh = null;

            _waterObject = new GameObject("Water");
            _waterObject.transform.SetParent(transform, false);
            _waterFilter = _waterObject.AddComponent<MeshFilter>();
            _waterFilter.sharedMesh = WaterMesh;
            _waterRenderer = _waterObject.AddComponent<MeshRenderer>();
            _waterRenderer.sharedMaterial = waterMaterial;
            _waterRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            var scatter = new GameObject("Scatter");
            scatter.transform.SetParent(transform, false);
            ScatterRoot = scatter.transform;
        }

        public void Bind(ChunkKey key)
        {
            Key = key;
            int size = WorldMetrics.ChunkSize;
            transform.localPosition = new Vector3(key.X * size, key.Y * size, key.Z * size);
            gameObject.name = $"Chunk {key}";
            gameObject.SetActive(true);
        }

        /// <summary>Call after the mesh data has been written. Re-cooks the collider when needed.</summary>
        public void RefreshParts(bool hasTerrain, bool hasWater)
        {
            _renderer.enabled = hasTerrain;

            // Re-assigning is what triggers the physics cook for the updated mesh.
            _collider.sharedMesh = null;
            if (hasTerrain)
            {
                _collider.sharedMesh = TerrainMesh;
            }

            _waterObject.SetActive(hasWater);
        }

        public void Recycle()
        {
            gameObject.SetActive(false);
            _collider.sharedMesh = null;
            TerrainMesh.Clear(false);
            WaterMesh.Clear(false);
        }

        private void OnDestroy()
        {
            if (TerrainMesh != null)
            {
                Destroy(TerrainMesh);
            }

            if (WaterMesh != null)
            {
                Destroy(WaterMesh);
            }
        }
    }
}
