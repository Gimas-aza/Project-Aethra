using System.Collections.Generic;
using Project.World.Generation;
using UnityEngine;

namespace Project.World.Streaming
{
    /// <summary>Pools the greybox props emitted by <see cref="ScatterStage"/>.</summary>
    public sealed class ScatterSpawner
    {
        private readonly GameObject[] _prefabs;
        private readonly Stack<GameObject>[] _pools;
        private readonly Transform _poolRoot;

        public ScatterSpawner(Transform poolRoot, GameObject rock, GameObject fallenTree, GameObject tree, GameObject poiSocket)
        {
            _poolRoot = poolRoot;
            _prefabs = new[] { rock, fallenTree, tree, poiSocket };
            _pools = new Stack<GameObject>[_prefabs.Length];

            for (int i = 0; i < _pools.Length; i++)
            {
                _pools[i] = new Stack<GameObject>();
            }
        }

        public void Spawn(IReadOnlyList<ScatterPlacement> placements, Transform parent)
        {
            for (int i = 0; i < placements.Count; i++)
            {
                ScatterPlacement placement = placements[i];
                int kind = placement.Kind;
                if (kind < 0 || kind >= _prefabs.Length || _prefabs[kind] == null)
                {
                    continue;
                }

                GameObject instance = Rent(kind);
                Transform t = instance.transform;
                t.SetParent(parent, false);
                t.position = new Vector3(placement.Voxel.x + 0.5f, placement.Voxel.y, placement.Voxel.z + 0.5f);
                t.rotation = Quaternion.Euler(0f, placement.Yaw, 0f);
                t.localScale = Vector3.one * placement.Scale;
                instance.SetActive(true);
            }
        }

        public void Recycle(Transform parent)
        {
            for (int i = parent.childCount - 1; i >= 0; i--)
            {
                GameObject child = parent.GetChild(i).gameObject;
                var tag = child.GetComponent<ScatterInstance>();
                child.SetActive(false);
                child.transform.SetParent(_poolRoot, false);

                if (tag != null)
                {
                    _pools[tag.Kind].Push(child);
                }
                else
                {
                    Object.Destroy(child);
                }
            }
        }

        private GameObject Rent(int kind)
        {
            Stack<GameObject> pool = _pools[kind];
            if (pool.Count > 0)
            {
                return pool.Pop();
            }

            GameObject instance = Object.Instantiate(_prefabs[kind], _poolRoot);
            ScatterInstance tag = instance.GetComponent<ScatterInstance>();
            if (tag == null)
            {
                tag = instance.AddComponent<ScatterInstance>();
            }

            tag.Kind = kind;
            return instance;
        }
    }

    /// <summary>Marks which pool an instantiated prop belongs to.</summary>
    public sealed class ScatterInstance : MonoBehaviour
    {
        public int Kind;
    }
}
