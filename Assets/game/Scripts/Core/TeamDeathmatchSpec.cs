using System;

namespace Shooter.Core
{
    /// <summary>
    /// チームデスマッチの設定。生成時に値を検査し、以後は変わらない。
    /// </summary>
    public sealed class TeamDeathmatchSpec
    {
        public const int DefaultSideCount = 2;
        public const int DefaultSideCapacity = 8;
        public const int DefaultPointsPerKill = 1;

        /// <summary>どちらかのチームがこの点数に届いたら試合が終わる。1 以上。</summary>
        public int ScoreLimit { get; }

        /// <summary>試合の制限時間 (秒)。0 より大きい有限値。</summary>
        public float TimeLimitSeconds { get; }

        /// <summary>倒されてからリスポーンできるまでの秒数。0 以上の有限値。0 なら倒された時点ですぐ出る。</summary>
        public float RespawnSeconds { get; }

        /// <summary>このモードのルール。指定しなければ <see cref="GameRules.Default"/>。</summary>
        public GameRules Rules { get; }

        /// <summary>陣営の数。2 以上。</summary>
        public int SideCount { get; }

        /// <summary>1 つの陣営に入れる人数の上限。1 以上。</summary>
        public int SideCapacity { get; }

        // KillScore は double を取れるが、チームの点数と ScoreLimit を整数で扱うので整数に絞る。
        /// <summary>別の陣営を 1 回倒すごとに入る点。1 以上。</summary>
        public int PointsPerKill { get; }

        /// <param name="scoreLimit">勝利に必要な点数。1 以上。</param>
        /// <param name="timeLimitSeconds">制限時間 (秒)。0 より大きい有限値。</param>
        /// <param name="respawnSeconds">リスポーンまでの秒数。0 以上の有限値。</param>
        /// <param name="rules">モードのルール。null なら <see cref="GameRules.Default"/>。</param>
        /// <param name="sideCount">陣営の数。2 以上。</param>
        /// <param name="sideCapacity">1 つの陣営の人数の上限。1 以上。</param>
        /// <param name="pointsPerKill">キル 1 回の点。1 以上。</param>
        public TeamDeathmatchSpec(
            int scoreLimit,
            float timeLimitSeconds,
            float respawnSeconds,
            GameRules rules = null,
            int sideCount = DefaultSideCount,
            int sideCapacity = DefaultSideCapacity,
            int pointsPerKill = DefaultPointsPerKill)
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

            ScoreLimit = scoreLimit;
            TimeLimitSeconds = timeLimitSeconds;
            RespawnSeconds = respawnSeconds;
            Rules = rules ?? GameRules.Default;
            SideCount = sideCount;
            SideCapacity = sideCapacity;
            PointsPerKill = pointsPerKill;
        }
    }
}
