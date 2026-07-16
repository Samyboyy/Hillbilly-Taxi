using HillbillyTaxi.Input;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace HillbillyTaxi.Player
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkObject))]
    [RequireComponent(typeof(CharacterController))]
    [RequireComponent(typeof(PlayerInputReader))]
    [RequireComponent(typeof(FirstPersonCharacterMotor))]
    public sealed class NetworkPlayerCharacter : NetworkBehaviour
    {
        [Header("Owner-only presentation")]
        [Tooltip("Put the first-person camera rig and its AudioListener here.")]
        [SerializeField] private GameObject[] ownerOnlyObjects;

        [Tooltip("Optional body renderers that should be hidden from the owning first-person camera.")]
        [SerializeField] private Renderer[] renderersHiddenFromOwner;

        private PlayerInputReader _inputReader;
        private FirstPersonCharacterMotor _motor;
        private bool _localControlEnabled;

        private void Awake()
        {
            _inputReader = GetComponent<PlayerInputReader>();
            _motor = GetComponent<FirstPersonCharacterMotor>();
            SetLocalControl(false);
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            SetLocalControl(IsOwner);
        }

        public override void OnNetworkDespawn()
        {
            SetLocalControl(false);
            base.OnNetworkDespawn();
        }

        public override void OnGainedOwnership()
        {
            base.OnGainedOwnership();
            SetLocalControl(true);
        }

        public override void OnLostOwnership()
        {
            SetLocalControl(false);
            base.OnLostOwnership();
        }

        private void Update()
        {
            if (!IsSpawned || !IsOwner || !_localControlEnabled)
            {
                return;
            }

            HandleCursorState();

            CharacterInputFrame input = _inputReader.ReadFrame();

            if (Cursor.lockState != CursorLockMode.Locked)
            {
                input = input.WithoutLook();
            }

            _motor.Tick(input, Time.deltaTime);
        }

        private void SetLocalControl(bool enabled)
        {
            bool previouslyHadLocalControl = _localControlEnabled;
            _localControlEnabled = enabled;
            _inputReader.SetInputEnabled(enabled);
            _motor.SetSimulationEnabled(enabled);

            if (ownerOnlyObjects != null)
            {
                foreach (GameObject ownerOnlyObject in ownerOnlyObjects)
                {
                    if (ownerOnlyObject != null)
                    {
                        ownerOnlyObject.SetActive(enabled);
                    }
                }
            }

            if (renderersHiddenFromOwner != null)
            {
                foreach (Renderer targetRenderer in renderersHiddenFromOwner)
                {
                    if (targetRenderer != null)
                    {
                        targetRenderer.enabled = !enabled;
                    }
                }
            }

            if (enabled)
            {
                CaptureCursor();
            }
            else if (previouslyHadLocalControl && Cursor.lockState == CursorLockMode.Locked)
            {
                ReleaseCursor();
            }
        }

        private static void HandleCursorState()
        {
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                ReleaseCursor();
                return;
            }

            if (Cursor.lockState != CursorLockMode.Locked &&
                Mouse.current != null &&
                Mouse.current.leftButton.wasPressedThisFrame)
            {
                CaptureCursor();
            }
        }

        private static void CaptureCursor()
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        private static void ReleaseCursor()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (hasFocus && IsSpawned && IsOwner)
            {
                CaptureCursor();
            }
        }
    }
}
