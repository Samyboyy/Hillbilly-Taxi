using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace HillbillyTaxi.Networking
{
    /// <summary>
    /// Assigns up to one connected client to each spawn point during connection
    /// approval, before Netcode creates the player's owner-authoritative object.
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    [DisallowMultipleComponent]
    public sealed class NetworkPlayerSpawnManager : MonoBehaviour
    {
        [SerializeField] private NetworkManager networkManager;
        [SerializeField] private Transform[] spawnPoints;

        private readonly Dictionary<ulong, int>
            _clientSpawnAssignments =
                new Dictionary<ulong, int>();

        private readonly HashSet<int> _occupiedSpawnIndices =
            new HashSet<int>();

        private void Awake()
        {
            if (networkManager == null)
            {
                networkManager =
                    FindFirstObjectByType<NetworkManager>();
            }

            if (networkManager == null)
            {
                Debug.LogError(
                    $"{nameof(NetworkPlayerSpawnManager)} needs a " +
                    $"{nameof(NetworkManager)} in the scene.",
                    this);

                enabled = false;
                return;
            }

            networkManager.NetworkConfig.ConnectionApproval =
                true;

            networkManager.ConnectionApprovalCallback +=
                ApproveConnection;

            networkManager.OnClientDisconnectCallback +=
                HandleClientDisconnected;
        }

        private void OnDestroy()
        {
            if (networkManager == null)
            {
                return;
            }

            networkManager.ConnectionApprovalCallback -=
                ApproveConnection;

            networkManager.OnClientDisconnectCallback -=
                HandleClientDisconnected;
        }

        private void ApproveConnection(
            NetworkManager.ConnectionApprovalRequest request,
            NetworkManager.ConnectionApprovalResponse response)
        {
            int spawnIndex = FindAvailableSpawnIndex();

            if (spawnIndex < 0)
            {
                response.Approved = false;
                response.CreatePlayerObject = false;
                response.Reason = "The game is full.";
                response.Pending = false;
                return;
            }

            Transform spawnPoint = spawnPoints[spawnIndex];

            _clientSpawnAssignments[
                request.ClientNetworkId] = spawnIndex;

            _occupiedSpawnIndices.Add(spawnIndex);

            response.Approved = true;
            response.CreatePlayerObject = true;
            response.Position = spawnPoint.position;
            response.Rotation = spawnPoint.rotation;
            response.Pending = false;
        }

        private int FindAvailableSpawnIndex()
        {
            if (spawnPoints == null)
            {
                return -1;
            }

            for (
                int index = 0;
                index < spawnPoints.Length;
                index++)
            {
                if (spawnPoints[index] != null &&
                    !_occupiedSpawnIndices.Contains(index))
                {
                    return index;
                }
            }

            return -1;
        }

        private void HandleClientDisconnected(ulong clientId)
        {
            if (!_clientSpawnAssignments.TryGetValue(
                    clientId,
                    out int spawnIndex))
            {
                return;
            }

            _clientSpawnAssignments.Remove(clientId);
            _occupiedSpawnIndices.Remove(spawnIndex);
        }

        private void Reset()
        {
            networkManager =
                FindFirstObjectByType<NetworkManager>();
        }
    }
}
