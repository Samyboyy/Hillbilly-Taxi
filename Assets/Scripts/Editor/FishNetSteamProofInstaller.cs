#if UNITY_EDITOR
using FishNet.Managing;
using FishNet.Managing.Transporting;
using FishNet.Transporting;
using FishNet.Transporting.Multipass;
using FishNet.Transporting.Tugboat;
using HillbillyTaxi.FishNetMigration;
using HillbillyTaxi.FishNetMigration.Steam;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

using FishySteamTransport =
    FishySteamworks.FishySteamworks;

namespace HillbillyTaxi.EditorTools
{
    public static class FishNetSteamProofInstaller
    {
        [MenuItem(
            "Hillbilly Taxi/Steam/Phase 7/Install Steam Transport Proof")]
        public static void InstallSteamTransportProof()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogWarning(
                    "Exit Play Mode before installing the Steam proof.");

                return;
            }

            NetworkManager networkManager =
                Object.FindFirstObjectByType<NetworkManager>();

            if (networkManager == null)
            {
                Debug.LogError(
                    "No FishNet NetworkManager exists in the active scene.");

                return;
            }

            GameObject managerObject =
                networkManager.gameObject;

            TransportManager transportManager =
                GetOrAdd<TransportManager>(
                    managerObject);

            Tugboat tugboat =
                GetOrAdd<Tugboat>(
                    managerObject);

            FishySteamTransport fishySteamworks =
                GetOrAdd<FishySteamTransport>(
                    managerObject);

            Multipass multipass =
                GetOrAdd<Multipass>(
                    managerObject);

            HillbillyTaxiSteamBootstrap steamBootstrap =
                GetOrAdd<HillbillyTaxiSteamBootstrap>(
                    managerObject);

            FishNetSteamProofLauncher launcher =
                GetOrAdd<FishNetSteamProofLauncher>(
                    managerObject);

            ConfigureFishySteamworks(
                fishySteamworks);

            ConfigureMultipass(
                multipass,
                tugboat,
                fishySteamworks);

            transportManager.Transport =
                multipass;

            RemoveOldDevelopmentLauncher(
                managerObject);

            ConfigureLauncher(
                launcher,
                networkManager,
                multipass,
                tugboat,
                fishySteamworks,
                steamBootstrap);

            EditorUtility.SetDirty(
                managerObject);

            EditorUtility.SetDirty(
                transportManager);

            EditorUtility.SetDirty(
                tugboat);

            EditorUtility.SetDirty(
                fishySteamworks);

            EditorUtility.SetDirty(
                multipass);

            EditorUtility.SetDirty(
                steamBootstrap);

            EditorUtility.SetDirty(
                launcher);

            Scene activeScene =
                SceneManager.GetActiveScene();

            if (activeScene.IsValid())
            {
                EditorSceneManager.MarkSceneDirty(
                    activeScene);
            }

            Selection.activeGameObject =
                managerObject;

            Debug.Log(
                "Installed FishNet Tugboat + FishySteamworks proof. " +
                "Multipass is now the active transport, with only the " +
                "chosen server transport started by the proof launcher. " +
                "Save FishNetProof.unity.",
                managerObject);
        }

        private static void ConfigureFishySteamworks(
            FishySteamTransport fishySteamworks)
        {
            SerializedObject serialized =
                new(fishySteamworks);

            SetBool(
                serialized,
                "_peerToPeer",
                true);

            SetInteger(
                serialized,
                "_maximumClients",
                4);

            SetInteger(
                serialized,
                "_port",
                7770);

            SetString(
                serialized,
                "_clientAddress",
                string.Empty);

            SetString(
                serialized,
                "_serverBindAddress",
                string.Empty);

            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void ConfigureMultipass(
            Multipass multipass,
            Tugboat tugboat,
            FishySteamTransport fishySteamworks)
        {
            multipass.GlobalServerActions = false;

            SerializedObject serialized =
                new(multipass);

            SerializedProperty transports =
                serialized.FindProperty(
                    "_transports");

            transports.arraySize = 2;

            transports
                .GetArrayElementAtIndex(0)
                .objectReferenceValue = tugboat;

            transports
                .GetArrayElementAtIndex(1)
                .objectReferenceValue =
                    fishySteamworks;

            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void ConfigureLauncher(
            FishNetSteamProofLauncher launcher,
            NetworkManager networkManager,
            Multipass multipass,
            Tugboat tugboat,
            FishySteamTransport fishySteamworks,
            HillbillyTaxiSteamBootstrap steamBootstrap)
        {
            SerializedObject serialized =
                new(launcher);

            serialized
                .FindProperty("networkManager")
                .objectReferenceValue =
                    networkManager;

            serialized
                .FindProperty("multipass")
                .objectReferenceValue =
                    multipass;

            serialized
                .FindProperty("tugboat")
                .objectReferenceValue =
                    tugboat;

            serialized
                .FindProperty("fishySteamworks")
                .objectReferenceValue =
                    fishySteamworks;

            serialized
                .FindProperty("steamBootstrap")
                .objectReferenceValue =
                    steamBootstrap;

            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void RemoveOldDevelopmentLauncher(
            GameObject managerObject)
        {
            FishNetDevelopmentLauncher oldLauncher =
                managerObject.GetComponent<
                    FishNetDevelopmentLauncher>();

            if (oldLauncher != null)
            {
                Undo.DestroyObjectImmediate(
                    oldLauncher);
            }
        }

        private static T GetOrAdd<T>(
            GameObject target)
            where T : Component
        {
            T component =
                target.GetComponent<T>();

            if (component == null)
            {
                component =
                    Undo.AddComponent<T>(
                        target);
            }

            return component;
        }

        private static void SetBool(
            SerializedObject serialized,
            string propertyName,
            bool value)
        {
            SerializedProperty property =
                serialized.FindProperty(
                    propertyName);

            if (property != null)
            {
                property.boolValue = value;
            }
        }

        private static void SetInteger(
            SerializedObject serialized,
            string propertyName,
            int value)
        {
            SerializedProperty property =
                serialized.FindProperty(
                    propertyName);

            if (property != null)
            {
                property.intValue = value;
            }
        }

        private static void SetString(
            SerializedObject serialized,
            string propertyName,
            string value)
        {
            SerializedProperty property =
                serialized.FindProperty(
                    propertyName);

            if (property != null)
            {
                property.stringValue = value;
            }
        }
    }
}
#endif
