using UnityEngine;

namespace IndustrialSim.UnityRuntime.Player
{
    /// <summary>
    /// Controla a locomoção do personagem através de CharacterController.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CharacterController))]
    [RequireComponent(typeof(PlayerInputReader))]
    public sealed class FirstPersonMotor : MonoBehaviour
    {
        [Header("Horizontal Movement")]

        [SerializeField]
        [Min(0f)]
        private float walkSpeed = 4.5f;

        [SerializeField]
        [Min(0f)]
        private float sprintSpeed = 7f;

        [SerializeField]
        [Min(0f)]
        private float groundAcceleration = 25f;

        [SerializeField]
        [Min(0f)]
        private float airAcceleration = 8f;

        [Header("Vertical Movement")]

        [SerializeField]
        [Min(0f)]
        private float jumpHeight = 1.2f;

        [SerializeField]
        private float gravity = -25f;

        [SerializeField]
        [Min(0f)]
        private float groundedStickForce = 2f;

        [SerializeField]
        private float terminalVelocity = -50f;

        private CharacterController _characterController;
        private PlayerInputReader _input;

        private Vector3 _planarVelocity;
        private float _verticalVelocity;

        public Vector3 Velocity =>
            _planarVelocity + Vector3.up * _verticalVelocity;

        public bool IsGrounded =>
            _characterController != null &&
            _characterController.isGrounded;

        private void Awake()
        {
            _characterController = GetComponent<CharacterController>();
            _input = GetComponent<PlayerInputReader>();
        }

        private void Update()
        {
            float deltaTime = Time.deltaTime;

            if (deltaTime <= 0f)
            {
                return;
            }

            bool grounded = _characterController.isGrounded;
            bool jumpPressed = _input.ConsumeJumpPressed();

            UpdateHorizontalVelocity(grounded, deltaTime);
            UpdateVerticalVelocity(grounded, jumpPressed, deltaTime);

            Vector3 motion =
                _planarVelocity +
                Vector3.up * _verticalVelocity;

            CollisionFlags collisionFlags =
                _characterController.Move(motion * deltaTime);

            ResolveVerticalCollisions(collisionFlags);
        }

        private void UpdateHorizontalVelocity(
            bool grounded,
            float deltaTime)
        {
            Vector2 inputValue =
                Vector2.ClampMagnitude(_input.Move, 1f);

            Vector3 desiredDirection =
                transform.right * inputValue.x +
                transform.forward * inputValue.y;

            float targetSpeed =
                _input.SprintHeld
                    ? sprintSpeed
                    : walkSpeed;

            Vector3 targetVelocity =
                desiredDirection * targetSpeed;

            float acceleration =
                grounded
                    ? groundAcceleration
                    : airAcceleration;

            _planarVelocity = Vector3.MoveTowards(
                _planarVelocity,
                targetVelocity,
                acceleration * deltaTime);
        }

        private void UpdateVerticalVelocity(
            bool grounded,
            bool jumpPressed,
            float deltaTime)
        {
            if (grounded && _verticalVelocity < 0f)
            {
                _verticalVelocity = -groundedStickForce;
            }

            if (grounded && jumpPressed)
            {
                _verticalVelocity = Mathf.Sqrt(
                    jumpHeight * -2f * gravity);
            }

            _verticalVelocity += gravity * deltaTime;

            _verticalVelocity = Mathf.Max(
                _verticalVelocity,
                terminalVelocity);
        }

        private void ResolveVerticalCollisions(
            CollisionFlags collisionFlags)
        {
            bool hitCeiling =
                (collisionFlags & CollisionFlags.Above) != 0;

            bool hitGround =
                (collisionFlags & CollisionFlags.Below) != 0;

            if (hitCeiling && _verticalVelocity > 0f)
            {
                _verticalVelocity = 0f;
            }

            if (hitGround && _verticalVelocity < 0f)
            {
                _verticalVelocity = -groundedStickForce;
            }
        }

        private void OnValidate()
        {
            if (sprintSpeed < walkSpeed)
            {
                sprintSpeed = walkSpeed;
            }

            gravity = Mathf.Min(gravity, -0.01f);
            terminalVelocity = Mathf.Min(terminalVelocity, gravity);
        }
    }
}