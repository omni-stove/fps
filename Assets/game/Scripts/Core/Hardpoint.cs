using System;
using System.Collections.Generic;

namespace Shooter.Core
{
    public sealed class Hardpoint
    {
        // 小刻みな Tick を足し合わせると移る時刻にわずかに届かないことがあるので、この差までは届いたものとして扱う。
        private const double _snap = 1e-6;

        public event Action<int, int> PointMoved;

        private readonly Dictionary<int, int?> _playerPoints = new Dictionary<int, int?>();
        private double _secondsUntilMove;

        public HardpointSpec Spec { get; }

        // 参加者の追加をラップせず ModeSession をそのまま見せるのは、シーンの Runner が ModeSession に参加者を加える形に合わせるため。
        public ModeSession Session { get; }

        public int ActivePoint { get; private set; }

        public float SecondsUntilMove => (float)_secondsUntilMove;

        public int NextPoint => (ActivePoint + 1) % Spec.PointCount;

        public int PointCount => Spec.PointCount;

        public Hardpoint(HardpointSpec spec)
        {
            Spec = spec ?? throw new ArgumentNullException(nameof(spec));
            _secondsUntilMove = spec.RotateSeconds;
            Session = new ModeSession(
                new TwoTeams(),
                spec.TimeLimitSeconds,
                new IScoreRule[] { new ActivePointScore(this) },
                new IndividualRespawn(spec.RespawnSeconds),
                new IWinCondition[] { new ScoreLimitWin(spec.ScoreLimit), new TimeLimitWin() });
        }

        public void Reset()
        {
            ActivePoint = 0;
            _secondsUntilMove = Spec.RotateSeconds;
            _playerPoints.Clear();
            Session.Reset();
        }

        public void SetPlayerPoint(int playerId, int? pointIndex)
        {
            if (!Session.HasPlayer(playerId))
            {
                throw new ArgumentOutOfRangeException(nameof(playerId), playerId, "no player with this id");
            }

            if (pointIndex.HasValue && (pointIndex.Value < 0 || pointIndex.Value >= Spec.PointCount))
            {
                throw new ArgumentOutOfRangeException(nameof(pointIndex), pointIndex, "no control point with this index");
            }

            _playerPoints[playerId] = pointIndex;
        }

        public int? GetPlayerPoint(int playerId)
        {
            if (!Session.HasPlayer(playerId))
            {
                throw new ArgumentOutOfRangeException(nameof(playerId), playerId, "no player with this id");
            }

            return _playerPoints.TryGetValue(playerId, out var point) ? point : null;
        }

        public int CountOnActivePoint(Side side)
        {
            var count = 0;
            foreach (var pair in _playerPoints)
            {
                if (pair.Value == ActivePoint
                    && Session.HasPlayer(pair.Key)
                    && !Session.IsDown(pair.Key)
                    && Session.GetSide(pair.Key) == side)
                {
                    count++;
                }
            }

            return count;
        }

        public HardpointStatus Status
        {
            get
            {
                var alpha = CountOnActivePoint(Side.Alpha) > 0;
                var bravo = CountOnActivePoint(Side.Bravo) > 0;
                if (alpha && bravo)
                {
                    return HardpointStatus.Contested;
                }

                return alpha || bravo ? HardpointStatus.Held : HardpointStatus.Empty;
            }
        }

        public Side? Holder
        {
            get
            {
                var alpha = CountOnActivePoint(Side.Alpha) > 0;
                var bravo = CountOnActivePoint(Side.Bravo) > 0;
                if (alpha == bravo)
                {
                    return null;
                }

                return alpha ? Side.Alpha : Side.Bravo;
            }
        }

        private void Advance(float seconds)
        {
            if (!(seconds > 0f))
            {
                return;
            }

            _secondsUntilMove -= seconds;
            while (_secondsUntilMove <= _snap)
            {
                var previous = ActivePoint;
                ActivePoint = NextPoint;
                _secondsUntilMove += Spec.RotateSeconds;
                PointMoved?.Invoke(previous, ActivePoint);

                // ハンドラの中で Reset されたら、戻した値の上でさらに移さない。
                if (Session.State != GameModeState.Playing)
                {
                    return;
                }
            }
        }

        private sealed class ActivePointScore : IScoreRule
        {
            private readonly Hardpoint _owner;

            public ActivePointScore(Hardpoint owner)
            {
                _owner = owner;
            }

            public double PointsForKill(ModeSession session, int killerId, int victimId) => 0d;

            public double PointsForAssist(ModeSession session, int assisterId, int victimId) => 0d;

            public double PointsPerSecond(ModeSession session, Side side)
            {
                return _owner.Holder == side ? _owner.Spec.PointsPerSecond : 0d;
            }

            public void Advance(ModeSession session, float seconds) => _owner.Advance(seconds);
        }
    }
}
