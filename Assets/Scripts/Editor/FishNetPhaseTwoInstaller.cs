#if UNITY_EDITOR
using FishNet.Managing;
using FishNet.Object;
using HillbillyTaxi.FishNetMigration;
using HillbillyTaxi.FishNetMigration.Interaction;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HillbillyTaxi.EditorTools
{
    public static class FishNetPhaseTwoInstaller
    {
        private const string FishNetPlayerPath =
            "Assets/FishNetMigration/FishNetPlayer.prefab";

        private const string TestSwitchName =
            "FishNet Interaction Test Switch";

        [MenuItem(
            "Hillbilly Taxi/FishNet Migration/Phase 2/Install Player Interaction")]
        public static void InstallPlayerInteraction()
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

                Transform cameraRig =
                    playerCamera.transform.parent;

                FishNetInteractionPromptView promptView =
                    cameraRig.GetComponent<
                        FishNetInteractionPromptView>();

                if (promptView == null)
                {
                    promptView =
                        cameraRig.gameObject.AddComponent<
                            FishNetInteractionPromptView>();
                }

                FishNetPlayerInteractor interactor =
                    root.GetComponent<
                        FishNetPlayerInteractor>();

                if (interactor == null)
                {
                    interactor =
                        root.AddComponent<
                            FishNetPlayerInteractor>();
                }

                SerializedObject serializedInteractor =
                    new SerializedObject(interactor);

                serializedInteractor
                    .FindProperty("interactionCamera")
                    .objectReferenceValue = playerCamera;

                serializedInteractor
                    .FindProperty("promptView")
                    .objectReferenceValue = promptView;

                serializedInteractor
                    .ApplyModifiedPropertiesWithoutUndo();

                PrefabUtility.SaveAsPrefabAsset(
                    root,
                    FishNetPlayerPath);

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                Debug.Log(
                    "FishNet player interaction installed.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        [MenuItem(
            "Hillbilly Taxi/FishNet Migration/Phase 2/Configure Scene")]
        public static void ConfigureScene()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogWarning(
                    "Exit Play Mode before configuring Phase 2.");

                return;
            }

            InstallPlayerInteraction();

            NetworkManager networkManager =
                ResolveSelectedNetworkManager();

            if (networkManager == null)
            {
                Debug.LogError(
                    "Select the FishNet NetworkManager in the Hierarchy.");

                return;
            }

            FishNetDevelopmentLauncher launcher =
                networkManager.GetComponent<
                    FishNetDevelopmentLauncher>();

            if (launcher == null)
            {
                launcher =
                    Undo.AddComponent<
                        FishNetDevelopmentLauncher>(
                        networkManager.gameObject);
            }

            FishNetConnectionLimiter limiter =
                networkManager.GetComponent<
                    FishNetConnectionLimiter>();

            if (limiter == null)
            {
                limiter =
                    Undo.AddComponent<
                        FishNetConnectionLimiter>(
                        networkManager.gameObject);
            }

            AssignNetworkManagerReference(
                launcher,
                networkManager);

            AssignNetworkManagerReference(
                limiter,
                networkManager);

            DisableDemoHud(networkManager);
            CreateOrUpdateTestSwitch();

            EditorUtility.SetDirty(
                networkManager.gameObject);

            Scene activeScene =
                SceneManager.GetActiveScene();

            if (activeScene.IsValid())
            {
                EditorSceneManager.MarkSceneDirty(
                    activeScene);
            }

            Selection.activeGameObject =
                networkManager.gameObject;

            Debug.Log(
                "FishNet Phase 2 scene configured. " +
                "Save FishNetProof.unity.");
        }

        private static void CreateOrUpdateTestSwitch()
        {
            GameObject testObject =
                GameObject.Find(TestSwitchName);

            if (testObject == null)
            {
                testObject =
                    GameObject.CreatePrimitive(
                        PrimitiveType.Cube);

                Undo.RegisterCreatedObjectUndo(
                    testObject,
                    "Create FishNet Test Switch");

                testObject.name = TestSwitchName;
                testObject.transform.position =
                    new Vector3(0f, 0.5f, 3f);

                testObject.transform.localScale =
                    Vector3.one;
            }

            NetworkObject networkObject =
                testObject.GetComponent<NetworkObject>();

            if (networkObject == null)
            {
                networkObject =
                    Undo.AddComponent<NetworkObject>(
                        testObject);
            }

            FishNetToggleInteractable toggle =
                testObject.GetComponent<
                    FishNetToggleInteractable>();

            if (toggle == null)
            {
                toggle =
                    Undo.AddComponent<
                        FishNetToggleInteractable>(
                        testObject);
            }

            Renderer targetRenderer =
                testObject.GetComponent<Renderer>();

            SerializedObject serializedToggle =
                new SerializedObject(toggle);

            serializedToggle
                .FindProperty("targetRenderer")
                .objectReferenceValue =
                    targetRenderer;

            serializedToggle
                .ApplyModifiedPropertiesWithoutUndo();

            EditorUtility.SetDirty(networkObject);
            EditorUtility.SetDirty(toggle);
        }

        private static void DisableDemoHud(
            NetworkManager networkManager)
        {
            Canvas[] canvases =
                networkManager.GetComponentsInChildren<
                    Canvas>(true);

            foreach (Canvas canvas in canvases)
            {
                string objectName =
                    canvas.gameObject.name.ToLowerInvariant();

                if (!objectName.Contains("hud"))
                {
                    continue;
                }

                Undo.RecordObject(
                    canvas.gameObject,
                    "Disable FishNet Demo HUD");

                canvas.gameObject.SetActive(false);
            }
        }

        private static void AssignNetworkManagerReference(
            Component component,
            NetworkManager networkManager)
        {
            SerializedObject serializedComponent =
                new SerializedObject(component);

            SerializedProperty property =
                serializedComponent.FindProperty(
                    "networkManager");

            if (property != null)
            {
                property.objectReferenceValue =
                    networkManager;

                serializedComponent
                    .ApplyModifiedPropertiesWithoutUndo();
            }
        }

        private static NetworkManager
            ResolveSelectedNetworkManager()
        {
            GameObject selected =
                Selection.activeGameObject;

            if (selected != null)
            {
                NetworkManager fromParent =
                    selected.GetComponentInParent<
                        NetworkManager>();

                if (fromParent != null)
                {
                    return fromParent;
                }

                NetworkManager fromChildren =
                    selected.GetComponentInChildren<
                        NetworkManager>(true);

                if (fromChildren != null)
                {
                    return fromChildren;
                }
            }

            return Object.FindFirstObjectByType<
                NetworkManager>();
        }
    }
}
#endif
