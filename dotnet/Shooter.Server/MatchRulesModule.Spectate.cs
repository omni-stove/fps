using System.Collections.Generic;
using Shooter.Core;
using Shooter.Protocol;

namespace Shooter.Server
{
    // 映す相手はクライアントの言う Id を受け取らず、Next / Previous の向きだけを受けて SpectateTargets で選ぶ。
    // Id を信じると、敵や死んでいる人を映す相手として記録できてしまうため。
    internal sealed partial class MatchRulesModule
    {
        private readonly Dictionary<int, int> _spectating = new Dictionary<int, int>();
        private readonly List<SpectateCandidate> _spectateCandidates = new List<SpectateCandidate>();
        private readonly List<int> _spectateScratch = new List<int>();

        public bool TryGetSpectateTarget(int viewerId, out int targetId)
        {
            return _spectating.TryGetValue(viewerId, out targetId);
        }

        partial void OnSpectateRequest(ServerPlayer player, SpectateRequestMessage request)
        {
            ServerSpectate(player.PlayerId, request.Step);
        }

        internal int? ServerSpectate(int viewerId, SpectateStep step)
        {
            if (_session == null || !_session.HasPlayer(viewerId) || viewerId < 0)
            {
                return null;
            }

            // ラウンドの合間にやられ演出の決まりと生死を見ないのは、クライアントの ClientSpectate が同じ決まりで観戦を始めるため
            var betweenRounds = CurrentRoundPhase() == RoundPhase.Preparing;
            if (!betweenRounds
                && (_data.Rules.DeathCamera != DeathCamera.Ally || _session.State != GameModeState.Playing || _session.IsAlive(viewerId)))
            {
                _spectating.Remove(viewerId);
                return null;
            }

            _spectateCandidates.Clear();
            foreach (var other in _match.Players)
            {
                var id = other.PlayerId;
                if (id >= 0 && _session.HasPlayer(id))
                {
                    _spectateCandidates.Add(new SpectateCandidate((ulong)id, _session.GetTeam(id), _session.IsAlive(id)));
                }
            }

            ulong? current = _spectating.TryGetValue(viewerId, out var watched) ? (ulong)watched : null;
            var picked = SpectateTargets.Pick(
                _spectateCandidates, (ulong)viewerId, _session.GetTeam(viewerId), current, step == SpectateStep.Previous ? -1 : 1);
            if (!picked.HasValue)
            {
                _spectating.Remove(viewerId);
                return null;
            }

            var target = (int)picked.Value;
            _spectating[viewerId] = target;
            return target;
        }

        private void SpectatePlayerKilled(int victimId)
        {
            ForgetSpectateTarget(victimId);
        }

        private void SpectatePlayerSpawned(int playerId)
        {
            _spectating.Remove(playerId);
        }

        private void SpectatePlayerLeft(int playerId)
        {
            _spectating.Remove(playerId);
            ForgetSpectateTarget(playerId);
        }

        private void SpectateMatchRestarted()
        {
            _spectating.Clear();
        }

        private void ForgetSpectateTarget(int targetId)
        {
            _spectateScratch.Clear();
            foreach (var pair in _spectating)
            {
                if (pair.Value == targetId)
                {
                    _spectateScratch.Add(pair.Key);
                }
            }

            foreach (var viewerId in _spectateScratch)
            {
                _spectating.Remove(viewerId);
            }
        }
    }
}
