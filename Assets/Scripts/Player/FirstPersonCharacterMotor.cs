using UnityEngine;

namespace HillbillyTaxi.Player
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CharacterController))]
    public sealed class FirstPersonCharacterMotor : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Transform cameraPitchTransform;

        [Header("Movement")]
        [SerializeField, Min(0f)] private float walkSpeed = 4.5f;
        [SerializeField, Min(0f)] private float sprintSpeed = 7f;
        [SerializeField, Min(0f)] private float groundAcceleration = 24f;
        [SerializeField, Min(0f)] private float airAcceleration = 7f;
        [SerializeField, Min(0f)] private float jumpHeight = 1.2f;
        [SerializeField] private float gravity = -25f;
        [SerializeField] private float groundedVerticalVelocity = -2f;

        [Header("Look")]
        [Tooltip("Degrees per mouse delta unit.")]
        [SerializeField, Min(0f)] private float mouseSensitivity = 0.08f;
        [Tooltip("Degrees per second at full controller-stick deflection.")]
        [SerializeField, Min(0f)] private float gamepadLookSpeed = 180f;
        [SerializeField, Range(1f, 89f)] private float maximumPitch = 85f;

        private CharacterController _controller;
        private Vector3 _horizontalVelocity;
        private float _verticalVelocity;
        private float _pitch;

        public Vector3 Velocity => _horizontalVelocity + Vector3.up * _verticalVelocity;
        public bool IsGrounded => _controller != null && _controller.enabled && _controller.isGrounded;

        private void Awake()
        {
            _controller = GetComponent<CharacterController>();

            if (cameraPitchTransform == null)
            {
                Debug.LogError(
                    $"{nameof(FirstPersonCharacterMotor)} on '{name}' needs a camera pitch transform.",
                    this);
            }
        }

        public void SetSimulationEnabled(bool enabled)
        {
            if (_controller == null)
            {
                _controller = GetComponent<CharacterController>();
            }

            _horizontalVelocity = Vector3.zero;
            _verticalVelocity = 0f;
            _controller.enabled = enabled;
        }

        public void Tick(CharacterInputFrame input, float deltaTime)
        {
            if (deltaTime <= 0f || _controller == null || !_controller.enabled)
            {
                return;
            }

            UpdateLook(input, deltaTime);
            UpdateMovement(input, deltaTime);
        }

        private void UpdateLook(CharacterInputFrame input, float deltaTime)
        {
            if (cameraPitchTransform == null)
            {
                return;
            }

            float lookMultiplier = input.LookComesFromMouse
                ? mouseSensitivity
                : gamepadLookSpeed * deltaTime;

            float yawDelta = input.Look.x * lookMultiplier;
            float pitchDelta = input.Look.y * lookMultiplier;

            transform.Rotate(0f, yawDelta, 0f, Space.Self);

            _pitch = Mathf.Clamp(_pitch - pitchDelta, -maximumPitch, maximumPitch);
            cameraPitchTransform.localRotation = Quaternion.Euler(_pitch, 0f, 0f);
        }

        private void UpdateMovement(CharacterInputFrame input, float deltaTime)
        {
            bool wasGrounded = _controller.isGrounded;

            if (wasGrounded && _verticalVelocity < 0f)
            {
                _verticalVelocity = groundedVerticalVelocity;
            }

            Vector3 desiredDirection = transform.right * input.Move.x + transform.forward * input.Move.y;
            desiredDirection = Vector3.ClampMagnitude(desiredDirection, 1f);

            float targetSpeed = input.SprintHeld ? sprintSpeed : walkSpeed;
            Vector3 desiredHorizontalVelocity = desiredDirection * targetSpeed;
            float acceleration = wasGrounded ? groundAcceleration : airAcceleration;

            _horizontalVelocity = Vector3.MoveTowards(
                _horizontalVelocity,
                desiredHorizontalVelocity,
                acceleration * deltaTime);

            if (input.JumpPressed && wasGrounded)
            {
                _verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);
            }

            _verticalVelocity += gravity * deltaTime;

            Vector3 motion = (_horizontalVelocity + Vector3.up * _verticalVelocity) * deltaTime;
            CollisionFlags collisionFlags = _controller.Move(motion);

            if ((collisionFlags & CollisionFlags.Above) != 0 && _verticalVelocity > 0f)
            {
                _verticalVelocity = 0f;
            }

            if ((collisionFlags & CollisionFlags.Below) != 0 && _verticalVelocity < 0f)
            {
                _verticalVelocity = groundedVerticalVelocity;
            }
        }

        private void OnValidate()
        {
            sprintSpeed = Mathf.Max(sprintSpeed, walkSpeed);
            gravity = Mathf.Min(gravity, -0.01f);
            groundedVerticalVelocity = Mathf.Min(groundedVerticalVelocity, -0.01f);
        }
    }
}
