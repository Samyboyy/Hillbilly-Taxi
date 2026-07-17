using FishNet;
using FishNet.Managing;
using FishNet.Managing.Transporting;
using UnityEngine;

namespace HillbillyTaxi.FishNetMigration.Diagnostics
{
    /// <summary>
    /// Development-only controls for FishNet's built-in latency simulator.
    ///
    /// Apply the same preset on the Editor host and standalone development client.
    /// Each side delays its own outgoing traffic, so the two configured one-way
    /// delays combine into the approximate target round-trip time.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FishNetNetworkConditionPanel : MonoBehaviour
    {
        private enum PresetId
        {
            Clean,
            Good,
            Typical,
            Poor,
            Bad,
            Stress
        }

        private readonly struct NetworkPreset
        {
            public NetworkPreset(
                PresetId id,
                string displayName,
                long approximateRoundTripMilliseconds,
                long localOutgoingLatencyMilliseconds,
                double unreliablePacketLoss,
                double unreliableOutOfOrder)
            {
                Id = id;
                DisplayName = displayName;
                ApproximateRoundTripMilliseconds =
                    approximateRoundTripMilliseconds;

                LocalOutgoingLatencyMilliseconds =
                    localOutgoingLatencyMilliseconds;

                UnreliablePacketLoss =
                    unreliablePacketLoss;

                UnreliableOutOfOrder =
                    unreliableOutOfOrder;
            }

            public PresetId Id { get; }
            public string DisplayName { get; }

            public long ApproximateRoundTripMilliseconds
            {
                get;
            }

            public long LocalOutgoingLatencyMilliseconds
            {
                get;
            }

            public double UnreliablePacketLoss { get; }
            public double UnreliableOutOfOrder { get; }

            public bool Enabled =>
                LocalOutgoingLatencyMilliseconds > 0 ||
                UnreliablePacketLoss > 0d ||
                UnreliableOutOfOrder > 0d;
        }

        private static readonly NetworkPreset CleanPreset =
            new(
                PresetId.Clean,
                "Clean",
                0,
                0,
                0d,
                0d);

        private static readonly NetworkPreset GoodPreset =
            new(
                PresetId.Good,
                "Good",
                40,
                20,
                0d,
                0d);

        private static readonly NetworkPreset TypicalPreset =
            new(
                PresetId.Typical,
                "Typical",
                80,
                40,
                0.005d,
                0d);

        private static readonly NetworkPreset PoorPreset =
            new(
                PresetId.Poor,
                "Poor",
                140,
                70,
                0.01d,
                0.005d);

        private static readonly NetworkPreset BadPreset =
            new(
                PresetId.Bad,
                "Bad",
                220,
                110,
                0.025d,
                0.01d);

        private static readonly NetworkPreset StressPreset =
            new(
                PresetId.Stress,
                "Stress",
                400,
                200,
                0.05d,
                0.03d);

        [Header("References")]
        [SerializeField] private NetworkManager networkManager;

        [Header("Presentation")]
        [SerializeField] private bool panelVisible = true;

        [SerializeField, Min(260f)]
        private float panelWidth = 330f;

        private PresetId _activePreset = PresetId.Clean;
        private string _lastStatus = "Clean network";

        private void Awake()
        {
            ResolveNetworkManager();

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            ApplyPreset(CleanPreset, writeLog: false);
#endif
        }

        private void OnGUI()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (!panelVisible ||
                networkManager == null ||
                networkManager.TransportManager == null)
            {
                return;
            }

            float width = Mathf.Max(
                260f,
                panelWidth);

            Rect area =
                new(
                    Screen.width - width - 12f,
                    12f,
                    width,
                    440f);

            GUILayout.BeginArea(
                area,
                GUI.skin.box);

            GUILayout.Label(
                "Network Conditions");

            GUILayout.Label(
                $"Mode: {GetConnectionMode()}");

            if (InstanceFinder.IsClientStarted)
            {
                GUILayout.Label(
                    $"Measured RTT: " +
                    $"{networkManager.TimeManager.RoundTripTime} ms");
            }
            else
            {
                GUILayout.Label(
                    "Measured RTT: connect a client");
            }

            GUILayout.Space(4f);

            GUILayout.Label(
                "Apply the same preset in both windows.");

            GUILayout.Label(
                "The Host's local player is excluded.");

            GUILayout.Space(6f);

            DrawPresetButton(CleanPreset);
            DrawPresetButton(GoodPreset);
            DrawPresetButton(TypicalPreset);
            DrawPresetButton(PoorPreset);
            DrawPresetButton(BadPreset);
            DrawPresetButton(StressPreset);

            GUILayout.Space(8f);

            GUILayout.Label(
                $"Active: {_lastStatus}");

            GUILayout.Label(
                "Loss and reordering affect unreliable packets.");

            GUILayout.Label(
                "Change presets while the truck is stopped.");

            GUILayout.EndArea();
#endif
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private void DrawPresetButton(
            NetworkPreset preset)
        {
            bool wasEnabled = GUI.enabled;

            GUI.enabled =
                preset.Id != _activePreset;

            string label =
                preset.Id == PresetId.Clean
                    ? "Clean — no simulation"
                    : $"{preset.DisplayName} — " +
                      $"~{preset.ApproximateRoundTripMilliseconds} ms RTT";

            if (GUILayout.Button(label))
            {
                ApplyPreset(
                    preset,
                    writeLog: true);
            }

            GUI.enabled = wasEnabled;
        }

        private void ApplyPreset(
            NetworkPreset preset,
            bool writeLog)
        {
            if (networkManager == null ||
                networkManager.TransportManager == null)
            {
                return;
            }

            LatencySimulator simulator =
                networkManager
                    .TransportManager
                    .LatencySimulator;

            // Disabling first flushes any queued packets and ensures a clean
            // transition between profiles.
            simulator.SetEnabled(false);

            simulator.SetLatency(
                preset.LocalOutgoingLatencyMilliseconds);

            simulator.SetPacketLoss(
                preset.UnreliablePacketLoss);

            simulator.SetOutOfOrder(
                preset.UnreliableOutOfOrder);

            simulator.SetEnabled(
                preset.Enabled);

            _activePreset = preset.Id;

            _lastStatus =
                preset.Id == PresetId.Clean
                    ? "Clean network"
                    : $"{preset.DisplayName}, " +
                      $"{preset.LocalOutgoingLatencyMilliseconds} ms " +
                      "outgoing delay per instance, " +
                      $"{preset.UnreliablePacketLoss * 100d:0.##}% loss, " +
                      $"{preset.UnreliableOutOfOrder * 100d:0.##}% reordered";

            if (writeLog)
            {
                Debug.Log(
                    $"FishNet network condition preset applied: " +
                    $"{_lastStatus}.",
                    this);
            }
        }
#endif

        private string GetConnectionMode()
        {
            bool serverStarted =
                InstanceFinder.IsServerStarted;

            bool clientStarted =
                InstanceFinder.IsClientStarted;

            if (serverStarted &&
                clientStarted)
            {
                return "Host";
            }

            if (serverStarted)
            {
                return "Server";
            }

            if (clientStarted)
            {
                return "Client";
            }

            return "Offline";
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

        private void OnValidate()
        {
            panelWidth =
                Mathf.Max(
                    260f,
                    panelWidth);

            ResolveNetworkManager();
        }
    }
}
