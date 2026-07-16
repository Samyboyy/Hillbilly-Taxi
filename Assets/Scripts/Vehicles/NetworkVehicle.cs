using HillbillyTaxi.Interaction;
using HillbillyTaxi.Player;
using Unity.Netcode;
using UnityEngine;

namespace HillbillyTaxi.Vehicles
{
    /// <summary>
    /// Server-authoritative occupancy for a multi-seat vehicle.
    /// This milestone keeps the vehicle stationary; the same seat state can later
    /// be used by the networked truck controller.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class NetworkVehicle : NetworkInteractable
    {
        public const ulong EmptySeatClientId = ulong.MaxValue;

        [Header("Seats")]
        [SerializeField] private VehicleSeatDefinition[] seats;

        private readonly NetworkList<ulong> _occupantClientIds =
            new NetworkList<ulong>();

        public override bool AllowDirectColliderTargeting => false;
        public int SeatCount => seats?.Length ?? 0;

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            if (!IsServer)
            {
                return;
            }

            _occupantClientIds.Clear();

            for (int index = 0; index < SeatCount; index++)
            {
                _occupantClientIds.Add(EmptySeatClientId);
            }

            NetworkManager.OnClientDisconnectCallback +=
                HandleClientDisconnected;
        }

        public override void OnNetworkDespawn()
        {
            if (IsServer && NetworkManager != null)
            {
                NetworkManager.OnClientDisconnectCallback -=
                    HandleClientDisconnected;
            }

            base.OnNetworkDespawn();
        }

        public override string GetPrompt(
            NetworkPlayerInteractor interactor,
            int interactionId)
        {
            if (!TryGetSeat(
                    interactionId,
                    out VehicleSeatDefinition seat))
            {
                return string.Empty;
            }

            return $"Enter {seat.DisplayName}";
        }

        public override bool CanShowPrompt(
            NetworkPlayerInteractor interactor,
            int interactionId)
        {
            if (!base.CanShowPrompt(interactor, interactionId) ||
                !TryGetSeat(interactionId, out _) ||
                IsSeatOccupied(interactionId))
            {
                return false;
            }

            NetworkPlayerSeatController playerSeat =
                interactor.GetComponent<NetworkPlayerSeatController>();

            return playerSeat != null && !playerSeat.IsSeated;
        }

        public override Vector3 GetInteractionPosition(
            int interactionId,
            Vector3 observerPosition)
        {
            if (TryGetSeat(
                    interactionId,
                    out VehicleSeatDefinition seat) &&
                seat.InteractionPoint != null)
            {
                return seat.InteractionPoint.position;
            }

            return base.GetInteractionPosition(
                interactionId,
                observerPosition);
        }

        public bool TryGetSeat(
            int seatIndex,
            out VehicleSeatDefinition seat)
        {
            seat = null;

            if (seats == null ||
                seatIndex < 0 ||
                seatIndex >= seats.Length)
            {
                return false;
            }

            seat = seats[seatIndex];
            return seat != null && seat.IsConfigured;
        }

        public bool IsSeatOccupied(int seatIndex)
        {
            return seatIndex >= 0 &&
                   seatIndex < _occupantClientIds.Count &&
                   _occupantClientIds[seatIndex] !=
                       EmptySeatClientId;
        }

        public ulong GetOccupantClientId(int seatIndex)
        {
            return IsSeatOccupied(seatIndex)
                ? _occupantClientIds[seatIndex]
                : EmptySeatClientId;
        }

        protected override bool CanInteractOnServer(
            NetworkPlayerInteractor interactor,
            int interactionId)
        {
            if (!TryGetSeat(interactionId, out _) ||
                IsSeatOccupied(interactionId))
            {
                return false;
            }

            NetworkPlayerSeatController playerSeat =
                interactor.GetComponent<NetworkPlayerSeatController>();

            return playerSeat != null && !playerSeat.IsSeated;
        }

        protected override void InteractOnServer(
            NetworkPlayerInteractor interactor,
            int interactionId)
        {
            NetworkPlayerSeatController playerSeat =
                interactor.GetComponent<NetworkPlayerSeatController>();

            if (playerSeat != null)
            {
                TryEnterSeatOnServer(playerSeat, interactionId);
            }
        }

        internal bool TryEnterSeatOnServer(
            NetworkPlayerSeatController playerSeat,
            int seatIndex)
        {
            if (!IsServer ||
                playerSeat == null ||
                playerSeat.IsSeated ||
                !TryGetSeat(seatIndex, out _) ||
                IsSeatOccupied(seatIndex))
            {
                return false;
            }

            if (seatIndex >= _occupantClientIds.Count)
            {
                return false;
            }

            _occupantClientIds[seatIndex] =
                playerSeat.OwnerClientId;

            if (playerSeat.AssignSeatOnServer(this, seatIndex))
            {
                return true;
            }

            _occupantClientIds[seatIndex] =
                EmptySeatClientId;

            return false;
        }

        internal bool TryExitSeatOnServer(
            NetworkPlayerSeatController playerSeat)
        {
            if (!IsServer ||
                playerSeat == null ||
                !playerSeat.TryGetServerSeat(
                    out NetworkVehicle vehicle,
                    out int seatIndex) ||
                vehicle != this ||
                seatIndex < 0 ||
                seatIndex >= _occupantClientIds.Count ||
                _occupantClientIds[seatIndex] !=
                    playerSeat.OwnerClientId)
            {
                return false;
            }

            _occupantClientIds[seatIndex] =
                EmptySeatClientId;

            playerSeat.ClearSeatOnServer();
            return true;
        }

        internal void ReleaseSeatForDisconnect(
            ulong clientId,
            int expectedSeatIndex)
        {
            if (!IsServer)
            {
                return;
            }

            if (expectedSeatIndex >= 0 &&
                expectedSeatIndex < _occupantClientIds.Count &&
                _occupantClientIds[expectedSeatIndex] == clientId)
            {
                _occupantClientIds[expectedSeatIndex] =
                    EmptySeatClientId;
                return;
            }

            HandleClientDisconnected(clientId);
        }

        private void HandleClientDisconnected(ulong clientId)
        {
            if (!IsServer)
            {
                return;
            }

            for (
                int index = 0;
                index < _occupantClientIds.Count;
                index++)
            {
                if (_occupantClientIds[index] == clientId)
                {
                    _occupantClientIds[index] =
                        EmptySeatClientId;
                }
            }
        }

        private void OnDestroy()
        {
            _occupantClientIds.Dispose();
        }
    }
}
