using System;
using System.Numerics;
using Shooter.Core;
using Shooter.Protocol;

namespace Shooter.NetV2
{
    // 届いた MatchSnapshot の円から、オフラインの ZoneSchedule.Evaluate と同じ形の ZoneState を作る。
    // サーバーは段階 (待ち・縮み・閉じた) を送らないので、同じ段階の中で円が動いたか残りの秒が増えたら縮み始めたとみなし、次の段階に移るまで縮みのままにする。
    // 円の外のダメージはサーバーが与え、HUD も出さないので送られず、DamagePerSecond は 0 にする
    public sealed class ClientZone
    {
        private bool _has;
        private int _phaseIndex;
        private bool _shrinking;
        private Vector2 _center;
        private float _radius;
        private int _remaining;

        // エリアが動いていない (BR でない・始まる前) なら null
        public ZoneState? State { get; private set; }

        // State が変わったら true
        public bool Apply(in MatchSnapshot match)
        {
            if (match.Objective != MatchObjective.BattleRoyale || match.ZonePhase <= 0 || !(match.ZoneRadius > 0f))
            {
                return Clear();
            }

            var phaseIndex = match.ZonePhase - 1;
            var center = match.ZoneCenter;
            var radius = match.ZoneRadius;
            var remaining = Math.Max(0, match.ZonePhaseRemainingSeconds);
            if (!_has || phaseIndex != _phaseIndex)
            {
                _shrinking = false;
            }
            else if (radius < _radius || center != _center || remaining > _remaining)
            {
                _shrinking = true;
            }

            _has = true;
            _phaseIndex = phaseIndex;
            _center = center;
            _radius = radius;
            _remaining = remaining;

            var target = match.NextZoneCenter;
            var targetRadius = Math.Min(match.NextZoneRadius, radius);
            var closed = remaining == 0 && radius <= targetRadius && center == target;
            var stage = closed ? ZoneStage.Closed : _shrinking ? ZoneStage.Shrinking : ZoneStage.Waiting;
            var state = new ZoneState(
                phaseIndex,
                stage,
                center,
                radius,
                target,
                targetRadius,
                0f,
                stage == ZoneStage.Waiting ? remaining : 0f,
                stage == ZoneStage.Closed ? 0f : remaining);
            if (State.HasValue && Same(State.Value, state))
            {
                return false;
            }

            State = state;
            return true;
        }

        // 消したら true
        public bool Clear()
        {
            _has = false;
            _shrinking = false;
            if (!State.HasValue)
            {
                return false;
            }

            State = null;
            return true;
        }

        private static bool Same(ZoneState a, ZoneState b)
        {
            return a.PhaseIndex == b.PhaseIndex
                && a.Stage == b.Stage
                && a.Center == b.Center
                && a.Radius == b.Radius
                && a.TargetCenter == b.TargetCenter
                && a.TargetRadius == b.TargetRadius
                && a.SecondsUntilShrink == b.SecondsUntilShrink
                && a.SecondsUntilStageEnds == b.SecondsUntilStageEnds;
        }
    }
}
