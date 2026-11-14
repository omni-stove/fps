using System;
using System.Globalization;
using Fps.Core;
using Fps.Protocol;

namespace Fps.Server
{
    // GameRules.Objective が Domination のとき、拠点の占領を判定し、キル数に代えて拠点のポイントで勝敗を決める。
    // キル数の上限では終わらせず、ポイントが上限に届いたら Session を Finish で終え、時間切れのときもポイントの多いほうを勝ちにする
    internal sealed partial class MatchRulesModule
    {
        // 拠点制圧の試合でなければ null
        private DominationObjective _domination;

        internal DominationObjective Domination => _domination;

        public void FillControlPoints(SnapshotList<ControlPointSnapshot> points)
        {
            ArgumentNullException.ThrowIfNull(points);
            points.Clear();
            if (_domination == null)
            {
                return;
            }

            var mode = _domination.Mode;
            for (var i = 0; i < mode.PointCount; i++)
            {
                ref var point = ref points.Add();
                var owner = mode.GetOwner(i);
                point.Index = i;
                point.Present = ControlPointFields.All;
                point.HasOwner = owner.HasValue;
                point.Owner = owner ?? Team.Alpha;
                point.ControlPercent = ToPercent(mode.GetControl(i));
                point.Contested = mode.IsContested(i);
            }
        }

        // Unity の NetControlPointState と同じく、1% 未満の動きで送り直さないよう 0 に向けて切り捨てる
        internal static sbyte ToPercent(float control)
        {
            if (float.IsNaN(control))
            {
                return 0;
            }

            var clamped = Math.Max(-1f, Math.Min(1f, control));
            return (sbyte)(int)Math.Truncate(clamped * ControlPointSnapshot.FullPercent);
        }

        private void PrepareObjective(GameRules rules, NetMatchSettings settings, NetMapPoints points, out int killScoreLimit)
        {
            _domination = null;
            if (rules.Objective != MatchObjective.Domination)
            {
                killScoreLimit = settings.ScoreLimit;
                return;
            }

            killScoreLimit = int.MaxValue;
            if (points.ControlPoints.Count == 0)
            {
                throw new InvalidOperationException(
                    "cannot start the " + rules.Objective + " match: map " + _context.Map.Name + " has no control points");
            }

            try
            {
                _domination = new DominationObjective(
                    new ControlPointMap(points.ControlPoints),
                    settings.CaptureSeconds,
                    settings.PointsPerSecond,
                    settings.DominationScoreLimit,
                    settings.TimeLimitSeconds,
                    settings.MaxCapturers);
            }
            catch (ArgumentException e)
            {
                throw new InvalidOperationException("cannot start the " + rules.Objective + " match: " + e.Message, e);
            }
        }

        private void StartObjective()
        {
            if (_domination == null)
            {
                return;
            }

            _domination.Mode.PointCaptured += HandlePointCaptured;
            _domination.Mode.PointNeutralized += HandlePointNeutralized;
            var spec = _domination.Mode.Spec;
            _log.WriteLine(DominationLog.Tag + " net points=" + _domination.Mode.PointCount.ToString(CultureInfo.InvariantCulture)
                + " scoreLimit=" + spec.ScoreLimit.ToString(CultureInfo.InvariantCulture)
                + " captureSeconds=" + spec.CaptureSeconds.ToString(CultureInfo.InvariantCulture)
                + " pointsPerSecond=" + spec.PointsPerSecond.ToString(CultureInfo.InvariantCulture)
                + " maxCapturers=" + spec.MaxCapturers.ToString(CultureInfo.InvariantCulture));
        }

        private void StopObjective()
        {
            if (_domination == null)
            {
                return;
            }

            _domination.Mode.PointCaptured -= HandlePointCaptured;
            _domination.Mode.PointNeutralized -= HandlePointNeutralized;
            _domination = null;
        }

        private void TickObjective(float deltaSeconds)
        {
            if (_domination == null)
            {
                return;
            }

            var playing = _session.State == GameModeState.Playing;
            if (playing)
            {
                LocatePlayers();
            }

            if (_domination.Tick(playing, deltaSeconds))
            {
                // 続く HandleEnded の DecideWinner が同じ勝者を選ぶ
                _session.Finish(_domination.DecideWinner());
            }
        }

        // 倒れている人とダウン中の人は拠点に数えない
        private void LocatePlayers()
        {
            foreach (var body in _bodies.All)
            {
                var playerId = body.PlayerId;
                if (!_domination.HasPlayer(playerId))
                {
                    continue;
                }

                var counts = _session.HasPlayer(playerId) && _session.IsAlive(playerId) && !_session.IsDowned(playerId);
                _domination.Locate(playerId, counts ? body.Position : (System.Numerics.Vector3?)null);
            }
        }

        private void ObjectivePlayerJoined(int playerId)
        {
            if (_domination != null && _session.HasPlayer(playerId))
            {
                _domination.SetPlayer(playerId, _session.GetTeam(playerId));
            }
        }

        private void ObjectivePlayerLeft(int playerId)
        {
            _domination?.RemovePlayer(playerId);
        }

        private void ObjectivePlayerKilled(int victimId)
        {
            if (_domination != null && _domination.HasPlayer(victimId))
            {
                _domination.Locate(victimId, null);
            }
        }

        private void ObjectiveMatchRestarted()
        {
            if (_domination == null)
            {
                return;
            }

            _domination.Reset();
            _log.WriteLine(DominationLog.Restart());
        }

        private void ObjectiveMatchEnded()
        {
            if (_domination != null)
            {
                _log.WriteLine(DominationLog.State(_domination.Mode));
            }
        }

        // 拠点制圧ならポイントで決めた勝者に差し替える。そうでなければ Session がキル数で決めた値のまま
        private Team? DecideWinner(Team? winner)
        {
            return _domination != null ? _domination.DecideWinner() : winner;
        }

        private void FillObjective(ref MatchSnapshot snapshot)
        {
            snapshot.Objective = _data.Rules.Objective;
            if (_domination == null)
            {
                snapshot.AlphaPoints = 0;
                snapshot.BravoPoints = 0;
                return;
            }

            snapshot.AlphaPoints = DominationHudFormat.DisplayedScore(_domination.Mode.GetScore(Team.Alpha));
            snapshot.BravoPoints = DominationHudFormat.DisplayedScore(_domination.Mode.GetScore(Team.Bravo));
        }

        private void HandlePointCaptured(int pointIndex, Team team)
        {
            _log.WriteLine(DominationLog.Captured(pointIndex, team));
        }

        private void HandlePointNeutralized(int pointIndex, Team previousOwner)
        {
            _log.WriteLine(DominationLog.Neutralized(pointIndex, previousOwner));
        }
    }
}
