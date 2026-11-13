using System;

namespace Fps.Core
{
    /// <summary>
    /// ハードポイント (<see cref="Hardpoint"/>) の設定。生成時に値を検査し、以後は変わらない。
    /// </summary>
    public sealed class HardpointSpec
    {
        /// <summary>拠点の数。2 以上 (1 つだけでは移る先が無い)。</summary>
        public int PointCount { get; }

        /// <summary>有効な拠点が次の拠点へ移るまでの秒数。0 より大きい有限値。</summary>
        public float RotateSeconds { get; }

        /// <summary>有効な拠点に自チームだけがいる間、1 秒あたり入るポイント。0 より大きい有限値。</summary>
        public float PointsPerSecond { get; }

        /// <summary>どちらかのチームがこのポイントに届いたら試合が終わる。1 以上。</summary>
        public int ScoreLimit { get; }

        /// <summary>試合の制限時間 (秒)。0 より大きい有限値。</summary>
        public float TimeLimitSeconds { get; }

        /// <summary>倒れてから戻るまでの秒数。0 以上の有限値。</summary>
        public float RespawnSeconds { get; }

        /// <param name="pointCount">拠点の数。2 以上。</param>
        /// <param name="rotateSeconds">有効な拠点が移るまでの秒数。0 より大きい有限値。</param>
        /// <param name="pointsPerSecond">有効な拠点を確保している間に 1 秒あたり入るポイント。0 より大きい有限値。</param>
        /// <param name="scoreLimit">勝利に必要なポイント。1 以上。</param>
        /// <param name="timeLimitSeconds">制限時間 (秒)。0 より大きい有限値。</param>
        /// <param name="respawnSeconds">倒れてから戻るまでの秒数。0 以上の有限値。</param>
        public HardpointSpec(
            int pointCount,
            float rotateSeconds,
            float pointsPerSecond,
            int scoreLimit,
            float timeLimitSeconds,
            float respawnSeconds)
        {
            if (pointCount < 2)
            {
                throw new ArgumentOutOfRangeException(nameof(pointCount), pointCount, "pointCount must be at least 2");
            }

            if (!(rotateSeconds > 0f) || float.IsInfinity(rotateSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(rotateSeconds), rotateSeconds, "rotateSeconds must be positive and finite");
            }

            if (!(pointsPerSecond > 0f) || float.IsInfinity(pointsPerSecond))
            {
                throw new ArgumentOutOfRangeException(nameof(pointsPerSecond), pointsPerSecond, "pointsPerSecond must be positive and finite");
            }

            if (scoreLimit < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(scoreLimit), scoreLimit, "scoreLimit must be at least 1");
            }

            if (!(timeLimitSeconds > 0f) || float.IsInfinity(timeLimitSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(timeLimitSeconds), timeLimitSeconds, "timeLimitSeconds must be positive and finite");
            }

            if (!(respawnSeconds >= 0f) || float.IsInfinity(respawnSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(respawnSeconds), respawnSeconds, "respawnSeconds must not be negative and must be finite");
            }

            PointCount = pointCount;
            RotateSeconds = rotateSeconds;
            PointsPerSecond = pointsPerSecond;
            ScoreLimit = scoreLimit;
            TimeLimitSeconds = timeLimitSeconds;
            RespawnSeconds = respawnSeconds;
        }
    }
}
