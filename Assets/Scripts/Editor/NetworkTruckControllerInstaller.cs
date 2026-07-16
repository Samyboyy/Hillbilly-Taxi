#if UNITY_EDITOR
using HillbillyTaxi.Vehicles;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HillbillyTaxi.EditorTools
{
    public static class NetworkTruckControllerInstaller
    {
        private const string TestPickupName =
            "Test Pickup Seat Rig";

        private static readonly Vector3 FrontLeftPosition =
            new Vector3(-1.02f, 0.43f, 1.45f);

        private static readonly Vector3 FrontRightPosition =
            new Vector3(1.02f, 0.43f, 1.45f);

        private static readonly Vector3 RearLeftPosition =
            new Vector3(-1.02f, 0.43f, -1.45f);

        private static readonly Vector3 RearRightPosition =
            new Vector3(1.02f, 0.43f, -1.45f);

        [MenuItem(
            "Hillbilly Taxi/Vehicle Driving/Install Network Truck Controller")]
        public static void InstallNetworkTruckController()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogWarning(
                    "Exit Play Mode before installing the truck controller.");

                return;
            }

            NetworkVehicle vehicle = FindTestVehicle();

            if (vehicle == null)
            {
                Debug.LogError(
                    $"Could not find '{TestPickupName}' or another " +
                    $"{nameof(NetworkVehicle)} in the active scene.");

                return;
            }

            GameObject root = vehicle.gameObject;

            NetworkTransform networkTransform =
                GetOrAddComponent<NetworkTransform>(root);

            NetworkRigidbody networkRigidbody =
                GetOrAddComponent<NetworkRigidbody>(root);

            Rigidbody body =
                GetOrAddComponent<Rigidbody>(root);

            NetworkTruckMotor motor =
                GetOrAddComponent<NetworkTruckMotor>(root);

            ConfigureRigidbody(body);
            ConfigureNetworkRigidbody(networkRigidbody);

            Transform physicsRoot =
                FindOrCreateChild(
                    root.transform,
                    "Vehicle Physics");

            WheelSetup frontLeft =
                CreateOrUpdateWheel(
                    physicsRoot,
                    "Front Left",
                    FrontLeftPosition);

            WheelSetup frontRight =
                CreateOrUpdateWheel(
                    physicsRoot,
                    "Front Right",
                    FrontRightPosition);

            WheelSetup rearLeft =
                CreateOrUpdateWheel(
                    physicsRoot,
                    "Rear Left",
                    RearLeftPosition);

            WheelSetup rearRight =
                CreateOrUpdateWheel(
                    physicsRoot,
                    "Rear Right",
                    RearRightPosition);

            ConfigureMotor(
                motor,
                vehicle,
                body,
                frontLeft,
                frontRight,
                rearLeft,
                rearRight);

            EditorUtility.SetDirty(root);
            EditorUtility.SetDirty(vehicle);
            EditorUtility.SetDirty(body);
            EditorUtility.SetDirty(networkTransform);
            EditorUtility.SetDirty(networkRigidbody);
            EditorUtility.SetDirty(motor);

            Scene activeScene =
                SceneManager.GetActiveScene();

            if (activeScene.IsValid())
            {
                EditorSceneManager.MarkSceneDirty(activeScene);
            }

            Selection.activeGameObject = root;

            Debug.Log(
                "Installed the server-authoritative network truck controller. " +
                "Save the scene, then test W/S, A/D, Space, and E.");
        }

        private static NetworkVehicle FindTestVehicle()
        {
            GameObject namedObject =
                GameObject.Find(TestPickupName);

            if (namedObject != null &&
                namedObject.TryGetComponent(
                    out NetworkVehicle namedVehicle))
            {
                return namedVehicle;
            }

            return Object.FindFirstObjectByType<NetworkVehicle>();
        }

        private static void ConfigureRigidbody(Rigidbody body)
        {
            Undo.RecordObject(
                body,
                "Configure Truck Rigidbody");

            body.mass = 1600f;
            body.linearDamping = 0.05f;
            body.angularDamping = 0.45f;
            body.useGravity = true;
            body.isKinematic = false;
            body.interpolation =
                RigidbodyInterpolation.Interpolate;

            body.collisionDetectionMode =
                CollisionDetectionMode.ContinuousDynamic;

            body.centerOfMass =
                new Vector3(0f, 0.08f, 0f);

            body.maxAngularVelocity = 7f;
        }

        private static void ConfigureNetworkRigidbody(
            NetworkRigidbody networkRigidbody)
        {
            Undo.RecordObject(
                networkRigidbody,
                "Configure Network Rigidbody");

            networkRigidbody.UseRigidBodyForMotion = true;
            networkRigidbody.AutoUpdateKinematicState = true;
            networkRigidbody.AutoSetKinematicOnDespawn = true;
        }

        private static WheelSetup CreateOrUpdateWheel(
            Transform physicsRoot,
            string wheelName,
            Vector3 localPosition)
        {
            Transform wheelRoot =
                FindOrCreateChild(
                    physicsRoot,
                    wheelName);

            wheelRoot.localPosition = localPosition;
            wheelRoot.localRotation = Quaternion.identity;
            wheelRoot.localScale = Vector3.one;

            WheelCollider collider =
                GetOrAddComponent<WheelCollider>(
                    wheelRoot.gameObject);

            ConfigureWheelCollider(collider);

            Transform visualPose =
                FindOrCreateChild(
                    wheelRoot,
                    "Visual Pose");

            visualPose.localPosition = Vector3.zero;
            visualPose.localRotation = Quaternion.identity;
            visualPose.localScale = Vector3.one;

            Transform visualModel =
                visualPose.Find("Wheel Model");

            if (visualModel == null)
            {
                GameObject cylinder =
                    GameObject.CreatePrimitive(
                        PrimitiveType.Cylinder);

                Undo.RegisterCreatedObjectUndo(
                    cylinder,
                    "Create Wheel Model");

                cylinder.name = "Wheel Model";
                cylinder.transform.SetParent(
                    visualPose,
                    false);

                Collider primitiveCollider =
                    cylinder.GetComponent<Collider>();

                if (primitiveCollider != null)
                {
                    Object.DestroyImmediate(
                        primitiveCollider);
                }

                visualModel = cylinder.transform;
            }

            visualModel.localPosition = Vector3.zero;
            visualModel.localRotation =
                Quaternion.Euler(0f, 0f, 90f);

            visualModel.localScale =
                new Vector3(0.42f, 0.16f, 0.42f);

            return new WheelSetup(
                collider,
                visualPose);
        }

        private static void ConfigureWheelCollider(
            WheelCollider wheel)
        {
            Undo.RecordObject(
                wheel,
                "Configure Truck Wheel");

            wheel.mass = 35f;
            wheel.radius = 0.42f;
            wheel.wheelDampingRate = 1f;
            wheel.suspensionDistance = 0.22f;
            wheel.forceAppPointDistance = 0.12f;

            JointSpring suspension =
                wheel.suspensionSpring;

            suspension.spring = 35000f;
            suspension.damper = 4500f;
            suspension.targetPosition = 0.5f;

            wheel.suspensionSpring = suspension;

            WheelFrictionCurve forward =
                wheel.forwardFriction;

            forward.extremumSlip = 0.4f;
            forward.extremumValue = 1f;
            forward.asymptoteSlip = 0.8f;
            forward.asymptoteValue = 0.75f;
            forward.stiffness = 1.35f;

            wheel.forwardFriction = forward;

            WheelFrictionCurve sideways =
                wheel.sidewaysFriction;

            sideways.extremumSlip = 0.25f;
            sideways.extremumValue = 1f;
            sideways.asymptoteSlip = 0.5f;
            sideways.asymptoteValue = 0.8f;
            sideways.stiffness = 1.7f;

            wheel.sidewaysFriction = sideways;
        }

        private static void ConfigureMotor(
            NetworkTruckMotor motor,
            NetworkVehicle vehicle,
            Rigidbody body,
            WheelSetup frontLeft,
            WheelSetup frontRight,
            WheelSetup rearLeft,
            WheelSetup rearRight)
        {
            SerializedObject serializedMotor =
                new SerializedObject(motor);

            serializedMotor
                .FindProperty("vehicle")
                .objectReferenceValue = vehicle;

            serializedMotor
                .FindProperty("body")
                .objectReferenceValue = body;

            serializedMotor
                .FindProperty("frontLeftWheel")
                .objectReferenceValue =
                    frontLeft.Collider;

            serializedMotor
                .FindProperty("frontRightWheel")
                .objectReferenceValue =
                    frontRight.Collider;

            serializedMotor
                .FindProperty("rearLeftWheel")
                .objectReferenceValue =
                    rearLeft.Collider;

            serializedMotor
                .FindProperty("rearRightWheel")
                .objectReferenceValue =
                    rearRight.Collider;

            serializedMotor
                .FindProperty("frontLeftVisual")
                .objectReferenceValue =
                    frontLeft.VisualPose;

            serializedMotor
                .FindProperty("frontRightVisual")
                .objectReferenceValue =
                    frontRight.VisualPose;

            serializedMotor
                .FindProperty("rearLeftVisual")
                .objectReferenceValue =
                    rearLeft.VisualPose;

            serializedMotor
                .FindProperty("rearRightVisual")
                .objectReferenceValue =
                    rearRight.VisualPose;

            serializedMotor
                .ApplyModifiedPropertiesWithoutUndo();
        }

        private static T GetOrAddComponent<T>(
            GameObject target)
            where T : Component
        {
            T component = target.GetComponent<T>();

            if (component == null)
            {
                component =
                    Undo.AddComponent<T>(target);
            }

            return component;
        }

        private static Transform FindOrCreateChild(
            Transform parent,
            string childName)
        {
            Transform child =
                parent.Find(childName);

            if (child != null)
            {
                return child;
            }

            GameObject childObject =
                new GameObject(childName);

            Undo.RegisterCreatedObjectUndo(
                childObject,
                $"Create {childName}");

            childObject.transform.SetParent(
                parent,
                false);

            return childObject.transform;
        }

        private readonly struct WheelSetup
        {
            public WheelSetup(
                WheelCollider collider,
                Transform visualPose)
            {
                Collider = collider;
                VisualPose = visualPose;
            }

            public WheelCollider Collider { get; }

            public Transform VisualPose { get; }
        }
    }
}
#endif
