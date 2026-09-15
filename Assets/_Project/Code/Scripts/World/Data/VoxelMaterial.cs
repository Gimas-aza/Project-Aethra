using UnityEngine;

namespace Project.World
{
    [CreateAssetMenu(fileName = "VoxelMaterial", menuName = "Aethra/World/Voxel Material")]
    public sealed class VoxelMaterial : ScriptableObject
    {
        [SerializeField] private byte _id;
        [SerializeField] private string _displayName;
        [SerializeField] private int _hardnessTier;
        [SerializeField] private byte _maxDamage = 1;
        [SerializeField] private bool _isLiquid;
        [SerializeField] private bool _supportsTerrain = true;
        [SerializeField] private Color _color = Color.gray;
        [SerializeField] private Material _renderMaterial;

        public byte Id => _id;
        public string DisplayName => _displayName;
        public int HardnessTier => _hardnessTier;
        public byte MaxDamage => _maxDamage;
        public bool IsLiquid => _isLiquid;
        public bool SupportsTerrain => _supportsTerrain && !_isLiquid;
        public Color Color => _color;
        public Material RenderMaterial => _renderMaterial;
    }
}
