#if UNITY_EDITOR || UNITY_STANDALONE_WIN || UNITY_STANDALONE_LINUX || UNITY_STANDALONE_OSX
#define HILLBILLY_STEAM_SUPPORTED
#endif

using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using FishNet;
using FishNet.Managing;
using FishNet.Transporting;
using FishNet.Transporting.Multipass;
using FishNet.Transporting.Tugboat;
using UnityEngine;

#if HILLBILLY_STEAM_SUPPORTED
using Steamworks;
#endif

using FishySteamTransport =
    FishySteamworks.FishySteamworks;

namespace HillbillyTaxi.FishNetMigration.Steam
{
    /// <summary>
    /// Steam lobby lifecycle layered over the already-proven FishySteamworks
    /// peer-to-peer connection path.
    ///
    /// The lobby owner is the FishNet listen-server host. Other lobby members
    /// automatically connect FishySteamworks to the owner's SteamID64.
    /// </summary>
    [DefaultExecutionOrder(-9000)]
    [DisallowMultipleComponent]
    public sealed class FishNetSteamLobbyService : MonoBehaviour
    {
        private const string ProductKey = "hillbilly_product";
        private const string ProductValue = "hillbilly_taxi";
        private const string ProtocolKey = "hillbilly_protocol";
        private const string StateKey = "hillbilly_state";
        private const string LobbyNameKey = "hillbilly_name";
        private const string HostNameKey = "hillbilly_host_name";
        private const string HostSteamIdKey = "hillbilly_host_id";
        private const string PrivacyKey = "hillbilly_privacy";
        private const string MembersKey = "hillbilly_members";
        private const string OpenState = "open";
        private const string ClosingState = "closing";

        private enum PendingOperation
        {
            None,
            CreatingLobby,
            JoiningLobby,
            BrowsingLobbies
        }

        [Header("References")]
        [SerializeField] private NetworkManager networkManager;
        [SerializeField] private Multipass multipass;
        [SerializeField] private Tugboat tugboat;
        [SerializeField] private FishySteamTransport fishySteamworks;
        [SerializeField] private HillbillyTaxiSteamBootstrap steamBootstrap;

        [Header("Lobby compatibility")]
        [SerializeField] private string networkProtocol = "1";
        [SerializeField, Range(2, 4)] private int maximumPlayers = 4;
        [SerializeField, Range(1, 100)] private int maximumBrowserResults = 30;

        private readonly List<HillbillyTaxiLobbyListing>
            _publicLobbies = new();

        private readonly List<string>
            _currentMembers = new();

        private PendingOperation _pendingOperation;
        private HillbillyTaxiLobbyPrivacy _pendingPrivacy;
        private ulong _currentLobbyId;
        private ulong _sessionHostSteamId;
        private int _activeServerTransportIndex = -1;
        private bool _callbacksRegistered;
        private bool _shuttingDown;

        private string _currentLobbyName = string.Empty;
        private string _currentPrivacyName = string.Empty;
        private string _status = "Choose local development or Steam lobbies.";

#if HILLBILLY_STEAM_SUPPORTED
        private Callback<LobbyCreated_t> _lobbyCreatedCallback;
        private Callback<LobbyEnter_t> _lobbyEnterCallback;
        private Callback<LobbyMatchList_t> _lobbyMatchListCallback;
        private Callback<LobbyDataUpdate_t> _lobbyDataUpdateCallback;
        private Callback<LobbyChatUpdate_t> _lobbyChatUpdateCallback;
        private Callback<GameLobbyJoinRequested_t>
            _gameLobbyJoinRequestedCallback;
        private Callback<LobbyKicked_t> _lobbyKickedCallback;
        private Callback<NewUrlLaunchParameters_t>
            _newUrlLaunchParametersCallback;
#endif

        public bool SteamReady =>
            steamBootstrap != null &&
            steamBootstrap.Initialized;

        public bool IsBusy =>
            _pendingOperation != PendingOperation.None;

        public bool IsInLobby =>
            _currentLobbyId != 0;

        public bool IsLobbyOwner =>
            SteamReady &&
            _sessionHostSteamId != 0 &&
            steamBootstrap.SteamId ==
                _sessionHostSteamId;

        public ulong CurrentLobbyId => _currentLobbyId;
        public string CurrentLobbyName => _currentLobbyName;
        public string CurrentPrivacyName => _currentPrivacyName;
        public string Status => _status;

        public IReadOnlyList<HillbillyTaxiLobbyListing>
            PublicLobbies => _publicLobbies;

        public IReadOnlyList<string>
            CurrentMembers => _currentMembers;

        private void Awake()
        {
            ResolveReferences();
            SubscribeToFishNetEvents();
        }

        private void Start()
        {
            EnsureSteamCallbacksRegistered();

            if (SteamReady)
            {
                TryJoinLobbyFromLaunchCommandLine();
            }
        }

        private void OnDestroy()
        {
            UnsubscribeFromFishNetEvents();
            DisposeSteamCallbacks();
        }

        public bool RetrySteamInitialization()
        {
            if (steamBootstrap == null)
            {
                _status =
                    "Steam bootstrap is missing. Run the Phase 8 installer.";

                return false;
            }

            bool initialized =
                steamBootstrap.InitializeSteam();

            if (initialized)
            {
                EnsureSteamCallbacksRegistered();
                _status =
                    $"Steam ready as {steamBootstrap.PersonaName}.";
            }
            else
            {
                _status =
                    steamBootstrap.LastError;
            }

            return initialized;
        }

        public void StartLocalHost()
        {
            if (!CanStartNewConnection())
            {
                return;
            }

            int tugboatIndex =
                GetTransportIndex(tugboat);

            if (tugboatIndex < 0)
            {
                _status =
                    "Tugboat is not configured in Multipass.";

                return;
            }

            multipass.SetClientTransport(tugboat);
            _activeServerTransportIndex = tugboatIndex;

            bool serverStarting =
                multipass.StartConnection(
                    server: true,
                    index: tugboatIndex);

            if (!serverStarting)
            {
                _activeServerTransportIndex = -1;
                _status =
                    "The local Tugboat server refused to start.";

                return;
            }

            bool clientStarting =
                networkManager.ClientManager.StartConnection();

            _status =
                clientStarting
                    ? "Starting local Tugboat host."
                    : "Local server started, but its client refused to start.";
        }

        public void StartLocalClient(
            string address)
        {
            if (!CanStartNewConnection())
            {
                return;
            }

            tugboat.SetClientAddress(
                string.IsNullOrWhiteSpace(address)
                    ? "localhost"
                    : address.Trim());

            multipass.SetClientTransport(tugboat);

            bool starting =
                networkManager.ClientManager.StartConnection();

            _status =
                starting
                    ? "Starting local Tugboat client."
                    : "The local Tugboat client refused to start.";
        }

        public void StartLocalServer()
        {
            if (!CanStartNewConnection())
            {
                return;
            }

            int tugboatIndex =
                GetTransportIndex(tugboat);

            if (tugboatIndex < 0)
            {
                _status =
                    "Tugboat is not configured in Multipass.";

                return;
            }

            _activeServerTransportIndex = tugboatIndex;

            bool starting =
                multipass.StartConnection(
                    server: true,
                    index: tugboatIndex);

            _status =
                starting
                    ? "Starting local Tugboat server."
                    : "The local Tugboat server refused to start.";
        }

        public void CreateSteamLobby(
            HillbillyTaxiLobbyPrivacy privacy)
        {
#if !HILLBILLY_STEAM_SUPPORTED
            _status =
                "Steam lobbies are unsupported on this build target.";
#else
            if (!EnsureSteamReady() ||
                !CanStartNewConnection())
            {
                return;
            }

            _pendingPrivacy = privacy;
            _pendingOperation =
                PendingOperation.CreatingLobby;

            SteamMatchmaking.CreateLobby(
                ToSteamLobbyType(privacy),
                maximumPlayers);

            _status =
                $"Creating a {PrivacyToDisplayName(privacy)} lobby...";
#endif
        }

        public void BrowsePublicLobbies()
        {
#if !HILLBILLY_STEAM_SUPPORTED
            _status =
                "Steam lobbies are unsupported on this build target.";
#else
            if (!EnsureSteamReady())
            {
                return;
            }

            if (IsInLobby ||
                InstanceFinder.IsClientStarted ||
                InstanceFinder.IsServerStarted)
            {
                _status =
                    "Leave the current game before browsing lobbies.";

                return;
            }

            _publicLobbies.Clear();
            _pendingOperation =
                PendingOperation.BrowsingLobbies;

            SteamMatchmaking.AddRequestLobbyListStringFilter(
                ProductKey,
                ProductValue,
                ELobbyComparison.k_ELobbyComparisonEqual);

            SteamMatchmaking.AddRequestLobbyListStringFilter(
                ProtocolKey,
                networkProtocol,
                ELobbyComparison.k_ELobbyComparisonEqual);

            SteamMatchmaking.AddRequestLobbyListStringFilter(
                StateKey,
                OpenState,
                ELobbyComparison.k_ELobbyComparisonEqual);

            SteamMatchmaking.AddRequestLobbyListFilterSlotsAvailable(1);

            SteamMatchmaking.AddRequestLobbyListDistanceFilter(
                ELobbyDistanceFilter
                    .k_ELobbyDistanceFilterWorldwide);

            SteamMatchmaking.AddRequestLobbyListResultCountFilter(
                maximumBrowserResults);

            SteamMatchmaking.RequestLobbyList();
            _status = "Searching for public Hillbilly Taxi lobbies...";
#endif
        }

        public void JoinSteamLobby(
            ulong lobbyId)
        {
#if !HILLBILLY_STEAM_SUPPORTED
            _status =
                "Steam lobbies are unsupported on this build target.";
#else
            if (!EnsureSteamReady())
            {
                return;
            }

            if (lobbyId == 0)
            {
                _status = "The selected lobby ID is invalid.";
                return;
            }

            if (!CanStartNewConnection())
            {
                return;
            }

            _pendingOperation =
                PendingOperation.JoiningLobby;

            SteamMatchmaking.JoinLobby(
                new CSteamID(lobbyId));

            _status =
                $"Joining Steam lobby {lobbyId}...";
#endif
        }

        public void OpenInviteOverlay()
        {
#if HILLBILLY_STEAM_SUPPORTED
            if (!EnsureSteamReady() ||
                _currentLobbyId == 0)
            {
                _status =
                    "Create or join a Steam lobby before inviting friends.";

                return;
            }

            SteamFriends.ActivateGameOverlayInviteDialog(
                new CSteamID(_currentLobbyId));

            _status =
                "Steam invite overlay opened.";
#else
            _status =
                "Steam overlay is unsupported on this build target.";
#endif
        }

        public void LeaveGame()
        {
            ShutdownConnectionsAndLobby(
                "Left the current game.");
        }

        private bool CanStartNewConnection()
        {
            if (IsBusy)
            {
                _status =
                    "A lobby operation is already in progress.";

                return false;
            }

            if (IsInLobby ||
                InstanceFinder.IsClientStarted ||
                InstanceFinder.IsServerStarted)
            {
                _status =
                    "Leave the current game before starting another.";

                return false;
            }

            return true;
        }

        private bool EnsureSteamReady()
        {
            if (SteamReady)
            {
                EnsureSteamCallbacksRegistered();
                return true;
            }

            _status =
                steamBootstrap != null
                    ? steamBootstrap.LastError
                    : "Steam bootstrap is missing.";

            return false;
        }

#if HILLBILLY_STEAM_SUPPORTED
        private void EnsureSteamCallbacksRegistered()
        {
            if (_callbacksRegistered ||
                !SteamReady)
            {
                return;
            }

            _lobbyCreatedCallback =
                Callback<LobbyCreated_t>.Create(
                    HandleLobbyCreated);

            _lobbyEnterCallback =
                Callback<LobbyEnter_t>.Create(
                    HandleLobbyEntered);

            _lobbyMatchListCallback =
                Callback<LobbyMatchList_t>.Create(
                    HandleLobbyMatchList);

            _lobbyDataUpdateCallback =
                Callback<LobbyDataUpdate_t>.Create(
                    HandleLobbyDataUpdated);

            _lobbyChatUpdateCallback =
                Callback<LobbyChatUpdate_t>.Create(
                    HandleLobbyChatUpdated);

            _gameLobbyJoinRequestedCallback =
                Callback<GameLobbyJoinRequested_t>.Create(
                    HandleGameLobbyJoinRequested);

            _lobbyKickedCallback =
                Callback<LobbyKicked_t>.Create(
                    HandleLobbyKicked);

            _newUrlLaunchParametersCallback =
                Callback<NewUrlLaunchParameters_t>.Create(
                    HandleNewUrlLaunchParameters);

            _callbacksRegistered = true;
        }

        private void DisposeSteamCallbacks()
        {
            _lobbyCreatedCallback?.Dispose();
            _lobbyEnterCallback?.Dispose();
            _lobbyMatchListCallback?.Dispose();
            _lobbyDataUpdateCallback?.Dispose();
            _lobbyChatUpdateCallback?.Dispose();
            _gameLobbyJoinRequestedCallback?.Dispose();
            _lobbyKickedCallback?.Dispose();
            _newUrlLaunchParametersCallback?.Dispose();

            _callbacksRegistered = false;
        }

        private void HandleLobbyCreated(
            LobbyCreated_t callback)
        {
            if (_pendingOperation !=
                PendingOperation.CreatingLobby)
            {
                return;
            }

            if (callback.m_eResult != EResult.k_EResultOK ||
                callback.m_ulSteamIDLobby == 0)
            {
                _pendingOperation = PendingOperation.None;
                _status =
                    $"Steam could not create the lobby: " +
                    $"{callback.m_eResult}.";

                return;
            }

            CSteamID lobbyId =
                new(callback.m_ulSteamIDLobby);

            _currentLobbyId =
                callback.m_ulSteamIDLobby;

            _sessionHostSteamId =
                steamBootstrap.SteamId;

            string lobbyName =
                $"{steamBootstrap.PersonaName}'s Hillbilly Taxi";

            SteamMatchmaking.SetLobbyMemberLimit(
                lobbyId,
                maximumPlayers);

            SteamMatchmaking.SetLobbyType(
                lobbyId,
                ToSteamLobbyType(_pendingPrivacy));

            SteamMatchmaking.SetLobbyJoinable(
                lobbyId,
                true);

            SetLobbyDataChecked(
                lobbyId,
                ProductKey,
                ProductValue);

            SetLobbyDataChecked(
                lobbyId,
                ProtocolKey,
                networkProtocol);

            SetLobbyDataChecked(
                lobbyId,
                StateKey,
                OpenState);

            SetLobbyDataChecked(
                lobbyId,
                LobbyNameKey,
                lobbyName);

            SetLobbyDataChecked(
                lobbyId,
                HostNameKey,
                steamBootstrap.PersonaName);

            SetLobbyDataChecked(
                lobbyId,
                HostSteamIdKey,
                steamBootstrap.SteamId.ToString());

            SetLobbyDataChecked(
                lobbyId,
                PrivacyKey,
                PrivacyToMetadata(_pendingPrivacy));

            SetLobbyDataChecked(
                lobbyId,
                MembersKey,
                "1");

            _pendingOperation = PendingOperation.None;

            if (!StartSteamHostNetwork())
            {
                SteamMatchmaking.SetLobbyJoinable(
                    lobbyId,
                    false);

                SteamMatchmaking.SetLobbyData(
                    lobbyId,
                    StateKey,
                    ClosingState);

                SteamMatchmaking.LeaveLobby(lobbyId);
                ClearLobbyState();
                return;
            }

            RefreshCurrentLobbySnapshot();

            _status =
                $"{PrivacyToDisplayName(_pendingPrivacy)} Steam lobby " +
                "created. Invite a friend or wait for players.";
        }

        private void HandleLobbyEntered(
            LobbyEnter_t callback)
        {
            if (callback.m_EChatRoomEnterResponse !=
                (uint)EChatRoomEnterResponse
                    .k_EChatRoomEnterResponseSuccess)
            {
                _pendingOperation = PendingOperation.None;
                _status =
                    "Could not enter the Steam lobby. Response: " +
                    ((EChatRoomEnterResponse)
                        callback.m_EChatRoomEnterResponse);

                return;
            }

            CSteamID lobbyId =
                new(callback.m_ulSteamIDLobby);

            _currentLobbyId =
                callback.m_ulSteamIDLobby;

            CSteamID owner =
                SteamMatchmaking.GetLobbyOwner(lobbyId);

            bool localOwnsLobby =
                owner.m_SteamID ==
                steamBootstrap.SteamId;

            if (localOwnsLobby)
            {
                // CreateLobby posts both LobbyCreated_t and LobbyEnter_t. The
                // creation callback owns metadata and starts the server.
                RefreshCurrentLobbySnapshot();
                return;
            }

            if (!ValidateLobbyCompatibility(
                    lobbyId,
                    out string validationError,
                    out ulong sessionHostSteamId))
            {
                SteamMatchmaking.LeaveLobby(lobbyId);
                ClearLobbyState();
                _pendingOperation = PendingOperation.None;
                _status = validationError;
                return;
            }

            if (owner.m_SteamID == 0)
            {
                SteamMatchmaking.LeaveLobby(lobbyId);
                ClearLobbyState();
                _pendingOperation = PendingOperation.None;
                _status =
                    "The Steam lobby has no valid owner.";
                return;
            }

            _sessionHostSteamId =
                sessionHostSteamId;

            fishySteamworks.SetClientAddress(
                _sessionHostSteamId.ToString());

            multipass.SetClientTransport(
                fishySteamworks);

            bool clientStarting =
                networkManager.ClientManager.StartConnection();

            _pendingOperation = PendingOperation.None;

            if (!clientStarting)
            {
                SteamMatchmaking.LeaveLobby(lobbyId);
                ClearLobbyState();
                _status =
                    "Joined the Steam lobby, but FishySteamworks " +
                    "refused to start its client.";

                return;
            }

            RefreshCurrentLobbySnapshot();

            _status =
                $"Joined {_currentLobbyName}. Connecting to " +
                $"{SteamFriends.GetFriendPersonaName(owner)}...";
        }

        private void HandleLobbyMatchList(
            LobbyMatchList_t callback)
        {
            _publicLobbies.Clear();

            int resultCount =
                Mathf.Min(
                    (int)callback.m_nLobbiesMatching,
                    maximumBrowserResults);

            for (int index = 0;
                 index < resultCount;
                 index++)
            {
                CSteamID lobbyId =
                    SteamMatchmaking.GetLobbyByIndex(index);

                if (lobbyId.m_SteamID == 0)
                {
                    continue;
                }

                string product =
                    SteamMatchmaking.GetLobbyData(
                        lobbyId,
                        ProductKey);

                string protocol =
                    SteamMatchmaking.GetLobbyData(
                        lobbyId,
                        ProtocolKey);

                string state =
                    SteamMatchmaking.GetLobbyData(
                        lobbyId,
                        StateKey);

                if (product != ProductValue ||
                    protocol != networkProtocol ||
                    state != OpenState)
                {
                    continue;
                }

                string lobbyName =
                    SteamMatchmaking.GetLobbyData(
                        lobbyId,
                        LobbyNameKey);

                string hostName =
                    SteamMatchmaking.GetLobbyData(
                        lobbyId,
                        HostNameKey);

                int memberCount =
                    ParsePositiveInt(
                        SteamMatchmaking.GetLobbyData(
                            lobbyId,
                            MembersKey),
                        fallback: 1);

                int memberLimit =
                    SteamMatchmaking.GetLobbyMemberLimit(
                        lobbyId);

                if (memberLimit <= 0)
                {
                    memberLimit = maximumPlayers;
                }

                _publicLobbies.Add(
                    new HillbillyTaxiLobbyListing(
                        lobbyId.m_SteamID,
                        string.IsNullOrWhiteSpace(lobbyName)
                            ? "Hillbilly Taxi Lobby"
                            : lobbyName,
                        string.IsNullOrWhiteSpace(hostName)
                            ? "Unknown host"
                            : hostName,
                        memberCount,
                        memberLimit));
            }

            _pendingOperation = PendingOperation.None;

            _status =
                _publicLobbies.Count == 0
                    ? "No open public Hillbilly Taxi lobbies found."
                    : $"Found {_publicLobbies.Count} public " +
                      "Hillbilly Taxi lobby/lobbies.";
        }

        private void HandleLobbyDataUpdated(
            LobbyDataUpdate_t callback)
        {
            if (callback.m_bSuccess == 0)
            {
                return;
            }

            if (_currentLobbyId == 0 ||
                callback.m_ulSteamIDLobby !=
                    _currentLobbyId)
            {
                return;
            }

            CSteamID lobbyId =
                new(_currentLobbyId);

            string state =
                SteamMatchmaking.GetLobbyData(
                    lobbyId,
                    StateKey);

            if (state == ClosingState &&
                !_shuttingDown)
            {
                ShutdownConnectionsAndLobby(
                    "The Steam host closed the lobby.");

                return;
            }

            if (HasSessionHostChanged(lobbyId))
            {
                CloseMigratedLobbyAndLeave();
                return;
            }

            RefreshCurrentLobbySnapshot();
        }

        private void HandleLobbyChatUpdated(
            LobbyChatUpdate_t callback)
        {
            if (_currentLobbyId == 0 ||
                callback.m_ulSteamIDLobby !=
                    _currentLobbyId)
            {
                return;
            }

            CSteamID lobbyId =
                new(_currentLobbyId);

            if (HasSessionHostChanged(lobbyId))
            {
                CloseMigratedLobbyAndLeave();
                return;
            }

            RefreshCurrentLobbySnapshot();

            if (IsLobbyOwner)
            {
                UpdateHostedLobbyMemberMetadata();
            }
        }

        private void HandleGameLobbyJoinRequested(
            GameLobbyJoinRequested_t callback)
        {
            if (callback.m_steamIDLobby.m_SteamID == 0)
            {
                return;
            }

            if (IsInLobby ||
                InstanceFinder.IsClientStarted ||
                InstanceFinder.IsServerStarted)
            {
                ShutdownConnectionsAndLobby(
                    "Switching to an invited Steam lobby.");
            }

            JoinSteamLobby(
                callback.m_steamIDLobby.m_SteamID);
        }

        private void HandleLobbyKicked(
            LobbyKicked_t callback)
        {
            if (_currentLobbyId == 0 ||
                callback.m_ulSteamIDLobby !=
                    _currentLobbyId)
            {
                return;
            }

            ShutdownConnectionsAndLobby(
                callback.m_bKickedDueToDisconnect != 0
                    ? "Steam removed this client from the lobby " +
                      "after a connection failure."
                    : "The client was removed from the Steam lobby.");
        }

        private void HandleNewUrlLaunchParameters(
            NewUrlLaunchParameters_t callback)
        {
            TryJoinLobbyFromLaunchCommandLine();
        }

        private void TryJoinLobbyFromLaunchCommandLine()
        {
            if (!SteamReady)
            {
                return;
            }

            string launchCommandLine = string.Empty;

            SteamApps.GetLaunchCommandLine(
                out launchCommandLine,
                2048);

            string combinedCommandLine =
                string.Join(
                    " ",
                    Environment.GetCommandLineArgs()) +
                " " +
                launchCommandLine;

            Match match =
                Regex.Match(
                    combinedCommandLine,
                    @"\+connect_lobby\s+[""']?(?<id>\d+)[""']?",
                    RegexOptions.IgnoreCase);

            if (!match.Success ||
                !ulong.TryParse(
                    match.Groups["id"].Value,
                    out ulong lobbyId) ||
                lobbyId == 0)
            {
                return;
            }

            JoinSteamLobby(lobbyId);
        }

        private bool StartSteamHostNetwork()
        {
            int steamIndex =
                GetTransportIndex(
                    fishySteamworks);

            if (steamIndex < 0)
            {
                _status =
                    "FishySteamworks is not configured in Multipass.";

                return false;
            }

            multipass.SetClientTransport(
                fishySteamworks);

            _activeServerTransportIndex =
                steamIndex;

            bool serverStarting =
                multipass.StartConnection(
                    server: true,
                    index: steamIndex);

            if (!serverStarting)
            {
                _activeServerTransportIndex = -1;
                _status =
                    "The FishySteamworks server refused to start.";

                return false;
            }

            bool clientStarting =
                networkManager.ClientManager.StartConnection();

            if (!clientStarting)
            {
                multipass.StopServerConnection(
                    sendDisconnectMessage: true,
                    transportIndex: steamIndex);

                _activeServerTransportIndex = -1;
                _status =
                    "Steam server started, but its local client " +
                    "refused to start.";

                return false;
            }

            return true;
        }

        private bool ValidateLobbyCompatibility(
            CSteamID lobbyId,
            out string error,
            out ulong sessionHostSteamId)
        {
            sessionHostSteamId = 0;
            string product =
                SteamMatchmaking.GetLobbyData(
                    lobbyId,
                    ProductKey);

            if (product != ProductValue)
            {
                error =
                    "That Spacewar lobby was not created by Hillbilly Taxi.";

                return false;
            }

            string protocol =
                SteamMatchmaking.GetLobbyData(
                    lobbyId,
                    ProtocolKey);

            if (protocol != networkProtocol)
            {
                error =
                    "The lobby uses an incompatible Hillbilly Taxi " +
                    "network protocol.";

                return false;
            }

            string state =
                SteamMatchmaking.GetLobbyData(
                    lobbyId,
                    StateKey);

            if (state != OpenState)
            {
                error =
                    "The selected lobby is closing or no longer joinable.";

                return false;
            }

            if (!ulong.TryParse(
                    SteamMatchmaking.GetLobbyData(
                        lobbyId,
                        HostSteamIdKey),
                    out sessionHostSteamId) ||
                sessionHostSteamId == 0)
            {
                error =
                    "The lobby does not contain a valid game-host identity.";

                return false;
            }

            CSteamID currentLobbyOwner =
                SteamMatchmaking.GetLobbyOwner(lobbyId);

            if (currentLobbyOwner.m_SteamID !=
                sessionHostSteamId)
            {
                error =
                    "The original game host has left. This lobby cannot " +
                    "continue because host migration is disabled.";

                return false;
            }

            error = string.Empty;
            return true;
        }

        private void RefreshCurrentLobbySnapshot()
        {
            _currentMembers.Clear();

            if (!SteamReady ||
                _currentLobbyId == 0)
            {
                _currentLobbyName = string.Empty;
                _currentPrivacyName = string.Empty;
                return;
            }

            CSteamID lobbyId =
                new(_currentLobbyId);

            _currentLobbyName =
                SteamMatchmaking.GetLobbyData(
                    lobbyId,
                    LobbyNameKey);

            if (string.IsNullOrWhiteSpace(
                    _currentLobbyName))
            {
                _currentLobbyName =
                    "Hillbilly Taxi Lobby";
            }

            _currentPrivacyName =
                PrivacyMetadataToDisplayName(
                    SteamMatchmaking.GetLobbyData(
                        lobbyId,
                        PrivacyKey));

            int memberCount =
                SteamMatchmaking.GetNumLobbyMembers(
                    lobbyId);

            for (int index = 0;
                 index < memberCount;
                 index++)
            {
                CSteamID memberId =
                    SteamMatchmaking.GetLobbyMemberByIndex(
                        lobbyId,
                        index);

                string memberName =
                    memberId.m_SteamID ==
                        steamBootstrap.SteamId
                        ? steamBootstrap.PersonaName
                        : SteamFriends.GetFriendPersonaName(
                            memberId);

                if (string.IsNullOrWhiteSpace(memberName))
                {
                    memberName =
                        memberId.m_SteamID.ToString();
                }

                bool isOwner =
                    _sessionHostSteamId != 0 &&
                    memberId.m_SteamID ==
                    _sessionHostSteamId;

                _currentMembers.Add(
                    isOwner
                        ? $"{memberName} — Host"
                        : memberName);
            }
        }

        private bool HasSessionHostChanged(
            CSteamID lobbyId)
        {
            if (_sessionHostSteamId == 0)
            {
                return false;
            }

            CSteamID currentOwner =
                SteamMatchmaking.GetLobbyOwner(lobbyId);

            return currentOwner.m_SteamID != 0 &&
                   currentOwner.m_SteamID !=
                       _sessionHostSteamId;
        }

        private bool LocalOwnsCurrentSteamLobby()
        {
            if (!SteamReady ||
                _currentLobbyId == 0)
            {
                return false;
            }

            CSteamID currentOwner =
                SteamMatchmaking.GetLobbyOwner(
                    new CSteamID(_currentLobbyId));

            return currentOwner.m_SteamID ==
                   steamBootstrap.SteamId;
        }

        private void CloseMigratedLobbyAndLeave()
        {
            if (_shuttingDown)
            {
                return;
            }

            // Steam always selects a new lobby owner when the previous owner
            // leaves. FishNet's authoritative listen server and world state do
            // not move with that Steam ownership change.
            if (LocalOwnsCurrentSteamLobby())
            {
                CSteamID lobbyId =
                    new(_currentLobbyId);

                SteamMatchmaking.SetLobbyData(
                    lobbyId,
                    StateKey,
                    ClosingState);

                SteamMatchmaking.SetLobbyJoinable(
                    lobbyId,
                    false);
            }

            ShutdownConnectionsAndLobby(
                "The original game host left. Everyone returned to the menu.");
        }

        private void UpdateHostedLobbyMemberMetadata()
        {
            if (!IsLobbyOwner ||
                !LocalOwnsCurrentSteamLobby() ||
                _currentLobbyId == 0)
            {
                return;
            }

            CSteamID lobbyId =
                new(_currentLobbyId);

            int memberCount =
                SteamMatchmaking.GetNumLobbyMembers(
                    lobbyId);

            SteamMatchmaking.SetLobbyData(
                lobbyId,
                MembersKey,
                memberCount.ToString());

            SteamMatchmaking.SetLobbyJoinable(
                lobbyId,
                memberCount < maximumPlayers);
        }

        private void SetLobbyDataChecked(
            CSteamID lobbyId,
            string key,
            string value)
        {
            if (!SteamMatchmaking.SetLobbyData(
                    lobbyId,
                    key,
                    value))
            {
                Debug.LogWarning(
                    $"Steam rejected lobby metadata '{key}'.",
                    this);
            }
        }
#endif

        private void ShutdownConnectionsAndLobby(
            string status)
        {
            if (_shuttingDown)
            {
                return;
            }

            _shuttingDown = true;

#if HILLBILLY_STEAM_SUPPORTED
            if (SteamReady &&
                _currentLobbyId != 0)
            {
                CSteamID lobbyId =
                    new(_currentLobbyId);

                if (LocalOwnsCurrentSteamLobby())
                {
                    SteamMatchmaking.SetLobbyData(
                        lobbyId,
                        StateKey,
                        ClosingState);

                    SteamMatchmaking.SetLobbyJoinable(
                        lobbyId,
                        false);
                }
            }
#endif

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

#if HILLBILLY_STEAM_SUPPORTED
            if (SteamReady &&
                _currentLobbyId != 0)
            {
                SteamMatchmaking.LeaveLobby(
                    new CSteamID(_currentLobbyId));
            }
#endif

            _activeServerTransportIndex = -1;
            _pendingOperation = PendingOperation.None;
            ClearLobbyState();
            _status = status;
            _shuttingDown = false;
        }

        private void ClearLobbyState()
        {
            _currentLobbyId = 0;
            _sessionHostSteamId = 0;
            _currentLobbyName = string.Empty;
            _currentPrivacyName = string.Empty;
            _currentMembers.Clear();
        }

        private int GetTransportIndex(
            Transport transport)
        {
            if (multipass == null ||
                transport == null)
            {
                return -1;
            }

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

        private void SubscribeToFishNetEvents()
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

        private void UnsubscribeFromFishNetEvents()
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
            if (args.ConnectionState ==
                LocalConnectionState.Started)
            {
                _status =
                    IsInLobby
                        ? $"Connected through Steam lobby " +
                          $"{_currentLobbyName}."
                        : "Local client connected.";

                return;
            }

            if (args.ConnectionState !=
                    LocalConnectionState.Stopped ||
                _shuttingDown)
            {
                return;
            }

            if (IsInLobby &&
                _sessionHostSteamId != 0 &&
                steamBootstrap.SteamId !=
                    _sessionHostSteamId)
            {
                ShutdownConnectionsAndLobby(
                    "The Steam host connection ended.");
            }
        }

        private void HandleServerConnectionState(
            ServerConnectionStateArgs args)
        {
            if (args.ConnectionState ==
                LocalConnectionState.Started)
            {
                _status =
                    IsInLobby
                        ? "Steam lobby host is running."
                        : "Local server is running.";

                return;
            }

            if (args.ConnectionState !=
                    LocalConnectionState.Stopped ||
                _shuttingDown)
            {
                return;
            }

            if (IsInLobby &&
                IsLobbyOwner)
            {
                ShutdownConnectionsAndLobby(
                    "The Steam host stopped.");
            }
        }

#if HILLBILLY_STEAM_SUPPORTED
        private static ELobbyType ToSteamLobbyType(
            HillbillyTaxiLobbyPrivacy privacy)
        {
            return privacy switch
            {
                HillbillyTaxiLobbyPrivacy.Public =>
                    ELobbyType.k_ELobbyTypePublic,

                HillbillyTaxiLobbyPrivacy.FriendsOnly =>
                    ELobbyType.k_ELobbyTypeFriendsOnly,

                HillbillyTaxiLobbyPrivacy.Private =>
                    ELobbyType.k_ELobbyTypePrivate,

                _ => ELobbyType.k_ELobbyTypeFriendsOnly
            };
        }
#endif

        private static string PrivacyToMetadata(
            HillbillyTaxiLobbyPrivacy privacy)
        {
            return privacy switch
            {
                HillbillyTaxiLobbyPrivacy.Public => "public",
                HillbillyTaxiLobbyPrivacy.FriendsOnly => "friends",
                HillbillyTaxiLobbyPrivacy.Private => "private",
                _ => "friends"
            };
        }

        private static string PrivacyToDisplayName(
            HillbillyTaxiLobbyPrivacy privacy)
        {
            return privacy switch
            {
                HillbillyTaxiLobbyPrivacy.Public => "Public",
                HillbillyTaxiLobbyPrivacy.FriendsOnly =>
                    "Friends-only",

                HillbillyTaxiLobbyPrivacy.Private => "Private",
                _ => "Friends-only"
            };
        }

        private static string PrivacyMetadataToDisplayName(
            string metadata)
        {
            return metadata switch
            {
                "public" => "Public",
                "friends" => "Friends-only",
                "private" => "Private",
                _ => "Unknown"
            };
        }

        private static int ParsePositiveInt(
            string value,
            int fallback)
        {
            return int.TryParse(
                       value,
                       out int parsed) &&
                   parsed >= 0
                ? parsed
                : fallback;
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

        private void OnValidate()
        {
            maximumPlayers =
                Mathf.Clamp(
                    maximumPlayers,
                    2,
                    4);

            maximumBrowserResults =
                Mathf.Clamp(
                    maximumBrowserResults,
                    1,
                    100);

            if (string.IsNullOrWhiteSpace(networkProtocol))
            {
                networkProtocol = "1";
            }

            ResolveReferences();
        }
    }
}
