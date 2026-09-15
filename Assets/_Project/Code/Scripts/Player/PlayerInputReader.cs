using UnityEngine;
using UnityEngine.InputSystem;

namespace Project.Player
{
    /// <summary>
    /// Resolves the Player action map once and owns its enabled state, so the controller and the
    /// voxel tool share one set of actions instead of fighting over Enable/Disable.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerInputReader : MonoBehaviour
    {
        [SerializeField] private InputActionAsset _actions;
        [SerializeField] private string _mapName = "Player";

        private InputActionMap _map;

        public InputAction Move { get; private set; }
        public InputAction Look { get; private set; }
        public InputAction Jump { get; private set; }
        public InputAction Sprint { get; private set; }
        public InputAction Crouch { get; private set; }
        public InputAction Attack { get; private set; }
        public InputAction Interact { get; private set; }
        public InputAction Previous { get; private set; }
        public InputAction Next { get; private set; }

        public bool IsReady => _map != null;

        private void Awake()
        {
            if (_actions == null)
            {
                Debug.LogError("PlayerInputReader is missing an InputActionAsset.", this);
                enabled = false;
                return;
            }

            _map = _actions.FindActionMap(_mapName, true);

            Move = _map.FindAction("Move", true);
            Look = _map.FindAction("Look", true);
            Jump = _map.FindAction("Jump", true);
            Sprint = _map.FindAction("Sprint", true);
            Crouch = _map.FindAction("Crouch", true);
            Attack = _map.FindAction("Attack", true);
            Interact = _map.FindAction("Interact", true);
            Previous = _map.FindAction("Previous", true);
            Next = _map.FindAction("Next", true);
        }

        private void OnEnable()
        {
            _map?.Enable();
        }

        private void OnDisable()
        {
            _map?.Disable();
        }
    }
}
