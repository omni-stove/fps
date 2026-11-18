using System;
using System.Globalization;
using Shooter.Core;
using Shooter.Protocol;

namespace Shooter.Server
{
    // GameRules.Objective が Hardpoint のとき、順に移る 1 つの拠点を押さえている間の点で勝敗を決める。点・拠点の移り・時間切れはオフラインと同じ
    // Core の Hardpoint に任せ、ここは体の位置を有効な拠点にいるかどうかに直して渡すだけにする。
    // Session (TeamDeathmatchSession) は体力・出現だけを受け持ち、キル数でも時間でも先に終わらないよう上限を届かない値にする
    internal sealed class HardpointRules : IMatchObjectiveRules
    {
        private readonly NetMatchSettings _settings;
        private readonly IMatchObjectiveHost _host;
        private Hardpoint _hardpoint;
        private ControlPointMap _map;
        private bool _started;
        private HardpointStatus? _loggedStatus;
        private Side? _loggedHolder;

        public HardpointRules(NetMatchSettings settings, IMatchObjectiveHost host)
        {
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _host = host ?? throw new ArgumentNullException(nameof(host));
        }

        public MatchObjective Objective => MatchObjective.Hardpoint;

        // UseMap の前は null
        public Hardpoint Hardpoint => _hardpoint;

        public bool PlaysInRounds => false;

        public bool KeepsMatchScore => false;

        public bool HoldsFire => false;

        public float? RemainingSeconds => _hardpoint != null ? Math.Max(0f, _hardpoint.Session.RemainingSeconds) : null;

        public TeamDeathmatchSpec CreateSessionSpec(GameRules rules, NetMatchSettings settings)
        {
            ArgumentNullException.ThrowIfNull(settings);
            return new TeamDeathmatchSpec(int.MaxValue, float.MaxValue, settings.Hardpoint.RespawnSeconds, rules);
        }

        public void CheckMap(ServerMapData map, NetMapPoints points)
        {
            ArgumentNullException.ThrowIfNull(map);
            ArgumentNullException.ThrowIfNull(points);
            Build(points, "cannot change to map " + map.Name + " for the " + Objective + " match: ");
        }

        public void UseMap(ServerMapData map, NetMapPoints points)
        {
            ArgumentNullException.ThrowIfNull(map);
            ArgumentNullException.ThrowIfNull(points);
            var built = Build(points, "cannot start the " + Objective + " match on map " + map.Name + ": ");
            var started = _started;
            if (started)
            {
                Stop();
            }

            _map = built.Map;
            _hardpoint = built.Hardpoint;
            _loggedStatus = null;
            _loggedHolder = null;
            if (started)
            {
                Start();
            }
        }

        public void Start()
        {
            if (_hardpoint == null)
            {
                throw new InvalidOperationException("the " + Objective + " rules have no map; call UseMap before Start");
            }

            if (_started)
            {
                return;
            }

            _started = true;
            _hardpoint.PointMoved += HandlePointMoved;
            _host.Log.WriteLine(HardpointRulesLog.Started(_hardpoint.Spec));
        }

        public void Stop()
        {
            if (!_started)
            {
                return;
            }

            _started = false;
            _hardpoint.PointMoved -= HandlePointMoved;
        }

        public bool Tick(bool matchPlaying, float deltaSeconds)
        {
            if (_hardpoint == null || !matchPlaying)
            {
                return false;
            }

            var session = _hardpoint.Session;
            if (session.State == GameModeState.Waiting)
            {
                session.Start();
            }

            if (session.State != GameModeState.Playing)
            {
                return false;
            }

            LocatePlayers();
            LogStatus();
            session.Tick(deltaSeconds);
            return session.State == GameModeState.Ended;
        }

        // チームを替えて入り直した人も来るので、陣営が違えば入れ直す
        public void PlayerJoined(int playerId)
        {
            var session = _host.Session;
            if (_hardpoint == null || session == null || !session.HasPlayer(playerId))
            {
                return;
            }

            var side = Side.FromTeam(session.GetTeam(playerId));
            var modeSession = _hardpoint.Session;
            if (modeSession.HasPlayer(playerId))
            {
                if (modeSession.GetSide(playerId) == side)
                {
                    return;
                }

                RemovePlayer(playerId);
            }

            modeSession.AddPlayer(playerId, side);
            _hardpoint.SetPlayerPoint(playerId, null);
        }

        public void PlayerLeft(int playerId)
        {
            if (_hardpoint != null && _hardpoint.Session.HasPlayer(playerId))
            {
                RemovePlayer(playerId);
            }
        }

        // 次の Tick で位置を見直すまでの間も、倒れた人をスナップショットで拠点に数えない
        public void PlayerKilled(int killerId, int victimId)
        {
            if (_hardpoint != null && _hardpoint.Session.HasPlayer(victimId))
            {
                _hardpoint.SetPlayerPoint(victimId, null);
            }
        }

        public void MatchRestarted()
        {
            if (_hardpoint == null)
            {
                return;
            }

            _hardpoint.Reset();
            _loggedStatus = null;
            _loggedHolder = null;
            _host.Log.WriteLine(HardpointRulesLog.Restart());
        }

        public void MatchEnded()
        {
            if (_hardpoint != null)
            {
                _host.Log.WriteLine(HardpointRulesLog.State(_hardpoint));
            }
        }

        public RoundPhase? CurrentRoundPhase() => null;

        public Team? DecideWinner(Team? killWinner)
        {
            if (_hardpoint == null)
            {
                return killWinner;
            }

            var session = _hardpoint.Session;
            return Side.ToTeam(session.State == GameModeState.Ended ? session.Winner : session.LeadingSide);
        }

        public void FillMatch(ref MatchSnapshot snapshot)
        {
            snapshot.Objective = Objective;
            if (_hardpoint == null)
            {
                snapshot.AlphaPoints = 0;
                snapshot.BravoPoints = 0;
                snapshot.HasActivePoint = false;
                snapshot.ActivePointIndex = 0;
                snapshot.ObjectiveRemainingSeconds = 0;
                return;
            }

            var session = _hardpoint.Session;
            snapshot.AlphaPoints = HudFormat.DisplayedScore(session.GetScore(Side.Alpha));
            snapshot.BravoPoints = HudFormat.DisplayedScore(session.GetScore(Side.Bravo));
            snapshot.HasActivePoint = true;
            snapshot.ActivePointIndex = _hardpoint.ActivePoint;
            snapshot.ObjectiveRemainingSeconds = HudFormat.DisplayedSeconds(_hardpoint.SecondsUntilMove);
        }

        // 有効でない拠点は持ち主も取り合いも無しで送る。有効な拠点の値は押さえている陣営の側に振り切る (Domination の ControlPercent と同じく Alpha が正)
        public void FillControlPoints(SnapshotList<ControlPointSnapshot> points)
        {
            ArgumentNullException.ThrowIfNull(points);
            points.Clear();
            if (_hardpoint == null)
            {
                return;
            }

            var active = _hardpoint.ActivePoint;
            var holder = Side.ToTeam(_hardpoint.Holder);
            var contested = _hardpoint.Status == HardpointStatus.Contested;
            for (var i = 0; i < _hardpoint.PointCount; i++)
            {
                ref var point = ref points.Add();
                var held = i == active && holder.HasValue;
                point.Index = i;
                point.Present = ControlPointFields.All;
                point.HasOwner = held;
                point.Owner = held ? holder.Value : Team.Alpha;
                point.ControlPercent = !held ? (sbyte)0
                    : holder.Value == Team.Alpha ? (sbyte)ControlPointSnapshot.FullPercent : (sbyte)-ControlPointSnapshot.FullPercent;
                point.Contested = i == active && contested;
            }
        }

        // スナップショットの有効な拠点の番号は 1 バイトで MaxControlPoints 未満に限るので、それを超える拠点のマップは受け付けない
        private (ControlPointMap Map, Hardpoint Hardpoint) Build(NetMapPoints points, string failure)
        {
            var count = points.ControlPoints.Count;
            if (count < HardpointSpec.MinPointCount)
            {
                throw new InvalidOperationException(
                    failure + "it has " + Text(count) + " control point(s) but needs at least " + Text(HardpointSpec.MinPointCount));
            }

            if (count > SnapshotMessage.MaxControlPoints)
            {
                throw new InvalidOperationException(
                    failure + "it has " + Text(count) + " control points but at most " + Text(SnapshotMessage.MaxControlPoints) + " can be sent");
            }

            try
            {
                var map = new ControlPointMap(points.ControlPoints);
                var tuning = _settings.Hardpoint;
                var spec = new HardpointSpec(
                    count, tuning.RotateSeconds, tuning.PointsPerSecond, tuning.ScoreLimit, tuning.TimeLimitSeconds, tuning.RespawnSeconds);
                return (map, new Hardpoint(spec));
            }
            catch (ArgumentException e)
            {
                throw new InvalidOperationException(failure + e.Message, e);
            }
        }

        private static string Text(int value) => value.ToString(CultureInfo.InvariantCulture);

        // 倒れている人・ダウン中の人・まだ出ていない人は拠点に数えない。数えるのは有効な拠点だけなので、ほかの拠点にいる人はどこにもいないことにする
        private void LocatePlayers()
        {
            var session = _host.Session;
            var active = _hardpoint.ActivePoint;
            var area = _map[active];
            foreach (var body in _host.Bodies.All)
            {
                var playerId = body.PlayerId;
                if (!_hardpoint.Session.HasPlayer(playerId))
                {
                    continue;
                }

                var counts = body.HasSpawned
                    && session != null
                    && session.HasPlayer(playerId)
                    && session.IsAlive(playerId)
                    && !session.IsDowned(playerId);
                _hardpoint.SetPlayerPoint(playerId, counts && area.Contains(body.Position) ? active : (int?)null);
            }
        }

        private void RemovePlayer(int playerId)
        {
            _hardpoint.SetPlayerPoint(playerId, null);
            _hardpoint.Session.RemovePlayer(playerId);
        }

        private void LogStatus()
        {
            var status = _hardpoint.Status;
            var holder = _hardpoint.Holder;
            if (_loggedStatus == status && _loggedHolder == holder)
            {
                return;
            }

            _loggedStatus = status;
            _loggedHolder = holder;
            _host.Log.WriteLine(HardpointRulesLog.Status(_hardpoint.ActivePoint, status, holder));
        }

        // 移った先にいる人はまだ調べていないので、次の Tick で調べるまで誰もいないことにする (HardpointRunner と同じ)
        private void HandlePointMoved(int previous, int next)
        {
            _host.Log.WriteLine(HardpointRulesLog.Moved(previous, next));
            foreach (var playerId in _hardpoint.Session.GetPlayerIds())
            {
                _hardpoint.SetPlayerPoint(playerId, null);
            }

            _loggedStatus = null;
            _loggedHolder = null;
        }
    }
}
