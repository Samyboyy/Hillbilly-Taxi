using Unity.Netcode;
using UnityEngine;

namespace HillbillyTaxi.Networking
{
    /// <summary>
    /// Temporary development UI for starting a host, client, or dedicated server.
    /// Remove or replace this when the real lobby flow is built.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DevelopmentNetworkLauncher : MonoBehaviour
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private void OnGUI()
        {
            NetworkManager manager = NetworkManager.Singleton;

            if (manager == null)
            {
                return;
            }

            GUILayout.BeginArea(new Rect(12f, 12f, 220f, 190f), GUI.skin.box);
            GUILayout.Label("Hillbilly Taxi - Network Test");

            if (!manager.IsListening)
            {
                if (GUILayout.Button("Start Host"))
                {
                    manager.StartHost();
                }

                if (GUILayout.Button("Start Client"))
                {
                    manager.StartClient();
                }

                if (GUILayout.Button("Start Server"))
                {
                    manager.StartServer();
                }
            }
            else
            {
                string mode = manager.IsHost ? "Host" : manager.IsServer ? "Server" : "Client";
                GUILayout.Label($"Running as: {mode}");
                GUILayout.Label($"Local client ID: {manager.LocalClientId}");

                if (GUILayout.Button("Shutdown"))
                {
                    manager.Shutdown();
                }
            }

            GUILayout.EndArea();
        }
#endif
    }
}
