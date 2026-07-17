#if UNITY_EDITOR
using FishNet.Managing;
using FishNet.Managing.Transporting;
using HillbillyTaxi.FishNetMigration.Diagnostics;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HillbillyTaxi.EditorTools
{
    public static class FishNetNetworkConditionInstaller
    {
        [MenuItem(
            "Hillbilly Taxi/FishNet Migration/Phase 5/Install Network Condition Panel")]
        public static void InstallNetworkConditionPanel()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogWarning(
                    "Exit Play Mode before installing " +
                    "the network-condition panel.");

                return;
            }

            NetworkManager networkManager =
                Object.FindFirstObjectByType<NetworkManager>();

            if (networkManager == null)
            {
                Debug.LogError(
                    "No FishNet NetworkManager was found " +
                    "in the active scene.");

                return;
            }

            // FishNet normally creates TransportManager during NetworkManager.Awake.
            // This installer runs in Edit Mode, so the component may not exist yet.
            // Add it as a scene/prefab-instance override so its latency settings can
            // be serialized; FishNet's runtime GetOrCreateComponent then reuses it.
            TransportManager transportManager =
                networkManager.GetComponent<TransportManager>();

            if (transportManager == null)
            {
                transportManager =
                    Undo.AddComponent<TransportManager>(
                        networkManager.gameObject);

                Debug.Log(
                    "Added FishNet TransportManager so network-condition " +
                    "settings can be serialized before Play Mode.",
                    transportManager);
            }

            FishNetNetworkConditionPanel panel =
                networkManager.GetComponent<
                    FishNetNetworkConditionPanel>();

            if (panel == null)
            {
                panel =
                    Undo.AddComponent<
                        FishNetNetworkConditionPanel>(
                        networkManager.gameObject);
            }

            SerializedObject serializedPanel =
                new SerializedObject(panel);

            serializedPanel
                .FindProperty("networkManager")
                .objectReferenceValue =
                    networkManager;

            serializedPanel
                .ApplyModifiedPropertiesWithoutUndo();

            ConfigureLatencySimulator(
                transportManager);

            EditorUtility.SetDirty(
                networkManager.gameObject);

            EditorUtility.SetDirty(
                transportManager);

            EditorUtility.SetDirty(panel);

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
                "Installed the FishNet network-condition panel. " +
                "The built-in simulator starts clean and excludes " +
                "the Host's local client. Save FishNetProof.unity.");
        }

        private static void ConfigureLatencySimulator(
            TransportManager transportManager)
        {
            SerializedObject serializedTransport =
                new SerializedObject(
                    transportManager);

            SerializedProperty latencySimulator =
                serializedTransport.FindProperty(
                    "_latencySimulator");

            if (latencySimulator == null)
            {
                Debug.LogError(
                    "FishNet's serialized latency simulator " +
                    "could not be found.");

                return;
            }

            SetRelativeBool(
                latencySimulator,
                "_enabled",
                false);

            // This prevents the Editor Host's own player from being delayed.
            // Server traffic to the standalone remote client is still simulated.
            SetRelativeBool(
                latencySimulator,
                "_simulateHost",
                false);

            SetRelativeLong(
                latencySimulator,
                "_latency",
                0L);

            SetRelativeDouble(
                latencySimulator,
                "_packetLoss",
                0d);

            SetRelativeDouble(
                latencySimulator,
                "_outOfOrder",
                0d);

            serializedTransport
                .ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetRelativeBool(
            SerializedProperty parent,
            string propertyName,
            bool value)
        {
            SerializedProperty property =
                parent.FindPropertyRelative(
                    propertyName);

            if (property != null)
            {
                property.boolValue = value;
            }
        }

        private static void SetRelativeLong(
            SerializedProperty parent,
            string propertyName,
            long value)
        {
            SerializedProperty property =
                parent.FindPropertyRelative(
                    propertyName);

            if (property != null)
            {
                property.longValue = value;
            }
        }

        private static void SetRelativeDouble(
            SerializedProperty parent,
            string propertyName,
            double value)
        {
            SerializedProperty property =
                parent.FindPropertyRelative(
                    propertyName);

            if (property != null)
            {
                property.doubleValue = value;
            }
        }
    }
}
#endif
