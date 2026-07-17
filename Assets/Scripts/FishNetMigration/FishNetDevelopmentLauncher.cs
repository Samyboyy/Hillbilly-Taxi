using FishNet;
using FishNet.Managing;
using UnityEngine;

namespace HillbillyTaxi.FishNetMigration
{
    /// <summary>
    /// Temporary local-development connection UI.
    ///
    /// Tugboat uses localhost by default, so this intentionally avoids an IP field.
    /// Steam lobbies and FishySteamworks will replace this UI later.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FishNetDevelopmentLauncher : MonoBehaviour
    {
        [SerializeField] private NetworkManager networkManager;

        private void Awake()
        {
            ResolveNetworkManager();
        }

        private void OnGUI()
        {
            if (networkManager == null)
            {
                return;
            }

            GUILayout.BeginArea(
                new Rect(12f, 12f, 240f, 230f),
                GUI.skin.box);

            GUILayout.Label("Hillbilly Taxi - FishNet Test");

            bool serverStarted = InstanceFinder.IsServerStarted;
            bool clientStarted = InstanceFinder.IsClientStarted;

            if (!serverStarted && !clientStarted)
            {
                if (GUILayout.Button("Start Host"))
                {
                    StartHost();
                }

                if (GUILayout.Button("Start Client"))
                {
                    networkManager.ClientManager.StartConnection();
                }

                if (GUILayout.Button("Start Server"))
                {
                    networkManager.ServerManager.StartConnection();
                }
            }
            else
            {
                string mode =
                    serverStarted && clientStarted
                        ? "Host"
                        : serverStarted
                            ? "Server"
                            : "Client";

                GUILayout.Label($"Running as: {mode}");

                if (serverStarted)
                {
                    int playerCount =
                        networkManager.ServerManager.Clients.Count;

                    GUILayout.Label($"Connected players: {playerCount} / 4");
                }

                if (GUILayout.Button("Shutdown"))
                {
                    Shutdown();
                }
            }

            GUILayout.EndArea();
        }

        private void StartHost()
        {
            // A FishNet host is the server and local client running together.
            if (!InstanceFinder.IsServerStarted)
            {
                networkManager.ServerManager.StartConnection();
            }

            if (!InstanceFinder.IsClientStarted)
            {
                networkManager.ClientManager.StartConnection();
            }
        }

        private void Shutdown()
        {
            if (InstanceFinder.IsClientStarted)
            {
                networkManager.ClientManager.StopConnection();
            }

            if (InstanceFinder.IsServerStarted)
            {
                networkManager.ServerManager.StopConnection(true);
            }
        }

        private void ResolveNetworkManager()
        {
            if (networkManager == null)
            {
                networkManager =
                    FindFirstObjectByType<NetworkManager>();
            }
        }

        private void Reset()
        {
            ResolveNetworkManager();
        }
    }
}
