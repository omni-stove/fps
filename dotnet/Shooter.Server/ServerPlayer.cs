using Shooter.Core;
using Shooter.Protocol;

namespace Shooter.Server
{
    internal sealed class ServerPlayer
    {
        public ServerPlayer(ConnectionId connection, int playerId, Team team, string rejoinKey, string productUserId = null)
        {
            ProductUserId = productUserId;
            Connection = connection;
            PlayerId = playerId;
            Team = team;
            RejoinKey = rejoinKey;
        }

        public ConnectionId Connection { get; }

        public int PlayerId { get; }

        public Team Team { get; }

        public string RejoinKey { get; }

        public string ProductUserId { get; }

        public bool IsBot => BotRoster.IsBotId(PlayerId);
    }
}
