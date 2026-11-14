using Fps.Core;
using Fps.Protocol;

namespace Fps.Server
{
    internal sealed class ServerPlayer
    {
        public ServerPlayer(ConnectionId connection, int playerId, Team team, string rejoinKey)
        {
            Connection = connection;
            PlayerId = playerId;
            Team = team;
            RejoinKey = rejoinKey;
        }

        public ConnectionId Connection { get; }

        public int PlayerId { get; }

        public Team Team { get; }

        public string RejoinKey { get; }
    }
}
