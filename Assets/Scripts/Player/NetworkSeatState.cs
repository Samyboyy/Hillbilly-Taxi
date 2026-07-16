using System;
using Unity.Netcode;

namespace HillbillyTaxi.Player
{
    public struct NetworkSeatState :
        INetworkSerializable,
        IEquatable<NetworkSeatState>
    {
        public NetworkObjectReference Vehicle;
        public int SeatIndex;

        public NetworkSeatState(
            NetworkObjectReference vehicle,
            int seatIndex)
        {
            Vehicle = vehicle;
            SeatIndex = seatIndex;
        }

        public bool IsSeated => SeatIndex >= 0;

        public static NetworkSeatState NotSeated =>
            new NetworkSeatState(default, -1);

        public void NetworkSerialize<T>(
            BufferSerializer<T> serializer)
            where T : IReaderWriter
        {
            serializer.SerializeValue(ref Vehicle);
            serializer.SerializeValue(ref SeatIndex);
        }

        public bool Equals(NetworkSeatState other)
        {
            return Vehicle.Equals(other.Vehicle) &&
                   SeatIndex == other.SeatIndex;
        }

        public override bool Equals(object obj)
        {
            return obj is NetworkSeatState other &&
                   Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (Vehicle.GetHashCode() * 397) ^
                       SeatIndex;
            }
        }
    }
}
