using System;

namespace Shooter.Core
{
    public sealed class TeamDeathmatchSpec
    {
        public const int DefaultSideCount = 2;
        public const int DefaultSideCapacity = 8;
        public const int DefaultPointsPerKill = 1;
        public const int DefaultPointsPerAssist = 0;

        public int ScoreLimit { get; }

        public float TimeLimitSeconds { get; }

        public float RespawnSeconds { get; }

        public GameRules Rules { get; }

        public int SideCount { get; }

        public int SideCapacity { get; }

        // KillScore は double を取れるが、チームの点数と ScoreLimit を整数で扱うので整数に絞る。
        public int PointsPerKill { get; }

        public int PointsPerAssist { get; }

        // 全滅で試合を終えないのは、全滅をラウンドの終わりとして外 (EliminationObjective) が数え、試合を次のラウンドへ続けるため
        public bool RespawnsByRound { get; }

        // 立っている味方のいない人もダウンさせ、全員がダウンしても倒さない。ダウン中の敵にとどめを要る試合 (Execution) が、
        // 最後の 1 人も倒れずにとどめを待つようにするため。ダウン (ActionRules.Downed) が Off なら何もしない
        public bool DownsLastStanding { get; }

        public TeamDeathmatchSpec(
            int scoreLimit,
            float timeLimitSeconds,
            float respawnSeconds,
            GameRules rules = null,
            int sideCount = DefaultSideCount,
            int sideCapacity = DefaultSideCapacity,
            int pointsPerKill = DefaultPointsPerKill,
            int pointsPerAssist = DefaultPointsPerAssist,
            bool respawnsByRound = false,
            bool downsLastStanding = false)
        {
            if (scoreLimit <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(scoreLimit), scoreLimit, "scoreLimit must be positive");
            }

            if (!(timeLimitSeconds > 0f) || float.IsInfinity(timeLimitSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(timeLimitSeconds), timeLimitSeconds, "timeLimitSeconds must be positive and finite");
            }

            if (!(respawnSeconds >= 0f) || float.IsInfinity(respawnSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(respawnSeconds), respawnSeconds, "respawnSeconds must not be negative and must be finite");
            }

            if (sideCount < 2)
            {
                throw new ArgumentOutOfRangeException(nameof(sideCount), sideCount, "sideCount must be at least 2");
            }

            if (sideCapacity < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(sideCapacity), sideCapacity, "sideCapacity must be at least 1");
            }

            if (pointsPerKill < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(pointsPerKill), pointsPerKill, "pointsPerKill must be at least 1");
            }

            if (pointsPerAssist < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(pointsPerAssist), pointsPerAssist, "pointsPerAssist must not be negative");
            }

            ScoreLimit = scoreLimit;
            TimeLimitSeconds = timeLimitSeconds;
            RespawnSeconds = respawnSeconds;
            Rules = rules ?? GameRules.Default;
            SideCount = sideCount;
            SideCapacity = sideCapacity;
            PointsPerKill = pointsPerKill;
            PointsPerAssist = pointsPerAssist;
            RespawnsByRound = respawnsByRound;
            DownsLastStanding = downsLastStanding;
        }
    }
}
