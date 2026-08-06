using UnityEngine;
using UnityEngine.InputSystem;

namespace IndustrialSim.UnityRuntime.Player
{
    /// <summary>
    /// Centraliza o estado das entradas utilizadas pelo personagem.
    /// Outros componentes não precisam conhecer diretamente o Input System.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PlayerInput))]
    public sealed class PlayerInputReader : MonoBehaviour
    {
        private const string PlayerActionMapName = "Player";
        private const string MoveActionName = "Move";
        private const string LookActionName = "Look";
        private const string JumpActionName = "Jump";
        private const string SprintActionName = "Sprint";

        private PlayerInput _playerInput;

        private InputAction _moveAction;
        private InputAction _lookAction;
        private InputAction _jumpAction;
        private InputAction _sprintAction;

        private bool _jumpPressed;

        public Vector2 Move { get; private set; }

        public Vector2 Look { get; private set; }

        public bool SprintHeld { get; private set; }

        /// <summary>
        /// Indica que o valor de Look veio de um dispositivo baseado
        /// em delta, como o mouse.
        /// </summary>
        public bool LookUsesPointerDelta { get; private set; }

        private void Awake()
        {
            _playerInput = GetComponent<PlayerInput>();

            if (_playerInput.actions == null)
            {
                throw new MissingReferenceException(
                    $"{nameof(PlayerInput)} em '{name}' não possui um Input Actions Asset.");
            }

            InputActionMap playerMap =
                _playerInput.actions.FindActionMap(
                    PlayerActionMapName,
                    throwIfNotFound: true);

            _moveAction =
                playerMap.FindAction(MoveActionName, throwIfNotFound: true);

            _lookAction =
                playerMap.FindAction(LookActionName, throwIfNotFound: true);

            _jumpAction =
                playerMap.FindAction(JumpActionName, throwIfNotFound: true);

            _sprintAction =
                playerMap.FindAction(SprintActionName, throwIfNotFound: true);
        }

        private void OnEnable()
        {
            _moveAction.performed += OnMove;
            _moveAction.canceled += OnMove;

            _lookAction.performed += OnLook;
            _lookAction.canceled += OnLook;

            _jumpAction.performed += OnJump;

            _sprintAction.performed += OnSprint;
            _sprintAction.canceled += OnSprint;
        }

        private void OnDisable()
        {
            _moveAction.performed -= OnMove;
            _moveAction.canceled -= OnMove;

            _lookAction.performed -= OnLook;
            _lookAction.canceled -= OnLook;

            _jumpAction.performed -= OnJump;

            _sprintAction.performed -= OnSprint;
            _sprintAction.canceled -= OnSprint;

            Move = Vector2.zero;
            Look = Vector2.zero;
            SprintHeld = false;
            _jumpPressed = false;
        }

        public bool ConsumeJumpPressed()
        {
            bool jumpPressed = _jumpPressed;
            _jumpPressed = false;

            return jumpPressed;
        }

        private void OnMove(InputAction.CallbackContext context)
        {
            Move = context.ReadValue<Vector2>();
        }

        private void OnLook(InputAction.CallbackContext context)
        {
            Look = context.ReadValue<Vector2>();

            if (context.performed && context.control != null)
            {
                LookUsesPointerDelta =
                    context.control.device is Pointer;
            }
        }

        private void OnJump(InputAction.CallbackContext context)
        {
            if (context.performed)
            {
                _jumpPressed = true;
            }
        }

        private void OnSprint(InputAction.CallbackContext context)
        {
            SprintHeld = context.ReadValueAsButton();
        }
    }
}