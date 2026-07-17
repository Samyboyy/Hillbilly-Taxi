using System;
using System.Collections.Generic;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using HillbillyTaxi.FishNetMigration.TaxiJobs;
using UnityEngine;

namespace HillbillyTaxi.FishNetMigration.DynamicWorld
{
    /// <summary>
    /// Server-owned dynamic-world foundation.
    ///
    /// It selects persistent situations at fixed scene locations. It never moves
    /// a situation to counter the players' selected route and never changes the
    /// taxi contract's destination or instructions.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkObject))]
    public sealed class FishNetWorldDirector : NetworkBehaviour
    {
        private const int MaximumSituationCount = 63;

        [Header("Contract context")]
        [SerializeField] private FishNetTaxiJobManager taxiJobManager;

        [Header("Debug")]
        [SerializeField] private bool logSelections = true;

        private readonly SyncVar<ulong> _activeSituationMask = new();
        private readonly SyncVar<int> _worldSeed = new();
        private readonly SyncVar<int> _worldCycle = new();

        private FishNetWorldSituationAnchor[] _situations =
            Array.Empty<FishNetWorldSituationAnchor>();

        private int _activatedPhaseMask;
        private FishNetTaxiJobState _lastObservedContractState;
        private bool _hasObservedContractState;
        private bool _serverWorldInitialized;

        public int WorldSeed => _worldSeed.Value;
        public int WorldCycle => _worldCycle.Value;
        public ulong ActiveSituationMask =>
            _activeSituationMask.Value;

        public IReadOnlyList<FishNetWorldSituationAnchor>
            Situations => _situations;

        private void Awake()
        {
            _activeSituationMask.OnChange +=
                HandleActiveSituationMaskChanged;

            CacheAndValidateSituations();
        }

        private void OnDestroy()
        {
            _activeSituationMask.OnChange -=
                HandleActiveSituationMaskChanged;
        }

        private void Update()
        {
            if (!IsServerInitialized ||
                taxiJobManager == null)
            {
                return;
            }

            if (!_serverWorldInitialized)
            {
                BeginNewWorldCycleOnServer();
                _serverWorldInitialized = true;
            }

            FishNetTaxiJobState currentState =
                taxiJobManager.State;

            if (!_hasObservedContractState)
            {
                _lastObservedContractState = currentState;
                _hasObservedContractState = true;

                ActivatePhaseForStateOnServer(
                    currentState);

                return;
            }

            if (currentState ==
                _lastObservedContractState)
            {
                return;
            }

            FishNetTaxiJobState previousState =
                _lastObservedContractState;

            _lastObservedContractState =
                currentState;

            if (currentState ==
                    FishNetTaxiJobState.WaitingForPickup &&
                (previousState ==
                     FishNetTaxiJobState.ContractComplete ||
                 previousState ==
                     FishNetTaxiJobState.ContractFailed))
            {
                BeginNewWorldCycleOnServer();
                return;
            }

            ActivatePhaseForStateOnServer(
                currentState);
        }

        public override void OnStartServer()
        {
            base.OnStartServer();

            CacheAndValidateSituations();
            _serverWorldInitialized = false;
            _hasObservedContractState = false;
            _activatedPhaseMask = 0;
            _activeSituationMask.Value = 0UL;
        }

        public override void OnStartClient()
        {
            base.OnStartClient();

            CacheAndValidateSituations();
            ApplySituationMask(
                _activeSituationMask.Value);
        }

        public void RequestDebugReroll()
        {
            if (taxiJobManager == null ||
                taxiJobManager.State !=
                    FishNetTaxiJobState.WaitingForPickup)
            {
                return;
            }

            if (IsServerInitialized)
            {
                BeginNewWorldCycleOnServer();
                return;
            }

            if (IsClientInitialized)
            {
                RequestDebugRerollServerRpc();
            }
        }

        [ServerRpc(RequireOwnership = false)]
        private void RequestDebugRerollServerRpc()
        {
            if (taxiJobManager != null &&
                taxiJobManager.State ==
                    FishNetTaxiJobState.WaitingForPickup)
            {
                BeginNewWorldCycleOnServer();
            }
        }

        public bool IsSituationActive(int situationIndex)
        {
            if (situationIndex < 0 ||
                situationIndex >= _situations.Length ||
                situationIndex >= MaximumSituationCount)
            {
                return false;
            }

            ulong bit =
                1UL << situationIndex;

            return (_activeSituationMask.Value &
                    bit) != 0UL;
        }

        public string GetActiveSituationSummary()
        {
            if (_situations.Length == 0)
            {
                return "No world situations configured.";
            }

            List<string> activeNames = new();

            for (int index = 0;
                 index < _situations.Length;
                 index++)
            {
                if (IsSituationActive(index))
                {
                    activeNames.Add(
                        _situations[index].DisplayName);
                }
            }

            return activeNames.Count == 0
                ? "No situations active."
                : string.Join(", ", activeNames);
        }

        private void BeginNewWorldCycleOnServer()
        {
            if (!IsServerInitialized)
            {
                return;
            }

            _worldCycle.Value += 1;
            _worldSeed.Value =
                GenerateServerSeed(
                    _worldCycle.Value);

            _activatedPhaseMask = 0;
            _activeSituationMask.Value = 0UL;

            ActivatePhaseOnServer(
                FishNetWorldSituationPhase
                    .ContractStart);

            if (logSelections)
            {
                Debug.Log(
                    $"World cycle {_worldCycle.Value} committed with " +
                    $"seed {_worldSeed.Value}. Active: " +
                    $"{GetActiveSituationSummary()}",
                    this);
            }
        }

        private void ActivatePhaseForStateOnServer(
            FishNetTaxiJobState state)
        {
            switch (state)
            {
                case FishNetTaxiJobState
                    .WaitingAtRequiredStop:
                    ActivatePhaseOnServer(
                        FishNetWorldSituationPhase
                            .RequiredStopWait);
                    break;

                case FishNetTaxiJobState
                    .TravellingToDestination:
                    ActivatePhaseOnServer(
                        FishNetWorldSituationPhase
                            .FinalJourney);
                    break;
            }
        }

        private void ActivatePhaseOnServer(
            FishNetWorldSituationPhase phase)
        {
            if (!IsServerInitialized)
            {
                return;
            }

            int phaseBit =
                1 << (int)phase;

            if ((_activatedPhaseMask &
                 phaseBit) != 0)
            {
                return;
            }

            _activatedPhaseMask |= phaseBit;

            Dictionary<string, List<int>>
                groupedCandidates =
                    new(StringComparer.Ordinal);

            for (int index = 0;
                 index < _situations.Length;
                 index++)
            {
                FishNetWorldSituationAnchor situation =
                    _situations[index];

                if (situation.ActivationPhase != phase)
                {
                    continue;
                }

                string group =
                    situation.SelectionGroup;

                if (string.IsNullOrEmpty(group))
                {
                    ActivateSituationOnServer(index);
                    continue;
                }

                if (!groupedCandidates.TryGetValue(
                        group,
                        out List<int> indices))
                {
                    indices = new List<int>();

                    groupedCandidates.Add(
                        group,
                        indices);
                }

                indices.Add(index);
            }

            foreach (
                KeyValuePair<string, List<int>>
                    group in groupedCandidates)
            {
                int selectedIndex =
                    SelectWeightedSituation(
                        group.Key,
                        phase,
                        group.Value);

                if (selectedIndex >= 0)
                {
                    ActivateSituationOnServer(
                        selectedIndex);
                }
            }
        }

        private int SelectWeightedSituation(
            string selectionGroup,
            FishNetWorldSituationPhase phase,
            List<int> candidateIndices)
        {
            if (candidateIndices == null ||
                candidateIndices.Count == 0)
            {
                return -1;
            }

            int groupSeed =
                StableHash(selectionGroup);

            int combinedSeed =
                unchecked(
                    _worldSeed.Value ^
                    groupSeed ^
                    ((int)phase * 486187739));

            System.Random random =
                new(combinedSeed);

            double totalWeight = 0d;

            foreach (int index in candidateIndices)
            {
                totalWeight +=
                    _situations[index]
                        .SelectionWeight;
            }

            if (totalWeight <= 0d)
            {
                return candidateIndices[0];
            }

            double roll =
                random.NextDouble() *
                totalWeight;

            double accumulated = 0d;

            foreach (int index in candidateIndices)
            {
                accumulated +=
                    _situations[index]
                        .SelectionWeight;

                if (roll <= accumulated)
                {
                    return index;
                }
            }

            return candidateIndices[
                candidateIndices.Count - 1];
        }

        private void ActivateSituationOnServer(
            int situationIndex)
        {
            if (situationIndex < 0 ||
                situationIndex >= _situations.Length ||
                situationIndex >= MaximumSituationCount)
            {
                return;
            }

            ulong bit =
                1UL << situationIndex;

            if ((_activeSituationMask.Value &
                 bit) != 0UL)
            {
                return;
            }

            _activeSituationMask.Value |= bit;

            if (logSelections)
            {
                FishNetWorldSituationAnchor situation =
                    _situations[situationIndex];

                Debug.Log(
                    $"World situation committed: " +
                    $"{situation.DisplayName} at " +
                    $"{situation.transform.position}.",
                    situation);
            }
        }

        private void HandleActiveSituationMaskChanged(
            ulong previousValue,
            ulong newValue,
            bool asServer)
        {
            ApplySituationMask(newValue);
        }

        private void ApplySituationMask(
            ulong mask)
        {
            for (int index = 0;
                 index < _situations.Length;
                 index++)
            {
                bool active =
                    index <
                        MaximumSituationCount &&
                    (mask &
                     (1UL << index)) != 0UL;

                _situations[index]
                    .SetSituationActive(active);
            }
        }

        private void CacheAndValidateSituations()
        {
            _situations =
                GetComponentsInChildren<
                    FishNetWorldSituationAnchor>(
                    includeInactive: true);

            Array.Sort(
                _situations,
                CompareSituations);

            if (_situations.Length >
                MaximumSituationCount)
            {
                Debug.LogError(
                    $"World director supports a maximum of " +
                    $"{MaximumSituationCount} situations. " +
                    $"Found {_situations.Length}.",
                    this);
            }

            HashSet<string> ids =
                new(StringComparer.Ordinal);

            foreach (
                FishNetWorldSituationAnchor situation
                in _situations)
            {
                if (!ids.Add(
                        situation.SituationId))
                {
                    Debug.LogError(
                        $"Duplicate world situation ID " +
                        $"'{situation.SituationId}'.",
                        situation);
                }
            }
        }

        private static int CompareSituations(
            FishNetWorldSituationAnchor left,
            FishNetWorldSituationAnchor right)
        {
            return string.Compare(
                left.SituationId,
                right.SituationId,
                StringComparison.Ordinal);
        }

        private static int GenerateServerSeed(
            int worldCycle)
        {
            return unchecked(
                (int)DateTime.UtcNow.Ticks ^
                (worldCycle * 16777619) ^
                Environment.TickCount);
        }

        private static int StableHash(
            string value)
        {
            unchecked
            {
                int hash = (int)2166136261;

                for (int index = 0;
                     index < value.Length;
                     index++)
                {
                    hash ^= value[index];
                    hash *= 16777619;
                }

                return hash;
            }
        }

        protected override void Reset()
        {
            base.Reset();

            if (taxiJobManager == null)
            {
                taxiJobManager =
                    FindFirstObjectByType<
                        FishNetTaxiJobManager>();
            }

            CacheAndValidateSituations();
        }
    }
}
