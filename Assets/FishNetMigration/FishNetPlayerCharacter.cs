using FishNet.Object;
using HillbillyTaxi.Input;
using HillbillyTaxi.Player;
using UnityEngine;
using UnityEngine.InputSystem;

namespace HillbillyTaxi.FishNetMigration
{
    /// <summary>
    /// FishNet ownership wrapper around the existing framework-neutral movement motor.
    /// Only the owning client enables input, movement, camera, and AudioListener.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkObject))]
    [RequireComponent(typeof(CharacterController))]
    [RequireComponent(typeof(PlayerInputReader))]
    [RequireComponent(typeof(FirstPersonCharacterMotor))]
    public sealed class FishNetPlayerCharacter : NetworkBehaviour
    {
        [Header("Owner-only presentation")]
        [Tooltip("Put CameraRig here. It contains the gameplay camera and AudioListener.")]
        [SerializeField] private GameObject[] ownerOnlyObjects;

        [Tooltip(
            "Renderers excluded from the owning gameplay camera. They remain enabled " +
            "for remote players and future mirror/reflection cameras.")]
        [SerializeField] private Renderer[] renderersHiddenFromOwner;

        [SerializeField] private string localPlayerLayerName = "LocalPlayer";

        private PlayerInputReader _inputReader;
        private FirstPersonCharacterMotor _motor;
        private Camera _ownerCamera;

        private int _ownerCameraOriginalCullingMask;
        private int[] _originalRendererLayers;
        private int _localPlayerLayer = -1;

        private bool _presentationCached;
        private bool _localControlEnabled;
        private bool _missingLayerErrorLogged;

        private void Awake()
        {
            _inputReader = GetComponent<PlayerInputReader>();
            _motor = GetComponent<FirstPersonCharacterMotor>();

            CachePresentationState();
            SetLocalControl(false);
        }

        public override void OnStartClient()
        {
            base.OnStartClient();
            SetLocalControl(IsOwner);
        }

        public override void OnStopClient()
        {
            SetLocalControl(false);
            base.OnStopClient();
        }

        private void Update()
        {
            if (!IsClientInitialized ||
                !IsOwner ||
                !_localControlEnabled)
            {
                return;
            }

            HandleCursorState();

            CharacterInputFrame input =
                _inputReader.ReadFrame();

            if (Cursor.lockState != CursorLockMode.Locked)
            {
                input = input.WithoutLook();
            }

            _motor.Tick(input, Time.deltaTime);
        }

        private void SetLocalControl(bool enabled)
        {
            bool previouslyEnabled = _localControlEnabled;
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

            ConfigureOwnerRendererVisibility(enabled);

            if (enabled)
            {
                CaptureCursor();
            }
            else if (previouslyEnabled &&
                     Cursor.lockState == CursorLockMode.Locked)
            {
                ReleaseCursor();
            }
        }

        private void CachePresentationState()
        {
            if (_presentationCached)
            {
                return;
            }

            _presentationCached = true;
            _ownerCamera = GetComponentInChildren<Camera>(true);

            if (_ownerCamera != null)
            {
                _ownerCameraOriginalCullingMask =
                    _ownerCamera.cullingMask;
            }

            if (renderersHiddenFromOwner == null)
            {
                _originalRendererLayers =
                    System.Array.Empty<int>();
            }
            else
            {
                _originalRendererLayers =
                    new int[renderersHiddenFromOwner.Length];

                for (int index = 0;
                     index < renderersHiddenFromOwner.Length;
                     index++)
                {
                    Renderer targetRenderer =
                        renderersHiddenFromOwner[index];

                    _originalRendererLayers[index] =
                        targetRenderer != null
                            ? targetRenderer.gameObject.layer
                            : 0;
                }
            }

            _localPlayerLayer =
                LayerMask.NameToLayer(localPlayerLayerName);
        }

        private void ConfigureOwnerRendererVisibility(
            bool isLocalOwner)
        {
            CachePresentationState();

            if (renderersHiddenFromOwner == null ||
                renderersHiddenFromOwner.Length == 0)
            {
                return;
            }

            if (!isLocalOwner)
            {
                RestoreRendererLayers();

                if (_ownerCamera != null)
                {
                    _ownerCamera.cullingMask =
                        _ownerCameraOriginalCullingMask;
                }

                return;
            }

            if (_localPlayerLayer < 0)
            {
                if (!_missingLayerErrorLogged)
                {
                    Debug.LogError(
                        $"Layer '{localPlayerLayerName}' is missing. " +
                        "Keep the existing LocalPlayer layer in Tags and Layers.",
                        this);

                    _missingLayerErrorLogged = true;
                }

                return;
            }

            foreach (Renderer targetRenderer in renderersHiddenFromOwner)
            {
                if (targetRenderer != null)
                {
                    targetRenderer.gameObject.layer =
                        _localPlayerLayer;
                }
            }

            if (_ownerCamera != null)
            {
                _ownerCamera.cullingMask =
                    _ownerCameraOriginalCullingMask &
                    ~(1 << _localPlayerLayer);
            }
        }

        private void RestoreRendererLayers()
        {
            int count = Mathf.Min(
                renderersHiddenFromOwner.Length,
                _originalRendererLayers.Length);

            for (int index = 0; index < count; index++)
            {
                Renderer targetRenderer =
                    renderersHiddenFromOwner[index];

                if (targetRenderer != null)
                {
                    targetRenderer.gameObject.layer =
                        _originalRendererLayers[index];
                }
            }
        }

        private static void HandleCursorState()
        {
            if (Keyboard.current != null &&
                Keyboard.current.escapeKey.wasPressedThisFrame)
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
            if (hasFocus &&
                IsClientInitialized &&
                IsOwner)
            {
                CaptureCursor();
            }
        }
    }
}
