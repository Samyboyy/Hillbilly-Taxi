using System;
using HillbillyTaxi.Vehicles;
using Unity.Netcode;
using UnityEngine;

namespace HillbillyTaxi.Player
{
    /// <summary>
    /// Synchronises which vehicle seat a player occupies, handles the local seated
    /// camera, and sends driver input through the player's owned NetworkObject.
    /// The server alone changes seat state and vehicle physics.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkObject))]
    public sealed class NetworkPlayerSeatController : NetworkBehaviour
    {
        [Header("References")]
        [SerializeField] private Transform cameraRig;

        [Header("Seated look")]
        [SerializeField, Min(0f)] private float mouseSensitivity = 0.08f;
        [SerializeField, Min(0f)] private float gamepadLookSpeed = 180f;
        [SerializeField, Range(1f, 89f)] private float maximumPitch = 75f;
        [SerializeField, Range(1f, 180f)] private float maximumYaw = 105f;

        [Header("Driver input networking")]
        [SerializeField, Range(5f, 30f)] private float driverInputSendRate = 20f;
        [SerializeField, Min(0f)] private float driverInputChangeThreshold = 0.02f;

        private readonly NetworkVariable<NetworkSeatState> _seatState =
            new NetworkVariable<NetworkSeatState>(
                NetworkSeatState.NotSeated,
                NetworkVariableReadPermission.Everyone,
                NetworkVariableWritePermission.Server);

        private CharacterController _characterController;

        private Vector3 _standingCameraLocalPosition;
        private Quaternion _standingCameraLocalRotation;
        private bool _standingCameraPoseCached;

        private float _seatPitch;
        private float _seatYaw;
        private bool _localControlEnabled;

        private Vector2 _lastSentDriverMove;
        private bool _lastSentHandbrake;
        private float _nextDriverInputSendTime;

        public event Action<bool> SeatStateChanged;

        public bool IsSeated => _seatState.Value.IsSeated;
        public int SeatIndex => _seatState.Value.SeatIndex;

        private void Awake()
        {
            _characterController = GetComponent<CharacterController>();
            EnsureStandingCameraPoseCached();
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            EnsureStandingCameraPoseCached();

            _seatState.OnValueChanged += HandleSeatStateChanged;

            ApplyStateTransition(
                NetworkSeatState.NotSeated,
                _seatState.Value);
        }

        public override void OnNetworkDespawn()
        {
            _seatState.OnValueChanged -= HandleSeatStateChanged;

            if (IsServer &&
                _seatState.Value.IsSeated &&
                TryResolveVehicle(
                    _seatState.Value,
                    out NetworkVehicle vehicle))
            {
                vehicle.ReleaseSeatForDisconnect(
                    OwnerClientId,
                    _seatState.Value.SeatIndex);
            }

            SetCharacterCollisionEnabled(true);
            RestoreStandingCamera();
            base.OnNetworkDespawn();
        }

        public void SetLocalControl(bool enabled)
        {
            EnsureStandingCameraPoseCached();

            _localControlEnabled = enabled;

            if (!enabled)
            {
                RestoreStandingCamera();
            }
        }

        public void TickOwnerInput(
            CharacterInputFrame input,
            float deltaTime,
            bool gameplayCursorLocked)
        {
            if (!_localControlEnabled ||
                !IsOwner ||
                !IsSeated)
            {
                return;
            }

            if (gameplayCursorLocked)
            {
                UpdateSeatedLook(input, deltaTime);
            }

            SendDriverInputIfNeeded(
                input.Move,
                input.JumpHeld);

            if (input.InteractPressed)
            {
                RequestExitSeatRpc();
            }
        }

        public bool TryGetCurrentSeat(
            out NetworkVehicle vehicle,
            out VehicleSeatDefinition seat)
        {
            vehicle = null;
            seat = null;

            if (!TryResolveVehicle(
                    _seatState.Value,
                    out vehicle))
            {
                return false;
            }

            return vehicle.TryGetSeat(
                _seatState.Value.SeatIndex,
                out seat);
        }

        internal bool TryGetServerSeat(
            out NetworkVehicle vehicle,
            out int seatIndex)
        {
            seatIndex = _seatState.Value.SeatIndex;

            if (!IsServer ||
                !_seatState.Value.IsSeated)
            {
                vehicle = null;
                return false;
            }

            return TryResolveVehicle(
                _seatState.Value,
                out vehicle);
        }

        internal bool AssignSeatOnServer(
            NetworkVehicle vehicle,
            int seatIndex)
        {
            if (!IsServer ||
                vehicle == null ||
                IsSeated)
            {
                return false;
            }

            _seatState.Value =
                new NetworkSeatState(
                    new NetworkObjectReference(
                        vehicle.NetworkObject),
                    seatIndex);

            return true;
        }

        internal void ClearSeatOnServer()
        {
            if (IsServer)
            {
                _seatState.Value =
                    NetworkSeatState.NotSeated;
            }
        }

        [Rpc(SendTo.Server)]
        private void SubmitDriverInputRpc(
            float steering,
            float throttle,
            bool handbrake,
            RpcParams rpcParams = default)
        {
            if (rpcParams.Receive.SenderClientId != OwnerClientId ||
                !TryResolveVehicle(
                    _seatState.Value,
                    out NetworkVehicle vehicle) ||
                !vehicle.TryGetSeat(
                    _seatState.Value.SeatIndex,
                    out VehicleSeatDefinition seat) ||
                seat.Role != VehicleSeatRole.Driver)
            {
                return;
            }

            NetworkTruckMotor motor =
                vehicle.GetComponent<NetworkTruckMotor>();

            if (motor == null)
            {
                return;
            }

            motor.SetDriverInputOnServer(
                OwnerClientId,
                steering,
                throttle,
                handbrake);
        }

        [Rpc(SendTo.Server)]
        private void RequestExitSeatRpc(
            RpcParams rpcParams = default)
        {
            if (rpcParams.Receive.SenderClientId !=
                    OwnerClientId ||
                !TryResolveVehicle(
                    _seatState.Value,
                    out NetworkVehicle vehicle))
            {
                return;
            }

            vehicle.TryExitSeatOnServer(this);
        }

        private void LateUpdate()
        {
            if (!IsSpawned ||
                !IsSeated ||
                !TryGetCurrentSeat(
                    out _,
                    out VehicleSeatDefinition seat))
            {
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

            cameraRig.position =
                seat.CameraAnchor.position;

            cameraRig.rotation =
                seat.CameraAnchor.rotation *
                Quaternion.Euler(
                    _seatPitch,
                    _seatYaw,
                    0f);
        }

        private void SendDriverInputIfNeeded(
            Vector2 move,
            bool handbrake)
        {
            if (!TryGetCurrentSeat(
                    out _,
                    out VehicleSeatDefinition seat) ||
                seat.Role != VehicleSeatRole.Driver)
            {
                return;
            }

            move = Vector2.ClampMagnitude(move, 1f);

            float thresholdSquared =
                driverInputChangeThreshold *
                driverInputChangeThreshold;

            bool inputChanged =
                (move - _lastSentDriverMove).sqrMagnitude >
                    thresholdSquared ||
                handbrake != _lastSentHandbrake;

            float now = Time.unscaledTime;

            if (!inputChanged &&
                now < _nextDriverInputSendTime)
            {
                return;
            }

            _lastSentDriverMove = move;
            _lastSentHandbrake = handbrake;
            _nextDriverInputSendTime =
                now + 1f / Mathf.Max(1f, driverInputSendRate);

            SubmitDriverInputRpc(
                move.x,
                move.y,
                handbrake);
        }

        private void HandleSeatStateChanged(
            NetworkSeatState previousValue,
            NetworkSeatState newValue)
        {
            ApplyStateTransition(
                previousValue,
                newValue);
        }

        private void ApplyStateTransition(
            NetworkSeatState previousValue,
            NetworkSeatState newValue)
        {
            bool wasSeated = previousValue.IsSeated;
            bool isSeated = newValue.IsSeated;

            if (wasSeated && !isSeated)
            {
                if (TryResolveVehicle(
                        previousValue,
                        out NetworkVehicle previousVehicle) &&
                    previousVehicle.TryGetSeat(
                        previousValue.SeatIndex,
                        out VehicleSeatDefinition previousSeat))
                {
                    transform.SetPositionAndRotation(
                        previousSeat.ExitPoint.position,
                        previousSeat.ExitPoint.rotation);
                }

                SetCharacterCollisionEnabled(true);
                RestoreStandingCamera();
            }

            if (isSeated)
            {
                SetCharacterCollisionEnabled(false);

                _seatPitch = 0f;
                _seatYaw = 0f;
                _lastSentDriverMove = Vector2.zero;
                _lastSentHandbrake = false;
                _nextDriverInputSendTime = 0f;
            }

            SeatStateChanged?.Invoke(isSeated);
        }

        private bool TryResolveVehicle(
            NetworkSeatState state,
            out NetworkVehicle vehicle)
        {
            vehicle = null;

            if (!state.IsSeated ||
                !state.Vehicle.TryGet(
                    out NetworkObject vehicleObject,
                    NetworkManager))
            {
                return false;
            }

            vehicle =
                vehicleObject.GetComponent<NetworkVehicle>();

            return vehicle != null;
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

        private void SetCharacterCollisionEnabled(bool enabled)
        {
            if (_characterController != null &&
                _characterController.enabled != enabled)
            {
                _characterController.enabled = enabled;
            }
        }

        private void RestoreStandingCamera()
        {
            EnsureStandingCameraPoseCached();

            if (cameraRig == null ||
                !_standingCameraPoseCached)
            {
                return;
            }

            cameraRig.localPosition =
                _standingCameraLocalPosition;

            cameraRig.localRotation =
                _standingCameraLocalRotation;
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

            _standingCameraLocalPosition =
                cameraRig.localPosition;

            _standingCameraLocalRotation =
                cameraRig.localRotation;

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

        private void Reset()
        {
            _characterController = GetComponent<CharacterController>();
            ResolveCameraRig();
        }

        private void OnValidate()
        {
            mouseSensitivity =
                Mathf.Max(0f, mouseSensitivity);

            gamepadLookSpeed =
                Mathf.Max(0f, gamepadLookSpeed);

            driverInputSendRate =
                Mathf.Clamp(driverInputSendRate, 5f, 30f);

            driverInputChangeThreshold =
                Mathf.Max(0f, driverInputChangeThreshold);

            ResolveCameraRig();
        }
    }
}
