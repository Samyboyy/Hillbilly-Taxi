#if UNITY_EDITOR
using FishNet.Managing;
using FishNet.Transporting.Multipass;
using FishNet.Transporting.Tugboat;
using HillbillyTaxi.FishNetMigration.Steam;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

using FishySteamTransport =
    FishySteamworks.FishySteamworks;

namespace HillbillyTaxi.EditorTools
{
    public static class FishNetSteamLobbyInstaller
    {
        [MenuItem(
            "Hillbilly Taxi/Steam/Phase 8/Install Steam Lobby Flow")]
        public static void InstallSteamLobbyFlow()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogWarning(
                    "Exit Play Mode before installing Steam lobbies.");

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

            Multipass multipass =
                managerObject.GetComponent<Multipass>();

            Tugboat tugboat =
                managerObject.GetComponent<Tugboat>();

            FishySteamTransport fishySteamworks =
                managerObject.GetComponent<
                    FishySteamTransport>();

            HillbillyTaxiSteamBootstrap steamBootstrap =
                managerObject.GetComponent<
                    HillbillyTaxiSteamBootstrap>();

            if (multipass == null ||
                tugboat == null ||
                fishySteamworks == null ||
                steamBootstrap == null)
            {
                Debug.LogError(
                    "Phase 7 transport components are incomplete. " +
                    "Run the Phase 7 installer before Phase 8.");

                return;
            }

            FishNetSteamLobbyService service =
                managerObject.GetComponent<
                    FishNetSteamLobbyService>();

            if (service == null)
            {
                service =
                    Undo.AddComponent<
                        FishNetSteamLobbyService>(
                        managerObject);
            }

            FishNetSteamLobbyLauncher launcher =
                managerObject.GetComponent<
                    FishNetSteamLobbyLauncher>();

            if (launcher == null)
            {
                launcher =
                    Undo.AddComponent<
                        FishNetSteamLobbyLauncher>(
                        managerObject);
            }

            FishNetSteamProofLauncher proofLauncher =
                managerObject.GetComponent<
                    FishNetSteamProofLauncher>();

            if (proofLauncher != null)
            {
                Undo.DestroyObjectImmediate(
                    proofLauncher);
            }

            ConfigureService(
                service,
                networkManager,
                multipass,
                tugboat,
                fishySteamworks,
                steamBootstrap);

            ConfigureLauncher(
                launcher,
                service);

            EditorUtility.SetDirty(managerObject);
            EditorUtility.SetDirty(service);
            EditorUtility.SetDirty(launcher);

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
                "Installed the Phase 8 Steam lobby flow. " +
                "The manual SteamID proof UI was removed; Tugboat, " +
                "Multipass and FishySteamworks were preserved. " +
                "Save FishNetProof.unity.",
                managerObject);
        }

        private static void ConfigureService(
            FishNetSteamLobbyService service,
            NetworkManager networkManager,
            Multipass multipass,
            Tugboat tugboat,
            FishySteamTransport fishySteamworks,
            HillbillyTaxiSteamBootstrap steamBootstrap)
        {
            SerializedObject serialized =
                new(service);

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

        private static void ConfigureLauncher(
            FishNetSteamLobbyLauncher launcher,
            FishNetSteamLobbyService service)
        {
            SerializedObject serialized =
                new(launcher);

            serialized
                .FindProperty("lobbyService")
                .objectReferenceValue =
                    service;

            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
#endif
