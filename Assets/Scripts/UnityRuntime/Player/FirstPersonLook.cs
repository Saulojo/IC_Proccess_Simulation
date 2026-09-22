using UnityEngine;

namespace IndustrialSim.UnityRuntime.Player
{
    /// <summary>
    /// Controla a rotação horizontal do personagem e a rotação
    /// vertical do ponto utilizado pela câmera.
    /// </summary>
    [DefaultExecutionOrder(-50)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PlayerInputReader))]
    public sealed class FirstPersonLook : MonoBehaviour
    {
        [Header("References")]

        [SerializeField]
        private Transform cameraTarget;

        [Header("Sensitivity")]

        [SerializeField]
        [Min(0f)]
        private float mouseSensitivity = 0.08f;

        [SerializeField]
        [Min(0f)]
        private float gamepadLookSpeed = 180f;

        [Header("Vertical Limits")]

        [SerializeField]
        [Range(-89f, 0f)]
        private float minimumPitch = -85f;

        [SerializeField]
        [Range(0f, 89f)]
        private float maximumPitch = 85f;

        [Header("Cursor")]

        [SerializeField]
        private bool lockCursorOnEnable = true;

        private PlayerInputReader _input;

        private float _yaw;
        private float _pitch;

        private void Awake()
        {
            _input = GetComponent<PlayerInputReader>();

            if (cameraTarget == null)
            {
                throw new MissingReferenceException(
                    $"{nameof(FirstPersonLook)} em '{name}' não possui Camera Target.");
            }

            _yaw = transform.eulerAngles.y;

            _pitch = Mathf.DeltaAngle(
                0f,
                cameraTarget.localEulerAngles.x);
        }

        private void OnEnable()
        {
            if (lockCursorOnEnable)
            {
                SetCursorLocked(true);
            }
        }

        private void OnDisable()
        {
            SetCursorLocked(false);
        }

        private void Update()
        {
            if (Cursor.lockState != CursorLockMode.Locked)
            {
                return;
            }

            Vector2 lookInput = _input.Look;

            float sensitivity;

            if (_input.LookUsesPointerDelta)
            {
                // Mouse retorna deslocamento em pixels.
                sensitivity = mouseSensitivity;
            }
            else
            {
                // Controle retorna um eixo contínuo entre -1 e 1.
                sensitivity =
                    gamepadLookSpeed * Time.deltaTime;
            }

            _yaw += lookInput.x * sensitivity;

            _pitch -= lookInput.y * sensitivity;
            _pitch = Mathf.Clamp(
                _pitch,
                minimumPitch,
                maximumPitch);

            transform.rotation =
                Quaternion.Euler(0f, _yaw, 0f);

            cameraTarget.localRotation =
                Quaternion.Euler(_pitch, 0f, 0f);
        }

        public void SetCursorLocked(bool locked)
        {
            Cursor.lockState = locked
                ? CursorLockMode.Locked
                : CursorLockMode.None;

            Cursor.visible = !locked;
        }

        private void OnValidate()
        {
            minimumPitch =
                Mathf.Clamp(minimumPitch, -89f, 0f);

            maximumPitch =
                Mathf.Clamp(maximumPitch, 0f, 89f);
        }
    }
}