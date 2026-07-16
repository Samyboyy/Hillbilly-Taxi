using System;
using HillbillyTaxi.Vehicles;
using Unity.Netcode;
using UnityEngine;

namespace HillbillyTaxi.Player
{
    /// <summary>
    /// Synchronises which vehicle seat a player occupies and handles the local
    /// seated camera. The server alone changes the seat state.
    /// </summary>
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

        private readonly NetworkVariable<NetworkSeatState> _seatState =
            new NetworkVariable<NetworkSeatState>(
                NetworkSeatState.NotSeated,
                NetworkVariableReadPermission.Everyone,
                NetworkVariableWritePermission.Server);

        private Vector3 _standingCameraLocalPosition;
        private Quaternion _standingCameraLocalRotation;

        private float _seatPitch;
        private float _seatYaw;
        private bool _localControlEnabled;

        public event Action<bool> SeatStateChanged;

        public bool IsSeated => _seatState.Value.IsSeated;
        public int SeatIndex => _seatState.Value.SeatIndex;

        private void Awake()
        {
            ResolveCameraRig();

            if (cameraRig != null)
            {
                _standingCameraLocalPosition =
                    cameraRig.localPosition;

                _standingCameraLocalRotation =
                    cameraRig.localRotation;
            }
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            _seatState.OnValueChanged +=
                HandleSeatStateChanged;

            ApplyStateTransition(
                NetworkSeatState.NotSeated,
                _seatState.Value);
        }

        public override void OnNetworkDespawn()
        {
            _seatState.OnValueChanged -=
                HandleSeatStateChanged;

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

            RestoreStandingCamera();
            base.OnNetworkDespawn();
        }

        public void SetLocalControl(bool enabled)
        {
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

                RestoreStandingCamera();
            }

            if (isSeated)
            {
                _seatPitch = 0f;
                _seatYaw = 0f;
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

        private void RestoreStandingCamera()
        {
            if (cameraRig == null)
            {
                return;
            }

            cameraRig.localPosition =
                _standingCameraLocalPosition;

            cameraRig.localRotation =
                _standingCameraLocalRotation;
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
            ResolveCameraRig();
        }

        private void OnValidate()
        {
            mouseSensitivity =
                Mathf.Max(0f, mouseSensitivity);

            gamepadLookSpeed =
                Mathf.Max(0f, gamepadLookSpeed);

            ResolveCameraRig();
        }
    }
}
