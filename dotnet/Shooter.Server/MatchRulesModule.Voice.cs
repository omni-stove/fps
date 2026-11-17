using System;
using System.Collections.Generic;
using Shooter.Protocol;

namespace Shooter.Server
{
    // ボイスの部屋は EOS のロビーにあり、サーバーは声を中継しない。クライアントが声の主を味方か見分けられるよう、
    // ProductUserId とプレイヤー Id の対応だけを、変わったときに全員へ送る。
    internal sealed partial class MatchRulesModule
    {
        private readonly VoiceRosterMessage _voiceRoster = new VoiceRosterMessage();
        private readonly Dictionary<string, int> _sentVoiceRoster = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, int> _nextVoiceRoster = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, VoiceClaim> _voiceClaims = new Dictionary<string, VoiceClaim>(StringComparer.OrdinalIgnoreCase);
        private readonly List<string> _ambiguousVoiceUserIds = new List<string>();

        internal VoiceRosterMessage CurrentVoiceRoster => _voiceRoster;

        // 誰も Id を送っていない試合では 1 通も送らず、ボイスを使わない試合の通信を増やさない
        private void VoicePlayerJoined(ServerPlayer player)
        {
            if (player.IsBot)
            {
                return;
            }

            if (RefreshVoiceRoster())
            {
                SendToAll(TransportChannel.Reliable, _voiceRoster);
                return;
            }

            if (_voiceRoster.Count > 0)
            {
                _context.Messenger.Send(player.Connection, TransportChannel.Reliable, _voiceRoster);
            }
        }

        private void VoicePlayerLeft(ServerPlayer player)
        {
            if (!player.IsBot && RefreshVoiceRoster())
            {
                SendToAll(TransportChannel.Reliable, _voiceRoster);
            }
        }

        private bool RefreshVoiceRoster()
        {
            BuildVoiceRoster(_match.Players, _nextVoiceRoster, _voiceClaims, _ambiguousVoiceUserIds);
            if (SameRoster(_sentVoiceRoster, _nextVoiceRoster))
            {
                return false;
            }

            _sentVoiceRoster.Clear();
            foreach (var entry in _nextVoiceRoster)
            {
                _sentVoiceRoster.Add(entry.Key, entry.Value);
            }

            _voiceRoster.Clear();
            foreach (var player in _match.Players)
            {
                if (player.VoiceUserId != null && _sentVoiceRoster.TryGetValue(player.VoiceUserId, out var playerId) && playerId == player.PlayerId)
                {
                    _voiceRoster.Add(playerId, player.VoiceUserId);
                }
            }

            foreach (var userId in _ambiguousVoiceUserIds)
            {
                _log.WriteLine(ServerVoiceLog.Ambiguous(userId));
            }

            _log.WriteLine(ServerVoiceLog.Roster(_voiceRoster.Count));
            return true;
        }

        // 確かめていない Id は誰でも名乗れるので、2 人が同じ Id を名乗ったら、通信路が確かめた 1 人がいればその人に、
        // いなければ誰にも結び付けない。結び付けないと、その声はどのクライアントでも受けない側に倒れる。
        internal static void BuildVoiceRoster(
            IEnumerable<ServerPlayer> players, Dictionary<string, int> roster, Dictionary<string, VoiceClaim> claims, List<string> ambiguous)
        {
            roster.Clear();
            claims.Clear();
            ambiguous.Clear();
            foreach (var player in players)
            {
                if (player.IsBot || player.VoiceUserId == null)
                {
                    continue;
                }

                claims.TryGetValue(player.VoiceUserId, out var claim);
                claims[player.VoiceUserId] = claim.With(player.PlayerId, player.ProductUserId != null);
            }

            foreach (var entry in claims)
            {
                var claim = entry.Value;
                if (claim.VerifiedCount == 1)
                {
                    roster.Add(entry.Key, claim.VerifiedPlayerId);
                }
                else if (claim.VerifiedCount == 0 && claim.Count == 1)
                {
                    roster.Add(entry.Key, claim.PlayerId);
                }
                else
                {
                    ambiguous.Add(entry.Key);
                }
            }
        }

        private static bool SameRoster(Dictionary<string, int> a, Dictionary<string, int> b)
        {
            if (a.Count != b.Count)
            {
                return false;
            }

            foreach (var entry in a)
            {
                if (!b.TryGetValue(entry.Key, out var playerId) || playerId != entry.Value)
                {
                    return false;
                }
            }

            return true;
        }

        internal readonly struct VoiceClaim
        {
            private VoiceClaim(int count, int playerId, int verifiedCount, int verifiedPlayerId)
            {
                Count = count;
                PlayerId = playerId;
                VerifiedCount = verifiedCount;
                VerifiedPlayerId = verifiedPlayerId;
            }

            public int Count { get; }

            public int PlayerId { get; }

            public int VerifiedCount { get; }

            public int VerifiedPlayerId { get; }

            public VoiceClaim With(int playerId, bool verified)
            {
                return verified
                    ? new VoiceClaim(Count + 1, playerId, VerifiedCount + 1, playerId)
                    : new VoiceClaim(Count + 1, playerId, VerifiedCount, VerifiedPlayerId);
            }
        }
    }
}
