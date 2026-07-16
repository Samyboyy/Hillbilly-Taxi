using HillbillyTaxi.Input;
using HillbillyTaxi.Interaction;
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
    [RequireComponent(typeof(NetworkPlayerInteractor))]
    public sealed class NetworkPlayerCharacter : NetworkBehaviour
    {
        [Header("Owner-only presentation")]
        [Tooltip("Put the first-person camera rig and its AudioListener here.")]
        [SerializeField] private GameObject[] ownerOnlyObjects;

        [Tooltip(
            "Renderers hidden from the owning gameplay camera. They remain enabled so mirrors, " +
            "reflection cameras, and other players can still render them.")]
        [SerializeField] private Renderer[] renderersHiddenFromOwner;

        [Tooltip(
            "Create this layer in Project Settings > Tags and Layers. The owning gameplay camera " +
            "will exclude it, while mirror/reflection cameras can include it.")]
        [SerializeField] private string localPlayerLayerName = "LocalPlayer";

        private PlayerInputReader _inputReader;
        private FirstPersonCharacterMotor _motor;
        private NetworkPlayerInteractor _interactor;
        private Camera _ownerCamera;

        private int _ownerCameraOriginalCullingMask;
        private int[] _originalRendererLayers;
        private int _localPlayerLayer = -1;

        private bool _presentationStateCached;
        private bool _localControlEnabled;
        private bool _missingLayerErrorLogged;

        private void Awake()
        {
            _inputReader = GetComponent<PlayerInputReader>();
            _motor = GetComponent<FirstPersonCharacterMotor>();
            _interactor = GetComponent<NetworkPlayerInteractor>();

            CachePresentationState();
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
            bool gameplayCursorLocked =
                Cursor.lockState == CursorLockMode.Locked;

            if (!gameplayCursorLocked)
            {
                input = input.WithoutLook();
            }

            _motor.Tick(input, Time.deltaTime);
            _interactor.Tick(
                input.InteractPressed,
                gameplayCursorLocked);
        }

        private void CachePresentationState()
        {
            if (_presentationStateCached)
            {
                return;
            }

            _presentationStateCached = true;
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

                for (
                    int index = 0;
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

        private void SetLocalControl(bool enabled)
        {
            bool previouslyHadLocalControl =
                _localControlEnabled;

            _localControlEnabled = enabled;

            _inputReader.SetInputEnabled(enabled);
            _motor.SetSimulationEnabled(enabled);
            _interactor.SetInteractionEnabled(enabled);

            if (ownerOnlyObjects != null)
            {
                foreach (
                    GameObject ownerOnlyObject
                    in ownerOnlyObjects)
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
            else if (
                previouslyHadLocalControl &&
                Cursor.lockState == CursorLockMode.Locked)
            {
                ReleaseCursor();
            }
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
                        $"Layer '{localPlayerLayerName}' does not exist. Create it in " +
                        "Project Settings > Tags and Layers so the local player can be hidden " +
                        "from the gameplay camera while remaining visible in mirrors.",
                        this);

                    _missingLayerErrorLogged = true;
                }

                return;
            }

            foreach (
                Renderer targetRenderer
                in renderersHiddenFromOwner)
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
            if (hasFocus && IsSpawned && IsOwner)
            {
                CaptureCursor();
            }
        }
    }
}
