#if UNITY_EDITOR
using HillbillyTaxi.Input;
using HillbillyTaxi.Interaction;
using HillbillyTaxi.Networking;
using HillbillyTaxi.Player;
using HillbillyTaxi.Vehicles;
using Unity.Netcode;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace HillbillyTaxi.EditorTools
{
    public static class VehicleSeatFoundationInstaller
    {
        private const string PlayerPrefabPath =
            "Assets/Player.prefab";

        [MenuItem(
            "Hillbilly Taxi/Vehicle Seats/Install Player Seat Support")]
        public static void InstallPlayerSeatSupport()
        {
            GameObject prefabRoot =
                PrefabUtility.LoadPrefabContents(
                    PlayerPrefabPath);

            if (prefabRoot == null)
            {
                Debug.LogError(
                    $"Could not load {PlayerPrefabPath}.");

                return;
            }

            try
            {
                PlayerInput playerInput =
                    prefabRoot.GetComponent<PlayerInput>();

                PlayerInputReader inputReader =
                    prefabRoot.GetComponent<PlayerInputReader>();

                Camera playerCamera =
                    prefabRoot
                        .GetComponentInChildren<Camera>(true);

                if (playerInput == null ||
                    inputReader == null ||
                    playerCamera == null)
                {
                    Debug.LogError(
                        "The Player prefab is missing PlayerInput, " +
                        "PlayerInputReader, or PlayerCamera.");

                    return;
                }

                // This is the warning fix:
                // keep the reader enabled and let owner code enable PlayerInput.
                inputReader.enabled = true;
                playerInput.enabled = false;

                NetworkPlayerSeatController seatController =
                    prefabRoot
                        .GetComponent<NetworkPlayerSeatController>();

                if (seatController == null)
                {
                    seatController =
                        prefabRoot.AddComponent<
                            NetworkPlayerSeatController>();
                }

                SerializedObject serializedSeatController =
                    new SerializedObject(seatController);

                serializedSeatController
                    .FindProperty("cameraRig")
                    .objectReferenceValue =
                        playerCamera.transform.parent;

                serializedSeatController
                    .ApplyModifiedPropertiesWithoutUndo();

                prefabRoot.transform.localPosition =
                    Vector3.zero;

                prefabRoot.transform.localRotation =
                    Quaternion.identity;

                prefabRoot.transform.localScale =
                    Vector3.one;

                PrefabUtility.SaveAsPrefabAsset(
                    prefabRoot,
                    PlayerPrefabPath);

                Debug.Log(
                    "Player seat support installed. " +
                    "PlayerInputReader is enabled and PlayerInput " +
                    "starts disabled to prevent remote-device pairing warnings.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(prefabRoot);
            }
        }

        [MenuItem(
            "Hillbilly Taxi/Vehicle Seats/Create Test Pickup and Spawn Points")]
        public static void CreateTestPickupAndSpawnPoints()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogWarning(
                    "Exit Play Mode before creating the test setup.");

                return;
            }

            InstallPlayerSeatSupport();

            if (GameObject.Find("Test Pickup Seat Rig") != null ||
                GameObject.Find("Player Spawn Points") != null)
            {
                Debug.LogWarning(
                    "The test pickup or spawn points already exist. " +
                    "Delete them before running this command again.");

                return;
            }

            CreateSpawnPoints();
            CreatePickupSeatRig();

            Scene activeScene =
                SceneManager.GetActiveScene();

            if (activeScene.IsValid())
            {
                EditorSceneManager.MarkSceneDirty(activeScene);
            }

            Debug.Log(
                "Created four network spawn points and a stationary " +
                "four-seat pickup blockout. Save the scene before testing.");
        }

        private static void CreateSpawnPoints()
        {
            GameObject root =
                new GameObject("Player Spawn Points");

            Undo.RegisterCreatedObjectUndo(
                root,
                "Create Player Spawn Points");

            NetworkPlayerSpawnManager spawnManager =
                root.AddComponent<NetworkPlayerSpawnManager>();

            Vector3[] positions =
            {
                new Vector3(-2f, 0f, -2f),
                new Vector3(0f, 0f, -2f),
                new Vector3(2f, 0f, -2f),
                new Vector3(4f, 0f, -2f)
            };

            Transform[] spawnPoints =
                new Transform[positions.Length];

            for (
                int index = 0;
                index < positions.Length;
                index++)
            {
                GameObject point =
                    new GameObject(
                        $"Spawn Point {index + 1}");

                point.transform.SetParent(
                    root.transform,
                    false);

                point.transform.position =
                    positions[index];

                spawnPoints[index] =
                    point.transform;
            }

            NetworkManager networkManager =
                Object.FindFirstObjectByType<NetworkManager>();

            SerializedObject serializedManager =
                new SerializedObject(spawnManager);

            serializedManager
                .FindProperty("networkManager")
                .objectReferenceValue = networkManager;

            SerializedProperty pointsProperty =
                serializedManager.FindProperty("spawnPoints");

            pointsProperty.arraySize =
                spawnPoints.Length;

            for (
                int index = 0;
                index < spawnPoints.Length;
                index++)
            {
                pointsProperty
                    .GetArrayElementAtIndex(index)
                    .objectReferenceValue =
                        spawnPoints[index];
            }

            serializedManager
                .ApplyModifiedPropertiesWithoutUndo();
        }

        private static void CreatePickupSeatRig()
        {
            GameObject root =
                new GameObject("Test Pickup Seat Rig");

            Undo.RegisterCreatedObjectUndo(
                root,
                "Create Test Pickup Seat Rig");

            root.transform.position =
                new Vector3(6f, 0f, 4f);

            root.AddComponent<NetworkObject>();

            NetworkVehicle vehicle =
                root.AddComponent<NetworkVehicle>();

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
                "Driver seat",
                "front passenger seat",
                "rear-left passenger seat",
                "rear-right passenger seat"
            };

            VehicleSeatRole[] roles =
            {
                VehicleSeatRole.Driver,
                VehicleSeatRole.FrontPassenger,
                VehicleSeatRole.RearLeftPassenger,
                VehicleSeatRole.RearRightPassenger
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

            for (int index = 0; index < 4; index++)
            {
                GameObject interactionObject =
                    GameObject.CreatePrimitive(
                        PrimitiveType.Cube);

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

                NetworkInteractionPoint interactionPoint =
                    interactionObject.AddComponent<
                        NetworkInteractionPoint>();

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

            for (int index = 0; index < 4; index++)
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
