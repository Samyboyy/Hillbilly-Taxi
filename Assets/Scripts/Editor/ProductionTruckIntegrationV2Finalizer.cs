#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using HillbillyTaxi.FishNetMigration.Vehicles;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HillbillyTaxi.EditorTools
{
    /// <summary>
    /// Consolidates the production truck that has already passed Play Mode.
    ///
    /// This intentionally does not regenerate wheel physics or reimport the
    /// truck. It validates and records the working hierarchy, applies the
    /// approved lively tune and creates a stable configuration component for
    /// subsequent gameplay systems.
    /// </summary>
    public static class ProductionTruckIntegrationV2Finalizer
    {
        private const string VehicleRootName =
            "FishNet Test Pickup Seat Rig";

        private const string IntegrationRootName =
            "Production Truck Integration";

        private const string FinalPhysicsRootName =
            "Production Truck Physics";

        private const string FinalColliderRootName =
            "Production Truck Body Colliders";

        private const string FinalCameraRootName =
            "Production Truck Seat Cameras";

        private const string StablePhysicsRootName =
            "Production WheelColliders - Stable";

        private const string StableColliderRootName =
            "Production Body Colliders - Stable";

        private const string SafeCameraRootName =
            "Production Seat Cameras - Safe";

        private const string ReportFolder =
            "Assets/TruckValidationReports";

        private const string PrefabFolder =
            "Assets/Vehicles/Truck/Integrated";

        [MenuItem(
            "Hillbilly Taxi/Truck/Production Integration V2/" +
            "Finalize Current Working Truck")]
        public static void FinalizeCurrentWorkingTruck()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogWarning(
                    "Exit Play Mode before finalizing the production truck.");

                return;
            }

            GameObject vehicleRoot =
                GameObject.Find(VehicleRootName);

            if (vehicleRoot == null ||
                !vehicleRoot.TryGetComponent(
                    out Rigidbody body) ||
                !vehicleRoot.TryGetComponent(
                    out FishNetTruckMotor motor) ||
                !vehicleRoot.TryGetComponent(
                    out FishNetVehicle vehicle) ||
                !vehicleRoot.TryGetComponent(
                    out FishNetProductionTruckRig visualRig))
            {
                Debug.LogError(
                    $"Could not find '{VehicleRootName}' with Rigidbody, " +
                    "FishNetTruckMotor, FishNetVehicle and " +
                    "FishNetProductionTruckRig.");

                return;
            }

            Transform integrationRoot =
                FindDeepChild(
                    vehicleRoot.transform,
                    IntegrationRootName);

            if (integrationRoot == null)
            {
                Debug.LogError(
                    "Production Truck Integration was not found. " +
                    "This finalizer is for the current working truck scene.");

                return;
            }

            WheelSet wheels =
                ResolveMotorWheels(
                    motor);

            if (!wheels.IsComplete)
            {
                Debug.LogError(
                    "The four working WheelCollider references could not be " +
                    "resolved from FishNetTruckMotor.");

                return;
            }

            WheelCollider[] allWheels =
                vehicleRoot.GetComponentsInChildren<
                    WheelCollider>(
                    includeInactive: true);

            if (allWheels.Length != 4)
            {
                Debug.LogError(
                    $"Expected exactly four WheelColliders but found " +
                    $"{allWheels.Length}. Finalization stopped rather than " +
                    "deleting physics automatically.");

                return;
            }

            if (!wheels.AllUseBody(body))
            {
                Debug.LogError(
                    "At least one WheelCollider is not attached to the " +
                    "authoritative truck Rigidbody. Finalization stopped.");

                return;
            }

            Transform physicsRoot =
                ResolveAndRenameRoot(
                    vehicleRoot.transform,
                    StablePhysicsRootName,
                    FinalPhysicsRootName,
                    wheels.FrontLeft.transform.parent);

            Transform colliderRoot =
                ResolveAndRenameRoot(
                    vehicleRoot.transform,
                    StableColliderRootName,
                    FinalColliderRootName,
                    FindColliderRoot(
                        vehicleRoot.transform));

            Transform cameraRoot =
                ResolveAndRenameRoot(
                    vehicleRoot.transform,
                    SafeCameraRootName,
                    FinalCameraRootName,
                    FindCameraRoot(
                        vehicleRoot.transform));

            if (colliderRoot == null)
            {
                Debug.LogError(
                    "The stable production body-collider root was not found.");

                return;
            }

            Collider[] physicalColliders =
                colliderRoot.GetComponentsInChildren<
                    Collider>(
                    includeInactive: true)
                    .Where(
                        collider =>
                            !(collider is WheelCollider) &&
                            !collider.isTrigger &&
                            collider.enabled)
                    .ToArray();

            if (physicalColliders.Length < 3)
            {
                Debug.LogError(
                    "Expected at least three enabled production body " +
                    "colliders. Finalization stopped.");

                return;
            }

            if (cameraRoot == null)
            {
                cameraRoot =
                    BuildSafeCameraRoot(
                        vehicleRoot.transform,
                        vehicle);
            }

            ApplyApprovedLivelyTune(
                body,
                motor,
                wheels);

            FishNetProductionTruckConfiguration configuration =
                vehicleRoot.GetComponent<
                    FishNetProductionTruckConfiguration>();

            if (configuration == null)
            {
                configuration =
                    Undo.AddComponent<
                        FishNetProductionTruckConfiguration>(
                            vehicleRoot);
            }

            Dictionary<string, Transform> imported =
                BuildUniqueNameLookup(
                    integrationRoot);

            ConfigureReferenceComponent(
                configuration,
                integrationRoot,
                physicsRoot,
                colliderRoot,
                cameraRoot,
                body,
                wheels,
                imported);

            DisableObsoleteGreyboxPhysics(
                vehicleRoot.transform);

            EditorUtility.SetDirty(vehicleRoot);
            EditorUtility.SetDirty(body);
            EditorUtility.SetDirty(motor);
            EditorUtility.SetDirty(vehicle);
            EditorUtility.SetDirty(visualRig);
            EditorUtility.SetDirty(configuration);

            Scene scene =
                SceneManager.GetActiveScene();

            if (scene.IsValid())
            {
                EditorSceneManager.MarkSceneDirty(scene);
            }

            string report =
                BuildReport(
                    vehicleRoot,
                    body,
                    motor,
                    configuration,
                    wheels,
                    physicalColliders,
                    cameraRoot);

            string reportPath =
                SaveReport(
                    report,
                    "ProductionTruckIntegrationV2");

            EditorGUIUtility.systemCopyBuffer =
                report;

            Selection.activeGameObject =
                vehicleRoot;

            Debug.Log(
                report +
                "\n\nSaved report: " +
                reportPath +
                "\nSave FishNetProof.unity and run the final local test.",
                vehicleRoot);
        }

        [MenuItem(
            "Hillbilly Taxi/Truck/Production Integration V2/" +
            "Validate Final Integration")]
        public static void ValidateFinalIntegration()
        {
            GameObject vehicleRoot =
                GameObject.Find(VehicleRootName);

            if (vehicleRoot == null ||
                !vehicleRoot.TryGetComponent(
                    out Rigidbody body) ||
                !vehicleRoot.TryGetComponent(
                    out FishNetTruckMotor motor) ||
                !vehicleRoot.TryGetComponent(
                    out FishNetProductionTruckConfiguration configuration))
            {
                Debug.LogError(
                    "A finalized production truck could not be found.");

                return;
            }

            WheelSet wheels =
                ResolveMotorWheels(motor);

            Collider[] colliders =
                configuration.ProductionBodyColliderRoot != null
                    ? configuration.ProductionBodyColliderRoot
                        .GetComponentsInChildren<Collider>(true)
                        .Where(
                            collider =>
                                !(collider is WheelCollider) &&
                                !collider.isTrigger &&
                                collider.enabled)
                        .ToArray()
                    : Array.Empty<Collider>();

            string report =
                BuildReport(
                    vehicleRoot,
                    body,
                    motor,
                    configuration,
                    wheels,
                    colliders,
                    configuration.ProductionSeatCameraRoot);

            string reportPath =
                SaveReport(
                    report,
                    "ProductionTruckIntegrationV2Validation");

            EditorGUIUtility.systemCopyBuffer =
                report;

            Debug.Log(
                report +
                "\n\nSaved report: " +
                reportPath,
                vehicleRoot);
        }

        [MenuItem(
            "Hillbilly Taxi/Truck/Production Integration V2/" +
            "Create Integrated Prefab Snapshot")]
        public static void CreatePrefabSnapshot()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogWarning(
                    "Exit Play Mode before creating the prefab snapshot.");

                return;
            }

            GameObject vehicleRoot =
                GameObject.Find(VehicleRootName);

            if (vehicleRoot == null ||
                !vehicleRoot.TryGetComponent(
                    out FishNetProductionTruckConfiguration configuration) ||
                !configuration.IsCurrentVersion ||
                !configuration.HasCompleteWheelSet)
            {
                Debug.LogError(
                    "Finalize and validate the production truck before " +
                    "creating a prefab snapshot.");

                return;
            }

            EnsureAssetFolder(PrefabFolder);

            GameObject clone =
                UnityEngine.Object.Instantiate(vehicleRoot);

            clone.name =
                "HT_ProductionTruck_Networked_V2";

            clone.transform.SetPositionAndRotation(
                Vector3.zero,
                Quaternion.identity);

            clone.transform.localScale =
                Vector3.one;

            string prefabPath =
                $"{PrefabFolder}/HT_ProductionTruck_Networked_V2.prefab";

            GameObject saved =
                PrefabUtility.SaveAsPrefabAsset(
                    clone,
                    prefabPath);

            UnityEngine.Object.DestroyImmediate(clone);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Selection.activeObject = saved;
            EditorGUIUtility.PingObject(saved);

            Debug.Log(
                "Created an unconnected integrated truck prefab snapshot at:\n" +
                prefabPath +
                "\n\nThe current FishNetProof scene object was not replaced.",
                saved);
        }

        private static void ApplyApprovedLivelyTune(
            Rigidbody body,
            FishNetTruckMotor motor,
            WheelSet wheels)
        {
            Undo.RecordObject(
                body,
                "Apply Approved Production Truck Tune");

            body.mass = 2450f;
            body.linearDamping = 0.03f;
            body.angularDamping = 0.28f;
            body.centerOfMass =
                new Vector3(
                    0f,
                    0.50f,
                    0.05f);

            body.maxAngularVelocity = 12f;
            body.useGravity = true;
            body.isKinematic = false;
            body.interpolation =
                RigidbodyInterpolation.Interpolate;

            body.collisionDetectionMode =
                CollisionDetectionMode.ContinuousDynamic;

            SerializedObject serializedMotor =
                new SerializedObject(motor);

            SetFloat(serializedMotor, "maximumForwardSpeed", 36f);
            SetFloat(serializedMotor, "maximumReverseSpeed", 12f);
            SetFloat(serializedMotor, "forwardMotorTorque", 3900f);
            SetFloat(serializedMotor, "reverseMotorTorque", 1500f);
            SetFloat(serializedMotor, "maximumSteerAngle", 35f);
            SetFloat(serializedMotor, "highSpeedSteerAngle", 9f);
            SetFloat(serializedMotor, "serviceBrakeTorque", 6600f);
            SetFloat(serializedMotor, "handbrakeTorque", 9800f);
            SetFloat(serializedMotor, "frontAntiRollForce", 5000f);
            SetFloat(serializedMotor, "rearAntiRollForce", 3300f);
            SetFloat(serializedMotor, "downforcePerMetrePerSecond", 6f);

            SetFloat(
                serializedMotor,
                "handbrakeRearSidewaysGripMultiplier",
                0.48f);

            SetFloat(
                serializedMotor,
                "handbrakeRearForwardGripMultiplier",
                0.62f);

            SetFloat(serializedMotor, "gripRecoverySpeed", 4f);

            serializedMotor.ApplyModifiedPropertiesWithoutUndo();

            ConfigureWheel(wheels.FrontLeft, true);
            ConfigureWheel(wheels.FrontRight, true);
            ConfigureWheel(wheels.RearLeft, false);
            ConfigureWheel(wheels.RearRight, false);

            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
        }

        private static void ConfigureWheel(
            WheelCollider wheel,
            bool isFront)
        {
            Undo.RecordObject(
                wheel,
                "Apply Approved Production Suspension Tune");

            wheel.mass = 48f;
            wheel.radius = 0.556f;
            wheel.suspensionDistance = 0.36f;
            wheel.wheelDampingRate = 0.85f;
            wheel.forceAppPointDistance = 0.10f;

            JointSpring spring =
                wheel.suspensionSpring;

            spring.spring =
                isFront
                    ? 37000f
                    : 34000f;

            spring.damper =
                isFront
                    ? 5000f
                    : 4500f;

            spring.targetPosition = 0.48f;
            wheel.suspensionSpring = spring;

            WheelFrictionCurve forward =
                wheel.forwardFriction;

            forward.stiffness =
                isFront
                    ? 1.30f
                    : 1.24f;

            wheel.forwardFriction = forward;

            WheelFrictionCurve sideways =
                wheel.sidewaysFriction;

            sideways.stiffness =
                isFront
                    ? 1.37f
                    : 1.24f;

            wheel.sidewaysFriction = sideways;
        }

        private static Transform BuildSafeCameraRoot(
            Transform vehicleRoot,
            FishNetVehicle vehicle)
        {
            GameObject cameraRootObject =
                new GameObject(FinalCameraRootName);

            Undo.RegisterCreatedObjectUndo(
                cameraRootObject,
                "Create Production Truck Seat Cameras");

            cameraRootObject.transform.SetParent(
                vehicleRoot,
                false);

            SerializedObject serializedVehicle =
                new SerializedObject(vehicle);

            SerializedProperty seats =
                serializedVehicle.FindProperty("seats");

            for (int seatIndex = 0;
                 seatIndex < vehicle.SeatCount;
                 seatIndex++)
            {
                if (!vehicle.TryGetSeat(
                        seatIndex,
                        out FishNetVehicleSeatDefinition seat))
                {
                    continue;
                }

                GameObject anchor =
                    new GameObject(
                        $"Production Camera - Seat {seatIndex}");

                Undo.RegisterCreatedObjectUndo(
                    anchor,
                    "Create Production Seat Camera");

                anchor.transform.SetParent(
                    cameraRootObject.transform,
                    false);

                float eyeHeight =
                    seatIndex <= 1
                        ? 0.73f
                        : 0.70f;

                anchor.transform.SetPositionAndRotation(
                    seat.OccupantAnchor.position +
                    vehicleRoot.up * eyeHeight +
                    vehicleRoot.forward * 0.06f,
                    vehicleRoot.rotation);

                SerializedProperty seatProperty =
                    seats.GetArrayElementAtIndex(seatIndex);

                seatProperty
                    .FindPropertyRelative("cameraAnchor")
                    .objectReferenceValue =
                    anchor.transform;
            }

            serializedVehicle.ApplyModifiedPropertiesWithoutUndo();

            return cameraRootObject.transform;
        }

        private static void ConfigureReferenceComponent(
            FishNetProductionTruckConfiguration configuration,
            Transform integrationRoot,
            Transform physicsRoot,
            Transform colliderRoot,
            Transform cameraRoot,
            Rigidbody body,
            WheelSet wheels,
            Dictionary<string, Transform> imported)
        {
            SerializedObject serialized =
                new SerializedObject(configuration);

            SetObject(
                serialized,
                "productionVisualRoot",
                integrationRoot);

            SetObject(
                serialized,
                "productionPhysicsRoot",
                physicsRoot);

            SetObject(
                serialized,
                "productionBodyColliderRoot",
                colliderRoot);

            SetObject(
                serialized,
                "productionSeatCameraRoot",
                cameraRoot);

            SetObject(serialized, "authoritativeBody", body);
            SetObject(serialized, "frontLeftWheel", wheels.FrontLeft);
            SetObject(serialized, "frontRightWheel", wheels.FrontRight);
            SetObject(serialized, "rearLeftWheel", wheels.RearLeft);
            SetObject(serialized, "rearRightWheel", wheels.RearRight);

            SetObject(serialized, "hood", Get(imported, "Hood"));
            SetObject(serialized, "tailgate", Get(imported, "Tailgate"));
            SetObject(serialized, "driverDoor", Get(imported, "Door_FL"));
            SetObject(serialized, "frontRightDoor", Get(imported, "Door_FR"));
            SetObject(serialized, "rearLeftDoor", Get(imported, "Door_RL"));
            SetObject(serialized, "rearRightDoor", Get(imported, "Door_RR"));

            SetObject(
                serialized,
                "driverSeat",
                Get(imported, "Seat_Driver_Pelvis"));

            SetObject(
                serialized,
                "frontRightSeat",
                Get(imported, "Seat_FrontRight_Pelvis"));

            SetObject(
                serialized,
                "rearLeftSeat",
                Get(imported, "Seat_RearLeft_Pelvis"));

            SetObject(
                serialized,
                "rearRightSeat",
                Get(imported, "Seat_RearRight_Pelvis"));

            SetObject(
                serialized,
                "taxiPassengerSeat",
                Get(imported, "Seat_TaxiPassenger_Pelvis"));

            SetObject(
                serialized,
                "engineBayInteraction",
                First(
                    imported,
                    "Interact_EngineBay",
                    "Repair_Engine",
                    "EngineBay_Interact"));

            SetObject(
                serialized,
                "fuelInteraction",
                First(
                    imported,
                    "Interact_Fuel",
                    "FuelFiller",
                    "Fuel_Interact"));

            SetObject(
                serialized,
                "towFront",
                First(
                    imported,
                    "TowPoint_Front",
                    "Tow_Front"));

            SetObject(
                serialized,
                "towRear",
                First(
                    imported,
                    "TowPoint_Rear",
                    "Tow_Rear"));

            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void DisableObsoleteGreyboxPhysics(
            Transform vehicleRoot)
        {
            string[] names =
            {
                "Truck Body",
                "Truck Cabin"
            };

            foreach (string name in names)
            {
                Transform target =
                    FindDirectChild(vehicleRoot, name);

                if (target == null)
                {
                    continue;
                }

                foreach (Renderer renderer in
                         target.GetComponentsInChildren<Renderer>(true))
                {
                    renderer.enabled = false;
                }

                foreach (Collider collider in
                         target.GetComponentsInChildren<Collider>(true))
                {
                    collider.enabled = false;
                }
            }
        }

        private static Transform ResolveAndRenameRoot(
            Transform vehicleRoot,
            string legacyName,
            string finalName,
            Transform fallback)
        {
            Transform final =
                FindDirectChild(vehicleRoot, finalName);

            if (final != null)
            {
                return final;
            }

            Transform legacy =
                FindDirectChild(vehicleRoot, legacyName);

            Transform result =
                legacy != null
                    ? legacy
                    : fallback;

            if (result != null &&
                result != vehicleRoot)
            {
                Undo.RecordObject(
                    result.gameObject,
                    $"Rename {finalName}");

                result.gameObject.name = finalName;
            }

            return result;
        }

        private static Transform FindColliderRoot(
            Transform vehicleRoot)
        {
            Transform[] descendants =
                vehicleRoot.GetComponentsInChildren<Transform>(true);

            return descendants.FirstOrDefault(
                transform =>
                    transform.name.IndexOf(
                        "Body Colliders",
                        StringComparison.OrdinalIgnoreCase) >= 0 &&
                    transform.GetComponentsInChildren<Collider>(true)
                        .Any(
                            collider =>
                                !(collider is WheelCollider) &&
                                !collider.isTrigger));
        }

        private static Transform FindCameraRoot(
            Transform vehicleRoot)
        {
            Transform[] descendants =
                vehicleRoot.GetComponentsInChildren<Transform>(true);

            return descendants.FirstOrDefault(
                transform =>
                    transform.name.IndexOf(
                        "Seat Cameras",
                        StringComparison.OrdinalIgnoreCase) >= 0);
        }

        private static WheelSet ResolveMotorWheels(
            FishNetTruckMotor motor)
        {
            SerializedObject serialized =
                new SerializedObject(motor);

            return new WheelSet
            {
                FrontLeft =
                    GetWheel(serialized, "frontLeftWheel"),

                FrontRight =
                    GetWheel(serialized, "frontRightWheel"),

                RearLeft =
                    GetWheel(serialized, "rearLeftWheel"),

                RearRight =
                    GetWheel(serialized, "rearRightWheel")
            };
        }

        private static WheelCollider GetWheel(
            SerializedObject serialized,
            string propertyName)
        {
            SerializedProperty property =
                serialized.FindProperty(propertyName);

            return property != null
                ? property.objectReferenceValue as WheelCollider
                : null;
        }

        private static string BuildReport(
            GameObject vehicleRoot,
            Rigidbody body,
            FishNetTruckMotor motor,
            FishNetProductionTruckConfiguration configuration,
            WheelSet wheels,
            Collider[] physicalColliders,
            Transform cameraRoot)
        {
            SerializedObject serializedMotor =
                new SerializedObject(motor);

            StringBuilder report =
                new StringBuilder(8192);

            report.AppendLine(
                "HILLBILLY TAXI — PRODUCTION TRUCK INTEGRATION V2");

            report.AppendLine(
                new string('=', 66));

            report.AppendLine(
                $"Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");

            report.AppendLine(
                $"Vehicle root: {vehicleRoot.name}");

            report.AppendLine(
                $"Integration version: {configuration.IntegrationVersion}");

            report.AppendLine(
                $"Configuration current: {configuration.IsCurrentVersion}");

            report.AppendLine();

            report.AppendLine("AUTHORITATIVE PHYSICS");
            report.AppendLine(new string('-', 66));
            report.AppendLine($"Rigidbody mass: {body.mass:0} kg");
            report.AppendLine($"Centre of mass: {body.centerOfMass}");
            report.AppendLine($"Physical body colliders: {physicalColliders.Length}");
            report.AppendLine($"Wheel set complete: {wheels.IsComplete}");
            report.AppendLine($"All wheels attached to body: {wheels.AllUseBody(body)}");

            AppendWheel(report, "Front Left", wheels.FrontLeft, body);
            AppendWheel(report, "Front Right", wheels.FrontRight, body);
            AppendWheel(report, "Rear Left", wheels.RearLeft, body);
            AppendWheel(report, "Rear Right", wheels.RearRight, body);

            report.AppendLine();
            report.AppendLine("APPROVED HANDLING BASELINE");
            report.AppendLine(new string('-', 66));

            AppendMotor(
                report,
                serializedMotor,
                "maximumForwardSpeed",
                "Maximum forward speed");

            AppendMotor(
                report,
                serializedMotor,
                "forwardMotorTorque",
                "Forward torque");

            AppendMotor(
                report,
                serializedMotor,
                "frontAntiRollForce",
                "Front anti-roll");

            AppendMotor(
                report,
                serializedMotor,
                "rearAntiRollForce",
                "Rear anti-roll");

            AppendMotor(
                report,
                serializedMotor,
                "downforcePerMetrePerSecond",
                "Downforce per m/s");

            report.AppendLine();
            report.AppendLine("GAMEPLAY REFERENCES");
            report.AppendLine(new string('-', 66));
            report.AppendLine($"Production visual root: {Path(configuration.ProductionVisualRoot)}");
            report.AppendLine($"Production physics root: {Path(configuration.ProductionPhysicsRoot)}");
            report.AppendLine($"Body collider root: {Path(configuration.ProductionBodyColliderRoot)}");
            report.AppendLine($"Seat camera root: {Path(cameraRoot)}");
            report.AppendLine($"Hood: {Path(configuration.Hood)}");
            report.AppendLine($"Tailgate: {Path(configuration.Tailgate)}");
            report.AppendLine($"Taxi passenger seat: {Path(configuration.TaxiPassengerSeat)}");
            report.AppendLine($"Engine repair anchor: {Path(configuration.EngineBayInteraction)}");
            report.AppendLine($"Fuel anchor: {Path(configuration.FuelInteraction)}");

            bool pass =
                configuration.IsCurrentVersion &&
                configuration.HasCompleteWheelSet &&
                wheels.IsComplete &&
                wheels.AllUseBody(body) &&
                physicalColliders.Length >= 3 &&
                cameraRoot != null;

            report.AppendLine();
            report.AppendLine(
                $"FINAL INTEGRATION RESULT: {(pass ? "PASS" : "FAIL")}");

            return report.ToString();
        }

        private static void AppendWheel(
            StringBuilder report,
            string label,
            WheelCollider wheel,
            Rigidbody body)
        {
            if (wheel == null)
            {
                report.AppendLine($"{label}: MISSING");
                return;
            }

            JointSpring spring =
                wheel.suspensionSpring;

            report.AppendLine(
                $"{label}: radius {wheel.radius:0.###}, " +
                $"travel {wheel.suspensionDistance:0.###}, " +
                $"spring {spring.spring:0}, " +
                $"damper {spring.damper:0}, " +
                $"attached {(wheel.attachedRigidbody == body)}");
        }

        private static void AppendMotor(
            StringBuilder report,
            SerializedObject motor,
            string propertyName,
            string label)
        {
            SerializedProperty property =
                motor.FindProperty(propertyName);

            report.AppendLine(
                $"{label}: " +
                $"{(property != null ? property.floatValue : -1f):0.###}");
        }

        private static Transform First(
            Dictionary<string, Transform> lookup,
            params string[] names)
        {
            foreach (string name in names)
            {
                Transform result = Get(lookup, name);

                if (result != null)
                {
                    return result;
                }
            }

            return null;
        }

        private static Transform Get(
            Dictionary<string, Transform> lookup,
            string name)
        {
            return lookup.TryGetValue(
                    name,
                    out Transform result)
                ? result
                : null;
        }

        private static Dictionary<string, Transform>
            BuildUniqueNameLookup(
                Transform root)
        {
            Dictionary<string, Transform> lookup =
                new Dictionary<string, Transform>(
                    StringComparer.Ordinal);

            foreach (Transform transform in
                     root.GetComponentsInChildren<Transform>(true))
            {
                if (!lookup.ContainsKey(transform.name))
                {
                    lookup.Add(transform.name, transform);
                }
            }

            return lookup;
        }

        private static Transform FindDirectChild(
            Transform parent,
            string exactName)
        {
            for (int index = 0;
                 index < parent.childCount;
                 index++)
            {
                Transform child = parent.GetChild(index);

                if (child.name == exactName)
                {
                    return child;
                }
            }

            return null;
        }

        private static Transform FindDeepChild(
            Transform parent,
            string exactName)
        {
            if (parent.name == exactName)
            {
                return parent;
            }

            for (int index = 0;
                 index < parent.childCount;
                 index++)
            {
                Transform result =
                    FindDeepChild(
                        parent.GetChild(index),
                        exactName);

                if (result != null)
                {
                    return result;
                }
            }

            return null;
        }

        private static void SetObject(
            SerializedObject serialized,
            string propertyName,
            UnityEngine.Object value)
        {
            SerializedProperty property =
                serialized.FindProperty(propertyName);

            if (property != null)
            {
                property.objectReferenceValue = value;
            }
        }

        private static void SetFloat(
            SerializedObject serialized,
            string propertyName,
            float value)
        {
            SerializedProperty property =
                serialized.FindProperty(propertyName);

            if (property != null)
            {
                property.floatValue = value;
            }
        }

        private static string Path(
            Transform transform)
        {
            if (transform == null)
            {
                return "<not authored>";
            }

            Stack<string> names =
                new Stack<string>();

            Transform current = transform;

            while (current != null)
            {
                names.Push(current.name);
                current = current.parent;
            }

            return string.Join("/", names);
        }

        private static string SaveReport(
            string report,
            string prefix)
        {
            EnsureAssetFolder(ReportFolder);

            string assetPath =
                $"{ReportFolder}/{prefix}_" +
                $"{DateTime.Now:yyyyMMdd_HHmmss}.txt";

            string projectRoot =
                Directory.GetParent(
                    Application.dataPath).FullName;

            File.WriteAllText(
                System.IO.Path.Combine(
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
            string[] parts =
                fullPath
                    .Replace('\\', '/')
                    .Split('/');

            string current = "Assets";

            for (int index = 1;
                 index < parts.Length;
                 index++)
            {
                string next =
                    $"{current}/{parts[index]}";

                if (!AssetDatabase.IsValidFolder(next))
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

            public bool AllUseBody(
                Rigidbody body)
            {
                return IsComplete &&
                       FrontLeft.attachedRigidbody == body &&
                       FrontRight.attachedRigidbody == body &&
                       RearLeft.attachedRigidbody == body &&
                       RearRight.attachedRigidbody == body;
            }
        }
    }
}
#endif
