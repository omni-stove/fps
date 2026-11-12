using System;
using Fps.Core;
using Unity.Netcode;

namespace Fps.Net
{
    /// <summary>
    /// <see cref="NetTeamDeathmatch.Players"/> の 1 行。プレイヤー 1 人分のチームとキル数・デス数。
    /// </summary>
    public struct NetPlayerStats : INetworkSerializable, IEquatable<NetPlayerStats>
    {
        public ulong ClientId;
        public Team Team;
        public int Kills;
        public int Deaths;

        public NetPlayerStats(ulong clientId, Team team, int kills, int deaths)
        {
            ClientId = clientId;
            Team = team;
            Kills = kills;
            Deaths = deaths;
        }

        public void NetworkSerialize<T>(BufferSerializer<T> serializer)
            where T : IReaderWriter
        {
            serializer.SerializeValue(ref ClientId);
            serializer.SerializeValue(ref Team);
            serializer.SerializeValue(ref Kills);
            serializer.SerializeValue(ref Deaths);
        }

        public bool Equals(NetPlayerStats other)
        {
            return ClientId == other.ClientId && Team == other.Team && Kills == other.Kills && Deaths == other.Deaths;
        }

        public override bool Equals(object obj)
        {
            return obj is NetPlayerStats other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(ClientId, (int)Team, Kills, Deaths);
        }

        public override string ToString()
        {
            return "client=" + ClientId + " team=" + Team + " K=" + Kills + " D=" + Deaths;
        }
    }
}
