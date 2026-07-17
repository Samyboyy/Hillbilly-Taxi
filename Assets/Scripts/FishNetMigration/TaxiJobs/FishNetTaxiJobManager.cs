using FishNet.Object;
using FishNet.Object.Synchronizing;
using HillbillyTaxi.FishNetMigration.Vehicles;
using UnityEngine;

namespace HillbillyTaxi.FishNetMigration.TaxiJobs
{
    /// <summary>
    /// Server-authoritative contract layer.
    ///
    /// This component owns required destinations, passenger transitions, timing,
    /// reward and failure. It intentionally does not own patrols, road closures,
    /// weather, enemies, route selection or suggested solutions.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkObject))]
    public sealed class FishNetTaxiJobManager : NetworkBehaviour
    {
        private const float ClockReplicationInterval = 0.2f;

        [Header("Taxi")]
        [SerializeField] private FishNetVehicle taxiVehicle;

        [Header("Contract")]
        [SerializeField]
        private FishNetTaxiContractDefinition contractDefinition;

        [Header("Passenger presentation")]
        [SerializeField] private GameObject waitingPassengerRoot;
        [SerializeField] private GameObject ridingPassengerRoot;

        [Header("Arrival validation")]
        [SerializeField, Min(0f)]
        private float maximumArrivalSpeed = 1.25f;

        [SerializeField, Min(0f)]
        private float maximumArrivalAngularSpeed = 1.5f;

        // Legacy Phase 6 fields remain hidden so existing serialized scene data
        // and the old installer can load without losing references.
        [SerializeField, HideInInspector]
        private GameObject pickupMarkerRoot;

        [SerializeField, HideInInspector]
        private GameObject dropoffMarkerRoot;

        [SerializeField, HideInInspector, Min(1)]
        private int baseFare = 25;

        [SerializeField, HideInInspector, Min(0)]
        private int fareIncreasePerJob = 5;

        [SerializeField, HideInInspector, Min(0.1f)]
        private float requiredStopDuration = 0.75f;

        [SerializeField, HideInInspector, Min(0f)]
        private float completionDisplayDuration = 2.5f;

        private readonly SyncVar<FishNetTaxiJobState>
            _state = new();

        private readonly SyncVar<bool>
            _passengerAboard = new();

        private readonly SyncVar<bool>
            _taxiAtRequiredStop = new();

        private readonly SyncVar<float>
            _stateTimeRemaining = new();

        private readonly SyncVar<float>
            _contractElapsedSeconds = new();

        private readonly SyncVar<int>
            _currentPayout = new();

        private readonly SyncVar<int>
            _lastPayout = new();

        private readonly SyncVar<int>
            _totalEarnings = new();

        private readonly SyncVar<int>
            _completedContracts = new();

        private FishNetTaxiJobZone[] _zones;
        private float _arrivalPresenceTimer;
        private float _serverStateTimeRemaining;
        private float _serverContractElapsed;
        private float _nextClockReplicationTime;
        private bool _serverTaxiAtRequiredStop;

        public FishNetVehicle TaxiVehicle => taxiVehicle;
        public FishNetTaxiJobState State => _state.Value;
        public bool PassengerAboard => _passengerAboard.Value;
        public bool TaxiAtRequiredStop => _taxiAtRequiredStop.Value;
        public float StateTimeRemaining => _stateTimeRemaining.Value;
        public float ContractElapsedSeconds =>
            _contractElapsedSeconds.Value;

        public int CurrentPayout => _currentPayout.Value;
        public int LastPayout => _lastPayout.Value;
        public int TotalEarnings => _totalEarnings.Value;
        public int CompletedContracts => _completedContracts.Value;

        // Compatibility aliases used by earlier proof UI.
        public int CurrentFare => CurrentPayout;
        public int CompletedJobs => CompletedContracts;

        public bool IsTerminal =>
            State == FishNetTaxiJobState.ContractComplete ||
            State == FishNetTaxiJobState.ContractFailed;

        public bool CanRestartPrototype => IsTerminal;

        public string ContractName =>
            contractDefinition != null
                ? contractDefinition.ContractName
                : "Missing Contract";

        public string PassengerName =>
            contractDefinition != null
                ? contractDefinition.PassengerName
                : "Unknown Passenger";

        public string SpecialRule =>
            contractDefinition != null
                ? contractDefinition.SpecialRule
                : "No contract definition is assigned.";

        public string ActiveObjectiveId
        {
            get
            {
                if (contractDefinition == null ||
                    IsTerminal)
                {
                    return string.Empty;
                }

                return State switch
                {
                    FishNetTaxiJobState.WaitingForPickup =>
                        contractDefinition.PickupObjectiveId,

                    FishNetTaxiJobState.TravellingToRequiredStop or
                    FishNetTaxiJobState.PassengerEnteringRequiredStop or
                    FishNetTaxiJobState.WaitingAtRequiredStop or
                    FishNetTaxiJobState.PassengerReturningFromRequiredStop =>
                        contractDefinition.RequiredStopObjectiveId,

                    FishNetTaxiJobState.TravellingToDestination =>
                        contractDefinition.FinalObjectiveId,

                    _ => string.Empty
                };
            }
        }

        public string ActiveDestinationName
        {
            get
            {
                if (contractDefinition == null)
                {
                    return "Unavailable";
                }

                return State switch
                {
                    FishNetTaxiJobState.WaitingForPickup =>
                        contractDefinition.PickupLocationName,

                    FishNetTaxiJobState.TravellingToRequiredStop or
                    FishNetTaxiJobState.PassengerEnteringRequiredStop or
                    FishNetTaxiJobState.WaitingAtRequiredStop or
                    FishNetTaxiJobState.PassengerReturningFromRequiredStop =>
                        contractDefinition.RequiredStopLocationName,

                    FishNetTaxiJobState.TravellingToDestination =>
                        contractDefinition.FinalLocationName,

                    FishNetTaxiJobState.ContractComplete =>
                        "Contract complete",

                    FishNetTaxiJobState.ContractFailed =>
                        "Contract failed",

                    _ => "Unavailable"
                };
            }
        }

        public string ObjectiveText
        {
            get
            {
                if (contractDefinition == null)
                {
                    return
                        "The contract definition is missing.";
                }

                return State switch
                {
                    FishNetTaxiJobState.WaitingForPickup =>
                        contractDefinition.PickupObjectiveText,

                    FishNetTaxiJobState.TravellingToRequiredStop =>
                        contractDefinition.TravelToRequiredStopText,

                    FishNetTaxiJobState.PassengerEnteringRequiredStop =>
                        $"{PassengerName} is going inside.",

                    FishNetTaxiJobState.WaitingAtRequiredStop =>
                        GetWaitingObjectiveText(),

                    FishNetTaxiJobState.PassengerReturningFromRequiredStop =>
                        TaxiAtRequiredStop
                            ? $"{PassengerName} is coming back."
                            : $"Return to " +
                              $"{contractDefinition.RequiredStopLocationName} " +
                              $"for {PassengerName}.",

                    FishNetTaxiJobState.TravellingToDestination =>
                        contractDefinition.FinalObjectiveText,

                    FishNetTaxiJobState.ContractComplete =>
                        $"Contract complete: +£{LastPayout}",

                    FishNetTaxiJobState.ContractFailed =>
                        "Contract failed.",

                    _ => string.Empty
                };
            }
        }

        public string PassengerStatusText =>
            State switch
            {
                FishNetTaxiJobState.WaitingForPickup =>
                    "Waiting at pickup",

                FishNetTaxiJobState.TravellingToRequiredStop or
                FishNetTaxiJobState.TravellingToDestination =>
                    "In the taxi",

                FishNetTaxiJobState.PassengerEnteringRequiredStop =>
                    "Entering the building",

                FishNetTaxiJobState.WaitingAtRequiredStop =>
                    "Inside the building",

                FishNetTaxiJobState.PassengerReturningFromRequiredStop =>
                    "Returning to the taxi",

                FishNetTaxiJobState.ContractComplete =>
                    "Delivered",

                FishNetTaxiJobState.ContractFailed =>
                    "Contract ended",

                _ => "Unknown"
            };

        public bool ShowStateTimer =>
            State == FishNetTaxiJobState.PassengerEnteringRequiredStop ||
            State == FishNetTaxiJobState.WaitingAtRequiredStop ||
            State == FishNetTaxiJobState.PassengerReturningFromRequiredStop ||
            (State == FishNetTaxiJobState.TravellingToDestination &&
             contractDefinition != null &&
             contractDefinition.FinalTimeLimitSeconds > 0f);

        public float CurrentStateDuration
        {
            get
            {
                if (contractDefinition == null)
                {
                    return 0f;
                }

                return State switch
                {
                    FishNetTaxiJobState.PassengerEnteringRequiredStop =>
                        contractDefinition.PassengerExitDuration,

                    FishNetTaxiJobState.WaitingAtRequiredStop =>
                        contractDefinition.RequiredStopWaitDuration,

                    FishNetTaxiJobState.PassengerReturningFromRequiredStop =>
                        contractDefinition.PassengerReturnDuration,

                    FishNetTaxiJobState.TravellingToDestination =>
                        contractDefinition.FinalTimeLimitSeconds,

                    _ => 0f
                };
            }
        }

        public float StateProgress01
        {
            get
            {
                float duration = CurrentStateDuration;

                if (duration <= 0f)
                {
                    return 1f;
                }

                return Mathf.Clamp01(
                    1f -
                    StateTimeRemaining / duration);
            }
        }

        private void Awake()
        {
            _state.OnChange += HandleStateChanged;
            _passengerAboard.OnChange +=
                HandlePassengerChanged;

            CacheZones();
        }

        private void OnDestroy()
        {
            _state.OnChange -= HandleStateChanged;
            _passengerAboard.OnChange -=
                HandlePassengerChanged;
        }

        private void Start()
        {
            if (!Application.isPlaying)
            {
                return;
            }

            CacheZones();
            ApplyPresentation();
        }

        private void LateUpdate()
        {
            if (!Application.isPlaying)
            {
                return;
            }

            // Scene objects from older installers may still be serialized as
            // inactive. Reassert the synchronized contract presentation after
            // all ordinary Update calls so stale hierarchy state or another
            // component cannot leave Earl or a destination disabled.
            ApplyPresentation();
        }

        private void Update()
        {
            if (!IsServerInitialized ||
                IsTerminal ||
                contractDefinition == null)
            {
                return;
            }

            float deltaTime =
                Mathf.Max(0f, Time.deltaTime);

            _serverContractElapsed += deltaTime;

            switch (State)
            {
                case FishNetTaxiJobState
                    .PassengerEnteringRequiredStop:
                    TickTimedTransition(
                        deltaTime,
                        requireTaxiPresence: false,
                        BeginWaitingAtRequiredStopOnServer);
                    break;

                case FishNetTaxiJobState.WaitingAtRequiredStop:
                    TickTimedTransition(
                        deltaTime,
                        contractDefinition
                            .RequireTaxiNearbyDuringWait,
                        BeginPassengerReturnOnServer);
                    break;

                case FishNetTaxiJobState
                    .PassengerReturningFromRequiredStop:
                    TickTimedTransition(
                        deltaTime,
                        requireTaxiPresence: true,
                        BeginFinalJourneyOnServer);
                    break;

                case FishNetTaxiJobState
                    .TravellingToDestination:
                    TickOptionalFinalDeadline(deltaTime);
                    break;
            }

            if (Time.unscaledTime >=
                _nextClockReplicationTime)
            {
                ReplicateClocks();
            }
        }

        public override void OnStartServer()
        {
            base.OnStartServer();

            _totalEarnings.Value = 0;
            _completedContracts.Value = 0;
            StartContractOnServer();
        }

        public override void OnStartClient()
        {
            base.OnStartClient();
            CacheZones();
            ApplyPresentation();
        }

        internal void ReportZonePresenceOnServer(
            string objectiveId,
            bool taxiInside,
            float fixedDeltaTime)
        {
            if (!IsServerInitialized ||
                contractDefinition == null ||
                taxiVehicle == null ||
                string.IsNullOrWhiteSpace(objectiveId) ||
                IsTerminal)
            {
                return;
            }

            bool isRequiredStopZone =
                contractDefinition.HasRequiredStop &&
                string.Equals(
                    objectiveId,
                    contractDefinition.RequiredStopObjectiveId,
                    System.StringComparison.Ordinal);

            if (isRequiredStopZone)
            {
                SetRequiredStopPresenceOnServer(
                    taxiInside);
            }

            string activeObjectiveId =
                ActiveObjectiveId;

            if (string.IsNullOrWhiteSpace(
                    activeObjectiveId) ||
                !string.Equals(
                    activeObjectiveId,
                    objectiveId,
                    System.StringComparison.Ordinal))
            {
                return;
            }

            switch (State)
            {
                case FishNetTaxiJobState.WaitingForPickup:
                    UpdateArrivalRequirement(
                        taxiInside,
                        fixedDeltaTime,
                        contractDefinition.PickupStopDuration,
                        BoardPassengerOnServer);
                    break;

                case FishNetTaxiJobState
                    .TravellingToRequiredStop:
                    UpdateArrivalRequirement(
                        taxiInside,
                        fixedDeltaTime,
                        contractDefinition
                            .RequiredStopArrivalDuration,
                        BeginPassengerExitOnServer);
                    break;

                case FishNetTaxiJobState
                    .TravellingToDestination:
                    UpdateArrivalRequirement(
                        taxiInside,
                        fixedDeltaTime,
                        contractDefinition.FinalStopDuration,
                        CompleteContractOnServer);
                    break;

                default:
                    _arrivalPresenceTimer = 0f;
                    break;
            }
        }

        public void RequestPrototypeRestart()
        {
            if (!CanRestartPrototype)
            {
                return;
            }

            if (IsServerInitialized)
            {
                StartContractOnServer();
                return;
            }

            if (IsClientInitialized)
            {
                RequestPrototypeRestartServerRpc();
            }
        }

        [ServerRpc(RequireOwnership = false)]
        private void RequestPrototypeRestartServerRpc()
        {
            if (CanRestartPrototype)
            {
                StartContractOnServer();
            }
        }

        private void UpdateArrivalRequirement(
            bool taxiInside,
            float fixedDeltaTime,
            float requiredDuration,
            System.Action completion)
        {
            if (!taxiInside ||
                !HasDriverOnServer() ||
                !IsTaxiStopped())
            {
                _arrivalPresenceTimer = 0f;
                return;
            }

            _arrivalPresenceTimer +=
                Mathf.Max(0f, fixedDeltaTime);

            if (_arrivalPresenceTimer <
                Mathf.Max(0.1f, requiredDuration))
            {
                return;
            }

            _arrivalPresenceTimer = 0f;
            completion?.Invoke();
        }

        private void TickTimedTransition(
            float deltaTime,
            bool requireTaxiPresence,
            System.Action completion)
        {
            if (requireTaxiPresence &&
                !_serverTaxiAtRequiredStop)
            {
                return;
            }

            _serverStateTimeRemaining -= deltaTime;

            if (_serverStateTimeRemaining > 0f)
            {
                return;
            }

            _serverStateTimeRemaining = 0f;
            ReplicateClocks();
            completion?.Invoke();
        }

        private void TickOptionalFinalDeadline(
            float deltaTime)
        {
            if (contractDefinition.FinalTimeLimitSeconds <=
                0f)
            {
                return;
            }

            _serverStateTimeRemaining -= deltaTime;

            if (_serverStateTimeRemaining > 0f)
            {
                return;
            }

            _serverStateTimeRemaining = 0f;
            ReplicateClocks();

            if (contractDefinition.FailOnFinalTimeout)
            {
                FailContractOnServer(
                    "The final deadline expired.");
            }
        }

        private void StartContractOnServer()
        {
            if (!IsServerInitialized)
            {
                return;
            }

            if (contractDefinition == null)
            {
                _state.Value =
                    FishNetTaxiJobState.ContractFailed;

                Debug.LogError(
                    "Taxi contract cannot start because no " +
                    "contract definition is assigned.",
                    this);

                ApplyPresentation();
                return;
            }

            _arrivalPresenceTimer = 0f;
            _serverContractElapsed = 0f;
            _serverStateTimeRemaining = 0f;
            _serverTaxiAtRequiredStop = false;

            _taxiAtRequiredStop.Value = false;
            _passengerAboard.Value = false;
            _currentPayout.Value =
                contractDefinition.BaseReward;

            _lastPayout.Value = 0;
            _state.Value =
                FishNetTaxiJobState.WaitingForPickup;

            ReplicateClocks();
            ApplyPresentation();

            Debug.Log(
                $"Started contract " +
                $"'{contractDefinition.ContractName}'.",
                this);
        }

        private void BoardPassengerOnServer()
        {
            if (!IsServerInitialized ||
                State !=
                    FishNetTaxiJobState.WaitingForPickup)
            {
                return;
            }

            _passengerAboard.Value = true;

            if (contractDefinition.HasRequiredStop)
            {
                SetStateOnServer(
                    FishNetTaxiJobState
                        .TravellingToRequiredStop,
                    0f);
            }
            else
            {
                BeginFinalJourneyOnServer();
            }
        }

        private void BeginPassengerExitOnServer()
        {
            if (!IsServerInitialized ||
                State !=
                    FishNetTaxiJobState
                        .TravellingToRequiredStop)
            {
                return;
            }

            _passengerAboard.Value = false;

            SetStateOnServer(
                FishNetTaxiJobState
                    .PassengerEnteringRequiredStop,
                contractDefinition.PassengerExitDuration);
        }

        private void BeginWaitingAtRequiredStopOnServer()
        {
            if (!IsServerInitialized)
            {
                return;
            }

            SetStateOnServer(
                FishNetTaxiJobState.WaitingAtRequiredStop,
                contractDefinition.RequiredStopWaitDuration);
        }

        private void BeginPassengerReturnOnServer()
        {
            if (!IsServerInitialized)
            {
                return;
            }

            SetStateOnServer(
                FishNetTaxiJobState
                    .PassengerReturningFromRequiredStop,
                contractDefinition.PassengerReturnDuration);
        }

        private void BeginFinalJourneyOnServer()
        {
            if (!IsServerInitialized)
            {
                return;
            }

            _passengerAboard.Value = true;

            SetStateOnServer(
                FishNetTaxiJobState
                    .TravellingToDestination,
                contractDefinition.FinalTimeLimitSeconds);
        }

        private void CompleteContractOnServer()
        {
            if (!IsServerInitialized ||
                IsTerminal)
            {
                return;
            }

            int paid =
                Mathf.Max(1, _currentPayout.Value);

            _lastPayout.Value = paid;
            _totalEarnings.Value += paid;
            _completedContracts.Value += 1;
            _passengerAboard.Value = false;
            _state.Value =
                FishNetTaxiJobState.ContractComplete;

            _serverStateTimeRemaining = 0f;
            ReplicateClocks();
            ApplyPresentation();

            Debug.Log(
                $"Contract complete. Earned £{paid}. " +
                $"Session total: £{_totalEarnings.Value}.",
                this);
        }

        private void FailContractOnServer(
            string reason)
        {
            if (!IsServerInitialized ||
                IsTerminal)
            {
                return;
            }

            _lastPayout.Value = 0;
            _passengerAboard.Value = false;
            _state.Value =
                FishNetTaxiJobState.ContractFailed;

            _serverStateTimeRemaining = 0f;
            ReplicateClocks();
            ApplyPresentation();

            Debug.LogWarning(
                $"Contract failed. {reason}",
                this);
        }

        private void SetStateOnServer(
            FishNetTaxiJobState newState,
            float timeRemaining)
        {
            _arrivalPresenceTimer = 0f;
            _serverStateTimeRemaining =
                Mathf.Max(0f, timeRemaining);

            _state.Value = newState;
            ReplicateClocks();
            ApplyPresentation();

            Debug.Log(
                $"Contract state changed to {newState}.",
                this);
        }

        private void SetRequiredStopPresenceOnServer(
            bool present)
        {
            _serverTaxiAtRequiredStop = present;

            if (_taxiAtRequiredStop.Value != present)
            {
                _taxiAtRequiredStop.Value = present;
            }
        }

        private void ReplicateClocks()
        {
            if (!IsServerInitialized)
            {
                return;
            }

            _stateTimeRemaining.Value =
                Mathf.Max(
                    0f,
                    _serverStateTimeRemaining);

            _contractElapsedSeconds.Value =
                Mathf.Max(
                    0f,
                    _serverContractElapsed);

            _nextClockReplicationTime =
                Time.unscaledTime +
                ClockReplicationInterval;
        }

        private string GetWaitingObjectiveText()
        {
            if (contractDefinition == null)
            {
                return string.Empty;
            }

            if (contractDefinition
                    .RequireTaxiNearbyDuringWait &&
                !TaxiAtRequiredStop)
            {
                return
                    $"Return to " +
                    $"{contractDefinition.RequiredStopLocationName} " +
                    $"and wait for {PassengerName}.";
            }

            return
                contractDefinition.WaitAtRequiredStopText;
        }

        private bool HasDriverOnServer()
        {
            for (int seatIndex = 0;
                 seatIndex < taxiVehicle.SeatCount;
                 seatIndex++)
            {
                if (!taxiVehicle.TryGetSeat(
                        seatIndex,
                        out FishNetVehicleSeatDefinition seat) ||
                    seat.Role !=
                        FishNetVehicleSeatRole.Driver)
                {
                    continue;
                }

                return taxiVehicle.GetOccupantClientId(
                           seatIndex) !=
                       FishNetVehicle.EmptySeatClientId;
            }

            return false;
        }

        private bool IsTaxiStopped()
        {
            Rigidbody body =
                taxiVehicle.GetComponent<Rigidbody>();

            if (body == null)
            {
                return true;
            }

            return body.linearVelocity.magnitude <=
                       maximumArrivalSpeed &&
                   body.angularVelocity.magnitude <=
                       maximumArrivalAngularSpeed;
        }

        private void HandleStateChanged(
            FishNetTaxiJobState previousValue,
            FishNetTaxiJobState newValue,
            bool asServer)
        {
            ApplyPresentation();
        }

        private void HandlePassengerChanged(
            bool previousValue,
            bool newValue,
            bool asServer)
        {
            ApplyPresentation();
        }

        private void CacheZones()
        {
            _zones =
                GetComponentsInChildren<
                    FishNetTaxiJobZone>(
                    includeInactive: true);
        }

        private void ApplyPresentation()
        {
            if (_zones == null ||
                _zones.Length == 0)
            {
                CacheZones();
            }

            string activeObjectiveId =
                ActiveObjectiveId;

            if (_zones != null)
            {
                foreach (FishNetTaxiJobZone zone in _zones)
                {
                    if (zone == null)
                    {
                        continue;
                    }

                    // Objective roots must remain active so their trigger
                    // colliders, passenger children and marker presentation
                    // can function. Older proof installers disabled the
                    // pickup/drop-off roots themselves rather than only their
                    // marker children.
                    if (!zone.gameObject.activeSelf)
                    {
                        zone.gameObject.SetActive(true);
                    }

                    bool active =
                        !string.IsNullOrWhiteSpace(
                            activeObjectiveId) &&
                        string.Equals(
                            zone.ObjectiveId,
                            activeObjectiveId,
                            System.StringComparison.Ordinal);

                    zone.SetMarkerVisible(active);
                }
            }

            bool waiting =
                State ==
                    FishNetTaxiJobState.WaitingForPickup &&
                !PassengerAboard;

            bool riding =
                PassengerAboard &&
                !IsTerminal;

            SetActiveIfNeeded(
                waitingPassengerRoot,
                waiting);

            SetActiveIfNeeded(
                ridingPassengerRoot,
                riding);

            // The legacy Phase 6 pickupMarkerRoot/dropoffMarkerRoot
            // fields are intentionally ignored. In old scenes those fields
            // can reference the complete objective hierarchy rather than only
            // a marker child. Disabling them would deactivate Earl, triggers
            // and future destinations.
        }

        private static void SetActiveIfNeeded(
            GameObject target,
            bool active)
        {
            if (target != null &&
                target.activeSelf != active)
            {
                target.SetActive(active);
            }
        }

        protected override void Reset()
        {
            base.Reset();

            if (taxiVehicle == null)
            {
                taxiVehicle =
                    FindFirstObjectByType<
                        FishNetVehicle>();
            }

            CacheZones();
        }

        protected override void OnValidate()
        {
            base.OnValidate();

            maximumArrivalSpeed =
                Mathf.Max(
                    0f,
                    maximumArrivalSpeed);

            maximumArrivalAngularSpeed =
                Mathf.Max(
                    0f,
                    maximumArrivalAngularSpeed);
        }
    }
}
