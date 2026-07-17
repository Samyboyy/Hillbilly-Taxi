using UnityEngine;

namespace HillbillyTaxi.FishNetMigration.TaxiJobs
{
    /// <summary>
    /// Defines what a contract requires, not how players must travel.
    ///
    /// Dynamic road conditions, patrols, weather, checkpoints and enemies must
    /// live in a separate world-director system. They are deliberately absent
    /// from this asset.
    /// </summary>
    [CreateAssetMenu(
        fileName = "TaxiContract",
        menuName = "Hillbilly Taxi/Taxi Contract")]
    public sealed class FishNetTaxiContractDefinition : ScriptableObject
    {
        [Header("Identity")]
        [SerializeField] private string contractId = "contract";
        [SerializeField] private string contractName = "Taxi Contract";
        [SerializeField] private string passengerName = "Passenger";

        [SerializeField, TextArea(2, 4)]
        private string specialRule =
            "Deliver the passenger safely.";

        [Header("Reward")]
        [SerializeField, Min(1)] private int baseReward = 100;

        [Header("Pickup")]
        [SerializeField] private string pickupObjectiveId = "pickup";
        [SerializeField] private string pickupLocationName = "Pickup";

        [SerializeField, TextArea(2, 3)]
        private string pickupObjectiveText =
            "Pick up the passenger.";

        [SerializeField, Min(0.1f)]
        private float pickupStopDuration = 0.75f;

        [Header("Optional required stop")]
        [SerializeField] private bool hasRequiredStop;

        [SerializeField]
        private string requiredStopObjectiveId = "required_stop";

        [SerializeField]
        private string requiredStopLocationName = "Required Stop";

        [SerializeField, TextArea(2, 3)]
        private string travelToRequiredStopText =
            "Take the passenger to the required stop.";

        [SerializeField, TextArea(2, 3)]
        private string waitAtRequiredStopText =
            "Wait for the passenger.";

        [SerializeField, Min(0.1f)]
        private float requiredStopArrivalDuration = 0.75f;

        [SerializeField, Min(0f)]
        private float passengerExitDuration = 2.5f;

        [SerializeField, Min(1f)]
        private float requiredStopWaitDuration = 35f;

        [SerializeField, Min(0f)]
        private float passengerReturnDuration = 3f;

        [SerializeField]
        private bool requireTaxiNearbyDuringWait = true;

        [Header("Final destination")]
        [SerializeField]
        private string finalObjectiveId = "final_dropoff";

        [SerializeField]
        private string finalLocationName = "Final Destination";

        [SerializeField, TextArea(2, 3)]
        private string finalObjectiveText =
            "Deliver the passenger.";

        [SerializeField, Min(0.1f)]
        private float finalStopDuration = 0.75f;

        [SerializeField, Min(0f)]
        private float finalTimeLimitSeconds;

        [SerializeField]
        private bool failOnFinalTimeout;

        public string ContractId => contractId;
        public string ContractName => contractName;
        public string PassengerName => passengerName;
        public string SpecialRule => specialRule;
        public int BaseReward => Mathf.Max(1, baseReward);

        public string PickupObjectiveId =>
            NormalizedId(pickupObjectiveId, "pickup");

        public string PickupLocationName =>
            FallbackText(pickupLocationName, "Pickup");

        public string PickupObjectiveText =>
            FallbackText(
                pickupObjectiveText,
                $"Pick up {PassengerName}.");

        public float PickupStopDuration =>
            Mathf.Max(0.1f, pickupStopDuration);

        public bool HasRequiredStop => hasRequiredStop;

        public string RequiredStopObjectiveId =>
            NormalizedId(
                requiredStopObjectiveId,
                "required_stop");

        public string RequiredStopLocationName =>
            FallbackText(
                requiredStopLocationName,
                "Required Stop");

        public string TravelToRequiredStopText =>
            FallbackText(
                travelToRequiredStopText,
                $"Take {PassengerName} to " +
                $"{RequiredStopLocationName}.");

        public string WaitAtRequiredStopText =>
            FallbackText(
                waitAtRequiredStopText,
                $"Wait for {PassengerName}.");

        public float RequiredStopArrivalDuration =>
            Mathf.Max(
                0.1f,
                requiredStopArrivalDuration);

        public float PassengerExitDuration =>
            Mathf.Max(0f, passengerExitDuration);

        public float RequiredStopWaitDuration =>
            Mathf.Max(1f, requiredStopWaitDuration);

        public float PassengerReturnDuration =>
            Mathf.Max(0f, passengerReturnDuration);

        public bool RequireTaxiNearbyDuringWait =>
            requireTaxiNearbyDuringWait;

        public string FinalObjectiveId =>
            NormalizedId(
                finalObjectiveId,
                "final_dropoff");

        public string FinalLocationName =>
            FallbackText(
                finalLocationName,
                "Final Destination");

        public string FinalObjectiveText =>
            FallbackText(
                finalObjectiveText,
                $"Take {PassengerName} to " +
                $"{FinalLocationName}.");

        public float FinalStopDuration =>
            Mathf.Max(0.1f, finalStopDuration);

        public float FinalTimeLimitSeconds =>
            Mathf.Max(0f, finalTimeLimitSeconds);

        public bool FailOnFinalTimeout =>
            failOnFinalTimeout;

        private static string NormalizedId(
            string value,
            string fallback)
        {
            return string.IsNullOrWhiteSpace(value)
                ? fallback
                : value.Trim();
        }

        private static string FallbackText(
            string value,
            string fallback)
        {
            return string.IsNullOrWhiteSpace(value)
                ? fallback
                : value.Trim();
        }

        private void OnValidate()
        {
            baseReward = Mathf.Max(1, baseReward);
            pickupStopDuration =
                Mathf.Max(0.1f, pickupStopDuration);

            requiredStopArrivalDuration =
                Mathf.Max(
                    0.1f,
                    requiredStopArrivalDuration);

            passengerExitDuration =
                Mathf.Max(0f, passengerExitDuration);

            requiredStopWaitDuration =
                Mathf.Max(
                    1f,
                    requiredStopWaitDuration);

            passengerReturnDuration =
                Mathf.Max(
                    0f,
                    passengerReturnDuration);

            finalStopDuration =
                Mathf.Max(0.1f, finalStopDuration);

            finalTimeLimitSeconds =
                Mathf.Max(0f, finalTimeLimitSeconds);
        }
    }
}
