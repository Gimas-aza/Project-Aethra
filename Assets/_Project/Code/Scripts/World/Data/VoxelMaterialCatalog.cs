using UnityEngine;

namespace Project.World
{
    [CreateAssetMenu(fileName = "VoxelMaterialCatalog", menuName = "Aethra/World/Voxel Material Catalog")]
    public sealed class VoxelMaterialCatalog : ScriptableObject
    {
        [SerializeField] private VoxelMaterial[] _materials;

        private VoxelMaterial[] _byId;

        public VoxelMaterial Get(byte id)
        {
            EnsureLookup();
            if (id >= _byId.Length)
            {
                return null;
            }

            return _byId[id];
        }

        public bool IsLiquid(byte id)
        {
            VoxelMaterial material = Get(id);
            return material != null && material.IsLiquid;
        }

        public bool SupportsTerrain(byte id)
        {
            if (id == VoxelIds.Air)
            {
                return false;
            }

            VoxelMaterial material = Get(id);
            return material != null && material.SupportsTerrain;
        }

        private void OnEnable()
        {
            _byId = null;
        }

        private void EnsureLookup()
        {
            if (_byId != null)
            {
                return;
            }

            int maxId = 0;
            if (_materials != null)
            {
                for (int i = 0; i < _materials.Length; i++)
                {
                    VoxelMaterial material = _materials[i];
                    if (material != null && material.Id > maxId)
                    {
                        maxId = material.Id;
                    }
                }
            }

            _byId = new VoxelMaterial[maxId + 1];
            if (_materials == null)
            {
                return;
            }

            for (int i = 0; i < _materials.Length; i++)
            {
                VoxelMaterial material = _materials[i];
                if (material == null)
                {
                    continue;
                }

                _byId[material.Id] = material;
            }
        }
    }
}
