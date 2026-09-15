using UnityEngine;

namespace Project.World
{
    [CreateAssetMenu(fileName = "WorldGenSettings", menuName = "Aethra/World/World Gen Settings")]
    public sealed class WorldGenSettings : ScriptableObject
    {
        [Header("Disk")]
        [SerializeField] [Range(512, 1024)] private int _worldRadius = 1024;
        [SerializeField] [Range(64, 256)] private int _worldHeight = 128;
        [SerializeField] [Range(8, 64)] private int _seaLevel = 36;
        [SerializeField] private int _seed = 1;

        [Header("Streaming")]
        [SerializeField] [Range(128, 512)] private float _streamRadius = 256f;
        [SerializeField] [Range(16, 128)] private float _unloadHysteresis = 32f;

        [Header("Collapse")]
        [SerializeField] [Range(8, 512)] private int _collapseVoxelThreshold = 300;
        [SerializeField] [Range(0.5f, 10f)] private float _collapseRestDelay = 3f;

        [Header("Save")]
        [SerializeField] [Range(60, 600)] private float _autoSaveIntervalSeconds = 420f;

        public int WorldRadius => _worldRadius;
        public int WorldHeight => _worldHeight;
        public int SeaLevel => _seaLevel;
        public int Seed => _seed;
        public float StreamRadius => _streamRadius;
        public float UnloadHysteresis => _unloadHysteresis;
        public int CollapseVoxelThreshold => _collapseVoxelThreshold;
        public float CollapseRestDelay => _collapseRestDelay;
        public float AutoSaveIntervalSeconds => _autoSaveIntervalSeconds;
        public int VerticalChunkCount => (_worldHeight + WorldMetrics.ChunkSize - 1) / WorldMetrics.ChunkSize;
    }
}
