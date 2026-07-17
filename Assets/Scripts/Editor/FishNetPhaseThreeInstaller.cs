#if UNITY_EDITOR
using FishNet.Object;
using HillbillyTaxi.FishNetMigration.Interaction;
using HillbillyTaxi.FishNetMigration.Player;
using HillbillyTaxi.FishNetMigration.Vehicles;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HillbillyTaxi.EditorTools
{
    public static class FishNetPhaseThreeInstaller
    {
        private const string FishNetPlayerPath =
            "Assets/FishNetMigration/FishNetPlayer.prefab";

        private const string TestPickupName =
            "FishNet Test Pickup Seat Rig";

        [MenuItem(
            "Hillbilly Taxi/FishNet Migration/Phase 3/Install Player Seat Support")]
        public static void InstallPlayerSeatSupport()
        {
            GameObject root =
                PrefabUtility.LoadPrefabContents(
                    FishNetPlayerPath);

            if (root == null)
            {
                Debug.LogError(
                    $"Could not load {FishNetPlayerPath}.");

                return;
            }

            try
            {
                Camera playerCamera =
                    root.GetComponentInChildren<Camera>(
                        true);

                if (playerCamera == null ||
                    playerCamera.transform.parent == null)
                {
                    Debug.LogError(
                        "FishNetPlayer has no PlayerCamera/CameraRig.");

                    return;
                }

                FishNetPlayerSeatController seatController =
                    root.GetComponent<
                        FishNetPlayerSeatController>();

                if (seatController == null)
                {
                    seatController =
                        root.AddComponent<
                            FishNetPlayerSeatController>();
                }

                SerializedObject serializedSeatController =
                    new SerializedObject(seatController);

                serializedSeatController
                    .FindProperty("cameraRig")
                    .objectReferenceValue =
                        playerCamera.transform.parent;

                serializedSeatController
                    .ApplyModifiedPropertiesWithoutUndo();

                PrefabUtility.SaveAsPrefabAsset(
                    root,
                    FishNetPlayerPath);

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                Debug.Log(
                    "FishNet player seat support installed.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        [MenuItem(
            "Hillbilly Taxi/FishNet Migration/Phase 3/Create Test Pickup")]
        public static void CreateTestPickup()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogWarning(
                    "Exit Play Mode before creating the pickup.");

                return;
            }

            InstallPlayerSeatSupport();

            GameObject existing =
                GameObject.Find(TestPickupName);

            if (existing != null)
            {
                bool replace =
                    EditorUtility.DisplayDialog(
                        "Replace test pickup?",
                        "The FishNet test pickup already exists.",
                        "Replace",
                        "Cancel");

                if (!replace)
                {
                    return;
                }

                Undo.DestroyObjectImmediate(existing);
            }

            CreatePickupSeatRig();

            Scene activeScene =
                SceneManager.GetActiveScene();

            if (activeScene.IsValid())
            {
                EditorSceneManager.MarkSceneDirty(
                    activeScene);
            }

            Debug.Log(
                "Created the stationary FishNet four-seat pickup. " +
                "Save FishNetProof.unity.");
        }

        private static void CreatePickupSeatRig()
        {
            GameObject root =
                new GameObject(TestPickupName);

            Undo.RegisterCreatedObjectUndo(
                root,
                "Create FishNet Test Pickup");

            root.transform.position =
                new Vector3(6f, 0f, 4f);

            NetworkObject networkObject =
                Undo.AddComponent<NetworkObject>(root);

            FishNetVehicle vehicle =
                Undo.AddComponent<FishNetVehicle>(root);

            CreateVisualBox(
                "Truck Body",
                root.transform,
                new Vector3(0f, 0.55f, 0f),
                new Vector3(2.4f, 0.7f, 4.4f));

            CreateVisualBox(
                "Truck Cabin",
                root.transform,
                new Vector3(0f, 1.2f, 0.45f),
                new Vector3(2.1f, 1.25f, 2.25f));

            string[] seatNames =
            {
                "driver seat",
                "front passenger seat",
                "rear-left passenger seat",
                "rear-right passenger seat"
            };

            FishNetVehicleSeatRole[] roles =
            {
                FishNetVehicleSeatRole.Driver,
                FishNetVehicleSeatRole.FrontPassenger,
                FishNetVehicleSeatRole.RearLeftPassenger,
                FishNetVehicleSeatRole.RearRightPassenger
            };

            Vector3[] interactionPositions =
            {
                new Vector3(-1.35f, 0.85f, 0.85f),
                new Vector3(1.35f, 0.85f, 0.85f),
                new Vector3(-1.35f, 0.85f, -0.55f),
                new Vector3(1.35f, 0.85f, -0.55f)
            };

            Vector3[] occupantPositions =
            {
                new Vector3(-0.48f, 0.35f, 0.75f),
                new Vector3(0.48f, 0.35f, 0.75f),
                new Vector3(-0.48f, 0.35f, -0.55f),
                new Vector3(0.48f, 0.35f, -0.55f)
            };

            Vector3[] cameraPositions =
            {
                new Vector3(-0.48f, 1.25f, 0.82f),
                new Vector3(0.48f, 1.25f, 0.82f),
                new Vector3(-0.48f, 1.25f, -0.48f),
                new Vector3(0.48f, 1.25f, -0.48f)
            };

            Vector3[] exitPositions =
            {
                new Vector3(-1.75f, 0f, 0.85f),
                new Vector3(1.75f, 0f, 0.85f),
                new Vector3(-1.75f, 0f, -0.55f),
                new Vector3(1.75f, 0f, -0.55f)
            };

            Transform[] interactionPoints =
                new Transform[4];

            Transform[] occupantAnchors =
                new Transform[4];

            Transform[] cameraAnchors =
                new Transform[4];

            Transform[] exitPoints =
                new Transform[4];

            for (int index = 0;
                 index < 4;
                 index++)
            {
                GameObject interactionObject =
                    GameObject.CreatePrimitive(
                        PrimitiveType.Cube);

                Undo.RegisterCreatedObjectUndo(
                    interactionObject,
                    "Create Seat Interaction");

                interactionObject.name =
                    $"{seatNames[index]} Interaction";

                interactionObject.transform.SetParent(
                    root.transform,
                    false);

                interactionObject.transform.localPosition =
                    interactionPositions[index];

                interactionObject.transform.localScale =
                    new Vector3(0.35f, 0.7f, 0.65f);

                BoxCollider interactionCollider =
                    interactionObject.GetComponent<BoxCollider>();

                interactionCollider.isTrigger = true;

                FishNetInteractionPoint interactionPoint =
                    Undo.AddComponent<
                        FishNetInteractionPoint>(
                        interactionObject);

                SerializedObject serializedPoint =
                    new SerializedObject(interactionPoint);

                serializedPoint
                    .FindProperty("interactable")
                    .objectReferenceValue = vehicle;

                serializedPoint
                    .FindProperty("interactionId")
                    .intValue = index;

                serializedPoint
                    .ApplyModifiedPropertiesWithoutUndo();

                interactionPoints[index] =
                    interactionObject.transform;

                occupantAnchors[index] =
                    CreateAnchor(
                        $"{seatNames[index]} Occupant Anchor",
                        root.transform,
                        occupantPositions[index]);

                cameraAnchors[index] =
                    CreateAnchor(
                        $"{seatNames[index]} Camera Anchor",
                        root.transform,
                        cameraPositions[index]);

                exitPoints[index] =
                    CreateAnchor(
                        $"{seatNames[index]} Exit Point",
                        root.transform,
                        exitPositions[index]);
            }

            SerializedObject serializedVehicle =
                new SerializedObject(vehicle);

            SerializedProperty seatsProperty =
                serializedVehicle.FindProperty("seats");

            seatsProperty.arraySize = 4;

            for (int index = 0;
                 index < 4;
                 index++)
            {
                SerializedProperty seatProperty =
                    seatsProperty
                        .GetArrayElementAtIndex(index);

                seatProperty
                    .FindPropertyRelative("displayName")
                    .stringValue = seatNames[index];

                seatProperty
                    .FindPropertyRelative("role")
                    .enumValueIndex = (int)roles[index];

                seatProperty
                    .FindPropertyRelative("interactionPoint")
                    .objectReferenceValue =
                        interactionPoints[index];

                seatProperty
                    .FindPropertyRelative("occupantAnchor")
                    .objectReferenceValue =
                        occupantAnchors[index];

                seatProperty
                    .FindPropertyRelative("cameraAnchor")
                    .objectReferenceValue =
                        cameraAnchors[index];

                seatProperty
                    .FindPropertyRelative("exitPoint")
                    .objectReferenceValue =
                        exitPoints[index];
            }

            serializedVehicle
                .ApplyModifiedPropertiesWithoutUndo();

            EditorUtility.SetDirty(networkObject);
            EditorUtility.SetDirty(vehicle);

            Selection.activeGameObject = root;
        }

        private static void CreateVisualBox(
            string objectName,
            Transform parent,
            Vector3 localPosition,
            Vector3 localScale)
        {
            GameObject visual =
                GameObject.CreatePrimitive(
                    PrimitiveType.Cube);

            Undo.RegisterCreatedObjectUndo(
                visual,
                $"Create {objectName}");

            visual.name = objectName;

            visual.transform.SetParent(
                parent,
                false);

            visual.transform.localPosition =
                localPosition;

            visual.transform.localScale =
                localScale;
        }

        private static Transform CreateAnchor(
            string objectName,
            Transform parent,
            Vector3 localPosition)
        {
            GameObject anchor =
                new GameObject(objectName);

            Undo.RegisterCreatedObjectUndo(
                anchor,
                $"Create {objectName}");

            anchor.transform.SetParent(
                parent,
                false);

            anchor.transform.localPosition =
                localPosition;

            anchor.transform.localRotation =
                Quaternion.identity;

            return anchor.transform;
        }
    }
}
#endif
