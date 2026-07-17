using System;

namespace HillbillyTaxi.FishNetMigration.Steam
{
    public enum HillbillyTaxiLobbyPrivacy
    {
        Public = 0,
        FriendsOnly = 1,
        Private = 2
    }

    [Serializable]
    public sealed class HillbillyTaxiLobbyListing
    {
        public HillbillyTaxiLobbyListing(
            ulong lobbyId,
            string lobbyName,
            string hostName,
            int memberCount,
            int maximumMembers)
        {
            LobbyId = lobbyId;
            LobbyName = lobbyName;
            HostName = hostName;
            MemberCount = memberCount;
            MaximumMembers = maximumMembers;
        }

        public ulong LobbyId { get; }
        public string LobbyName { get; }
        public string HostName { get; }
        public int MemberCount { get; }
        public int MaximumMembers { get; }
    }
}
