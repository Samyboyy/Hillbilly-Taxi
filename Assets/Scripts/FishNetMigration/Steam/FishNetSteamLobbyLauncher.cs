using FishNet;
using UnityEngine;

namespace HillbillyTaxi.FishNetMigration.Steam
{
    /// <summary>
    /// Temporary lobby and local-development UI.
    ///
    /// This is intentionally IMGUI for integration proof. It will be replaced by
    /// the art-directed front-end after the Steam lifecycle is proven.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FishNetSteamLobbyLauncher : MonoBehaviour
    {
        private enum MenuMode
        {
            LocalDevelopment = 0,
            SteamLobbies = 1
        }

        [SerializeField] private FishNetSteamLobbyService lobbyService;
        [SerializeField] private MenuMode selectedMode =
            MenuMode.SteamLobbies;

        [SerializeField] private string localAddress = "localhost";

        private Vector2 _browserScroll;
        private Vector2 _memberScroll;

        private void OnGUI()
        {
            if (lobbyService == null)
            {
                return;
            }

            GUILayout.BeginArea(
                new Rect(12f, 12f, 520f, 690f),
                GUI.skin.box);

            GUILayout.Label("HILLBILLY TAXI — MULTIPLAYER");
            GUILayout.Space(5f);

            DrawModeTabs();
            GUILayout.Space(8f);

            bool networkRunning =
                InstanceFinder.IsClientStarted ||
                InstanceFinder.IsServerStarted;

            if (lobbyService.IsInLobby)
            {
                DrawCurrentSteamLobby();
            }
            else if (networkRunning)
            {
                DrawRunningLocalSession();
            }
            else if (selectedMode ==
                     MenuMode.LocalDevelopment)
            {
                DrawLocalDevelopment();
            }
            else
            {
                DrawSteamLobbyMenu();
            }

            GUILayout.Space(10f);
            GUILayout.Label($"Status: {lobbyService.Status}");

            GUILayout.EndArea();
        }

        private void DrawModeTabs()
        {
            GUILayout.BeginHorizontal();

            bool previousEnabled = GUI.enabled;

            GUI.enabled =
                selectedMode !=
                MenuMode.LocalDevelopment &&
                !lobbyService.IsInLobby &&
                !InstanceFinder.IsClientStarted &&
                !InstanceFinder.IsServerStarted;

            if (GUILayout.Button(
                    "LOCAL / TUGBOAT",
                    GUILayout.Height(30f)))
            {
                selectedMode =
                    MenuMode.LocalDevelopment;
            }

            GUI.enabled =
                selectedMode !=
                MenuMode.SteamLobbies &&
                !lobbyService.IsInLobby &&
                !InstanceFinder.IsClientStarted &&
                !InstanceFinder.IsServerStarted;

            if (GUILayout.Button(
                    "STEAM LOBBIES",
                    GUILayout.Height(30f)))
            {
                selectedMode =
                    MenuMode.SteamLobbies;
            }

            GUI.enabled = previousEnabled;
            GUILayout.EndHorizontal();
        }

        private void DrawLocalDevelopment()
        {
            GUILayout.Label("Local development transport: Tugboat");
            GUILayout.Label("Client address");

            localAddress =
                GUILayout.TextField(
                    localAddress ?? "localhost",
                    GUILayout.Height(26f));

            GUILayout.Space(5f);

            if (GUILayout.Button(
                    "Start Local Host",
                    GUILayout.Height(34f)))
            {
                lobbyService.StartLocalHost();
            }

            if (GUILayout.Button(
                    "Start Local Client",
                    GUILayout.Height(34f)))
            {
                lobbyService.StartLocalClient(
                    localAddress);
            }

            if (GUILayout.Button(
                    "Start Local Server",
                    GUILayout.Height(34f)))
            {
                lobbyService.StartLocalServer();
            }
        }

        private void DrawSteamLobbyMenu()
        {
            if (!lobbyService.SteamReady)
            {
                GUILayout.Label("Steam is unavailable.");

                if (GUILayout.Button(
                        "Retry Steam Initialization",
                        GUILayout.Height(34f)))
                {
                    lobbyService.RetrySteamInitialization();
                }

                return;
            }

            HillbillyTaxiSteamBootstrap bootstrap =
                GetComponent<
                    HillbillyTaxiSteamBootstrap>();

            if (bootstrap != null)
            {
                GUILayout.Label(
                    $"Steam user: {bootstrap.PersonaName}");

                GUILayout.Label(
                    $"SteamID64: {bootstrap.SteamId}");
            }

            GUILayout.Space(8f);
            GUILayout.Label("HOST A FOUR-PLAYER LOBBY");

            bool previousEnabled = GUI.enabled;
            GUI.enabled = !lobbyService.IsBusy;

            GUILayout.BeginHorizontal();

            if (GUILayout.Button(
                    "Public",
                    GUILayout.Height(38f)))
            {
                lobbyService.CreateSteamLobby(
                    HillbillyTaxiLobbyPrivacy.Public);
            }

            if (GUILayout.Button(
                    "Friends Only",
                    GUILayout.Height(38f)))
            {
                lobbyService.CreateSteamLobby(
                    HillbillyTaxiLobbyPrivacy.FriendsOnly);
            }

            if (GUILayout.Button(
                    "Private",
                    GUILayout.Height(38f)))
            {
                lobbyService.CreateSteamLobby(
                    HillbillyTaxiLobbyPrivacy.Private);
            }

            GUILayout.EndHorizontal();

            GUILayout.Space(10f);
            GUILayout.Label("PUBLIC LOBBIES");

            if (GUILayout.Button(
                    lobbyService.IsBusy
                        ? "Searching..."
                        : "Refresh Public Lobbies",
                    GUILayout.Height(32f)))
            {
                lobbyService.BrowsePublicLobbies();
            }

            GUI.enabled = previousEnabled;

            DrawPublicLobbyBrowser();
        }

        private void DrawPublicLobbyBrowser()
        {
            _browserScroll =
                GUILayout.BeginScrollView(
                    _browserScroll,
                    GUI.skin.box,
                    GUILayout.Height(300f));

            if (lobbyService.PublicLobbies.Count == 0)
            {
                GUILayout.Label(
                    "No matching public lobbies loaded.");
            }

            for (int index = 0;
                 index <
                    lobbyService.PublicLobbies.Count;
                 index++)
            {
                HillbillyTaxiLobbyListing listing =
                    lobbyService.PublicLobbies[index];

                GUILayout.BeginVertical(
                    GUI.skin.box);

                GUILayout.Label(listing.LobbyName);
                GUILayout.Label(
                    $"Host: {listing.HostName}");

                GUILayout.Label(
                    $"Players: {listing.MemberCount} / " +
                    $"{listing.MaximumMembers}");

                bool previousEnabled = GUI.enabled;
                GUI.enabled = !lobbyService.IsBusy;

                if (GUILayout.Button(
                        "Join Lobby",
                        GUILayout.Height(30f)))
                {
                    lobbyService.JoinSteamLobby(
                        listing.LobbyId);
                }

                GUI.enabled = previousEnabled;
                GUILayout.EndVertical();
            }

            GUILayout.EndScrollView();
        }

        private void DrawCurrentSteamLobby()
        {
            GUILayout.Label("STEAM LOBBY");
            GUILayout.Label(
                $"Name: {lobbyService.CurrentLobbyName}");

            GUILayout.Label(
                $"Privacy: {lobbyService.CurrentPrivacyName}");

            GUILayout.Label(
                $"Lobby ID: {lobbyService.CurrentLobbyId}");

            GUILayout.Label(
                lobbyService.IsLobbyOwner
                    ? "Role: Host"
                    : "Role: Client");

            GUILayout.Label(
                $"FishNet: " +
                $"{GetFishNetRole()}");

            GUILayout.Space(6f);
            GUILayout.Label(
                $"PLAYERS ({lobbyService.CurrentMembers.Count} / 4)");

            _memberScroll =
                GUILayout.BeginScrollView(
                    _memberScroll,
                    GUI.skin.box,
                    GUILayout.Height(170f));

            foreach (
                string member
                in lobbyService.CurrentMembers)
            {
                GUILayout.Label(member);
            }

            GUILayout.EndScrollView();
            GUILayout.Space(8f);

            if (GUILayout.Button(
                    "Invite Friends Through Steam",
                    GUILayout.Height(38f)))
            {
                lobbyService.OpenInviteOverlay();
            }

            if (GUILayout.Button(
                    "Leave Lobby",
                    GUILayout.Height(38f)))
            {
                lobbyService.LeaveGame();
            }

            GUILayout.Space(6f);
            GUILayout.Label(
                "The session host runs the game server. " +
                "If they leave, everyone returns to the menu.");
        }

        private void DrawRunningLocalSession()
        {
            GUILayout.Label("LOCAL SESSION");
            GUILayout.Label(
                $"FishNet role: {GetFishNetRole()}");

            if (GUILayout.Button(
                    "Shutdown Local Session",
                    GUILayout.Height(38f)))
            {
                lobbyService.LeaveGame();
            }
        }

        private static string GetFishNetRole()
        {
            bool server =
                InstanceFinder.IsServerStarted;

            bool client =
                InstanceFinder.IsClientStarted;

            if (server && client)
            {
                return "Host";
            }

            if (server)
            {
                return "Server";
            }

            if (client)
            {
                return "Client";
            }

            return "Connecting";
        }

        private void Reset()
        {
            if (lobbyService == null)
            {
                lobbyService =
                    GetComponent<
                        FishNetSteamLobbyService>();
            }
        }
    }
}
