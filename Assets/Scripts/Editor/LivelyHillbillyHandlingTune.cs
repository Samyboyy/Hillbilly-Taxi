#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using HillbillyTaxi.FishNetMigration.Vehicles;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HillbillyTaxi.EditorTools
{
    /// <summary>
    /// Applies a livelier production-truck tune without changing the working
    /// network, seat, camera or visual-suspension architecture.
    /// </summary>
    public static class LivelyHillbillyHandlingTune
    {
        private const string VehicleRootName =
            "FishNet Test Pickup Seat Rig";

        private const string ReportFolder =
            "Assets/TruckValidationReports";

        [MenuItem(
            "Hillbilly Taxi/Truck/Handling/" +
            "Apply Lively Hillbilly Tune")]
        public static void ApplyLivelyTune()
        {
            ApplyPreset(
                lively: true);
        }

        [MenuItem(
            "Hillbilly Taxi/Truck/Handling/" +
            "Restore Stable Production Tune")]
        public static void RestoreStableTune()
        {
            ApplyPreset(
                lively: false);
        }

        private static void ApplyPreset(
            bool lively)
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogWarning(
                    "Exit Play Mode before changing truck handling.");

                return;
            }

            GameObject vehicleRoot =
                GameObject.Find(
                    VehicleRootName);

            if (vehicleRoot == null ||
                !vehicleRoot.TryGetComponent(
                    out Rigidbody body) ||
                !vehicleRoot.TryGetComponent(
                    out FishNetTruckMotor motor))
            {
                Debug.LogError(
                    $"Could not find '{VehicleRootName}' with its " +
                    "Rigidbody and FishNetTruckMotor.");

                return;
            }

            WheelSet wheels =
                ResolveWheels(
                    motor,
                    vehicleRoot);

            if (!wheels.IsComplete)
            {
                Debug.LogError(
                    "Could not resolve all four WheelColliders from " +
                    "FishNetTruckMotor or the vehicle hierarchy.");

                return;
            }

            Undo.RecordObject(
                body,
                lively
                    ? "Apply Lively Rigidbody Tune"
                    : "Restore Stable Rigidbody Tune");

            ConfigureRigidbody(
                body,
                lively);

            SerializedObject serializedMotor =
                new SerializedObject(motor);

            ConfigureMotor(
                serializedMotor,
                lively);

            serializedMotor
                .ApplyModifiedPropertiesWithoutUndo();

            ConfigureWheel(
                wheels.FrontLeft,
                isFront: true,
                lively);

            ConfigureWheel(
                wheels.FrontRight,
                isFront: true,
                lively);

            ConfigureWheel(
                wheels.RearLeft,
                isFront: false,
                lively);

            ConfigureWheel(
                wheels.RearRight,
                isFront: false,
                lively);

            body.linearVelocity =
                Vector3.zero;

            body.angularVelocity =
                Vector3.zero;

            Physics.SyncTransforms();

            EditorUtility.SetDirty(
                vehicleRoot);

            EditorUtility.SetDirty(
                body);

            EditorUtility.SetDirty(
                motor);

            EditorUtility.SetDirty(
                wheels.FrontLeft);

            EditorUtility.SetDirty(
                wheels.FrontRight);

            EditorUtility.SetDirty(
                wheels.RearLeft);

            EditorUtility.SetDirty(
                wheels.RearRight);

            Scene scene =
                SceneManager.GetActiveScene();

            if (scene.IsValid())
            {
                EditorSceneManager.MarkSceneDirty(
                    scene);
            }

            string report =
                BuildReport(
                    lively,
                    body,
                    serializedMotor,
                    wheels);

            string reportPath =
                SaveReport(
                    report,
                    lively);

            EditorGUIUtility.systemCopyBuffer =
                report;

            Selection.activeGameObject =
                vehicleRoot;

            Debug.Log(
                report +
                "\n\nSaved report: " +
                reportPath +
                "\nSave FishNetProof.unity before Play Mode.",
                vehicleRoot);
        }

        private static void ConfigureRigidbody(
            Rigidbody body,
            bool lively)
        {
            body.mass = 2450f;
            body.linearDamping =
                lively
                    ? 0.03f
                    : 0.04f;

            body.angularDamping =
                lively
                    ? 0.28f
                    : 0.40f;

            body.centerOfMass =
                lively
                    ? new Vector3(
                        0f,
                        0.50f,
                        0.05f)
                    : new Vector3(
                        0f,
                        0.42f,
                        0.05f);

            body.maxAngularVelocity =
                lively
                    ? 12f
                    : 10f;

            body.useGravity = true;
            body.isKinematic = false;

            body.interpolation =
                RigidbodyInterpolation.Interpolate;

            body.collisionDetectionMode =
                CollisionDetectionMode
                    .ContinuousDynamic;
        }

        private static void ConfigureMotor(
            SerializedObject motor,
            bool lively)
        {
            SetFloat(
                motor,
                "maximumForwardSpeed",
                lively
                    ? 36f
                    : 30f);

            SetFloat(
                motor,
                "maximumReverseSpeed",
                lively
                    ? 12f
                    : 11f);

            SetFloat(
                motor,
                "forwardMotorTorque",
                lively
                    ? 3900f
                    : 3000f);

            SetFloat(
                motor,
                "reverseMotorTorque",
                lively
                    ? 1500f
                    : 1350f);

            SetFloat(
                motor,
                "maximumSteerAngle",
                lively
                    ? 35f
                    : 34f);

            SetFloat(
                motor,
                "highSpeedSteerAngle",
                lively
                    ? 9f
                    : 10f);

            SetFloat(
                motor,
                "serviceBrakeTorque",
                lively
                    ? 6600f
                    : 6000f);

            SetFloat(
                motor,
                "handbrakeTorque",
                lively
                    ? 9800f
                    : 9000f);

            SetFloat(
                motor,
                "frontAntiRollForce",
                lively
                    ? 5000f
                    : 6500f);

            SetFloat(
                motor,
                "rearAntiRollForce",
                lively
                    ? 3300f
                    : 4800f);

            SetFloat(
                motor,
                "downforcePerMetrePerSecond",
                lively
                    ? 6f
                    : 10f);

            SetFloat(
                motor,
                "handbrakeRearSidewaysGripMultiplier",
                lively
                    ? 0.48f
                    : 0.55f);

            SetFloat(
                motor,
                "handbrakeRearForwardGripMultiplier",
                lively
                    ? 0.62f
                    : 0.68f);

            SetFloat(
                motor,
                "gripRecoverySpeed",
                lively
                    ? 4.0f
                    : 4.5f);
        }

        private static void ConfigureWheel(
            WheelCollider wheel,
            bool isFront,
            bool lively)
        {
            Undo.RecordObject(
                wheel,
                lively
                    ? "Apply Lively Suspension Tune"
                    : "Restore Stable Suspension Tune");

            wheel.mass = 48f;
            wheel.radius = 0.556f;

            wheel.suspensionDistance =
                lively
                    ? 0.36f
                    : 0.32f;

            wheel.wheelDampingRate =
                lively
                    ? 0.85f
                    : 1.10f;

            wheel.forceAppPointDistance =
                lively
                    ? 0.10f
                    : 0.12f;

            JointSpring spring =
                wheel.suspensionSpring;

            if (lively)
            {
                spring.spring =
                    isFront
                        ? 37000f
                        : 34000f;

                spring.damper =
                    isFront
                        ? 5000f
                        : 4500f;

                spring.targetPosition =
                    0.48f;
            }
            else
            {
                spring.spring =
                    isFront
                        ? 40000f
                        : 37000f;

                spring.damper =
                    isFront
                        ? 6800f
                        : 6300f;

                spring.targetPosition =
                    0.50f;
            }

            wheel.suspensionSpring =
                spring;

            WheelFrictionCurve forward =
                wheel.forwardFriction;

            forward.stiffness =
                lively
                    ? (isFront ? 1.30f : 1.24f)
                    : (isFront ? 1.32f : 1.27f);

            wheel.forwardFriction =
                forward;

            WheelFrictionCurve sideways =
                wheel.sidewaysFriction;

            sideways.stiffness =
                lively
                    ? (isFront ? 1.37f : 1.24f)
                    : (isFront ? 1.42f : 1.30f);

            wheel.sidewaysFriction =
                sideways;
        }

        private static WheelSet ResolveWheels(
            FishNetTruckMotor motor,
            GameObject vehicleRoot)
        {
            SerializedObject serialized =
                new SerializedObject(motor);

            WheelSet result =
                new WheelSet
                {
                    FrontLeft =
                        GetWheel(
                            serialized,
                            "frontLeftWheel"),

                    FrontRight =
                        GetWheel(
                            serialized,
                            "frontRightWheel"),

                    RearLeft =
                        GetWheel(
                            serialized,
                            "rearLeftWheel"),

                    RearRight =
                        GetWheel(
                            serialized,
                            "rearRightWheel")
                };

            if (result.IsComplete)
            {
                return result;
            }

            WheelCollider[] candidates =
                vehicleRoot
                    .GetComponentsInChildren<
                        WheelCollider>(
                            includeInactive: true);

            foreach (WheelCollider wheel in candidates)
            {
                string lower =
                    wheel.name.ToLowerInvariant();

                if (result.FrontLeft == null &&
                    (lower.Contains("fl") ||
                     lower.Contains("front left")))
                {
                    result.FrontLeft = wheel;
                }
                else if (
                    result.FrontRight == null &&
                    (lower.Contains("fr") ||
                     lower.Contains("front right")))
                {
                    result.FrontRight = wheel;
                }
                else if (
                    result.RearLeft == null &&
                    (lower.Contains("rl") ||
                     lower.Contains("rear left")))
                {
                    result.RearLeft = wheel;
                }
                else if (
                    result.RearRight == null &&
                    (lower.Contains("rr") ||
                     lower.Contains("rear right")))
                {
                    result.RearRight = wheel;
                }
            }

            return result;
        }

        private static WheelCollider GetWheel(
            SerializedObject serialized,
            string propertyName)
        {
            SerializedProperty property =
                serialized.FindProperty(
                    propertyName);

            return property != null
                ? property.objectReferenceValue
                    as WheelCollider
                : null;
        }

        private static void SetFloat(
            SerializedObject serialized,
            string propertyName,
            float value)
        {
            SerializedProperty property =
                serialized.FindProperty(
                    propertyName);

            if (property != null)
            {
                property.floatValue =
                    value;
            }
        }

        private static string BuildReport(
            bool lively,
            Rigidbody body,
            SerializedObject motor,
            WheelSet wheels)
        {
            StringBuilder report =
                new StringBuilder(4096);

            report.AppendLine(
                "HILLBILLY TAXI — HANDLING TUNE");

            report.AppendLine(
                new string('=', 58));

            report.AppendLine(
                $"Preset: " +
                $"{(lively ? "Lively Hillbilly" : "Stable Production")}");

            report.AppendLine(
                $"Generated: " +
                $"{DateTime.Now:yyyy-MM-dd HH:mm:ss}");

            report.AppendLine();

            report.AppendLine(
                "RIGIDBODY");

            report.AppendLine(
                new string('-', 58));

            report.AppendLine(
                $"Mass: {body.mass:0} kg");

            report.AppendLine(
                $"Angular damping: " +
                $"{body.angularDamping:0.###}");

            report.AppendLine(
                $"Centre of mass: " +
                $"{body.centerOfMass}");

            report.AppendLine();

            report.AppendLine(
                "MOTOR");

            report.AppendLine(
                new string('-', 58));

            AppendMotorValue(
                report,
                motor,
                "maximumForwardSpeed",
                "Maximum speed");

            AppendMotorValue(
                report,
                motor,
                "forwardMotorTorque",
                "Forward torque");

            AppendMotorValue(
                report,
                motor,
                "frontAntiRollForce",
                "Front anti-roll");

            AppendMotorValue(
                report,
                motor,
                "rearAntiRollForce",
                "Rear anti-roll");

            AppendMotorValue(
                report,
                motor,
                "downforcePerMetrePerSecond",
                "Downforce per m/s");

            report.AppendLine();

            report.AppendLine(
                "SUSPENSION");

            report.AppendLine(
                new string('-', 58));

            AppendWheel(
                report,
                "Front Left",
                wheels.FrontLeft);

            AppendWheel(
                report,
                "Front Right",
                wheels.FrontRight);

            AppendWheel(
                report,
                "Rear Left",
                wheels.RearLeft);

            AppendWheel(
                report,
                "Rear Right",
                wheels.RearRight);

            report.AppendLine();

            if (lively)
            {
                report.AppendLine(
                    "Target feel:");

                report.AppendLine(
                    "• One clearly visible rebound after a jump landing.");

                report.AppendLine(
                    "• Suspension settles before becoming trampoline-like.");

                report.AppendLine(
                    "• More outward lean during fast direction changes.");

                report.AppendLine(
                    "• Stronger acceleration and roughly 80 mph top speed.");

                report.AppendLine(
                    "• Looser rear behaviour under the handbrake.");
            }
            else
            {
                report.AppendLine(
                    "The conservative stable production values were restored.");
            }

            return report.ToString();
        }

        private static void AppendMotorValue(
            StringBuilder report,
            SerializedObject motor,
            string propertyName,
            string label)
        {
            SerializedProperty property =
                motor.FindProperty(
                    propertyName);

            report.AppendLine(
                $"{label}: " +
                $"{(property != null ? property.floatValue : -1f):0.###}");
        }

        private static void AppendWheel(
            StringBuilder report,
            string label,
            WheelCollider wheel)
        {
            JointSpring spring =
                wheel.suspensionSpring;

            report.AppendLine(
                $"{label}: travel " +
                $"{wheel.suspensionDistance:0.###}, " +
                $"spring {spring.spring:0}, " +
                $"damper {spring.damper:0}, " +
                $"target {spring.targetPosition:0.##}");
        }

        private static string SaveReport(
            string report,
            bool lively)
        {
            EnsureAssetFolder(
                ReportFolder);

            string assetPath =
                $"{ReportFolder}/" +
                $"{(lively ? "LivelyHillbillyTune" : "StableProductionTune")}_" +
                $"{DateTime.Now:yyyyMMdd_HHmmss}.txt";

            string projectRoot =
                Directory.GetParent(
                    Application.dataPath).FullName;

            File.WriteAllText(
                Path.Combine(
                    projectRoot,
                    assetPath),
                report,
                Encoding.UTF8);

            AssetDatabase.Refresh();

            return assetPath;
        }

        private static void EnsureAssetFolder(
            string fullPath)
        {
            string normalized =
                fullPath.Replace(
                    '\\',
                    '/');

            string[] parts =
                normalized.Split('/');

            string current =
                "Assets";

            for (int index = 1;
                 index < parts.Length;
                 index++)
            {
                string next =
                    $"{current}/{parts[index]}";

                if (!AssetDatabase.IsValidFolder(
                        next))
                {
                    AssetDatabase.CreateFolder(
                        current,
                        parts[index]);
                }

                current = next;
            }
        }

        private struct WheelSet
        {
            public WheelCollider FrontLeft;
            public WheelCollider FrontRight;
            public WheelCollider RearLeft;
            public WheelCollider RearRight;

            public bool IsComplete =>
                FrontLeft != null &&
                FrontRight != null &&
                RearLeft != null &&
                RearRight != null;
        }
    }
}
#endif
