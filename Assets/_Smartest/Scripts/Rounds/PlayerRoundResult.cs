using System;
using Unity.Netcode;

namespace Smartest.Rounds
{
    /// <summary>One reveal row, replicated to clients in GameState.Results.</summary>
    public struct PlayerRoundResult : INetworkSerializable, IEquatable<PlayerRoundResult>
    {
        public ulong ClientId;
        /// <summary>Social rounds: what they answered, -1 = none. Minigames: unused (-1).</summary>
        public int Answer;
        public int Delta;
        /// <summary>Minigames: 1-based finishing place. 0 for social rounds.</summary>
        public int Place;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref ClientId);
            serializer.SerializeValue(ref Answer);
            serializer.SerializeValue(ref Delta);
            serializer.SerializeValue(ref Place);
        }

        public bool Equals(PlayerRoundResult other)
        {
            return ClientId == other.ClientId && Answer == other.Answer && Delta == other.Delta && Place == other.Place;
        }

        public override bool Equals(object obj) => obj is PlayerRoundResult o && Equals(o);
        public override int GetHashCode() => ClientId.GetHashCode() ^ (Answer * 397) ^ (Delta * 7919) ^ (Place * 31);
    }
}
