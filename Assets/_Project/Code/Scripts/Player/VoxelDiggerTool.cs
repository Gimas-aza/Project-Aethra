using Project.World;
using Unity.Mathematics;
using UnityEngine;

namespace Project.Player
{
    /// <summary>
    /// Greybox digging tool: Attack breaks voxels, Interact places them, Previous/Next change the
    /// brush radius. Aiming uses the voxel grid, not the smoothed collider surface.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class VoxelDiggerTool : MonoBehaviour
    {
        [SerializeField] private PlayerInputReader _input;
        [SerializeField] private WorldBootstrap _world;
        [SerializeField] private Transform _aimSource;

        [Header("Tool")]
        [SerializeField] private float _reach = 6f;
        [SerializeField] private int _damagePerHit = 1;
        [SerializeField] private float _repeatInterval = 0.16f;
        [SerializeField] [Range(1, 4)] private int _radius = 1;
        [SerializeField] private byte _placeMaterial = VoxelIds.Dirt;

        private float _nextActionTime;

        public int Radius => _radius;

        private void Awake()
        {
            if (_input == null)
            {
                _input = GetComponentInParent<PlayerInputReader>();
            }

            if (_aimSource == null)
            {
                _aimSource = transform;
            }
        }

        private void Update()
        {
            if (_input == null || !_input.IsReady || _world == null || !_world.IsReady)
            {
                return;
            }

            UpdateRadius();

            if (Time.time < _nextActionTime)
            {
                return;
            }

            if (_input.Attack.IsPressed())
            {
                if (TryAim(out int3 hit, out _))
                {
                    _world.Dig(hit, _radius, _damagePerHit);
                    _nextActionTime = Time.time + _repeatInterval;
                }
            }
            else if (_input.Interact.IsPressed())
            {
                if (TryAim(out _, out int3 placement))
                {
                    _world.Place(placement, _radius - 1, _placeMaterial);
                    _nextActionTime = Time.time + _repeatInterval;
                }
            }
        }

        private void UpdateRadius()
        {
            if (_input.Next.WasPressedThisFrame())
            {
                _radius = Mathf.Min(4, _radius + 1);
            }

            if (_input.Previous.WasPressedThisFrame())
            {
                _radius = Mathf.Max(1, _radius - 1);
            }
        }

        private bool TryAim(out int3 hit, out int3 placement)
        {
            return _world.RaycastVoxel(_aimSource.position, _aimSource.forward, _reach, out hit, out placement);
        }
    }
}
