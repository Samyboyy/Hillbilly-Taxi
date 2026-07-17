using System.Collections;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using HillbillyTaxi.FishNetMigration.Vehicles;
using UnityEngine;

namespace HillbillyTaxi.FishNetMigration.TaxiJobs
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkObject))]
    public sealed class FishNetTaxiJobManager : NetworkBehaviour
    {
        [Header("Taxi")]
        [SerializeField] private FishNetVehicle taxiVehicle;

        [Header("Presentation")]
        [SerializeField] private GameObject pickupMarkerRoot;
        [SerializeField] private GameObject dropoffMarkerRoot;
        [SerializeField] private GameObject waitingPassengerRoot;
        [SerializeField] private GameObject ridingPassengerRoot;

        [Header("Job")]
        [SerializeField, Min(1)] private int baseFare = 25;
        [SerializeField, Min(0)] private int fareIncreasePerJob = 5;
        [SerializeField, Min(0.1f)] private float requiredStopDuration = 0.75f;
        [SerializeField, Min(0f)] private float maximumArrivalSpeed = 1.25f;
        [SerializeField, Min(0f)] private float maximumArrivalAngularSpeed = 1.5f;
        [SerializeField, Min(0f)] private float completionDisplayDuration = 2.5f;

        private readonly SyncVar<FishNetTaxiJobState> _state = new();
        private readonly SyncVar<int> _currentFare = new();
        private readonly SyncVar<int> _totalEarnings = new();
        private readonly SyncVar<int> _completedJobs = new();

        private float _pickupStopTimer;
        private float _dropoffStopTimer;
        private Coroutine _resetRoutine;

        public FishNetVehicle TaxiVehicle => taxiVehicle;
        public FishNetTaxiJobState State => _state.Value;
        public int CurrentFare => _currentFare.Value;
        public int TotalEarnings => _totalEarnings.Value;
        public int CompletedJobs => _completedJobs.Value;

        public string ObjectiveText => _state.Value switch
        {
            FishNetTaxiJobState.WaitingForPickup =>
                "Pick up the passenger at the yellow marker",
            FishNetTaxiJobState.PassengerAboard =>
                "Take the passenger to the green marker",
            FishNetTaxiJobState.JobComplete =>
                $"Fare complete: +£{_currentFare.Value}",
            _ => string.Empty
        };

        private void Awake()
        {
            _state.OnChange += HandleStateChanged;
        }

        private void OnDestroy()
        {
            _state.OnChange -= HandleStateChanged;
        }

        public override void OnStartServer()
        {
            base.OnStartServer();

            _pickupStopTimer = 0f;
            _dropoffStopTimer = 0f;
            _totalEarnings.Value = 0;
            _completedJobs.Value = 0;
            _currentFare.Value = Mathf.Max(1, baseFare);
            _state.Value = FishNetTaxiJobState.WaitingForPickup;
            ApplyPresentation();
        }

        public override void OnStopServer()
        {
            if (_resetRoutine != null)
            {
                StopCoroutine(_resetRoutine);
                _resetRoutine = null;
            }

            base.OnStopServer();
        }

        public override void OnStartClient()
        {
            base.OnStartClient();
            ApplyPresentation();
        }

        internal void ReportZonePresenceOnServer(
            FishNetTaxiJobZoneType zoneType,
            bool taxiInside,
            float fixedDeltaTime)
        {
            if (!IsServerInitialized || taxiVehicle == null)
            {
                return;
            }

            switch (_state.Value)
            {
                case FishNetTaxiJobState.WaitingForPickup:
                    _dropoffStopTimer = 0f;
                    if (zoneType != FishNetTaxiJobZoneType.Pickup)
                    {
                        return;
                    }

                    _pickupStopTimer = UpdateArrivalTimer(
                        _pickupStopTimer,
                        taxiInside,
                        fixedDeltaTime);

                    if (_pickupStopTimer >= requiredStopDuration)
                    {
                        BoardPassengerOnServer();
                    }
                    break;

                case FishNetTaxiJobState.PassengerAboard:
                    _pickupStopTimer = 0f;
                    if (zoneType != FishNetTaxiJobZoneType.Dropoff)
                    {
                        return;
                    }

                    _dropoffStopTimer = UpdateArrivalTimer(
                        _dropoffStopTimer,
                        taxiInside,
                        fixedDeltaTime);

                    if (_dropoffStopTimer >= requiredStopDuration)
                    {
                        CompleteJobOnServer();
                    }
                    break;

                default:
                    _pickupStopTimer = 0f;
                    _dropoffStopTimer = 0f;
                    break;
            }
        }

        private float UpdateArrivalTimer(
            float currentTimer,
            bool taxiInside,
            float fixedDeltaTime)
        {
            if (!taxiInside || !HasDriverOnServer() || !IsTaxiStopped())
            {
                return 0f;
            }

            return currentTimer + Mathf.Max(0f, fixedDeltaTime);
        }

        private bool HasDriverOnServer()
        {
            for (int seatIndex = 0; seatIndex < taxiVehicle.SeatCount; seatIndex++)
            {
                if (!taxiVehicle.TryGetSeat(
                        seatIndex,
                        out FishNetVehicleSeatDefinition seat) ||
                    seat.Role != FishNetVehicleSeatRole.Driver)
                {
                    continue;
                }

                return taxiVehicle.GetOccupantClientId(seatIndex) !=
                       FishNetVehicle.EmptySeatClientId;
            }

            return false;
        }

        private bool IsTaxiStopped()
        {
            Rigidbody body = taxiVehicle.GetComponent<Rigidbody>();
            if (body == null)
            {
                return true;
            }

            return body.linearVelocity.magnitude <= maximumArrivalSpeed &&
                   body.angularVelocity.magnitude <= maximumArrivalAngularSpeed;
        }

        private void BoardPassengerOnServer()
        {
            if (!IsServerInitialized ||
                _state.Value != FishNetTaxiJobState.WaitingForPickup)
            {
                return;
            }

            _pickupStopTimer = 0f;
            _dropoffStopTimer = 0f;
            _state.Value = FishNetTaxiJobState.PassengerAboard;

            Debug.Log(
                "Taxi passenger boarded. Drive to the drop-off marker.",
                this);
        }

        private void CompleteJobOnServer()
        {
            if (!IsServerInitialized ||
                _state.Value != FishNetTaxiJobState.PassengerAboard)
            {
                return;
            }

            _pickupStopTimer = 0f;
            _dropoffStopTimer = 0f;

            int paidFare = Mathf.Max(1, _currentFare.Value);
            _totalEarnings.Value += paidFare;
            _completedJobs.Value += 1;
            _state.Value = FishNetTaxiJobState.JobComplete;

            Debug.Log(
                $"Taxi job complete. Earned £{paidFare}. " +
                $"Session total: £{_totalEarnings.Value}.",
                this);

            if (_resetRoutine != null)
            {
                StopCoroutine(_resetRoutine);
            }

            _resetRoutine = StartCoroutine(ResetJobAfterDelayOnServer());
        }

        private IEnumerator ResetJobAfterDelayOnServer()
        {
            yield return new WaitForSeconds(completionDisplayDuration);

            if (!IsServerInitialized)
            {
                yield break;
            }

            _currentFare.Value = Mathf.Max(
                1,
                baseFare + _completedJobs.Value * fareIncreasePerJob);

            _state.Value = FishNetTaxiJobState.WaitingForPickup;
            _resetRoutine = null;
        }

        private void HandleStateChanged(
            FishNetTaxiJobState previousValue,
            FishNetTaxiJobState newValue,
            bool asServer)
        {
            ApplyPresentation();
        }

        private void ApplyPresentation()
        {
            bool waiting = _state.Value == FishNetTaxiJobState.WaitingForPickup;
            bool aboard = _state.Value == FishNetTaxiJobState.PassengerAboard;

            SetActiveIfNeeded(pickupMarkerRoot, waiting);
            SetActiveIfNeeded(waitingPassengerRoot, waiting);
            SetActiveIfNeeded(dropoffMarkerRoot, aboard);
            SetActiveIfNeeded(ridingPassengerRoot, aboard);
        }

        private static void SetActiveIfNeeded(GameObject target, bool active)
        {
            if (target != null && target.activeSelf != active)
            {
                target.SetActive(active);
            }
        }

        protected override void Reset()
        {
            base.Reset();
            if (taxiVehicle == null)
            {
                taxiVehicle = FindFirstObjectByType<FishNetVehicle>();
            }
        }

        protected override void OnValidate()
        {
            base.OnValidate();
            baseFare = Mathf.Max(1, baseFare);
            fareIncreasePerJob = Mathf.Max(0, fareIncreasePerJob);
            requiredStopDuration = Mathf.Max(0.1f, requiredStopDuration);
            maximumArrivalSpeed = Mathf.Max(0f, maximumArrivalSpeed);
            maximumArrivalAngularSpeed = Mathf.Max(0f, maximumArrivalAngularSpeed);
            completionDisplayDuration = Mathf.Max(0f, completionDisplayDuration);
        }
    }
}
