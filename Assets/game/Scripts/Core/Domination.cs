using System;
using System.Collections.Generic;

namespace Shooter.Core
{
    public sealed class Domination : IGameMode
    {
        public event Action<GameModeState> StateChanged;

        public event Action<Team?> Ended;

        public event Action<int, Team> PointCaptured;

        public event Action<int, Team> PointNeutralized;

        private readonly ModeSession _session;
        private readonly ControlPoint[] _points;

        // オフラインの Runner は AddPlayer を通さず Session に直に人を足すので、顔ぶれは _session を正とし、ここには拠点にいる人だけを持つ。
        private readonly Dictionary<int, int?> _playerPoints = new Dictionary<int, int?>();

        public DominationSpec Spec { get; }

        public ModeSession Session => _session;
        public GameModeState State => _session.State;

        public float RemainingSeconds => _session.RemainingSeconds;

        public Team? Winner => Side.ToTeam(_session.Winner);

        public int PlayerCount => _session.PlayerCount;

        public int PointCount => _points.Length;

        public Domination(DominationSpec spec, IRespawnPolicy respawnPolicy = null)
        {
            Spec = spec ?? throw new ArgumentNullException(nameof(spec));

            _points = new ControlPoint[spec.PointCount];
            for (var i = 0; i < _points.Length; i++)
            {
                var index = i;
                var point = new ControlPoint(spec.CaptureSeconds, spec.MaxCapturers);
                point.Captured += team => PointCaptured?.Invoke(index, team);
                point.Neutralized += team => PointNeutralized?.Invoke(index, team);
                _points[i] = point;
            }

            _session = new ModeSession(
                new TwoTeams(),
                spec.TimeLimitSeconds,
                new IScoreRule[] { new ControlPointScore(this) },
                respawnPolicy,
                new IWinCondition[] { new ScoreLimitWin(spec.ScoreLimit), new TimeLimitWin() });
            _session.StateChanged += state => StateChanged?.Invoke(state);
            _session.Ended += winner => Ended?.Invoke(Side.ToTeam(winner));
        }

        public void Start() => _session.Start();

        public void Reset()
        {
            foreach (var point in _points)
            {
                point.Reset();
            }

            _playerPoints.Clear();
            _session.Reset();
        }

        public void AddPlayer(int playerId, Team team)
        {
            _session.AddPlayer(playerId, Side.FromTeam(team));
        }

        public void RemovePlayer(int playerId)
        {
            _session.RemovePlayer(playerId);
            _playerPoints.Remove(playerId);
        }

        public bool HasPlayer(int playerId) => _session.HasPlayer(playerId);

        public Team GetTeam(int playerId) => _session.GetSide(playerId).ToTeam();

        public int GetKills(int playerId) => _session.GetKills(playerId);

        public int GetDeaths(int playerId) => _session.GetDeaths(playerId);

        public int GetAssists(int playerId) => _session.GetAssists(playerId);

        public int? GetPlayerPoint(int playerId) => FindPoint(playerId);

        public void SetPlayerPoint(int playerId, int? pointIndex)
        {
            FindPoint(playerId);
            if (pointIndex.HasValue && (pointIndex.Value < 0 || pointIndex.Value >= _points.Length))
            {
                throw new ArgumentOutOfRangeException(nameof(pointIndex), pointIndex, "no control point with this index");
            }

            _playerPoints[playerId] = pointIndex;
        }

        public Team? GetOwner(int pointIndex) => Point(pointIndex).Owner;

        public float GetControl(int pointIndex) => Point(pointIndex).Control;

        public bool IsContested(int pointIndex) => Point(pointIndex).IsContested;

        public int CountOwned(Team team)
        {
            Side.FromTeam(team);
            var owned = 0;
            foreach (var point in _points)
            {
                if (point.Owner == team)
                {
                    owned++;
                }
            }

            return owned;
        }

        public float GetScore(Team team) => (float)_session.GetScore(Side.FromTeam(team));

        public void ReportKill(int killerId, int victimId)
        {
            ReportKill(killerId, victimId, null);
        }

        public void ReportKill(int killerId, int victimId, IReadOnlyList<int> assisterIds)
        {
            var playing = State == GameModeState.Playing;
            _session.ReportKill(killerId, victimId, assisterIds);
            if (playing)
            {
                LeavePoint(victimId);
            }
        }

        public void ReportDeath(int playerId)
        {
            var playing = State == GameModeState.Playing;
            _session.ReportDeath(playerId);
            if (playing)
            {
                LeavePoint(playerId);
            }
        }

        public void Tick(float deltaSeconds) => _session.Tick(deltaSeconds);

        private void LeavePoint(int playerId)
        {
            _playerPoints.Remove(playerId);
        }

        private void UpdatePoints(float elapsed)
        {
            var alphaCounts = new int[_points.Length];
            var bravoCounts = new int[_points.Length];
            foreach (var pair in _playerPoints)
            {
                if (!pair.Value.HasValue || !_session.HasPlayer(pair.Key))
                {
                    continue;
                }

                if (_session.GetSide(pair.Key) == Side.Alpha)
                {
                    alphaCounts[pair.Value.Value]++;
                }
                else
                {
                    bravoCounts[pair.Value.Value]++;
                }
            }

            for (var i = 0; i < _points.Length && State == GameModeState.Playing; i++)
            {
                _points[i].Update(alphaCounts[i], bravoCounts[i], elapsed);
            }
        }

        private ControlPoint Point(int pointIndex)
        {
            if (pointIndex < 0 || pointIndex >= _points.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(pointIndex), pointIndex, "no control point with this index");
            }

            return _points[pointIndex];
        }

        private int? FindPoint(int playerId)
        {
            if (!_session.HasPlayer(playerId))
            {
                throw new ArgumentOutOfRangeException(nameof(playerId), playerId, "no player with this id");
            }

            return _playerPoints.TryGetValue(playerId, out var pointIndex) ? pointIndex : null;
        }

        private sealed class ControlPointScore : IScoreRule
        {
            private readonly Domination _owner;

            public ControlPointScore(Domination owner)
            {
                _owner = owner;
            }

            public double PointsForKill(ModeSession session, int killerId, int victimId) => 0d;

            public double PointsForAssist(ModeSession session, int assisterId, int victimId) => 0d;

            public double PointsPerSecond(ModeSession session, Side side)
            {
                return _owner.CountOwned(side.ToTeam()) * (double)_owner.Spec.PointsPerSecond;
            }

            public void Advance(ModeSession session, float seconds) => _owner.UpdatePoints(seconds);
        }
    }
}
