using Fps.Core;
using Fps.Protocol;

namespace Fps.Server
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

        /// <summary>EOS でつないだ人の、通信路が確かめた ProductUserId。EOS でなければ null。</summary>
        public string ProductUserId { get; }
    }
}
