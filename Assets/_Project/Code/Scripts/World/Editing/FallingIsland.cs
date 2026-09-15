using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

namespace Project.World.Editing
{
    /// <summary>
    /// A detached lump of terrain under physics. Carries the voxel grid it was cut from so it can be
    /// stamped back into the world once it stops moving.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody))]
    public sealed class FallingIsland : MonoBehaviour
    {
        private const float RestSpeedSq = 0.05f;

        private readonly List<BoxCollider> _colliders = new List<BoxCollider>();

        private Rigidbody _body;
        private MeshFilter _filter;
        private MeshRenderer _renderer;
        private PhysicsMaterial _surface;
        private float _restTimer;
        private float _restDelay;

        public Mesh Mesh { get; private set; }
        public int3 OriginVoxel { get; private set; }
        public int3 Size { get; private set; }
        public byte[] Materials { get; private set; }
        public bool HasSettled { get; private set; }

        /// <summary>Seconds since this island was cut loose.</summary>
        public float Age { get; private set; }

        public static FallingIsland Create(Transform parent, Material material, int layer)
        {
            var root = new GameObject("FallingIsland");
            root.transform.SetParent(parent, false);

            if (layer >= 0)
            {
                root.layer = layer;
            }

            FallingIsland island = root.AddComponent<FallingIsland>();
            island.Build(material);
            return island;
        }

        private void Build(Material material)
        {
            Mesh = new Mesh { name = "FallingIsland", hideFlags = HideFlags.DontSave };
            Mesh.MarkDynamic();

            _filter = gameObject.AddComponent<MeshFilter>();
            _filter.sharedMesh = Mesh;

            _renderer = gameObject.AddComponent<MeshRenderer>();
            _renderer.sharedMaterial = material;

            _body = GetComponent<Rigidbody>();
            _body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            _body.interpolation = RigidbodyInterpolation.Interpolate;

            // Rubble should slump and stop, not bounce and roll like a rigid stick.
            _body.linearDamping = 0.15f;
            _body.angularDamping = 2.5f;

            _surface = new PhysicsMaterial("FallingTerrain")
            {
                dynamicFriction = 0.9f,
                staticFriction = 0.95f,
                bounciness = 0f,
                frictionCombine = PhysicsMaterialCombine.Maximum,
                bounceCombine = PhysicsMaterialCombine.Minimum,
                hideFlags = HideFlags.DontSave
            };

            gameObject.SetActive(false);
        }

        public void Activate(int3 originVoxel, int3 size, byte[] materials, IReadOnlyList<BoxBounds> boxes, float mass, float restDelay)
        {
            OriginVoxel = originVoxel;
            Size = size;
            Materials = materials;
            HasSettled = false;
            Age = 0f;
            _restTimer = 0f;
            _restDelay = restDelay;

            transform.SetPositionAndRotation(
                new Vector3(originVoxel.x, originVoxel.y, originVoxel.z),
                Quaternion.identity);

            SyncColliders(boxes);

            gameObject.SetActive(true);

            _body.mass = mass;
            _body.isKinematic = false;
            _body.linearVelocity = Vector3.zero;
            _body.angularVelocity = Vector3.zero;
            _body.WakeUp();
        }

        public void TickRest(float deltaTime)
        {
            if (HasSettled)
            {
                return;
            }

            Age += deltaTime;

            bool resting = _body.IsSleeping()
                           || (_body.linearVelocity.sqrMagnitude < RestSpeedSq
                               && _body.angularVelocity.sqrMagnitude < RestSpeedSq);

            _restTimer = resting ? _restTimer + deltaTime : 0f;

            if (_restTimer >= _restDelay)
            {
                HasSettled = true;
            }
        }

        public void Recycle()
        {
            HasSettled = false;
            Age = 0f;
            _restTimer = 0f;
            Materials = null;
            _body.isKinematic = true;
            gameObject.SetActive(false);
            Mesh.Clear(false);

            for (int i = 0; i < _colliders.Count; i++)
            {
                _colliders[i].enabled = false;
            }
        }

        private void SyncColliders(IReadOnlyList<BoxBounds> boxes)
        {
            while (_colliders.Count < boxes.Count)
            {
                BoxCollider added = gameObject.AddComponent<BoxCollider>();
                added.sharedMaterial = _surface;
                _colliders.Add(added);
            }

            for (int i = 0; i < _colliders.Count; i++)
            {
                BoxCollider collider = _colliders[i];
                if (i >= boxes.Count)
                {
                    collider.enabled = false;
                    continue;
                }

                BoxBounds box = boxes[i];
                collider.enabled = true;
                collider.center = new Vector3(
                    box.Min.x + box.Size.x * 0.5f,
                    box.Min.y + box.Size.y * 0.5f,
                    box.Min.z + box.Size.z * 0.5f);
                collider.size = new Vector3(box.Size.x, box.Size.y, box.Size.z);
            }
        }

        private void OnDestroy()
        {
            if (Mesh != null)
            {
                Destroy(Mesh);
            }

            if (_surface != null)
            {
                Destroy(_surface);
            }
        }

        public struct BoxBounds
        {
            public int3 Min;
            public int3 Size;
        }
    }
}
