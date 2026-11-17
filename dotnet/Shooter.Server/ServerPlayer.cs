using Shooter.Core;
using Shooter.Protocol;

namespace Shooter.Server
{
    internal sealed class ServerPlayer
    {
        public ServerPlayer(ConnectionId connection, int playerId, Team team, string rejoinKey, string productUserId = null, string partyId = null)
        {
            ProductUserId = productUserId;
            Connection = connection;
            PlayerId = playerId;
            Team = team;
            RejoinKey = rejoinKey;
            PartyId = partyId;
        }

        public ConnectionId Connection { get; }

        public int PlayerId { get; }

        public Team Team { get; }

        public string RejoinKey { get; }

        /// <summary>EOS でつないだ人の、通信路が確かめた ProductUserId。EOS でなければ null。</summary>
        public string ProductUserId { get; }

        /// <summary>承認のデータで名乗ったパーティの Id。無ければ null。</summary>
        public string PartyId { get; }

        public bool IsBot => BotRoster.IsBotId(PlayerId);
    }
}
