using Project.World;
using UnityEngine;

namespace Project.Player
{
    /// <summary>
    /// Holds the player frozen at the generator's spawn point until the streamer has built a
    /// collider underneath, then drops them onto it.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerSpawnAnchor : MonoBehaviour
    {
        [SerializeField] private WorldBootstrap _world;
        [SerializeField] private GreyboxPlayerController _controller;
        [SerializeField] private float _probeHeight = 200f;
        [SerializeField] private float _clearance = 2f;

        private bool _placed;

        private void Awake()
        {
            if (_controller == null)
            {
                _controller = GetComponent<GreyboxPlayerController>();
            }
        }

        private void Start()
        {
            if (_world == null || _controller == null)
            {
                enabled = false;
                return;
            }

            _controller.SetFrozen(true);
            _controller.Teleport(_world.SpawnPoint + Vector3.up * _probeHeight * 0.5f);
        }

        private void Update()
        {
            if (_placed || _world == null || !_world.IsReady)
            {
                return;
            }

            Vector3 origin = new Vector3(transform.position.x, _world.SpawnPoint.y + _probeHeight, transform.position.z);
            int mask = WorldPhysicsLayers.Terrain >= 0 ? 1 << WorldPhysicsLayers.Terrain : Physics.DefaultRaycastLayers;

            if (!Physics.Raycast(origin, Vector3.down, out RaycastHit hit, _probeHeight * 2f, mask, QueryTriggerInteraction.Ignore))
            {
                return;
            }

            _controller.Teleport(hit.point + Vector3.up * _clearance);
            _controller.SetFrozen(false);
            _placed = true;
            enabled = false;
        }
    }
}
