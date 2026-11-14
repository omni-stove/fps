using System;
using Fps.Core;
using Unity.Netcode;

namespace Fps.Net
{
    /// <summary>
    /// <see cref="NetTeamDeathmatch.Players"/> の 1 行。プレイヤー 1 人分のチームとキル数・デス数と、最後に自分を倒した相手。
    /// </summary>
    public struct NetPlayerStats : INetworkSerializable, IEquatable<NetPlayerStats>
    {
        /// <summary>倒した相手がいない (まだ倒されていないか、自滅など自分以外に倒されていない) ことを表す <see cref="KillerCode"/>。</summary>
        public const ulong NoKiller = 0;

        /// <summary>人ならクライアント Id、Bot なら Bot のプレイヤー Id (<see cref="BotRoster.FirstBotPlayerId"/> 以上、<see cref="NetPlayer.RosterId"/>)。</summary>
        public ulong ClientId;
        public Team Team;
        public int Kills;
        public int Deaths;

        /// <summary>
        /// 最後に自分を倒した相手のクライアント Id に 1 を足したもの。いなければ <see cref="NoKiller"/>。
        /// 1 ずらすのは、既定値 (default) の行がクライアント 0 (ホスト) に倒されたことにならないようにするため。
        /// </summary>
        public ulong KillerCode;

        /// <param name="killer">最後に自分を倒した相手のクライアント Id。いなければ null。</param>
        /// <exception cref="ArgumentOutOfRangeException">killer が <see cref="ulong.MaxValue"/> (1 を足せない)。</exception>
        public NetPlayerStats(ulong clientId, Team team, int kills, int deaths, ulong? killer = null)
        {
            if (killer == ulong.MaxValue)
            {
                throw new ArgumentOutOfRangeException(nameof(killer), killer, "killer client id is too large to be synced");
            }

            ClientId = clientId;
            Team = team;
            Kills = kills;
            Deaths = deaths;
            KillerCode = killer.HasValue ? killer.Value + 1 : NoKiller;
        }

        /// <summary>最後に自分を倒した相手のクライアント Id。いなければ null。デスカムが映す相手を引くのに使う。</summary>
        public ulong? Killer => KillerCode == NoKiller ? (ulong?)null : KillerCode - 1;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer)
            where T : IReaderWriter
        {
            serializer.SerializeValue(ref ClientId);
            serializer.SerializeValue(ref Team);
            serializer.SerializeValue(ref Kills);
            serializer.SerializeValue(ref Deaths);
            serializer.SerializeValue(ref KillerCode);
        }

        public bool Equals(NetPlayerStats other)
        {
            return ClientId == other.ClientId && Team == other.Team && Kills == other.Kills && Deaths == other.Deaths
                && KillerCode == other.KillerCode;
        }

        public override bool Equals(object obj)
        {
            return obj is NetPlayerStats other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(ClientId, (int)Team, Kills, Deaths, KillerCode);
        }

        public override string ToString()
        {
            return "client=" + ClientId + " team=" + Team + " K=" + Kills + " D=" + Deaths
                + " killer=" + (Killer.HasValue ? Killer.Value.ToString() : "-");
        }
    }
}
