using FishNet;
using FishNet.Managing;
using FishNet.Transporting;
using FishNet.Transporting.Multipass;
using FishNet.Transporting.Tugboat;
using UnityEngine;

using FishySteamTransport =
    FishySteamworks.FishySteamworks;

namespace HillbillyTaxi.FishNetMigration.Steam
{
    /// <summary>
    /// Temporary proof UI for selecting Tugboat or FishySteamworks.
    ///
    /// Multipass remains FishNet's active transport, while this launcher starts
    /// only the chosen server transport and assigns the matching client transport.
    /// Proper Steam lobbies and invitations replace the manual Steam-ID field in
    /// the next phase.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FishNetSteamProofLauncher : MonoBehaviour
    {
        private enum ProofMode
        {
            TugboatLocal = 0,
            SteamPeerToPeer = 1
        }

        [Header("References")]
        [SerializeField] private NetworkManager networkManager;
        [SerializeField] private Multipass multipass;
        [SerializeField] private Tugboat tugboat;
        [SerializeField] private FishySteamTransport fishySteamworks;
        [SerializeField] private HillbillyTaxiSteamBootstrap steamBootstrap;

        [Header("Defaults")]
        [SerializeField] private ProofMode selectedMode =
            ProofMode.TugboatLocal;

        [SerializeField] private string localAddress = "localhost";
        [SerializeField] private string hostSteamId = string.Empty;

        private int _activeServerTransportIndex = -1;
        private string _status = "Choose a transport mode.";

        private void Awake()
        {
            ResolveReferences();
            SubscribeToConnectionEvents();
        }

        private void OnDestroy()
        {
            UnsubscribeFromConnectionEvents();
        }

        private void OnGUI()
        {
            if (!ReferencesReady())
            {
                return;
            }

            GUILayout.BeginArea(
                new Rect(12f, 12f, 430f, 520f),
                GUI.skin.box);

            GUILayout.Label("Hillbilly Taxi — Connection Proof");
            GUILayout.Space(4f);

            bool serverStarted =
                InstanceFinder.IsServerStarted;

            bool clientStarted =
                InstanceFinder.IsClientStarted;

            if (!serverStarted &&
                !clientStarted)
            {
                DrawOfflineControls();
            }
            else
            {
                DrawRunningControls(
                    serverStarted,
                    clientStarted);
            }

            GUILayout.Space(10f);
            GUILayout.Label($"Status: {_status}");

            GUILayout.EndArea();
        }

        private void DrawOfflineControls()
        {
            GUILayout.Label("Transport");

            GUILayout.BeginHorizontal();

            bool previousEnabled = GUI.enabled;

            GUI.enabled =
                selectedMode != ProofMode.TugboatLocal;

            if (GUILayout.Button(
                    "Local / Tugboat",
                    GUILayout.Height(28f)))
            {
                selectedMode =
                    ProofMode.TugboatLocal;

                _status =
                    "Tugboat selected for local development.";
            }

            GUI.enabled =
                selectedMode != ProofMode.SteamPeerToPeer;

            if (GUILayout.Button(
                    "Steam P2P",
                    GUILayout.Height(28f)))
            {
                selectedMode =
                    ProofMode.SteamPeerToPeer;

                _status =
                    "FishySteamworks Steam P2P selected.";
            }

            GUI.enabled = previousEnabled;

            // This EndHorizontal was missing in the original Phase 7 package.
            // Without it every later control was forced into the same row, which
            // caused the Invalid GUILayout state console spam.
            GUILayout.EndHorizontal();

            GUILayout.Space(10f);

            if (selectedMode ==
                ProofMode.TugboatLocal)
            {
                DrawTugboatControls();
            }
            else
            {
                DrawSteamControls();
            }
        }

        private void DrawTugboatControls()
        {
            GUILayout.Label("Development transport: Tugboat");
            GUILayout.Label($"Client address: {localAddress}");
            GUILayout.Space(4f);

            if (GUILayout.Button(
                    "Start Local Host",
                    GUILayout.Height(30f)))
            {
                StartHost(
                    tugboat,
                    GetTransportIndex(tugboat));
            }

            if (GUILayout.Button(
                    "Start Local Client",
                    GUILayout.Height(30f)))
            {
                tugboat.SetClientAddress(localAddress);

                StartClient(
                    tugboat);
            }

            if (GUILayout.Button(
                    "Start Local Server",
                    GUILayout.Height(30f)))
            {
                StartServer(
                    GetTransportIndex(tugboat));
            }
        }

        private void DrawSteamControls()
        {
            GUILayout.Label("Steam transport: FishySteamworks");

            if (!steamBootstrap.Initialized)
            {
                GUILayout.Label("Steam status: unavailable");

                if (!string.IsNullOrWhiteSpace(
                        steamBootstrap.LastError))
                {
                    GUILayout.TextArea(
                        steamBootstrap.LastError);
                }

                if (GUILayout.Button(
                        "Retry Steam Initialization",
                        GUILayout.Height(30f)))
                {
                    steamBootstrap.InitializeSteam();
                }

                return;
            }

            GUILayout.Label(
                $"Steam user: {steamBootstrap.PersonaName}");

            GUILayout.Label(
                $"Your SteamID64: {steamBootstrap.SteamId}");

            if (GUILayout.Button(
                    "Copy My SteamID64",
                    GUILayout.Height(30f)))
            {
                GUIUtility.systemCopyBuffer =
                    steamBootstrap.SteamId.ToString();

                _status =
                    "Local SteamID64 copied to the clipboard.";
            }

            GUILayout.Space(8f);

            if (GUILayout.Button(
                    "Start Steam Host",
                    GUILayout.Height(32f)))
            {
                StartHost(
                    fishySteamworks,
                    GetTransportIndex(fishySteamworks));
            }

            GUILayout.Space(8f);

            GUILayout.Label("Host SteamID64");

            hostSteamId =
                GUILayout.TextField(
                    hostSteamId ?? string.Empty,
                    GUILayout.Height(26f));

            if (GUILayout.Button(
                    "Join Steam Host",
                    GUILayout.Height(32f)))
            {
                JoinSteamHost();
            }
        }

        private void DrawRunningControls(
            bool serverStarted,
            bool clientStarted)
        {
            string role =
                serverStarted && clientStarted
                    ? "Host"
                    : serverStarted
                        ? "Server"
                        : "Client";

            GUILayout.Label($"Running as: {role}");

            string transportName =
                multipass.ClientTransport != null
                    ? multipass.ClientTransport
                        .GetType().Name
                    : "Unknown";

            GUILayout.Label(
                $"Client transport: {transportName}");

            if (steamBootstrap.Initialized)
            {
                GUILayout.Label(
                    $"Steam: {steamBootstrap.IdentityText}");
            }

            if (serverStarted)
            {
                GUILayout.Label(
                    $"Connected players: " +
                    $"{networkManager.ServerManager.Clients.Count} / 4");
            }

            GUILayout.Space(8f);

            if (GUILayout.Button(
                    "Shutdown",
                    GUILayout.Height(32f)))
            {
                ShutdownConnections();
            }
        }

        private void StartHost(
            Transport clientTransport,
            int serverTransportIndex)
        {
            if (serverTransportIndex < 0)
            {
                _status =
                    "The selected transport is not in Multipass.";

                return;
            }

            if (clientTransport == fishySteamworks &&
                !steamBootstrap.Initialized)
            {
                _status =
                    "Steam must initialize before starting a Steam host.";

                return;
            }

            multipass.SetClientTransport(
                clientTransport);

            _activeServerTransportIndex =
                serverTransportIndex;

            bool serverStarting =
                multipass.StartConnection(
                    server: true,
                    index: serverTransportIndex);

            if (!serverStarting)
            {
                _status =
                    "The selected server transport refused to start.";

                _activeServerTransportIndex = -1;
                return;
            }

            bool clientStarting =
                networkManager.ClientManager.StartConnection();

            _status =
                clientStarting
                    ? $"Starting host with " +
                      $"{clientTransport.GetType().Name}."
                    : "Server started, but the local client " +
                      "refused to start.";
        }

        private void StartServer(
            int serverTransportIndex)
        {
            if (serverTransportIndex < 0)
            {
                _status =
                    "The selected transport is not in Multipass.";

                return;
            }

            _activeServerTransportIndex =
                serverTransportIndex;

            bool starting =
                multipass.StartConnection(
                    server: true,
                    index: serverTransportIndex);

            _status =
                starting
                    ? "Starting local Tugboat server."
                    : "The Tugboat server refused to start.";
        }

        private void StartClient(
            Transport clientTransport)
        {
            multipass.SetClientTransport(
                clientTransport);

            bool starting =
                networkManager.ClientManager.StartConnection();

            _status =
                starting
                    ? $"Starting client with " +
                      $"{clientTransport.GetType().Name}."
                    : "The client transport refused to start.";
        }

        private void JoinSteamHost()
        {
            if (!steamBootstrap.Initialized)
            {
                _status =
                    "Steam must initialize before joining.";

                return;
            }

            string trimmedId =
                hostSteamId?.Trim();

            if (!ulong.TryParse(
                    trimmedId,
                    out ulong parsedSteamId) ||
                parsedSteamId == 0)
            {
                _status =
                    "Enter the host's numeric SteamID64.";

                return;
            }

            if (parsedSteamId ==
                steamBootstrap.SteamId)
            {
                _status =
                    "A Steam client cannot connect to the same " +
                    "Steam account. Use the second computer/account.";

                return;
            }

            fishySteamworks.SetClientAddress(
                parsedSteamId.ToString());

            StartClient(
                fishySteamworks);
        }

        private void ShutdownConnections()
        {
            if (InstanceFinder.IsClientStarted)
            {
                networkManager.ClientManager.StopConnection();
            }

            if (InstanceFinder.IsServerStarted &&
                _activeServerTransportIndex >= 0)
            {
                multipass.StopServerConnection(
                    sendDisconnectMessage: true,
                    transportIndex:
                        _activeServerTransportIndex);
            }

            _activeServerTransportIndex = -1;
            _status = "Connections stopped.";
        }

        private int GetTransportIndex(
            Transport transport)
        {
            for (int index = 0;
                 index < multipass.Transports.Count;
                 index++)
            {
                if (multipass.Transports[index] ==
                    transport)
                {
                    return index;
                }
            }

            return -1;
        }

        private void SubscribeToConnectionEvents()
        {
            if (networkManager == null)
            {
                return;
            }

            networkManager.ClientManager
                .OnClientConnectionState +=
                HandleClientConnectionState;

            networkManager.ServerManager
                .OnServerConnectionState +=
                HandleServerConnectionState;
        }

        private void UnsubscribeFromConnectionEvents()
        {
            if (networkManager == null)
            {
                return;
            }

            networkManager.ClientManager
                .OnClientConnectionState -=
                HandleClientConnectionState;

            networkManager.ServerManager
                .OnServerConnectionState -=
                HandleServerConnectionState;
        }

        private void HandleClientConnectionState(
            ClientConnectionStateArgs args)
        {
            _status =
                $"Client connection is " +
                $"{args.ConnectionState.ToString().ToLowerInvariant()}.";
        }

        private void HandleServerConnectionState(
            ServerConnectionStateArgs args)
        {
            _status =
                $"Server transport {args.TransportIndex} is " +
                $"{args.ConnectionState.ToString().ToLowerInvariant()}.";
        }

        private bool ReferencesReady()
        {
            if (networkManager != null &&
                multipass != null &&
                tugboat != null &&
                fishySteamworks != null &&
                steamBootstrap != null)
            {
                return true;
            }

            ResolveReferences();

            if (networkManager == null ||
                multipass == null ||
                tugboat == null ||
                fishySteamworks == null ||
                steamBootstrap == null)
            {
                Debug.LogError(
                    "Steam proof launcher references are incomplete. " +
                    "Run the Phase 7 installer again.",
                    this);

                enabled = false;
                return false;
            }

            return true;
        }

        private void ResolveReferences()
        {
            if (networkManager == null)
            {
                networkManager =
                    GetComponent<NetworkManager>();
            }

            if (multipass == null)
            {
                multipass =
                    GetComponent<Multipass>();
            }

            if (tugboat == null)
            {
                tugboat =
                    GetComponent<Tugboat>();
            }

            if (fishySteamworks == null)
            {
                fishySteamworks =
                    GetComponent<FishySteamTransport>();
            }

            if (steamBootstrap == null)
            {
                steamBootstrap =
                    GetComponent<
                        HillbillyTaxiSteamBootstrap>();
            }
        }

        private void Reset()
        {
            ResolveReferences();
        }
    }
}
