using Shooter.Core;
using Shooter.Protocol;

namespace Shooter.Server
{
    internal sealed class ServerPlayer
    {
        public ServerPlayer(
            ConnectionId connection,
            int playerId,
            Team team,
            string rejoinKey,
            string productUserId = null,
            string partyId = null,
            long joinOrder = 0,
            int? skillRating = null,
            string voiceUserId = null)
        {
            SkillRating = skillRating;
            ProductUserId = productUserId;
            VoiceUserId = productUserId ?? voiceUserId;
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

        // UDP では名乗った Id を確かめられないので、試合の記録や鍵に使う ProductUserId とは分け、声の主を見分けるのにだけ使う。
        // 通信路が確かめた Id があればそちらを使う。
        public string VoiceUserId { get; }

        public string PartyId { get; }

        public long JoinOrder { get; }

        public int? SkillRating { get; }

        public bool IsBot => BotRoster.IsBotId(PlayerId);

        internal void MoveTo(Team team)
        {
            Team = team;
        }
    }
}
