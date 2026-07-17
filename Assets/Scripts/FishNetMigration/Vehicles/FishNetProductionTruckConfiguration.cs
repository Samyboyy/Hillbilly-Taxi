using UnityEngine;

namespace HillbillyTaxi.FishNetMigration.Vehicles
{
    /// <summary>
    /// Records the final production-truck references in one stable component.
    ///
    /// Phase 10 damage, repair, audio and visual effects can use this component
    /// instead of searching the imported FBX hierarchy by name at runtime.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FishNetProductionTruckConfiguration :
        MonoBehaviour
    {
        public const int CurrentIntegrationVersion = 2;

        [Header("Integration")]
        [SerializeField] private int integrationVersion =
            CurrentIntegrationVersion;

        [SerializeField] private Transform productionVisualRoot;
        [SerializeField] private Transform productionPhysicsRoot;
        [SerializeField] private Transform productionBodyColliderRoot;
        [SerializeField] private Transform productionSeatCameraRoot;

        [Header("Authoritative physics")]
        [SerializeField] private Rigidbody authoritativeBody;
        [SerializeField] private WheelCollider frontLeftWheel;
        [SerializeField] private WheelCollider frontRightWheel;
        [SerializeField] private WheelCollider rearLeftWheel;
        [SerializeField] private WheelCollider rearRightWheel;

        [Header("Major articulated parts")]
        [SerializeField] private Transform hood;
        [SerializeField] private Transform tailgate;
        [SerializeField] private Transform driverDoor;
        [SerializeField] private Transform frontRightDoor;
        [SerializeField] private Transform rearLeftDoor;
        [SerializeField] private Transform rearRightDoor;

        [Header("Occupant anchors")]
        [SerializeField] private Transform driverSeat;
        [SerializeField] private Transform frontRightSeat;
        [SerializeField] private Transform rearLeftSeat;
        [SerializeField] private Transform rearRightSeat;
        [SerializeField] private Transform taxiPassengerSeat;

        [Header("Future damage interaction anchors")]
        [SerializeField] private Transform engineBayInteraction;
        [SerializeField] private Transform fuelInteraction;
        [SerializeField] private Transform towFront;
        [SerializeField] private Transform towRear;

        public int IntegrationVersion => integrationVersion;
        public Transform ProductionVisualRoot => productionVisualRoot;
        public Transform ProductionPhysicsRoot => productionPhysicsRoot;
        public Transform ProductionBodyColliderRoot => productionBodyColliderRoot;
        public Transform ProductionSeatCameraRoot => productionSeatCameraRoot;

        public Rigidbody AuthoritativeBody => authoritativeBody;
        public WheelCollider FrontLeftWheel => frontLeftWheel;
        public WheelCollider FrontRightWheel => frontRightWheel;
        public WheelCollider RearLeftWheel => rearLeftWheel;
        public WheelCollider RearRightWheel => rearRightWheel;

        public Transform Hood => hood;
        public Transform Tailgate => tailgate;
        public Transform DriverDoor => driverDoor;
        public Transform FrontRightDoor => frontRightDoor;
        public Transform RearLeftDoor => rearLeftDoor;
        public Transform RearRightDoor => rearRightDoor;

        public Transform DriverSeat => driverSeat;
        public Transform FrontRightSeat => frontRightSeat;
        public Transform RearLeftSeat => rearLeftSeat;
        public Transform RearRightSeat => rearRightSeat;
        public Transform TaxiPassengerSeat => taxiPassengerSeat;

        public Transform EngineBayInteraction => engineBayInteraction;
        public Transform FuelInteraction => fuelInteraction;
        public Transform TowFront => towFront;
        public Transform TowRear => towRear;

        public bool HasCompleteWheelSet =>
            frontLeftWheel != null &&
            frontRightWheel != null &&
            rearLeftWheel != null &&
            rearRightWheel != null;

        public bool IsCurrentVersion =>
            integrationVersion == CurrentIntegrationVersion;

        private void OnValidate()
        {
            integrationVersion =
                CurrentIntegrationVersion;
        }
    }
}
