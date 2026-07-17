using FishNet.Connection;
using FishNet.Managing;
using FishNet.Transporting;
using UnityEngine;

namespace HillbillyTaxi.FishNetMigration
{
    /// <summary>
    /// Enforces Hillbilly Taxi's current four-player session limit.
    /// The check is server-side and works whether the connection has already been
    /// added to ServerManager.Clients or the event fires immediately beforehand.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FishNetConnectionLimiter : MonoBehaviour
    {
        [SerializeField] private NetworkManager networkManager;
        [SerializeField, Min(1)] private int maximumPlayers = 4;

        private void Awake()
        {
            ResolveNetworkManager();

            if (networkManager == null)
            {
                Debug.LogError(
                    $"{nameof(FishNetConnectionLimiter)} needs a FishNet " +
                    $"{nameof(NetworkManager)}.",
                    this);

                enabled = false;
                return;
            }

            networkManager.ServerManager.OnRemoteConnectionState +=
                HandleRemoteConnectionState;
        }

        private void OnDestroy()
        {
            if (networkManager != null)
            {
                networkManager.ServerManager.OnRemoteConnectionState -=
                    HandleRemoteConnectionState;
            }
        }

        private void HandleRemoteConnectionState(
            NetworkConnection connection,
            RemoteConnectionStateArgs args)
        {
            if (args.ConnectionState !=
                RemoteConnectionState.Started)
            {
                return;
            }

            int currentCount =
                networkManager.ServerManager.Clients.Count;

            bool connectionAlreadyCounted =
                networkManager.ServerManager.Clients.ContainsKey(
                    connection.ClientId);

            int projectedCount =
                currentCount +
                (connectionAlreadyCounted ? 0 : 1);

            if (projectedCount <= maximumPlayers)
            {
                return;
            }

            Debug.LogWarning(
                $"Rejecting client {connection.ClientId}: " +
                $"session is full ({maximumPlayers} players).",
                this);

            connection.Disconnect(true);
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
            maximumPlayers = Mathf.Max(1, maximumPlayers);
            ResolveNetworkManager();
        }
    }
}
