using Shooter.Core;
using Shooter.Protocol;

namespace Shooter.Server
{
    internal sealed class ServerPlayer
    {
        public ServerPlayer(ConnectionId connection, int playerId, Team team, string rejoinKey, string productUserId = null, string partyId = null, long joinOrder = 0)
        {
            ProductUserId = productUserId;
            Connection = connection;
            PlayerId = playerId;
            Team = team;
            RejoinKey = rejoinKey;
            PartyId = string.IsNullOrEmpty(partyId) ? null : partyId;
            JoinOrder = joinOrder;
        }

        public ConnectionId Connection { get; }

        public int PlayerId { get; }

        /// <summary>試合の間に <see cref="ServerMatch"/> が人数をそろえるときだけ変わる。</summary>
        public Team Team { get; private set; }

        public string RejoinKey { get; }

        /// <summary>EOS でつないだ人の、通信路が確かめた ProductUserId。EOS でなければ null。</summary>
        public string ProductUserId { get; }

        /// <summary>承認のデータで名乗ったパーティの Id。無ければ null。</summary>
        public string PartyId { get; }

        /// <summary>試合に入った順の番号。後に入った人ほど大きい。</summary>
        public long JoinOrder { get; }

        public bool IsBot => BotRoster.IsBotId(PlayerId);

        internal void MoveTo(Team team)
        {
            Team = team;
        }
    }
}
