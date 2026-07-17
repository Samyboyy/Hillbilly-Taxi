#if UNITY_EDITOR
using FishNet.Component.Transforming;
using HillbillyTaxi.FishNetMigration.Vehicles;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HillbillyTaxi.EditorTools
{
    public static class FishNetPhaseFourInstaller
    {
        private const string TestPickupName =
            "FishNet Test Pickup Seat Rig";

        private static readonly Vector3 FrontLeftPosition =
            new Vector3(-1.02f, 0.43f, 1.45f);

        private static readonly Vector3 FrontRightPosition =
            new Vector3(1.02f, 0.43f, 1.45f);

        private static readonly Vector3 RearLeftPosition =
            new Vector3(-1.02f, 0.43f, -1.45f);

        private static readonly Vector3 RearRightPosition =
            new Vector3(1.02f, 0.43f, -1.45f);

        [MenuItem(
            "Hillbilly Taxi/FishNet Migration/Phase 4/Install Truck Driving")]
        public static void InstallTruckDriving()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogWarning(
                    "Exit Play Mode before installing truck driving.");

                return;
            }

            GameObject root =
                GameObject.Find(TestPickupName);

            if (root == null ||
                !root.TryGetComponent(
                    out FishNetVehicle vehicle))
            {
                Debug.LogError(
                    $"Could not find '{TestPickupName}' with " +
                    $"{nameof(FishNetVehicle)} in the active scene.");

                return;
            }

            Rigidbody body =
                GetOrAddComponent<Rigidbody>(root);

            NetworkTransform networkTransform =
                GetOrAddComponent<NetworkTransform>(root);

            FishNetTruckMotor motor =
                GetOrAddComponent<FishNetTruckMotor>(root);

            ConfigureRigidbody(body);
            ConfigureNetworkTransform(networkTransform);

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
            EditorUtility.SetDirty(motor);

            Scene activeScene =
                SceneManager.GetActiveScene();

            if (activeScene.IsValid())
            {
                EditorSceneManager.MarkSceneDirty(
                    activeScene);
            }

            Selection.activeGameObject = root;

            Debug.Log(
                "Installed FishNet Phase 4 server-authoritative truck driving. " +
                "Save FishNetProof.unity.");
        }

        private static void ConfigureRigidbody(
            Rigidbody body)
        {
            Undo.RecordObject(
                body,
                "Configure FishNet Truck Rigidbody");

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

        private static void ConfigureNetworkTransform(
            NetworkTransform networkTransform)
        {
            SerializedObject serializedTransform =
                new SerializedObject(networkTransform);

            SetBool(
                serializedTransform,
                "_clientAuthoritative",
                false);

            SetBool(
                serializedTransform,
                "_sendToOwner",
                true);

            SetBool(
                serializedTransform,
                "_synchronizePosition",
                true);

            SetBool(
                serializedTransform,
                "_synchronizeRotation",
                true);

            SetBool(
                serializedTransform,
                "_synchronizeScale",
                false);

            SetFloat(
                serializedTransform,
                "_positionSensitivity",
                0.01f);

            SerializedProperty componentConfiguration =
                serializedTransform.FindProperty(
                    "_componentConfiguration");

            if (componentConfiguration != null)
            {
                componentConfiguration.enumValueIndex =
                    (int)NetworkTransform
                        .ComponentConfigurationType
                        .Rigidbody;
            }

            SerializedProperty interpolation =
                serializedTransform.FindProperty(
                    "_interpolation");

            if (interpolation != null)
            {
                interpolation.intValue = 2;
            }

            SerializedProperty interval =
                serializedTransform.FindProperty(
                    "_interval");

            if (interval != null)
            {
                interval.intValue = 1;
            }

            serializedTransform
                .ApplyModifiedPropertiesWithoutUndo();
        }

        private static WheelSetup CreateOrUpdateWheel(
            Transform physicsRoot,
            string wheelName,
            Vector3 localPosition)
        {
            Transform colliderRoot =
                FindOrCreateChild(
                    physicsRoot,
                    wheelName + " WheelCollider");

            colliderRoot.localPosition =
                localPosition;

            colliderRoot.localRotation =
                Quaternion.identity;

            colliderRoot.localScale =
                Vector3.one;

            WheelCollider collider =
                GetOrAddComponent<WheelCollider>(
                    colliderRoot.gameObject);

            ConfigureWheelCollider(collider);

            Transform visualPose =
                FindOrCreateChild(
                    physicsRoot,
                    wheelName + " Visual Pose");

            visualPose.localPosition =
                localPosition;

            visualPose.localRotation =
                Quaternion.identity;

            visualPose.localScale =
                Vector3.one;

            Transform visualModel =
                visualPose.Find("Wheel Model");

            if (visualModel == null)
            {
                GameObject cylinder =
                    GameObject.CreatePrimitive(
                        PrimitiveType.Cylinder);

                Undo.RegisterCreatedObjectUndo(
                    cylinder,
                    "Create FishNet Wheel Model");

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

                visualModel =
                    cylinder.transform;
            }

            visualModel.localPosition =
                Vector3.zero;

            visualModel.localRotation =
                Quaternion.Euler(
                    0f,
                    0f,
                    90f);

            visualModel.localScale =
                new Vector3(
                    0.42f,
                    0.16f,
                    0.42f);

            return new WheelSetup(
                collider,
                visualPose);
        }

        private static void ConfigureWheelCollider(
            WheelCollider wheel)
        {
            Undo.RecordObject(
                wheel,
                "Configure FishNet Truck Wheel");

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
            FishNetTruckMotor motor,
            FishNetVehicle vehicle,
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
            T component =
                target.GetComponent<T>();

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

        private static void SetBool(
            SerializedObject serializedObject,
            string propertyName,
            bool value)
        {
            SerializedProperty property =
                serializedObject.FindProperty(
                    propertyName);

            if (property != null)
            {
                property.boolValue = value;
            }
        }

        private static void SetFloat(
            SerializedObject serializedObject,
            string propertyName,
            float value)
        {
            SerializedProperty property =
                serializedObject.FindProperty(
                    propertyName);

            if (property != null)
            {
                property.floatValue = value;
            }
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
