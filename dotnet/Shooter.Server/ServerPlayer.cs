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

        public Team Team { get; private set; }

        public string RejoinKey { get; }

        public string ProductUserId { get; }

        public string PartyId { get; }

        public long JoinOrder { get; }

        public bool IsBot => BotRoster.IsBotId(PlayerId);

        internal void MoveTo(Team team)
        {
            Team = team;
        }
    }
}
