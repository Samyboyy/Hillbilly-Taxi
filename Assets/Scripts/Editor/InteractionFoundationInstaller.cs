#if UNITY_EDITOR
using HillbillyTaxi.Interaction;
using Unity.Netcode;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HillbillyTaxi.EditorTools
{
    public static class InteractionFoundationInstaller
    {
        private const string PlayerPrefabPath =
            "Assets/Player.prefab";

        [MenuItem(
            "Hillbilly Taxi/Interaction/Install Player Components")]
        public static void InstallPlayerComponents()
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
                Camera playerCamera =
                    prefabRoot.GetComponentInChildren<Camera>(true);

                if (playerCamera == null)
                {
                    Debug.LogError(
                        "The Player prefab has no child Camera.");
                    return;
                }

                Transform cameraRig =
                    playerCamera.transform.parent;

                InteractionPromptView promptView =
                    cameraRig.GetComponent<InteractionPromptView>();

                if (promptView == null)
                {
                    promptView =
                        cameraRig.gameObject
                            .AddComponent<InteractionPromptView>();
                }

                NetworkPlayerInteractor interactor =
                    prefabRoot
                        .GetComponent<NetworkPlayerInteractor>();

                if (interactor == null)
                {
                    interactor =
                        prefabRoot
                            .AddComponent<NetworkPlayerInteractor>();
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
                    prefabRoot,
                    PlayerPrefabPath);

                Debug.Log(
                    "Interaction foundation installed on Assets/Player.prefab.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(prefabRoot);
            }
        }

        [MenuItem(
            "Hillbilly Taxi/Interaction/Create Network Test Switch")]
        public static void CreateNetworkTestSwitch()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogWarning(
                    "Exit Play Mode before creating the test switch.");
                return;
            }

            GameObject playerPrefab =
                AssetDatabase.LoadAssetAtPath<GameObject>(
                    PlayerPrefabPath);

            Vector3 playerSpawn =
                playerPrefab != null
                    ? playerPrefab.transform.position
                    : Vector3.zero;

            GameObject testObject =
                GameObject.CreatePrimitive(PrimitiveType.Cube);

            Undo.RegisterCreatedObjectUndo(
                testObject,
                "Create Network Test Switch");

            testObject.name = "Network Interaction Test Switch";
            testObject.transform.position =
                playerSpawn +
                Vector3.forward * 3f +
                Vector3.up * 0.5f;

            testObject.AddComponent<NetworkObject>();
            testObject.AddComponent<NetworkToggleInteractable>();

            Selection.activeGameObject = testObject;

            Scene activeScene =
                SceneManager.GetActiveScene();

            if (activeScene.IsValid())
            {
                EditorSceneManager.MarkSceneDirty(activeScene);
            }

            Debug.Log(
                "Created a scene NetworkObject test switch. " +
                "Save the scene before testing.");
        }
    }
}
#endif
