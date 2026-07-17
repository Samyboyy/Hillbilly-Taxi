using FishNet.Component.Transforming;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;

namespace HillbillyTaxi.FishNetMigration.Vehicles
{
    /// <summary>
    /// Server-authoritative truck controller with production suspension support.
    ///
    /// The server alone applies Rigidbody and WheelCollider physics. FishNet's
    /// NetworkTransform distributes the body pose. Steering, wheel spin and
    /// per-wheel suspension positions are synchronized for remote visual rigs.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(FishNetVehicle))]
    [RequireComponent(typeof(NetworkTransform))]
    [RequireComponent(typeof(Rigidbody))]
    public sealed class FishNetTruckMotor :
        NetworkBehaviour
    {
        [Header("References")]
        [SerializeField] private FishNetVehicle vehicle;
        [SerializeField] private Rigidbody body;
        [SerializeField] private FishNetProductionTruckRig productionRig;

        [Header("Wheel colliders")]
        [SerializeField] private WheelCollider frontLeftWheel;
        [SerializeField] private WheelCollider frontRightWheel;
        [SerializeField] private WheelCollider rearLeftWheel;
        [SerializeField] private WheelCollider rearRightWheel;

        [Header("Legacy wheel visual pose roots")]
        [SerializeField] private Transform frontLeftVisual;
        [SerializeField] private Transform frontRightVisual;
        [SerializeField] private Transform rearLeftVisual;
        [SerializeField] private Transform rearRightVisual;

        [Header("Speed")]
        [SerializeField, Min(1f)]
        private float maximumForwardSpeed = 22f;

        [SerializeField, Min(1f)]
        private float maximumReverseSpeed = 8f;

        [SerializeField, Min(0f)]
        private float forwardMotorTorque = 1800f;

        [SerializeField, Min(0f)]
        private float reverseMotorTorque = 950f;

        [Header("Steering")]
        [SerializeField, Range(1f, 60f)]
        private float maximumSteerAngle = 34f;

        [SerializeField, Range(1f, 45f)]
        private float highSpeedSteerAngle = 11f;

        [SerializeField, Min(0f)]
        private float steeringResponse = 95f;

        [Header("Braking")]
        [SerializeField, Min(0f)]
        private float serviceBrakeTorque = 5200f;

        [SerializeField, Min(0f)]
        private float handbrakeTorque = 8000f;

        [SerializeField, Min(0f)]
        private float coastBrakeTorque = 65f;

        [Header("Suspension and body roll")]
        [SerializeField, Min(0f)]
        private float frontAntiRollForce = 8200f;

        [SerializeField, Min(0f)]
        private float rearAntiRollForce = 6200f;

        [Tooltip(
            "Rear lateral grip multiplier while the handbrake is held. " +
            "Lower values create a larger slide.")]
        [SerializeField, Range(0.1f, 1f)]
        private float handbrakeRearSidewaysGripMultiplier = 0.55f;

        [Tooltip(
            "Rear forward grip multiplier while the handbrake is held.")]
        [SerializeField, Range(0.1f, 1f)]
        private float handbrakeRearForwardGripMultiplier = 0.68f;

        [SerializeField, Min(0.1f)]
        private float gripRecoverySpeed = 4.5f;

        [Header("Stability")]
        [SerializeField, Min(0f)]
        private float downforcePerMetrePerSecond = 12f;

        [Header("Networking")]
        [SerializeField, Min(0.05f)]
        private float driverInputTimeout = 0.35f;

        [SerializeField, Min(0f)]
        private float remoteWheelVisualSmoothing = 18f;

        private readonly SyncVar<float>
            _networkSteerAngle = new();

        private readonly SyncVar<float>
            _networkWheelSpinAngle = new();

        private readonly SyncVar<Vector3>
            _networkFrontLeftWheelLocalPosition = new();

        private readonly SyncVar<Vector3>
            _networkFrontRightWheelLocalPosition = new();

        private readonly SyncVar<Vector3>
            _networkRearLeftWheelLocalPosition = new();

        private readonly SyncVar<Vector3>
            _networkRearRightWheelLocalPosition = new();

        private int _inputClientId =
            FishNetVehicle.EmptySeatClientId;

        private float _steeringInput;
        private float _throttleInput;
        private bool _handbrakeInput;
        private float _lastInputTime;

        private float _currentSteerAngle;
        private float _wheelSpinAngle;

        private float _visualSteerAngle;
        private float _visualWheelSpinAngle;

        private Vector3 _visualFrontLeftWheelLocalPosition;
        private Vector3 _visualFrontRightWheelLocalPosition;
        private Vector3 _visualRearLeftWheelLocalPosition;
        private Vector3 _visualRearRightWheelLocalPosition;
        private bool _visualWheelPositionsInitialized;

        private WheelFrictionCurve _rearLeftBaseForwardFriction;
        private WheelFrictionCurve _rearRightBaseForwardFriction;
        private WheelFrictionCurve _rearLeftBaseSidewaysFriction;
        private WheelFrictionCurve _rearRightBaseSidewaysFriction;
        private bool _baseRearFrictionCached;

        private bool _missingWheelWarningLogged;

        public float ForwardSpeedMetresPerSecond
        {
            get
            {
                if (body == null)
                {
                    return 0f;
                }

                return Vector3.Dot(
                    body.linearVelocity,
                    transform.forward);
            }
        }

        public bool HandbrakeActive =>
            _handbrakeInput;

        public float CurrentSteerAngle =>
            _currentSteerAngle;

        private void Awake()
        {
            ResolveReferences();
            CacheBaseRearFriction();
        }

        public override void OnStartServer()
        {
            base.OnStartServer();

            ResetDriverInput();
            CacheBaseRearFriction();

            if (body != null)
            {
                body.WakeUp();
            }
        }

        public override void OnStartClient()
        {
            base.OnStartClient();
            _visualWheelPositionsInitialized = false;
        }

        public override void OnStopServer()
        {
            ResetDriverInput();
            ClearWheelForces();
            RestoreBaseRearFriction();

            base.OnStopServer();
        }

        internal void SetDriverInputOnServer(
            int clientId,
            float steering,
            float throttle,
            bool handbrake)
        {
            if (!IsServerInitialized)
            {
                return;
            }

            _inputClientId = clientId;

            _steeringInput =
                Mathf.Clamp(
                    steering,
                    -1f,
                    1f);

            _throttleInput =
                Mathf.Clamp(
                    throttle,
                    -1f,
                    1f);

            _handbrakeInput = handbrake;
            _lastInputTime = Time.unscaledTime;
        }

        private void FixedUpdate()
        {
            if (!IsSpawned ||
                !IsServerInitialized ||
                body == null)
            {
                return;
            }

            if (!HasAllWheelColliders())
            {
                LogMissingWheelWarning();
                return;
            }

            if (!IsCurrentDriverInputValid())
            {
                ResetDriverInput();
            }

            ApplySteering();
            ApplyHandbrakeGrip();
            ApplyDriveAndBrakes();

            ApplyAntiRoll(
                frontLeftWheel,
                frontRightWheel,
                frontAntiRollForce);

            ApplyAntiRoll(
                rearLeftWheel,
                rearRightWheel,
                rearAntiRollForce);

            ApplyDownforce();
            UpdateWheelSpin();
            CaptureAndSynchronizeWheelPoses();

            _networkSteerAngle.Value =
                _currentSteerAngle;

            _networkWheelSpinAngle.Value =
                _wheelSpinAngle;
        }

        private void LateUpdate()
        {
            if (productionRig != null &&
                HasAllWheelColliders())
            {
                UpdateProductionVisualRig();
                return;
            }

            if (!HasAllLegacyWheelVisuals())
            {
                return;
            }

            UpdateVisualSteeringAndSpin();

            ApplyLegacyWheelVisual(
                frontLeftVisual,
                _visualSteerAngle,
                _visualWheelSpinAngle);

            ApplyLegacyWheelVisual(
                frontRightVisual,
                _visualSteerAngle,
                _visualWheelSpinAngle);

            ApplyLegacyWheelVisual(
                rearLeftVisual,
                0f,
                _visualWheelSpinAngle);

            ApplyLegacyWheelVisual(
                rearRightVisual,
                0f,
                _visualWheelSpinAngle);
        }

        private void UpdateProductionVisualRig()
        {
            UpdateVisualSteeringAndSpin();

            Vector3 frontLeftTarget;
            Vector3 frontRightTarget;
            Vector3 rearLeftTarget;
            Vector3 rearRightTarget;

            if (IsServerInitialized)
            {
                frontLeftTarget =
                    GetWheelLocalPosition(
                        frontLeftWheel);

                frontRightTarget =
                    GetWheelLocalPosition(
                        frontRightWheel);

                rearLeftTarget =
                    GetWheelLocalPosition(
                        rearLeftWheel);

                rearRightTarget =
                    GetWheelLocalPosition(
                        rearRightWheel);
            }
            else
            {
                frontLeftTarget =
                    _networkFrontLeftWheelLocalPosition.Value;

                frontRightTarget =
                    _networkFrontRightWheelLocalPosition.Value;

                rearLeftTarget =
                    _networkRearLeftWheelLocalPosition.Value;

                rearRightTarget =
                    _networkRearRightWheelLocalPosition.Value;
            }

            if (!_visualWheelPositionsInitialized)
            {
                _visualFrontLeftWheelLocalPosition =
                    frontLeftTarget;

                _visualFrontRightWheelLocalPosition =
                    frontRightTarget;

                _visualRearLeftWheelLocalPosition =
                    rearLeftTarget;

                _visualRearRightWheelLocalPosition =
                    rearRightTarget;

                _visualWheelPositionsInitialized = true;
            }
            else
            {
                float blend =
                    1f -
                    Mathf.Exp(
                        -remoteWheelVisualSmoothing *
                        Time.deltaTime);

                _visualFrontLeftWheelLocalPosition =
                    Vector3.Lerp(
                        _visualFrontLeftWheelLocalPosition,
                        frontLeftTarget,
                        blend);

                _visualFrontRightWheelLocalPosition =
                    Vector3.Lerp(
                        _visualFrontRightWheelLocalPosition,
                        frontRightTarget,
                        blend);

                _visualRearLeftWheelLocalPosition =
                    Vector3.Lerp(
                        _visualRearLeftWheelLocalPosition,
                        rearLeftTarget,
                        blend);

                _visualRearRightWheelLocalPosition =
                    Vector3.Lerp(
                        _visualRearRightWheelLocalPosition,
                        rearRightTarget,
                        blend);
            }

            productionRig.ApplyVisualState(
                transform.TransformPoint(
                    _visualFrontLeftWheelLocalPosition),
                transform.TransformPoint(
                    _visualFrontRightWheelLocalPosition),
                transform.TransformPoint(
                    _visualRearLeftWheelLocalPosition),
                transform.TransformPoint(
                    _visualRearRightWheelLocalPosition),
                _visualSteerAngle,
                _visualWheelSpinAngle);
        }

        private void UpdateVisualSteeringAndSpin()
        {
            if (IsServerInitialized)
            {
                _visualSteerAngle =
                    _currentSteerAngle;

                _visualWheelSpinAngle =
                    _wheelSpinAngle;

                return;
            }

            float blend =
                1f -
                Mathf.Exp(
                    -remoteWheelVisualSmoothing *
                    Time.deltaTime);

            _visualSteerAngle =
                Mathf.LerpAngle(
                    _visualSteerAngle,
                    _networkSteerAngle.Value,
                    blend);

            _visualWheelSpinAngle =
                Mathf.LerpAngle(
                    _visualWheelSpinAngle,
                    _networkWheelSpinAngle.Value,
                    blend);
        }

        private void ApplySteering()
        {
            float absoluteSpeed =
                Mathf.Abs(
                    ForwardSpeedMetresPerSecond);

            float speedRatio =
                Mathf.Clamp01(
                    absoluteSpeed /
                    Mathf.Max(
                        0.01f,
                        maximumForwardSpeed));

            float availableSteerAngle =
                Mathf.Lerp(
                    maximumSteerAngle,
                    highSpeedSteerAngle,
                    speedRatio);

            float targetSteerAngle =
                _steeringInput *
                availableSteerAngle;

            _currentSteerAngle =
                Mathf.MoveTowards(
                    _currentSteerAngle,
                    targetSteerAngle,
                    steeringResponse *
                    Time.fixedDeltaTime);

            frontLeftWheel.steerAngle =
                _currentSteerAngle;

            frontRightWheel.steerAngle =
                _currentSteerAngle;
        }

        private void ApplyDriveAndBrakes()
        {
            float forwardSpeed =
                ForwardSpeedMetresPerSecond;

            float motorTorque = 0f;

            float frontBrakeTorque =
                coastBrakeTorque;

            float rearBrakeTorque =
                coastBrakeTorque;

            if (_handbrakeInput)
            {
                frontBrakeTorque =
                    Mathf.Max(
                        frontBrakeTorque,
                        serviceBrakeTorque * 0.12f);

                rearBrakeTorque =
                    handbrakeTorque;
            }
            else if (_throttleInput > 0.01f)
            {
                if (forwardSpeed < -0.5f)
                {
                    frontBrakeTorque =
                        serviceBrakeTorque *
                        _throttleInput;

                    rearBrakeTorque =
                        frontBrakeTorque;
                }
                else if (
                    forwardSpeed <
                    maximumForwardSpeed)
                {
                    motorTorque =
                        forwardMotorTorque *
                        _throttleInput;

                    frontBrakeTorque = 0f;
                    rearBrakeTorque = 0f;
                }
            }
            else if (_throttleInput < -0.01f)
            {
                float reverseAmount =
                    -_throttleInput;

                if (forwardSpeed > 0.5f)
                {
                    frontBrakeTorque =
                        serviceBrakeTorque *
                        reverseAmount;

                    rearBrakeTorque =
                        frontBrakeTorque;
                }
                else if (
                    forwardSpeed >
                    -maximumReverseSpeed)
                {
                    motorTorque =
                        -reverseMotorTorque *
                        reverseAmount;

                    frontBrakeTorque = 0f;
                    rearBrakeTorque = 0f;
                }
            }

            float torquePerWheel =
                motorTorque * 0.25f;

            frontLeftWheel.motorTorque =
                torquePerWheel;

            frontRightWheel.motorTorque =
                torquePerWheel;

            rearLeftWheel.motorTorque =
                torquePerWheel;

            rearRightWheel.motorTorque =
                torquePerWheel;

            frontLeftWheel.brakeTorque =
                frontBrakeTorque;

            frontRightWheel.brakeTorque =
                frontBrakeTorque;

            rearLeftWheel.brakeTorque =
                rearBrakeTorque;

            rearRightWheel.brakeTorque =
                rearBrakeTorque;
        }

        private void ApplyHandbrakeGrip()
        {
            CacheBaseRearFriction();

            if (!_baseRearFrictionCached)
            {
                return;
            }

            float targetSidewaysMultiplier =
                _handbrakeInput
                    ? handbrakeRearSidewaysGripMultiplier
                    : 1f;

            float targetForwardMultiplier =
                _handbrakeInput
                    ? handbrakeRearForwardGripMultiplier
                    : 1f;

            ApplyWheelGrip(
                rearLeftWheel,
                _rearLeftBaseForwardFriction,
                _rearLeftBaseSidewaysFriction,
                targetForwardMultiplier,
                targetSidewaysMultiplier);

            ApplyWheelGrip(
                rearRightWheel,
                _rearRightBaseForwardFriction,
                _rearRightBaseSidewaysFriction,
                targetForwardMultiplier,
                targetSidewaysMultiplier);
        }

        private void ApplyWheelGrip(
            WheelCollider wheel,
            WheelFrictionCurve baseForward,
            WheelFrictionCurve baseSideways,
            float forwardMultiplier,
            float sidewaysMultiplier)
        {
            float maximumChange =
                gripRecoverySpeed *
                Time.fixedDeltaTime;

            WheelFrictionCurve forward =
                wheel.forwardFriction;

            forward.stiffness =
                Mathf.MoveTowards(
                    forward.stiffness,
                    baseForward.stiffness *
                    forwardMultiplier,
                    maximumChange);

            wheel.forwardFriction =
                forward;

            WheelFrictionCurve sideways =
                wheel.sidewaysFriction;

            sideways.stiffness =
                Mathf.MoveTowards(
                    sideways.stiffness,
                    baseSideways.stiffness *
                    sidewaysMultiplier,
                    maximumChange);

            wheel.sidewaysFriction =
                sideways;
        }

        private void ApplyAntiRoll(
            WheelCollider leftWheel,
            WheelCollider rightWheel,
            float antiRollForce)
        {
            if (antiRollForce <= 0f)
            {
                return;
            }

            float leftTravel = 1f;
            float rightTravel = 1f;

            bool leftGrounded =
                leftWheel.GetGroundHit(
                    out WheelHit leftHit);

            if (leftGrounded)
            {
                leftTravel =
                    CalculateSuspensionTravel(
                        leftWheel,
                        leftHit);
            }

            bool rightGrounded =
                rightWheel.GetGroundHit(
                    out WheelHit rightHit);

            if (rightGrounded)
            {
                rightTravel =
                    CalculateSuspensionTravel(
                        rightWheel,
                        rightHit);
            }

            float force =
                (leftTravel - rightTravel) *
                antiRollForce;

            if (leftGrounded)
            {
                body.AddForceAtPosition(
                    leftWheel.transform.up *
                    -force,
                    leftWheel.transform.position,
                    ForceMode.Force);
            }

            if (rightGrounded)
            {
                body.AddForceAtPosition(
                    rightWheel.transform.up *
                    force,
                    rightWheel.transform.position,
                    ForceMode.Force);
            }
        }

        private static float CalculateSuspensionTravel(
            WheelCollider wheel,
            WheelHit hit)
        {
            float suspensionDistance =
                Mathf.Max(
                    0.001f,
                    wheel.suspensionDistance);

            float localHitY =
                wheel.transform
                    .InverseTransformPoint(
                        hit.point).y;

            float travel =
                (-localHitY -
                 wheel.radius) /
                suspensionDistance;

            return Mathf.Clamp01(travel);
        }

        private void ApplyDownforce()
        {
            float speed =
                body.linearVelocity.magnitude;

            if (speed <= 0.01f ||
                downforcePerMetrePerSecond <= 0f)
            {
                return;
            }

            body.AddForce(
                -transform.up *
                speed *
                downforcePerMetrePerSecond,
                ForceMode.Force);
        }

        private void UpdateWheelSpin()
        {
            float wheelRadius =
                Mathf.Max(
                    0.01f,
                    rearLeftWheel.radius);

            float radiansPerSecond =
                ForwardSpeedMetresPerSecond /
                wheelRadius;

            float degreesThisStep =
                radiansPerSecond *
                Mathf.Rad2Deg *
                Time.fixedDeltaTime;

            _wheelSpinAngle =
                Mathf.Repeat(
                    _wheelSpinAngle +
                    degreesThisStep,
                    360f);
        }

        private void CaptureAndSynchronizeWheelPoses()
        {
            _networkFrontLeftWheelLocalPosition.Value =
                GetWheelLocalPosition(
                    frontLeftWheel);

            _networkFrontRightWheelLocalPosition.Value =
                GetWheelLocalPosition(
                    frontRightWheel);

            _networkRearLeftWheelLocalPosition.Value =
                GetWheelLocalPosition(
                    rearLeftWheel);

            _networkRearRightWheelLocalPosition.Value =
                GetWheelLocalPosition(
                    rearRightWheel);
        }

        private Vector3 GetWheelLocalPosition(
            WheelCollider wheel)
        {
            wheel.GetWorldPose(
                out Vector3 worldPosition,
                out _);

            return transform.InverseTransformPoint(
                worldPosition);
        }

        private bool IsCurrentDriverInputValid()
        {
            if (_inputClientId ==
                    FishNetVehicle.EmptySeatClientId ||
                Time.unscaledTime -
                    _lastInputTime >
                    driverInputTimeout)
            {
                return false;
            }

            for (int seatIndex = 0;
                 seatIndex < vehicle.SeatCount;
                 seatIndex++)
            {
                if (!vehicle.TryGetSeat(
                        seatIndex,
                        out FishNetVehicleSeatDefinition seat) ||
                    seat.Role !=
                        FishNetVehicleSeatRole.Driver)
                {
                    continue;
                }

                return vehicle.GetOccupantClientId(
                           seatIndex) ==
                       _inputClientId;
            }

            return false;
        }

        private void ResetDriverInput()
        {
            _inputClientId =
                FishNetVehicle.EmptySeatClientId;

            _steeringInput = 0f;
            _throttleInput = 0f;
            _handbrakeInput = false;
            _lastInputTime =
                float.NegativeInfinity;
        }

        private void ClearWheelForces()
        {
            if (!HasAllWheelColliders())
            {
                return;
            }

            WheelCollider[] wheels =
            {
                frontLeftWheel,
                frontRightWheel,
                rearLeftWheel,
                rearRightWheel
            };

            foreach (WheelCollider wheel in wheels)
            {
                wheel.motorTorque = 0f;
                wheel.brakeTorque = 0f;
                wheel.steerAngle = 0f;
            }
        }

        private void CacheBaseRearFriction()
        {
            if (_baseRearFrictionCached ||
                rearLeftWheel == null ||
                rearRightWheel == null)
            {
                return;
            }

            _rearLeftBaseForwardFriction =
                rearLeftWheel.forwardFriction;

            _rearRightBaseForwardFriction =
                rearRightWheel.forwardFriction;

            _rearLeftBaseSidewaysFriction =
                rearLeftWheel.sidewaysFriction;

            _rearRightBaseSidewaysFriction =
                rearRightWheel.sidewaysFriction;

            _baseRearFrictionCached = true;
        }

        private void RestoreBaseRearFriction()
        {
            if (!_baseRearFrictionCached ||
                rearLeftWheel == null ||
                rearRightWheel == null)
            {
                return;
            }

            rearLeftWheel.forwardFriction =
                _rearLeftBaseForwardFriction;

            rearRightWheel.forwardFriction =
                _rearRightBaseForwardFriction;

            rearLeftWheel.sidewaysFriction =
                _rearLeftBaseSidewaysFriction;

            rearRightWheel.sidewaysFriction =
                _rearRightBaseSidewaysFriction;
        }

        private bool HasAllWheelColliders()
        {
            return frontLeftWheel != null &&
                   frontRightWheel != null &&
                   rearLeftWheel != null &&
                   rearRightWheel != null;
        }

        private bool HasAllLegacyWheelVisuals()
        {
            return frontLeftVisual != null &&
                   frontRightVisual != null &&
                   rearLeftVisual != null &&
                   rearRightVisual != null;
        }

        private void ResolveReferences()
        {
            if (vehicle == null)
            {
                vehicle =
                    GetComponent<FishNetVehicle>();
            }

            if (body == null)
            {
                body =
                    GetComponent<Rigidbody>();
            }

            if (productionRig == null)
            {
                productionRig =
                    GetComponent<
                        FishNetProductionTruckRig>();
            }
        }

        private void LogMissingWheelWarning()
        {
            if (_missingWheelWarningLogged)
            {
                return;
            }

            Debug.LogError(
                $"{nameof(FishNetTruckMotor)} on '{name}' is missing " +
                "one or more WheelCollider references. Run the production " +
                "truck implementor again.",
                this);

            _missingWheelWarningLogged = true;
        }

        private static void ApplyLegacyWheelVisual(
            Transform visualRoot,
            float steerAngle,
            float spinAngle)
        {
            Quaternion steering =
                Quaternion.AngleAxis(
                    steerAngle,
                    Vector3.up);

            Quaternion rolling =
                Quaternion.AngleAxis(
                    spinAngle,
                    Vector3.right);

            visualRoot.localRotation =
                steering * rolling;
        }

        protected override void Reset()
        {
            base.Reset();
            ResolveReferences();
        }

        protected override void OnValidate()
        {
            base.OnValidate();

            maximumForwardSpeed =
                Mathf.Max(
                    1f,
                    maximumForwardSpeed);

            maximumReverseSpeed =
                Mathf.Max(
                    1f,
                    maximumReverseSpeed);

            frontAntiRollForce =
                Mathf.Max(
                    0f,
                    frontAntiRollForce);

            rearAntiRollForce =
                Mathf.Max(
                    0f,
                    rearAntiRollForce);

            gripRecoverySpeed =
                Mathf.Max(
                    0.1f,
                    gripRecoverySpeed);

            driverInputTimeout =
                Mathf.Max(
                    0.05f,
                    driverInputTimeout);

            remoteWheelVisualSmoothing =
                Mathf.Max(
                    0f,
                    remoteWheelVisualSmoothing);

            ResolveReferences();
        }
    }
}
