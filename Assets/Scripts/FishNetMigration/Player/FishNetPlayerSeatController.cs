using System;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using HillbillyTaxi.FishNetMigration.Vehicles;
using HillbillyTaxi.Player;
using UnityEngine;

namespace HillbillyTaxi.FishNetMigration.Player
{
    /// <summary>
    /// Synchronizes the vehicle and seat occupied by this player.
    ///
    /// The server alone changes seat state. While the owner occupies the Driver
    /// seat this component also rate-limits and submits driving input through the
    /// player's owned NetworkObject.
    ///
    /// The local camera rig is temporarily parented to the active seat camera
    /// anchor. This avoids enormous local offsets caused by assigning a world
    /// pose while the camera remains parented to the moving player hierarchy.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkObject))]
    public sealed class FishNetPlayerSeatController :
        NetworkBehaviour
    {
        [Header("References")]
        [SerializeField] private Transform cameraRig;

        [Header("Seated look")]
        [SerializeField, Min(0f)]
        private float mouseSensitivity = 0.08f;

        [SerializeField, Min(0f)]
        private float gamepadLookSpeed = 180f;

        [SerializeField, Range(1f, 89f)]
        private float maximumPitch = 75f;

        [SerializeField, Range(1f, 180f)]
        private float maximumYaw = 105f;

        [Header("Driver input networking")]
        [SerializeField, Range(5f, 30f)]
        private float driverInputSendRate = 20f;

        [SerializeField, Min(0f)]
        private float driverInputChangeThreshold = 0.02f;

        private readonly SyncVar<NetworkObject>
            _currentVehicleObject = new();

        private readonly SyncVar<int>
            _currentSeatIndex = new();

        private CharacterController _characterController;

        private Transform _standingCameraParent;
        private int _standingCameraSiblingIndex;
        private Vector3 _standingCameraLocalPosition;
        private Quaternion _standingCameraLocalRotation;
        private Vector3 _standingCameraLocalScale;
        private bool _standingCameraPoseCached;
        private bool _cameraAttachedToSeat;

        private float _seatPitch;
        private float _seatYaw;
        private bool _localControlEnabled;

        private bool _presentationSeated;
        private FishNetVehicle _presentationVehicle;
        private int _presentationSeatIndex = -1;

        private Vector2 _lastSentDriverMove;
        private bool _lastSentHandbrake;
        private float _nextDriverInputSendTime;

        public event Action<bool> SeatStateChanged;

        public bool IsSeated =>
            _currentVehicleObject.Value != null &&
            _currentSeatIndex.Value >= 0;

        public int SeatIndex =>
            _currentSeatIndex.Value;

        private void Awake()
        {
            _characterController =
                GetComponent<CharacterController>();

            EnsureStandingCameraPoseCached();

            _currentVehicleObject.OnChange +=
                HandleVehicleChanged;

            _currentSeatIndex.OnChange +=
                HandleSeatIndexChanged;
        }

        private void OnDestroy()
        {
            RestoreStandingCamera();

            _currentVehicleObject.OnChange -=
                HandleVehicleChanged;

            _currentSeatIndex.OnChange -=
                HandleSeatIndexChanged;
        }

        public override void OnStartServer()
        {
            base.OnStartServer();

            _currentSeatIndex.Value = -1;
            _currentVehicleObject.Value = null;
        }

        public override void OnStartClient()
        {
            base.OnStartClient();

            EnsureStandingCameraPoseCached();
            ApplySynchronizedSeatState();
        }

        public override void OnStopClient()
        {
            ApplyStandingPresentation();
            SetCharacterCollisionEnabled(true);
            RestoreStandingCamera();

            base.OnStopClient();
        }

        public void SetLocalControl(bool enabled)
        {
            EnsureStandingCameraPoseCached();

            _localControlEnabled = enabled;

            if (!enabled)
            {
                RestoreStandingCamera();
                return;
            }

            if (IsSeated &&
                TryGetCurrentSeat(
                    out _,
                    out FishNetVehicleSeatDefinition seat))
            {
                AttachCameraToSeat(
                    seat.CameraAnchor);
            }
        }

        public void TickOwnerInput(
            CharacterInputFrame input,
            float deltaTime,
            bool gameplayCursorLocked)
        {
            if (!_localControlEnabled ||
                !IsClientInitialized ||
                !IsOwner ||
                !IsSeated)
            {
                return;
            }

            if (gameplayCursorLocked)
            {
                UpdateSeatedLook(
                    input,
                    deltaTime);
            }

            SendDriverInputIfNeeded(
                input.Move,
                input.JumpHeld);

            if (input.InteractPressed)
            {
                RequestExitSeatServerRpc();
            }
        }

        public bool TryGetCurrentSeat(
            out FishNetVehicle vehicle,
            out FishNetVehicleSeatDefinition seat)
        {
            vehicle =
                GetSynchronizedVehicle();

            seat = null;

            if (vehicle == null)
            {
                return false;
            }

            return vehicle.TryGetSeat(
                _currentSeatIndex.Value,
                out seat);
        }

        internal bool TryGetServerSeat(
            out FishNetVehicle vehicle,
            out int seatIndex)
        {
            vehicle =
                GetSynchronizedVehicle();

            seatIndex =
                _currentSeatIndex.Value;

            return IsServerInitialized &&
                   vehicle != null &&
                   seatIndex >= 0;
        }

        internal bool AssignSeatOnServer(
            FishNetVehicle vehicle,
            int seatIndex)
        {
            if (!IsServerInitialized ||
                vehicle == null ||
                IsSeated ||
                !vehicle.TryGetSeat(
                    seatIndex,
                    out _))
            {
                return false;
            }

            _currentVehicleObject.Value =
                vehicle.NetworkObject;

            _currentSeatIndex.Value =
                seatIndex;

            return true;
        }

        internal void ClearSeatOnServer()
        {
            if (!IsServerInitialized)
            {
                return;
            }

            // Preserve the vehicle reference for the first callback so clients
            // can resolve the moving door-side exit point.
            _currentSeatIndex.Value = -1;
            _currentVehicleObject.Value = null;
        }

        [ServerRpc]
        private void SubmitDriverInputServerRpc(
            float steering,
            float throttle,
            bool handbrake)
        {
            if (!TryGetServerSeat(
                    out FishNetVehicle vehicle,
                    out int seatIndex) ||
                !vehicle.TryGetSeat(
                    seatIndex,
                    out FishNetVehicleSeatDefinition seat) ||
                seat.Role != FishNetVehicleSeatRole.Driver ||
                Owner == null ||
                !Owner.IsValid ||
                vehicle.GetOccupantClientId(seatIndex) !=
                    Owner.ClientId)
            {
                return;
            }

            FishNetTruckMotor motor =
                vehicle.GetComponent<FishNetTruckMotor>();

            if (motor == null)
            {
                return;
            }

            motor.SetDriverInputOnServer(
                Owner.ClientId,
                steering,
                throttle,
                handbrake);
        }

        [ServerRpc]
        private void RequestExitSeatServerRpc()
        {
            FishNetVehicle vehicle =
                GetSynchronizedVehicle();

            if (vehicle == null)
            {
                return;
            }

            vehicle.TryExitSeatOnServer(this);
        }

        private void LateUpdate()
        {
            if (!IsSpawned)
            {
                return;
            }

            if (!IsSeated ||
                !TryGetCurrentSeat(
                    out _,
                    out FishNetVehicleSeatDefinition seat))
            {
                // FishNet publishes the seat index and vehicle as separate
                // SyncVars. Always force the local camera back to its standing
                // hierarchy whenever either one says we are no longer seated.
                if (_cameraAttachedToSeat)
                {
                    RestoreStandingCamera();
                }

                return;
            }

            Transform occupantAnchor =
                seat.OccupantAnchor;

            transform.SetPositionAndRotation(
                occupantAnchor.position,
                occupantAnchor.rotation);

            if (!_localControlEnabled ||
                !IsOwner ||
                cameraRig == null)
            {
                return;
            }

            AttachCameraToSeat(
                seat.CameraAnchor);

            cameraRig.localPosition =
                Vector3.zero;

            cameraRig.localRotation =
                Quaternion.Euler(
                    _seatPitch,
                    _seatYaw,
                    0f);

            cameraRig.localScale =
                Vector3.one;
        }

        private void SendDriverInputIfNeeded(
            Vector2 move,
            bool handbrake)
        {
            if (!TryGetCurrentSeat(
                    out _,
                    out FishNetVehicleSeatDefinition seat) ||
                seat.Role != FishNetVehicleSeatRole.Driver)
            {
                return;
            }

            move = Vector2.ClampMagnitude(
                move,
                1f);

            float thresholdSquared =
                driverInputChangeThreshold *
                driverInputChangeThreshold;

            bool changed =
                (move - _lastSentDriverMove)
                    .sqrMagnitude >
                    thresholdSquared ||
                handbrake != _lastSentHandbrake;

            float now = Time.unscaledTime;

            if (!changed &&
                now < _nextDriverInputSendTime)
            {
                return;
            }

            _lastSentDriverMove = move;
            _lastSentHandbrake = handbrake;

            _nextDriverInputSendTime =
                now +
                1f / Mathf.Max(
                    1f,
                    driverInputSendRate);

            SubmitDriverInputServerRpc(
                move.x,
                move.y,
                handbrake);
        }

        private void HandleVehicleChanged(
            NetworkObject previousValue,
            NetworkObject newValue,
            bool asServer)
        {
            ApplySynchronizedSeatState();
        }

        private void HandleSeatIndexChanged(
            int previousValue,
            int newValue,
            bool asServer)
        {
            ApplySynchronizedSeatState();
        }

        private void ApplySynchronizedSeatState()
        {
            FishNetVehicle synchronizedVehicle =
                GetSynchronizedVehicle();

            int synchronizedSeatIndex =
                _currentSeatIndex.Value;

            bool shouldBeSeated =
                synchronizedVehicle != null &&
                synchronizedSeatIndex >= 0 &&
                synchronizedVehicle.TryGetSeat(
                    synchronizedSeatIndex,
                    out _);

            if (shouldBeSeated)
            {
                ApplySeatedPresentation(
                    synchronizedVehicle,
                    synchronizedSeatIndex);

                return;
            }

            ApplyStandingPresentation();
        }

        private void ApplySeatedPresentation(
            FishNetVehicle vehicle,
            int seatIndex)
        {
            bool changedSeat =
                !_presentationSeated ||
                _presentationVehicle != vehicle ||
                _presentationSeatIndex != seatIndex;

            _presentationSeated = true;
            _presentationVehicle = vehicle;
            _presentationSeatIndex = seatIndex;

            SetCharacterCollisionEnabled(false);

            if (changedSeat)
            {
                _seatPitch = 0f;
                _seatYaw = 0f;

                _lastSentDriverMove = Vector2.zero;
                _lastSentHandbrake = false;
                _nextDriverInputSendTime = 0f;

                if (_localControlEnabled &&
                    IsOwner &&
                    vehicle.TryGetSeat(
                        seatIndex,
                        out FishNetVehicleSeatDefinition seat))
                {
                    AttachCameraToSeat(
                        seat.CameraAnchor);
                }

                SeatStateChanged?.Invoke(true);
            }
        }

        private void ApplyStandingPresentation()
        {
            FishNetVehicle previousVehicle =
                _presentationVehicle;

            int previousSeatIndex =
                _presentationSeatIndex;

            bool wasSeated =
                _presentationSeated;

            _presentationSeated = false;
            _presentationVehicle = null;
            _presentationSeatIndex = -1;

            if (wasSeated &&
                previousVehicle != null &&
                previousVehicle.TryGetSeat(
                    previousSeatIndex,
                    out FishNetVehicleSeatDefinition previousSeat))
            {
                transform.SetPositionAndRotation(
                    previousSeat.ExitPoint.position,
                    previousSeat.ExitPoint.rotation);
            }

            SetCharacterCollisionEnabled(true);

            // Do this even when the first SyncVar callback already changed
            // _presentationSeated. It makes the second callback idempotently
            // restore the standing camera rather than leaving a seated offset.
            RestoreStandingCamera();

            if (wasSeated)
            {
                SeatStateChanged?.Invoke(false);
            }
        }

        private FishNetVehicle GetSynchronizedVehicle()
        {
            NetworkObject vehicleObject =
                _currentVehicleObject.Value;

            return vehicleObject != null
                ? vehicleObject.GetComponent<FishNetVehicle>()
                : null;
        }

        private void UpdateSeatedLook(
            CharacterInputFrame input,
            float deltaTime)
        {
            float lookMultiplier =
                input.LookComesFromMouse
                    ? mouseSensitivity
                    : gamepadLookSpeed * deltaTime;

            _seatYaw = Mathf.Clamp(
                _seatYaw +
                input.Look.x * lookMultiplier,
                -maximumYaw,
                maximumYaw);

            _seatPitch = Mathf.Clamp(
                _seatPitch -
                input.Look.y * lookMultiplier,
                -maximumPitch,
                maximumPitch);
        }

        private void SetCharacterCollisionEnabled(
            bool enabled)
        {
            if (_characterController != null &&
                _characterController.enabled != enabled)
            {
                _characterController.enabled = enabled;
            }
        }

        private void AttachCameraToSeat(
            Transform seatCameraAnchor)
        {
            EnsureStandingCameraPoseCached();

            if (cameraRig == null ||
                seatCameraAnchor == null)
            {
                return;
            }

            if (cameraRig.parent !=
                seatCameraAnchor)
            {
                cameraRig.SetParent(
                    seatCameraAnchor,
                    worldPositionStays: false);
            }

            cameraRig.localPosition =
                Vector3.zero;

            cameraRig.localRotation =
                Quaternion.Euler(
                    _seatPitch,
                    _seatYaw,
                    0f);

            cameraRig.localScale =
                Vector3.one;

            _cameraAttachedToSeat = true;
        }

        private void RestoreStandingCamera()
        {
            EnsureStandingCameraPoseCached();

            if (cameraRig == null ||
                !_standingCameraPoseCached)
            {
                return;
            }

            if (cameraRig.parent !=
                _standingCameraParent)
            {
                cameraRig.SetParent(
                    _standingCameraParent,
                    worldPositionStays: false);
            }

            if (_standingCameraParent != null)
            {
                int maximumIndex =
                    Mathf.Max(
                        0,
                        _standingCameraParent.childCount - 1);

                cameraRig.SetSiblingIndex(
                    Mathf.Clamp(
                        _standingCameraSiblingIndex,
                        0,
                        maximumIndex));
            }

            cameraRig.localPosition =
                _standingCameraLocalPosition;

            cameraRig.localRotation =
                _standingCameraLocalRotation;

            cameraRig.localScale =
                _standingCameraLocalScale;

            _cameraAttachedToSeat = false;
        }

        private void EnsureStandingCameraPoseCached()
        {
            if (_standingCameraPoseCached)
            {
                return;
            }

            ResolveCameraRig();

            if (cameraRig == null)
            {
                return;
            }

            _standingCameraParent =
                cameraRig.parent;

            _standingCameraSiblingIndex =
                cameraRig.GetSiblingIndex();

            _standingCameraLocalPosition =
                cameraRig.localPosition;

            _standingCameraLocalRotation =
                cameraRig.localRotation;

            _standingCameraLocalScale =
                cameraRig.localScale;

            _standingCameraPoseCached = true;
        }

        private void ResolveCameraRig()
        {
            if (cameraRig != null)
            {
                return;
            }

            Camera playerCamera =
                GetComponentInChildren<Camera>(true);

            if (playerCamera != null)
            {
                cameraRig =
                    playerCamera.transform.parent;
            }
        }

        protected override void Reset()
        {
            base.Reset();

            _characterController =
                GetComponent<CharacterController>();

            ResolveCameraRig();
        }

        protected override void OnValidate()
        {
            base.OnValidate();

            mouseSensitivity =
                Mathf.Max(0f, mouseSensitivity);

            gamepadLookSpeed =
                Mathf.Max(0f, gamepadLookSpeed);

            driverInputSendRate =
                Mathf.Clamp(
                    driverInputSendRate,
                    5f,
                    30f);

            driverInputChangeThreshold =
                Mathf.Max(
                    0f,
                    driverInputChangeThreshold);

            ResolveCameraRig();
        }
    }
}
