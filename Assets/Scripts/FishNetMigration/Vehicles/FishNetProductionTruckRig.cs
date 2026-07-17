using UnityEngine;

namespace HillbillyTaxi.FishNetMigration.Vehicles
{
    /// <summary>
    /// Drives the production truck's authored visual suspension hierarchy.
    ///
    /// This component is presentation only. Rigidbody and WheelCollider physics
    /// remain on the server-authoritative FishNetTruckMotor.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FishNetProductionTruckRig :
        MonoBehaviour
    {
        [Header("Vehicle frame")]
        [SerializeField] private Transform vehicleRoot;
        [SerializeField] private Transform visualRoot;

        [Header("Front-left")]
        [SerializeField] private Transform frontLeftSuspension;
        [SerializeField] private Transform frontLeftSteer;
        [SerializeField] private Transform frontLeftSpin;
        [SerializeField] private Transform frontLeftShockRod;

        [Header("Front-right")]
        [SerializeField] private Transform frontRightSuspension;
        [SerializeField] private Transform frontRightSteer;
        [SerializeField] private Transform frontRightSpin;
        [SerializeField] private Transform frontRightShockRod;

        [Header("Rear-left")]
        [SerializeField] private Transform rearLeftSuspension;
        [SerializeField] private Transform rearLeftSpin;
        [SerializeField] private Transform rearLeftShockRod;

        [Header("Rear-right")]
        [SerializeField] private Transform rearRightSuspension;
        [SerializeField] private Transform rearRightSpin;
        [SerializeField] private Transform rearRightShockRod;

        [Header("Presentation tuning")]
        [SerializeField, Range(0f, 1.5f)]
        private float shockRodTravelMultiplier = 0.65f;

        private Quaternion _frontLeftSteerRestRotation;
        private Quaternion _frontRightSteerRestRotation;

        private Quaternion _frontLeftSpinRestRotation;
        private Quaternion _frontRightSpinRestRotation;
        private Quaternion _rearLeftSpinRestRotation;
        private Quaternion _rearRightSpinRestRotation;

        private Vector3 _frontLeftRestVehicleLocalPosition;
        private Vector3 _frontRightRestVehicleLocalPosition;
        private Vector3 _rearLeftRestVehicleLocalPosition;
        private Vector3 _rearRightRestVehicleLocalPosition;

        private Vector3 _frontLeftShockRodRestLocalPosition;
        private Vector3 _frontRightShockRodRestLocalPosition;
        private Vector3 _rearLeftShockRodRestLocalPosition;
        private Vector3 _rearRightShockRodRestLocalPosition;

        private bool _cached;

        public bool IsConfigured =>
            vehicleRoot != null &&
            visualRoot != null &&
            frontLeftSuspension != null &&
            frontRightSuspension != null &&
            rearLeftSuspension != null &&
            rearRightSuspension != null &&
            frontLeftSteer != null &&
            frontRightSteer != null &&
            frontLeftSpin != null &&
            frontRightSpin != null &&
            rearLeftSpin != null &&
            rearRightSpin != null;

        private void Awake()
        {
            CacheRestPose();
        }

        public void ApplyVisualState(
            Vector3 frontLeftWorldPosition,
            Vector3 frontRightWorldPosition,
            Vector3 rearLeftWorldPosition,
            Vector3 rearRightWorldPosition,
            float steerAngle,
            float spinAngle)
        {
            CacheRestPose();

            if (!IsConfigured)
            {
                return;
            }

            frontLeftSuspension.position =
                frontLeftWorldPosition;

            frontRightSuspension.position =
                frontRightWorldPosition;

            rearLeftSuspension.position =
                rearLeftWorldPosition;

            rearRightSuspension.position =
                rearRightWorldPosition;

            frontLeftSteer.localRotation =
                _frontLeftSteerRestRotation *
                Quaternion.AngleAxis(
                    steerAngle,
                    Vector3.up);

            frontRightSteer.localRotation =
                _frontRightSteerRestRotation *
                Quaternion.AngleAxis(
                    steerAngle,
                    Vector3.up);

            frontLeftSpin.localRotation =
                _frontLeftSpinRestRotation *
                Quaternion.AngleAxis(
                    spinAngle,
                    Vector3.right);

            frontRightSpin.localRotation =
                _frontRightSpinRestRotation *
                Quaternion.AngleAxis(
                    spinAngle,
                    Vector3.right);

            rearLeftSpin.localRotation =
                _rearLeftSpinRestRotation *
                Quaternion.AngleAxis(
                    spinAngle,
                    Vector3.right);

            rearRightSpin.localRotation =
                _rearRightSpinRestRotation *
                Quaternion.AngleAxis(
                    spinAngle,
                    Vector3.right);

            AnimateShockRod(
                frontLeftShockRod,
                _frontLeftShockRodRestLocalPosition,
                frontLeftSuspension,
                _frontLeftRestVehicleLocalPosition);

            AnimateShockRod(
                frontRightShockRod,
                _frontRightShockRodRestLocalPosition,
                frontRightSuspension,
                _frontRightRestVehicleLocalPosition);

            AnimateShockRod(
                rearLeftShockRod,
                _rearLeftShockRodRestLocalPosition,
                rearLeftSuspension,
                _rearLeftRestVehicleLocalPosition);

            AnimateShockRod(
                rearRightShockRod,
                _rearRightShockRodRestLocalPosition,
                rearRightSuspension,
                _rearRightRestVehicleLocalPosition);
        }

        private void AnimateShockRod(
            Transform shockRod,
            Vector3 restLocalPosition,
            Transform suspension,
            Vector3 suspensionRestVehicleLocalPosition)
        {
            if (shockRod == null ||
                shockRod.parent == null ||
                vehicleRoot == null)
            {
                return;
            }

            Vector3 currentVehicleLocalPosition =
                vehicleRoot.InverseTransformPoint(
                    suspension.position);

            float verticalTravel =
                currentVehicleLocalPosition.y -
                suspensionRestVehicleLocalPosition.y;

            Vector3 localVehicleUp =
                shockRod.parent
                    .InverseTransformDirection(
                        vehicleRoot.up)
                    .normalized;

            shockRod.localPosition =
                restLocalPosition +
                localVehicleUp *
                verticalTravel *
                shockRodTravelMultiplier;
        }

        private void CacheRestPose()
        {
            if (_cached)
            {
                return;
            }

            if (vehicleRoot == null)
            {
                vehicleRoot = transform;
            }

            if (!IsConfigured)
            {
                return;
            }

            _frontLeftSteerRestRotation =
                frontLeftSteer.localRotation;

            _frontRightSteerRestRotation =
                frontRightSteer.localRotation;

            _frontLeftSpinRestRotation =
                frontLeftSpin.localRotation;

            _frontRightSpinRestRotation =
                frontRightSpin.localRotation;

            _rearLeftSpinRestRotation =
                rearLeftSpin.localRotation;

            _rearRightSpinRestRotation =
                rearRightSpin.localRotation;

            _frontLeftRestVehicleLocalPosition =
                vehicleRoot.InverseTransformPoint(
                    frontLeftSuspension.position);

            _frontRightRestVehicleLocalPosition =
                vehicleRoot.InverseTransformPoint(
                    frontRightSuspension.position);

            _rearLeftRestVehicleLocalPosition =
                vehicleRoot.InverseTransformPoint(
                    rearLeftSuspension.position);

            _rearRightRestVehicleLocalPosition =
                vehicleRoot.InverseTransformPoint(
                    rearRightSuspension.position);

            _frontLeftShockRodRestLocalPosition =
                GetLocalPosition(
                    frontLeftShockRod);

            _frontRightShockRodRestLocalPosition =
                GetLocalPosition(
                    frontRightShockRod);

            _rearLeftShockRodRestLocalPosition =
                GetLocalPosition(
                    rearLeftShockRod);

            _rearRightShockRodRestLocalPosition =
                GetLocalPosition(
                    rearRightShockRod);

            _cached = true;
        }

        private static Vector3 GetLocalPosition(
            Transform target)
        {
            return target != null
                ? target.localPosition
                : Vector3.zero;
        }

        protected void Reset()
        {
            vehicleRoot = transform;
        }

        private void OnValidate()
        {
            shockRodTravelMultiplier =
                Mathf.Clamp(
                    shockRodTravelMultiplier,
                    0f,
                    1.5f);

            _cached = false;
        }
    }
}
