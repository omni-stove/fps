using System;

namespace Fps.Core
{
    /// <summary>
    /// 拠点制圧 (<see cref="Domination"/>) の設定。生成時に値を検査し、以後は変わらない。
    /// </summary>
    public sealed class DominationSpec
    {
        /// <summary><see cref="MaxCapturers"/> を指定しなかったときの値。</summary>
        public const int DefaultMaxCapturers = 3;

        /// <summary>拠点の数。1 以上。</summary>
        public int PointCount { get; }

        /// <summary>1 人で中立の拠点を占領し終えるまでの秒数。0 より大きい有限値。</summary>
        public float CaptureSeconds { get; }

        /// <summary>拠点 1 つを持っているチームに 1 秒あたり入るポイント。0 より大きい有限値。</summary>
        public float PointsPerSecond { get; }

        /// <summary>どちらかのチームがこのポイントに届いたら試合が終わる。1 以上。</summary>
        public int ScoreLimit { get; }

        /// <summary>試合の制限時間 (秒)。0 より大きい有限値。</summary>
        public float TimeLimitSeconds { get; }

        /// <summary>占領の速さに数える人数の上限。1 以上。</summary>
        public int MaxCapturers { get; }

        /// <param name="pointCount">拠点の数。1 以上。</param>
        /// <param name="captureSeconds">1 人で中立の拠点を占領し終えるまでの秒数。0 より大きい有限値。</param>
        /// <param name="pointsPerSecond">拠点 1 つあたり 1 秒に入るポイント。0 より大きい有限値。</param>
        /// <param name="scoreLimit">勝利に必要なポイント。1 以上。</param>
        /// <param name="timeLimitSeconds">制限時間 (秒)。0 より大きい有限値。</param>
        /// <param name="maxCapturers">占領の速さに数える人数の上限。1 以上。</param>
        public DominationSpec(
            int pointCount,
            float captureSeconds,
            float pointsPerSecond,
            int scoreLimit,
            float timeLimitSeconds,
            int maxCapturers = DefaultMaxCapturers)
        {
            if (pointCount < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(pointCount), pointCount, "pointCount must be at least 1");
            }

            if (!(captureSeconds > 0f) || float.IsInfinity(captureSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(captureSeconds), captureSeconds, "captureSeconds must be positive and finite");
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

            if (maxCapturers < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(maxCapturers), maxCapturers, "maxCapturers must be at least 1");
            }

            PointCount = pointCount;
            CaptureSeconds = captureSeconds;
            PointsPerSecond = pointsPerSecond;
            ScoreLimit = scoreLimit;
            TimeLimitSeconds = timeLimitSeconds;
            MaxCapturers = maxCapturers;
        }
    }
}
