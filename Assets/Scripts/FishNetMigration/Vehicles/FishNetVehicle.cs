using FishNet.Connection;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using FishNet.Transporting;
using HillbillyTaxi.FishNetMigration.Interaction;
using HillbillyTaxi.FishNetMigration.Player;
using UnityEngine;

namespace HillbillyTaxi.FishNetMigration.Vehicles
{
    /// <summary>
    /// Server-authoritative occupancy for a stationary multi-seat vehicle.
    ///
    /// Each seat uses a server-written SyncVar containing the occupying client's ID.
    /// This makes occupied/free state available to current clients and late joiners.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FishNetVehicle : FishNetInteractable
    {
        public const int EmptySeatClientId = -1;

        [Header("Seats")]
        [SerializeField] private FishNetVehicleSeatDefinition[] seats;

        private readonly SyncVar<int> _driverOccupantId = new();
        private readonly SyncVar<int> _frontPassengerOccupantId = new();
        private readonly SyncVar<int> _rearLeftOccupantId = new();
        private readonly SyncVar<int> _rearRightOccupantId = new();

        public override bool AllowDirectColliderTargeting => false;
        public int SeatCount => seats?.Length ?? 0;

        public override void OnStartServer()
        {
            base.OnStartServer();

            SetAllSeatsEmpty();

            NetworkManager.ServerManager.OnRemoteConnectionState +=
                HandleRemoteConnectionState;
        }

        public override void OnStopServer()
        {
            if (NetworkManager != null)
            {
                NetworkManager.ServerManager.OnRemoteConnectionState -=
                    HandleRemoteConnectionState;
            }

            base.OnStopServer();
        }

        public override string GetPrompt(
            FishNetPlayerInteractor interactor,
            int interactionId)
        {
            if (!TryGetSeat(
                    interactionId,
                    out FishNetVehicleSeatDefinition seat))
            {
                return string.Empty;
            }

            return $"Enter {seat.DisplayName}";
        }

        public override bool CanShowPrompt(
            FishNetPlayerInteractor interactor,
            int interactionId)
        {
            if (!base.CanShowPrompt(interactor, interactionId) ||
                !TryGetSeat(interactionId, out _) ||
                IsSeatOccupied(interactionId))
            {
                return false;
            }

            FishNetPlayerSeatController playerSeat =
                interactor.GetComponent<FishNetPlayerSeatController>();

            return playerSeat != null && !playerSeat.IsSeated;
        }

        public override Vector3 GetInteractionPosition(
            int interactionId,
            Vector3 observerPosition)
        {
            if (TryGetSeat(
                    interactionId,
                    out FishNetVehicleSeatDefinition seat) &&
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
            out FishNetVehicleSeatDefinition seat)
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
            return GetOccupantClientId(seatIndex) !=
                   EmptySeatClientId;
        }

        public int GetOccupantClientId(int seatIndex)
        {
            return seatIndex switch
            {
                0 => _driverOccupantId.Value,
                1 => _frontPassengerOccupantId.Value,
                2 => _rearLeftOccupantId.Value,
                3 => _rearRightOccupantId.Value,
                _ => EmptySeatClientId
            };
        }

        protected override bool CanInteractOnServer(
            FishNetPlayerInteractor interactor,
            int interactionId)
        {
            if (!TryGetSeat(interactionId, out _) ||
                IsSeatOccupied(interactionId))
            {
                return false;
            }

            FishNetPlayerSeatController playerSeat =
                interactor.GetComponent<FishNetPlayerSeatController>();

            return playerSeat != null &&
                   !playerSeat.IsSeated;
        }

        protected override void InteractOnServer(
            FishNetPlayerInteractor interactor,
            int interactionId)
        {
            FishNetPlayerSeatController playerSeat =
                interactor.GetComponent<FishNetPlayerSeatController>();

            if (playerSeat != null)
            {
                TryEnterSeatOnServer(
                    playerSeat,
                    interactionId);
            }
        }

        internal bool TryEnterSeatOnServer(
            FishNetPlayerSeatController playerSeat,
            int seatIndex)
        {
            if (!IsServerInitialized ||
                playerSeat == null ||
                playerSeat.IsSeated ||
                !TryGetSeat(seatIndex, out _) ||
                IsSeatOccupied(seatIndex))
            {
                return false;
            }

            NetworkConnection owner =
                playerSeat.Owner;

            if (owner == null ||
                !owner.IsValid)
            {
                return false;
            }

            int clientId = owner.ClientId;

            // The server executes interaction requests sequentially. Claiming the
            // seat before publishing player state prevents another request from
            // taking the same seat in the same frame.
            SetOccupantClientId(
                seatIndex,
                clientId);

            if (playerSeat.AssignSeatOnServer(
                    this,
                    seatIndex))
            {
                return true;
            }

            SetOccupantClientId(
                seatIndex,
                EmptySeatClientId);

            return false;
        }

        internal bool TryExitSeatOnServer(
            FishNetPlayerSeatController playerSeat)
        {
            if (!IsServerInitialized ||
                playerSeat == null ||
                !playerSeat.TryGetServerSeat(
                    out FishNetVehicle vehicle,
                    out int seatIndex) ||
                vehicle != this)
            {
                return false;
            }

            NetworkConnection owner =
                playerSeat.Owner;

            if (owner == null ||
                !owner.IsValid)
            {
                return false;
            }

            int occupantId =
                GetOccupantClientId(seatIndex);

            if (occupantId != owner.ClientId)
            {
                return false;
            }

            SetOccupantClientId(
                seatIndex,
                EmptySeatClientId);

            playerSeat.ClearSeatOnServer();
            return true;
        }

        private void HandleRemoteConnectionState(
            NetworkConnection connection,
            RemoteConnectionStateArgs args)
        {
            if (args.ConnectionState !=
                RemoteConnectionState.Stopped)
            {
                return;
            }

            ReleaseSeatsOwnedBy(
                connection.ClientId);
        }

        private void ReleaseSeatsOwnedBy(int clientId)
        {
            for (int seatIndex = 0;
                 seatIndex < SeatCount;
                 seatIndex++)
            {
                if (GetOccupantClientId(seatIndex) ==
                    clientId)
                {
                    SetOccupantClientId(
                        seatIndex,
                        EmptySeatClientId);
                }
            }
        }

        private void SetAllSeatsEmpty()
        {
            _driverOccupantId.Value =
                EmptySeatClientId;

            _frontPassengerOccupantId.Value =
                EmptySeatClientId;

            _rearLeftOccupantId.Value =
                EmptySeatClientId;

            _rearRightOccupantId.Value =
                EmptySeatClientId;
        }

        private void SetOccupantClientId(
            int seatIndex,
            int clientId)
        {
            switch (seatIndex)
            {
                case 0:
                    _driverOccupantId.Value = clientId;
                    break;

                case 1:
                    _frontPassengerOccupantId.Value = clientId;
                    break;

                case 2:
                    _rearLeftOccupantId.Value = clientId;
                    break;

                case 3:
                    _rearRightOccupantId.Value = clientId;
                    break;
            }
        }
    }
}
