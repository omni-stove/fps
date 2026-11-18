using System;
using System.Globalization;
using System.Numerics;
using Shooter.Core;
using Shooter.Protocol;

namespace Shooter.Server
{
    // GameRules.Objective が Domination のとき、拠点の占領を判定し、キル数に代えて拠点のポイントで勝敗を決める。
    // キル数の上限では終わらせず、ポイントが上限に届いたら試合を終え、時間切れのときもポイントの多いほうを勝ちにする
    internal sealed class DominationRules : IMatchObjectiveRules
    {
        private readonly NetMatchSettings _settings;
        private readonly IMatchObjectiveHost _host;
        private DominationObjective _domination;
        private bool _started;

        public DominationRules(NetMatchSettings settings, IMatchObjectiveHost host)
        {
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _host = host ?? throw new ArgumentNullException(nameof(host));
        }

        public MatchObjective Objective => MatchObjective.Domination;

        // UseMap の前は null
        public DominationObjective Domination => _domination;

        public bool PlaysInRounds => false;

        public bool KeepsMatchScore => false;

        public bool HoldsFire => false;

        public float? RemainingSeconds => null;

        public TeamDeathmatchSpec CreateSessionSpec(GameRules rules, NetMatchSettings settings)
        {
            return new TeamDeathmatchSpec(int.MaxValue, settings.TimeLimitSeconds, settings.RespawnSeconds, rules);
        }

        public void CheckMap(ServerMapData map, NetMapPoints points)
        {
            ArgumentNullException.ThrowIfNull(map);
            ArgumentNullException.ThrowIfNull(points);
            if (points.ControlPoints.Count == 0)
            {
                throw new InvalidOperationException(
                    "cannot change to map " + map.Name + " for the " + Objective + " match: it has no control points");
            }
        }

        public void UseMap(ServerMapData map, NetMapPoints points)
        {
            ArgumentNullException.ThrowIfNull(map);
            ArgumentNullException.ThrowIfNull(points);
            var started = _started;
            if (started)
            {
                Stop();
            }

            if (points.ControlPoints.Count == 0)
            {
                throw new InvalidOperationException(
                    "cannot start the " + Objective + " match: map " + map.Name + " has no control points");
            }

            try
            {
                _domination = new DominationObjective(
                    new ControlPointMap(points.ControlPoints),
                    _settings.CaptureSeconds,
                    _settings.PointsPerSecond,
                    _settings.DominationScoreLimit,
                    _settings.TimeLimitSeconds,
                    _settings.MaxCapturers);
            }
            catch (ArgumentException e)
            {
                throw new InvalidOperationException("cannot start the " + Objective + " match: " + e.Message, e);
            }

            if (started)
            {
                Start();
            }
        }

        public void Start()
        {
            if (_domination == null)
            {
                throw new InvalidOperationException("the " + Objective + " rules have no map; call UseMap before Start");
            }

            if (_started)
            {
                return;
            }

            _started = true;
            _domination.Mode.PointCaptured += HandlePointCaptured;
            _domination.Mode.PointNeutralized += HandlePointNeutralized;
            var spec = _domination.Mode.Spec;
            _host.Log.WriteLine(DominationLog.Tag + " net points=" + _domination.Mode.PointCount.ToString(CultureInfo.InvariantCulture)
                + " scoreLimit=" + spec.ScoreLimit.ToString(CultureInfo.InvariantCulture)
                + " captureSeconds=" + spec.CaptureSeconds.ToString(CultureInfo.InvariantCulture)
                + " pointsPerSecond=" + spec.PointsPerSecond.ToString(CultureInfo.InvariantCulture)
                + " maxCapturers=" + spec.MaxCapturers.ToString(CultureInfo.InvariantCulture));
        }

        public void Stop()
        {
            if (!_started)
            {
                return;
            }

            _started = false;
            _domination.Mode.PointCaptured -= HandlePointCaptured;
            _domination.Mode.PointNeutralized -= HandlePointNeutralized;
        }

        public bool Tick(bool matchPlaying, float deltaSeconds)
        {
            if (_domination == null)
            {
                return false;
            }

            if (matchPlaying)
            {
                LocatePlayers();
            }

            return _domination.Tick(matchPlaying, deltaSeconds);
        }

        public void PlayerJoined(int playerId)
        {
            var session = _host.Session;
            if (_domination != null && session != null && session.HasPlayer(playerId))
            {
                _domination.SetPlayer(playerId, session.GetTeam(playerId));
            }
        }

        public void PlayerLeft(int playerId)
        {
            _domination?.RemovePlayer(playerId);
        }

        public void PlayerKilled(int killerId, int victimId)
        {
            if (_domination != null && _domination.HasPlayer(victimId))
            {
                _domination.Locate(victimId, null);
            }
        }

        public void MatchRestarted()
        {
            if (_domination == null)
            {
                return;
            }

            _domination.Reset();
            _host.Log.WriteLine(DominationLog.Restart());
        }

        public void MatchEnded()
        {
            if (_domination != null)
            {
                _host.Log.WriteLine(DominationLog.State(_domination.Mode));
            }
        }

        public RoundPhase? CurrentRoundPhase() => null;

        public Team? DecideWinner(Team? killWinner)
        {
            return _domination != null ? _domination.DecideWinner() : killWinner;
        }

        public void FillMatch(ref MatchSnapshot snapshot)
        {
            snapshot.Objective = Objective;
            if (_domination == null)
            {
                snapshot.AlphaPoints = 0;
                snapshot.BravoPoints = 0;
                return;
            }

            snapshot.AlphaPoints = DominationHudFormat.DisplayedScore(_domination.Mode.GetScore(Team.Alpha));
            snapshot.BravoPoints = DominationHudFormat.DisplayedScore(_domination.Mode.GetScore(Team.Bravo));
        }

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

        public void FillObjectiveItems(SnapshotList<ObjectiveItemSnapshot> items)
        {
            ArgumentNullException.ThrowIfNull(items);
            items.Clear();
        }

        public void FillPlayer(int playerId, ref PlayerSnapshot snapshot)
        {
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

        // 倒れている人とダウン中の人は拠点に数えない
        private void LocatePlayers()
        {
            var session = _host.Session;
            foreach (var body in _host.Bodies.All)
            {
                var playerId = body.PlayerId;
                if (!_domination.HasPlayer(playerId))
                {
                    continue;
                }

                var counts = session.HasPlayer(playerId) && session.IsAlive(playerId) && !session.IsDowned(playerId);
                _domination.Locate(playerId, counts ? body.Position : (Vector3?)null);
            }
        }

        private void HandlePointCaptured(int pointIndex, Team team)
        {
            _host.Log.WriteLine(DominationLog.Captured(pointIndex, team));
        }

        private void HandlePointNeutralized(int pointIndex, Team previousOwner)
        {
            _host.Log.WriteLine(DominationLog.Neutralized(pointIndex, previousOwner));
        }
    }
}
