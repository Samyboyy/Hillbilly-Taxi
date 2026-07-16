using Unity.Netcode;
using UnityEngine;

namespace HillbillyTaxi.Vehicles
{
    /// <summary>
    /// Server-authoritative four-wheel pickup controller.
    ///
    /// The owning driver's PlayerObject sends input to the server. Only the server
    /// applies WheelCollider torque and steering. NetworkTransform and
    /// NetworkRigidbody synchronize the resulting Rigidbody motion.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkVehicle))]
    [RequireComponent(typeof(Rigidbody))]
    public sealed class NetworkTruckMotor : NetworkBehaviour
    {
        [Header("References")]
        [SerializeField] private NetworkVehicle vehicle;
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
        [SerializeField, Min(1f)] private float maximumForwardSpeed = 20f;
        [SerializeField, Min(1f)] private float maximumReverseSpeed = 8f;
        [SerializeField, Min(0f)] private float forwardMotorTorque = 1100f;
        [SerializeField, Min(0f)] private float reverseMotorTorque = 750f;

        [Header("Steering")]
        [SerializeField, Range(1f, 60f)] private float maximumSteerAngle = 32f;
        [SerializeField, Range(1f, 45f)] private float highSpeedSteerAngle = 12f;
        [SerializeField, Min(0f)] private float steeringResponse = 90f;

        [Header("Braking")]
        [SerializeField, Min(0f)] private float serviceBrakeTorque = 3600f;
        [SerializeField, Min(0f)] private float handbrakeTorque = 6000f;
        [SerializeField, Min(0f)] private float coastBrakeTorque = 80f;

        [Header("Stability")]
        [SerializeField, Min(0f)] private float downforcePerMetrePerSecond = 55f;

        [Header("Networking")]
        [SerializeField, Min(0.05f)] private float driverInputTimeout = 0.35f;

        private readonly NetworkVariable<float> _networkSteerAngle =
            new NetworkVariable<float>(
                0f,
                NetworkVariableReadPermission.Everyone,
                NetworkVariableWritePermission.Server);

        private ulong _inputClientId = NetworkVehicle.EmptySeatClientId;
        private float _steeringInput;
        private float _throttleInput;
        private bool _handbrakeInput;
        private float _lastInputTime;

        private float _currentSteerAngle;
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

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            ResolveReferences();
            ResetDriverInput();
        }

        public override void OnNetworkDespawn()
        {
            ResetDriverInput();
            base.OnNetworkDespawn();
        }

        internal void SetDriverInputOnServer(
            ulong clientId,
            float steering,
            float throttle,
            bool handbrake)
        {
            if (!IsServer)
            {
                return;
            }

            _inputClientId = clientId;
            _steeringInput = Mathf.Clamp(steering, -1f, 1f);
            _throttleInput = Mathf.Clamp(throttle, -1f, 1f);
            _handbrakeInput = handbrake;
            _lastInputTime = Time.unscaledTime;
        }

        private void FixedUpdate()
        {
            if (!IsSpawned ||
                !IsServer ||
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
        }

        private void LateUpdate()
        {
            if (!HasAllWheelColliders())
            {
                return;
            }

            float presentationSteer =
                IsServer
                    ? _currentSteerAngle
                    : _networkSteerAngle.Value;

            frontLeftWheel.steerAngle = presentationSteer;
            frontRightWheel.steerAngle = presentationSteer;

            UpdateWheelVisual(
                frontLeftWheel,
                frontLeftVisual);

            UpdateWheelVisual(
                frontRightWheel,
                frontRightVisual);

            UpdateWheelVisual(
                rearLeftWheel,
                rearLeftVisual);

            UpdateWheelVisual(
                rearRightWheel,
                rearRightVisual);
        }

        private void ApplySteering()
        {
            float absoluteSpeed =
                Mathf.Abs(ForwardSpeedMetresPerSecond);

            float speedRatio =
                Mathf.Clamp01(
                    absoluteSpeed /
                    Mathf.Max(0.01f, maximumForwardSpeed));

            float availableSteerAngle =
                Mathf.Lerp(
                    maximumSteerAngle,
                    highSpeedSteerAngle,
                    speedRatio);

            float targetSteerAngle =
                _steeringInput * availableSteerAngle;

            _currentSteerAngle =
                Mathf.MoveTowards(
                    _currentSteerAngle,
                    targetSteerAngle,
                    steeringResponse * Time.fixedDeltaTime);

            frontLeftWheel.steerAngle = _currentSteerAngle;
            frontRightWheel.steerAngle = _currentSteerAngle;
            _networkSteerAngle.Value = _currentSteerAngle;
        }

        private void ApplyDriveAndBrakes()
        {
            float forwardSpeed =
                ForwardSpeedMetresPerSecond;

            float motorTorque = 0f;
            float frontBrakeTorque = coastBrakeTorque;
            float rearBrakeTorque = coastBrakeTorque;

            if (_handbrakeInput)
            {
                frontBrakeTorque =
                    Mathf.Max(
                        frontBrakeTorque,
                        serviceBrakeTorque * 0.35f);

                rearBrakeTorque = handbrakeTorque;
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
                else if (forwardSpeed < maximumForwardSpeed)
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
                else if (forwardSpeed > -maximumReverseSpeed)
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

            frontLeftWheel.motorTorque = torquePerWheel;
            frontRightWheel.motorTorque = torquePerWheel;
            rearLeftWheel.motorTorque = torquePerWheel;
            rearRightWheel.motorTorque = torquePerWheel;

            frontLeftWheel.brakeTorque = frontBrakeTorque;
            frontRightWheel.brakeTorque = frontBrakeTorque;
            rearLeftWheel.brakeTorque = rearBrakeTorque;
            rearRightWheel.brakeTorque = rearBrakeTorque;
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

        private bool IsCurrentDriverInputValid()
        {
            if (_inputClientId ==
                    NetworkVehicle.EmptySeatClientId ||
                Time.unscaledTime - _lastInputTime >
                    driverInputTimeout)
            {
                return false;
            }

            for (int index = 0; index < vehicle.SeatCount; index++)
            {
                if (!vehicle.TryGetSeat(
                        index,
                        out VehicleSeatDefinition seat) ||
                    seat.Role != VehicleSeatRole.Driver)
                {
                    continue;
                }

                return vehicle.GetOccupantClientId(index) ==
                       _inputClientId;
            }

            return false;
        }

        private void ResetDriverInput()
        {
            _inputClientId =
                NetworkVehicle.EmptySeatClientId;

            _steeringInput = 0f;
            _throttleInput = 0f;
            _handbrakeInput = false;
            _lastInputTime = float.NegativeInfinity;
        }

        private bool HasAllWheelColliders()
        {
            return frontLeftWheel != null &&
                   frontRightWheel != null &&
                   rearLeftWheel != null &&
                   rearRightWheel != null;
        }

        private void ResolveReferences()
        {
            if (vehicle == null)
            {
                vehicle = GetComponent<NetworkVehicle>();
            }

            if (body == null)
            {
                body = GetComponent<Rigidbody>();
            }
        }

        private void LogMissingWheelWarning()
        {
            if (_missingWheelWarningLogged)
            {
                return;
            }

            Debug.LogError(
                $"{nameof(NetworkTruckMotor)} on '{name}' is missing one or more " +
                "WheelCollider references. Run the vehicle driving installer again.",
                this);

            _missingWheelWarningLogged = true;
        }

        private static void UpdateWheelVisual(
            WheelCollider wheel,
            Transform visualRoot)
        {
            if (wheel == null ||
                visualRoot == null)
            {
                return;
            }

            wheel.GetWorldPose(
                out Vector3 position,
                out Quaternion rotation);

            visualRoot.SetPositionAndRotation(
                position,
                rotation);
        }

        private void Reset()
        {
            ResolveReferences();
        }

        private void OnValidate()
        {
            maximumForwardSpeed =
                Mathf.Max(1f, maximumForwardSpeed);

            maximumReverseSpeed =
                Mathf.Max(1f, maximumReverseSpeed);

            driverInputTimeout =
                Mathf.Max(0.05f, driverInputTimeout);

            ResolveReferences();
        }
    }
}
