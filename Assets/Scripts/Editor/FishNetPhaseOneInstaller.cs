#if UNITY_EDITOR
using FishNet.Component.Spawning;
using FishNet.Component.Transforming;
using FishNet.Managing;
using FishNet.Object;
using HillbillyTaxi.FishNetMigration;
using HillbillyTaxi.Input;
using HillbillyTaxi.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace HillbillyTaxi.EditorTools
{
    public static class FishNetPhaseOneInstaller
    {
        private const string NeutralPlayerPath =
            "Assets/FishNetMigration/PlayerBase.prefab";

        private const string FishNetPlayerPath =
            "Assets/FishNetMigration/FishNetPlayer.prefab";

        private const string SpawnRootName =
            "FishNet Player Spawn Points";

        [MenuItem(
            "Hillbilly Taxi/FishNet Migration/Phase 1/Create FishNet Player Prefab")]
        public static void CreateFishNetPlayerPrefab()
        {
            GameObject neutralPlayer =
                AssetDatabase.LoadAssetAtPath<GameObject>(
                    NeutralPlayerPath);

            if (neutralPlayer == null)
            {
                Debug.LogError(
                    $"Could not find {NeutralPlayerPath}. " +
                    "Run the framework-neutral preparation first.");

                return;
            }

            if (AssetDatabase.LoadAssetAtPath<GameObject>(
                    FishNetPlayerPath) != null)
            {
                AssetDatabase.DeleteAsset(FishNetPlayerPath);
            }

            if (!AssetDatabase.CopyAsset(
                    NeutralPlayerPath,
                    FishNetPlayerPath))
            {
                Debug.LogError(
                    "Unity could not create FishNetPlayer.prefab.");

                return;
            }

            AssetDatabase.ImportAsset(
                FishNetPlayerPath,
                ImportAssetOptions.ForceSynchronousImport);

            GameObject root =
                PrefabUtility.LoadPrefabContents(
                    FishNetPlayerPath);

            try
            {
                PlayerInput playerInput =
                    root.GetComponent<PlayerInput>();

                PlayerInputReader inputReader =
                    root.GetComponent<PlayerInputReader>();

                FirstPersonCharacterMotor motor =
                    root.GetComponent<FirstPersonCharacterMotor>();

                Camera playerCamera =
                    root.GetComponentInChildren<Camera>(true);

                Renderer modelRenderer =
                    FindModelRenderer(root);

                if (playerInput == null ||
                    inputReader == null ||
                    motor == null ||
                    playerCamera == null)
                {
                    Debug.LogError(
                        "PlayerBase is missing PlayerInput, PlayerInputReader, " +
                        "FirstPersonCharacterMotor, or PlayerCamera.");

                    return;
                }

                playerInput.enabled = false;
                inputReader.enabled = true;
                motor.enabled = true;

                NetworkObject networkObject =
                    GetOrAddComponent<NetworkObject>(root);

                NetworkTransform networkTransform =
                    GetOrAddComponent<NetworkTransform>(root);

                ConfigureNetworkTransform(networkTransform);

                FishNetPlayerCharacter character =
                    GetOrAddComponent<FishNetPlayerCharacter>(root);

                SerializedObject serializedCharacter =
                    new SerializedObject(character);

                SerializedProperty ownerOnlyObjects =
                    serializedCharacter.FindProperty(
                        "ownerOnlyObjects");

                ownerOnlyObjects.arraySize = 1;
                ownerOnlyObjects
                    .GetArrayElementAtIndex(0)
                    .objectReferenceValue =
                        playerCamera.transform.parent.gameObject;

                SerializedProperty hiddenRenderers =
                    serializedCharacter.FindProperty(
                        "renderersHiddenFromOwner");

                hiddenRenderers.arraySize =
                    modelRenderer != null ? 1 : 0;

                if (modelRenderer != null)
                {
                    hiddenRenderers
                        .GetArrayElementAtIndex(0)
                        .objectReferenceValue =
                            modelRenderer;
                }

                serializedCharacter
                    .ApplyModifiedPropertiesWithoutUndo();

                root.name = "FishNetPlayer";
                root.transform.localPosition = Vector3.zero;
                root.transform.localRotation = Quaternion.identity;
                root.transform.localScale = Vector3.one;

                PrefabUtility.SaveAsPrefabAsset(
                    root,
                    FishNetPlayerPath);

                EditorUtility.SetDirty(networkObject);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                Selection.activeObject =
                    AssetDatabase.LoadAssetAtPath<GameObject>(
                        FishNetPlayerPath);

                Debug.Log(
                    "Created and configured " +
                    "Assets/FishNetMigration/FishNetPlayer.prefab.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        [MenuItem(
            "Hillbilly Taxi/FishNet Migration/Phase 1/Configure Selected NetworkManager")]
        public static void ConfigureSelectedNetworkManager()
        {
            GameObject playerPrefabObject =
                AssetDatabase.LoadAssetAtPath<GameObject>(
                    FishNetPlayerPath);

            if (playerPrefabObject == null)
            {
                Debug.LogError(
                    "Create FishNetPlayer.prefab before configuring the NetworkManager.");

                return;
            }

            NetworkObject playerNetworkObject =
                playerPrefabObject.GetComponent<NetworkObject>();

            if (playerNetworkObject == null)
            {
                Debug.LogError(
                    "FishNetPlayer.prefab has no FishNet NetworkObject.");

                return;
            }

            NetworkManager networkManager =
                ResolveSelectedNetworkManager();

            if (networkManager == null)
            {
                Debug.LogError(
                    "Select the FishNet NetworkManager object in the Hierarchy, " +
                    "then run this command again.");

                return;
            }

            Undo.RecordObject(
                networkManager.gameObject,
                "Configure FishNet NetworkManager");

            PlayerSpawner playerSpawner =
                networkManager.GetComponent<PlayerSpawner>();

            if (playerSpawner == null)
            {
                playerSpawner =
                    Undo.AddComponent<PlayerSpawner>(
                        networkManager.gameObject);
            }

            SerializedObject serializedSpawner =
                new SerializedObject(playerSpawner);

            SerializedProperty playerPrefabProperty =
                serializedSpawner.FindProperty("_playerPrefab");

            SerializedProperty addToDefaultSceneProperty =
                serializedSpawner.FindProperty("_addToDefaultScene");

            if (playerPrefabProperty == null ||
                addToDefaultSceneProperty == null)
            {
                Debug.LogError(
                    "FishNet PlayerSpawner serialized fields were not found. " +
                    "Assign FishNetPlayer.prefab manually in its Inspector.");

                return;
            }

            playerPrefabProperty.objectReferenceValue =
                playerNetworkObject;

            addToDefaultSceneProperty.boolValue = true;

            Transform[] spawnPoints =
                CreateOrReplaceSpawnPoints();

            playerSpawner.Spawns = spawnPoints;

            serializedSpawner.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(playerSpawner);

            Scene activeScene =
                SceneManager.GetActiveScene();

            if (activeScene.IsValid())
            {
                EditorSceneManager.MarkSceneDirty(activeScene);
            }

            Selection.activeGameObject =
                networkManager.gameObject;

            Debug.Log(
                "Configured PlayerSpawner with FishNetPlayer.prefab and " +
                "four test spawn points. Save FishNetProof.unity.");
        }

        [MenuItem(
            "Hillbilly Taxi/FishNet Migration/Phase 1/Validate FishNet Player")]
        public static void ValidateFishNetPlayer()
        {
            GameObject player =
                AssetDatabase.LoadAssetAtPath<GameObject>(
                    FishNetPlayerPath);

            if (player == null)
            {
                Debug.LogError(
                    "FishNetPlayer.prefab does not exist.");

                return;
            }

            NetworkObject networkObject =
                player.GetComponent<NetworkObject>();

            NetworkTransform networkTransform =
                player.GetComponent<NetworkTransform>();

            FishNetPlayerCharacter character =
                player.GetComponent<FishNetPlayerCharacter>();

            PlayerInput playerInput =
                player.GetComponent<PlayerInput>();

            PlayerInputReader inputReader =
                player.GetComponent<PlayerInputReader>();

            bool valid =
                networkObject != null &&
                networkTransform != null &&
                character != null &&
                playerInput != null &&
                !playerInput.enabled &&
                inputReader != null &&
                inputReader.enabled;

            if (!valid)
            {
                Debug.LogError(
                    "FishNetPlayer.prefab validation failed. " +
                    "Run Create FishNet Player Prefab again.");

                return;
            }

            SerializedObject serializedTransform =
                new SerializedObject(networkTransform);

            bool clientAuthoritative =
                ReadBool(
                    serializedTransform,
                    "_clientAuthoritative",
                    false);

            bool synchronizesPosition =
                ReadBool(
                    serializedTransform,
                    "_synchronizePosition",
                    false);

            bool synchronizesRotation =
                ReadBool(
                    serializedTransform,
                    "_synchronizeRotation",
                    false);

            bool synchronizesScale =
                ReadBool(
                    serializedTransform,
                    "_synchronizeScale",
                    true);

            if (!clientAuthoritative ||
                !synchronizesPosition ||
                !synchronizesRotation ||
                synchronizesScale)
            {
                Debug.LogError(
                    "FishNet NetworkTransform settings are not correct. " +
                    "Recreate the FishNet player prefab.");

                return;
            }

            Debug.Log(
                "FishNetPlayer.prefab validation passed.");
        }

        private static void ConfigureNetworkTransform(
            NetworkTransform networkTransform)
        {
            SerializedObject serializedTransform =
                new SerializedObject(networkTransform);

            SetBool(
                serializedTransform,
                "_clientAuthoritative",
                true);

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
                        .CharacterController;
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

        private static Transform[] CreateOrReplaceSpawnPoints()
        {
            GameObject existing =
                GameObject.Find(SpawnRootName);

            if (existing != null)
            {
                Undo.DestroyObjectImmediate(existing);
            }

            GameObject root =
                new GameObject(SpawnRootName);

            Undo.RegisterCreatedObjectUndo(
                root,
                "Create FishNet Spawn Points");

            Vector3[] positions =
            {
                new Vector3(-3f, 0f, -3f),
                new Vector3(-1f, 0f, -3f),
                new Vector3(1f, 0f, -3f),
                new Vector3(3f, 0f, -3f)
            };

            Transform[] result =
                new Transform[positions.Length];

            for (int index = 0;
                 index < positions.Length;
                 index++)
            {
                GameObject spawn =
                    new GameObject(
                        $"Spawn Point {index + 1}");

                Undo.RegisterCreatedObjectUndo(
                    spawn,
                    "Create FishNet Spawn Point");

                spawn.transform.SetParent(
                    root.transform,
                    false);

                spawn.transform.position =
                    positions[index];

                spawn.transform.rotation =
                    Quaternion.identity;

                result[index] = spawn.transform;
            }

            return result;
        }

        private static NetworkManager
            ResolveSelectedNetworkManager()
        {
            GameObject selected =
                Selection.activeGameObject;

            if (selected != null)
            {
                NetworkManager fromParent =
                    selected.GetComponentInParent<NetworkManager>();

                if (fromParent != null)
                {
                    return fromParent;
                }

                NetworkManager fromChildren =
                    selected.GetComponentInChildren<NetworkManager>(true);

                if (fromChildren != null)
                {
                    return fromChildren;
                }
            }

            return Object.FindFirstObjectByType<NetworkManager>();
        }

        private static Renderer FindModelRenderer(
            GameObject root)
        {
            Transform model =
                root.transform.Find("Model");

            if (model != null &&
                model.TryGetComponent(
                    out Renderer renderer))
            {
                return renderer;
            }

            return root.GetComponentInChildren<Renderer>(true);
        }

        private static T GetOrAddComponent<T>(
            GameObject target)
            where T : Component
        {
            T component = target.GetComponent<T>();

            if (component == null)
            {
                component = target.AddComponent<T>();
            }

            return component;
        }

        private static void SetBool(
            SerializedObject serializedObject,
            string propertyName,
            bool value)
        {
            SerializedProperty property =
                serializedObject.FindProperty(propertyName);

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
                serializedObject.FindProperty(propertyName);

            if (property != null)
            {
                property.floatValue = value;
            }
        }

        private static bool ReadBool(
            SerializedObject serializedObject,
            string propertyName,
            bool fallback)
        {
            SerializedProperty property =
                serializedObject.FindProperty(propertyName);

            return property != null
                ? property.boolValue
                : fallback;
        }
    }
}
#endif
