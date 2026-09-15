using UnityEngine;
using UnityEngine.InputSystem;

namespace Project.Player
{
    /// <summary>
    /// Minimal first-person CharacterController for testing the voxel world. Nothing here is meant
    /// to survive into the real movement system.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CharacterController))]
    public sealed class GreyboxPlayerController : MonoBehaviour
    {
        [SerializeField] private PlayerInputReader _input;
        [SerializeField] private Transform _cameraPivot;

        [Header("Movement")]
        [SerializeField] private float _walkSpeed = 5.5f;
        [SerializeField] private float _sprintSpeed = 9f;
        [SerializeField] private float _jumpHeight = 1.4f;
        [SerializeField] private float _gravity = -22f;

        [Header("Look")]
        [SerializeField] private float _lookSensitivity = 0.12f;
        [SerializeField] private float _pitchLimit = 88f;

        private CharacterController _controller;
        private float _verticalVelocity;
        private float _pitch;
        private float _yaw;
        private bool _frozen;

        public bool IsFrozen => _frozen;

        private void Awake()
        {
            _controller = GetComponent<CharacterController>();

            if (_input == null)
            {
                _input = GetComponent<PlayerInputReader>();
            }

            _yaw = transform.eulerAngles.y;
        }

        private void Start()
        {
            LockCursor(true);
        }

        /// <summary>Held in place until the streamer has built ground under the spawn point.</summary>
        public void SetFrozen(bool frozen)
        {
            _frozen = frozen;
            _verticalVelocity = 0f;
        }

        public void Teleport(Vector3 position)
        {
            _controller.enabled = false;
            transform.position = position;
            _controller.enabled = true;
            _verticalVelocity = 0f;
        }

        private void Update()
        {
            if (_input == null || !_input.IsReady)
            {
                return;
            }

            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                LockCursor(Cursor.lockState != CursorLockMode.Locked);
            }

            UpdateLook();

            if (_frozen)
            {
                return;
            }

            UpdateMove();
        }

        private void UpdateLook()
        {
            if (Cursor.lockState != CursorLockMode.Locked)
            {
                return;
            }

            Vector2 look = _input.Look.ReadValue<Vector2>();
            _yaw += look.x * _lookSensitivity;
            _pitch = Mathf.Clamp(_pitch - look.y * _lookSensitivity, -_pitchLimit, _pitchLimit);

            transform.rotation = Quaternion.Euler(0f, _yaw, 0f);

            if (_cameraPivot != null)
            {
                _cameraPivot.localRotation = Quaternion.Euler(_pitch, 0f, 0f);
            }
        }

        private void UpdateMove()
        {
            Vector2 move = _input.Move.ReadValue<Vector2>();
            Vector3 wish = transform.right * move.x + transform.forward * move.y;
            if (wish.sqrMagnitude > 1f)
            {
                wish.Normalize();
            }

            float speed = _input.Sprint.IsPressed() ? _sprintSpeed : _walkSpeed;

            if (_controller.isGrounded)
            {
                if (_verticalVelocity < 0f)
                {
                    _verticalVelocity = -2f;
                }

                if (_input.Jump.WasPressedThisFrame())
                {
                    _verticalVelocity = Mathf.Sqrt(-2f * _gravity * _jumpHeight);
                }
            }
            else
            {
                _verticalVelocity += _gravity * Time.deltaTime;
            }

            Vector3 velocity = wish * speed + Vector3.up * _verticalVelocity;
            _controller.Move(velocity * Time.deltaTime);
        }

        private static void LockCursor(bool locked)
        {
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }
    }
}
