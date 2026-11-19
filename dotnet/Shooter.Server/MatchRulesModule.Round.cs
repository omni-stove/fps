using System;
using System.Collections.Generic;
using Shooter.Core;
using Shooter.Protocol;

namespace Shooter.Server
{
    // ラウンドの段階は Session (TeamDeathmatchSession) が持たないので、ラウンド制のモードが外から段階の読み口を渡す。
    // 渡されていない試合はラウンド制でないとして、スナップショットに段階を載せない
    internal sealed partial class MatchRulesModule
    {
        private readonly List<int> _roundScratch = new List<int>();
        private RoundPhase? _lastRoundPhase;

        public Func<RoundPhase?> RoundPhaseSource { get; set; }

        public RoundPhase? CurrentRoundPhase()
        {
            var source = RoundPhaseSource;
            if (source == null)
            {
                return null;
            }

            var phase = source();
            if (phase.HasValue && !Enum.IsDefined(phase.Value))
            {
                throw new InvalidOperationException("the round phase source returned an undefined RoundPhase " + phase.Value);
            }

            return phase;
        }

        private void FillRound(ref MatchSnapshot snapshot)
        {
            var phase = CurrentRoundPhase();
            snapshot.HasRound = phase.HasValue;
            snapshot.Round = phase ?? RoundPhase.Waiting;
        }

        private void TickRound()
        {
            var phase = CurrentRoundPhase();
            if (phase == _lastRoundPhase)
            {
                return;
            }

            var leftPreparation = _lastRoundPhase == RoundPhase.Preparing;
            _lastRoundPhase = phase;
            _log.WriteLine(MatchLog.RoundPhaseChanged(phase));
            if (leftPreparation)
            {
                DropLivingSpectators();
            }
        }

        // 合間の間だけ生きたまま観戦できるので、合間が終わったら生きている人の記録を捨てる。倒れている人はやられ演出の決まりで観戦を続ける
        private void DropLivingSpectators()
        {
            _roundScratch.Clear();
            foreach (var viewerId in _spectating.Keys)
            {
                if (!_session.HasPlayer(viewerId) || _session.IsAlive(viewerId)
                    || _data.Rules.Spectating == Spectating.Off
                    || _data.Rules.DeathCamera != DeathCamera.Ally || _session.State != GameModeState.Playing)
                {
                    _roundScratch.Add(viewerId);
                }
            }

            foreach (var viewerId in _roundScratch)
            {
                _spectating.Remove(viewerId);
            }
        }
    }
}
