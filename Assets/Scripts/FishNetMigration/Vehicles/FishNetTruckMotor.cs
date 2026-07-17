using FishNet.Component.Transforming;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;

namespace HillbillyTaxi.FishNetMigration.Vehicles
{
    /// <summary>
    /// Correctness-first server-authoritative pickup controller.
    ///
    /// Driver input arrives through the driver's owned player object. The server
    /// validates that client still occupies the Driver seat and alone applies
    /// WheelCollider physics. FishNet NetworkTransform distributes the resulting
    /// Rigidbody motion to clients.
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

        [Header("Wheel colliders")]
        [SerializeField] private WheelCollider frontLeftWheel;
        [SerializeField] private WheelCollider frontRightWheel;
        [SerializeField] private WheelCollider rearLeftWheel;
        [SerializeField] private WheelCollider rearRightWheel;

        [Header("Wheel visual pose roots")]
        [SerializeField] private Transform frontLeftVisual;
        [SerializeField] private Transform frontRightVisual;
        [SerializeField] private Transform rearLeftVisual;
        [SerializeField] private Transform rearRightVisual;

        [Header("Speed")]
        [SerializeField, Min(1f)]
        private float maximumForwardSpeed = 20f;

        [SerializeField, Min(1f)]
        private float maximumReverseSpeed = 8f;

        [SerializeField, Min(0f)]
        private float forwardMotorTorque = 1100f;

        [SerializeField, Min(0f)]
        private float reverseMotorTorque = 750f;

        [Header("Steering")]
        [SerializeField, Range(1f, 60f)]
        private float maximumSteerAngle = 32f;

        [SerializeField, Range(1f, 45f)]
        private float highSpeedSteerAngle = 12f;

        [SerializeField, Min(0f)]
        private float steeringResponse = 90f;

        [Header("Braking")]
        [SerializeField, Min(0f)]
        private float serviceBrakeTorque = 3600f;

        [SerializeField, Min(0f)]
        private float handbrakeTorque = 6000f;

        [SerializeField, Min(0f)]
        private float coastBrakeTorque = 80f;

        [Header("Stability")]
        [SerializeField, Min(0f)]
        private float downforcePerMetrePerSecond = 55f;

        [Header("Networking")]
        [SerializeField, Min(0.05f)]
        private float driverInputTimeout = 0.35f;

        [SerializeField, Min(0f)]
        private float remoteWheelVisualSmoothing = 20f;

        private readonly SyncVar<float>
            _networkSteerAngle = new();

        private readonly SyncVar<float>
            _networkWheelSpinAngle = new();

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

        private void Awake()
        {
            ResolveReferences();
        }

        public override void OnStartServer()
        {
            base.OnStartServer();

            ResetDriverInput();

            if (body != null)
            {
                body.WakeUp();
            }
        }

        public override void OnStopServer()
        {
            ResetDriverInput();
            ClearWheelForces();

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
            ApplyDriveAndBrakes();
            ApplyDownforce();
            UpdateWheelSpin();

            _networkSteerAngle.Value =
                _currentSteerAngle;

            _networkWheelSpinAngle.Value =
                _wheelSpinAngle;
        }

        private void LateUpdate()
        {
            if (!HasAllWheelVisuals())
            {
                return;
            }

            if (IsServerInitialized)
            {
                _visualSteerAngle =
                    _currentSteerAngle;

                _visualWheelSpinAngle =
                    _wheelSpinAngle;
            }
            else
            {
                float smoothing =
                    remoteWheelVisualSmoothing *
                    Time.deltaTime;

                _visualSteerAngle =
                    Mathf.LerpAngle(
                        _visualSteerAngle,
                        _networkSteerAngle.Value,
                        smoothing);

                _visualWheelSpinAngle =
                    Mathf.LerpAngle(
                        _visualWheelSpinAngle,
                        _networkWheelSpinAngle.Value,
                        smoothing);
            }

            ApplyWheelVisual(
                frontLeftVisual,
                _visualSteerAngle,
                _visualWheelSpinAngle);

            ApplyWheelVisual(
                frontRightVisual,
                _visualSteerAngle,
                _visualWheelSpinAngle);

            ApplyWheelVisual(
                rearLeftVisual,
                0f,
                _visualWheelSpinAngle);

            ApplyWheelVisual(
                rearRightVisual,
                0f,
                _visualWheelSpinAngle);
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
                        serviceBrakeTorque * 0.35f);

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

        private void ApplyDownforce()
        {
            float speed =
                body.linearVelocity.magnitude;

            if (speed <= 0.01f)
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

        private bool HasAllWheelColliders()
        {
            return frontLeftWheel != null &&
                   frontRightWheel != null &&
                   rearLeftWheel != null &&
                   rearRightWheel != null;
        }

        private bool HasAllWheelVisuals()
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
        }

        private void LogMissingWheelWarning()
        {
            if (_missingWheelWarningLogged)
            {
                return;
            }

            Debug.LogError(
                $"{nameof(FishNetTruckMotor)} on '{name}' is missing " +
                "one or more WheelCollider references. Run the Phase 4 " +
                "truck installer again.",
                this);

            _missingWheelWarningLogged = true;
        }

        private static void ApplyWheelVisual(
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
