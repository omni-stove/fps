using System;

namespace Shooter.Core
{
    /// <summary>
    /// 旗取り (<see cref="CaptureTheFlag"/>) の設定。生成時に値を検査し、以後は変わらない。
    /// </summary>
    public sealed class CaptureTheFlagSpec
    {
        public const int DefaultScoreLimit = 3;
        public const float DefaultTimeLimitSeconds = 600f;
        public const float DefaultRespawnSeconds = 5f;
        public const float DefaultPickupRadius = 1.5f;
        public const float DefaultCaptureRadius = 2f;
        public const float DefaultReturnSeconds = 20f;
        public const int DefaultPointsPerCapture = 1;

        /// <summary>どちらかの陣営がこの得点に届いたら試合が終わる。1 以上。</summary>
        public int ScoreLimit { get; }

        /// <summary>試合の制限時間 (秒)。0 より大きい有限値。</summary>
        public float TimeLimitSeconds { get; }

        /// <summary>倒れてから戻るまでの秒数。0 以上の有限値。</summary>
        public float RespawnSeconds { get; }

        /// <summary>旗に触れたことにする距離 (m、水平)。敵の旗を持つのも、落ちた自陣の旗を台に戻すのもこの距離。0 より大きい有限値。</summary>
        public float PickupRadius { get; }

        /// <summary>敵の旗を運ぶ人が自陣の台に着いたことにする距離 (m、水平)。0 より大きい有限値。</summary>
        public float CaptureRadius { get; }

        /// <summary>落ちた旗が誰にも触れられずに台へ戻るまでの秒数。0 より大きい有限値。</summary>
        public float ReturnSeconds { get; }

        /// <summary>敵の旗を 1 回自陣の台へ運んだときに入る点。1 以上。</summary>
        public int PointsPerCapture { get; }

        /// <exception cref="ArgumentOutOfRangeException">どれかの値が範囲の外。</exception>
        public CaptureTheFlagSpec(
            int scoreLimit = DefaultScoreLimit,
            float timeLimitSeconds = DefaultTimeLimitSeconds,
            float respawnSeconds = DefaultRespawnSeconds,
            float pickupRadius = DefaultPickupRadius,
            float captureRadius = DefaultCaptureRadius,
            float returnSeconds = DefaultReturnSeconds,
            int pointsPerCapture = DefaultPointsPerCapture)
        {
            if (scoreLimit < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(scoreLimit), scoreLimit, "scoreLimit must be at least 1");
            }

            RequirePositiveFinite(timeLimitSeconds, nameof(timeLimitSeconds));

            if (!(respawnSeconds >= 0f) || float.IsInfinity(respawnSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(respawnSeconds), respawnSeconds, "respawnSeconds must not be negative and must be finite");
            }

            RequirePositiveFinite(pickupRadius, nameof(pickupRadius));
            RequirePositiveFinite(captureRadius, nameof(captureRadius));
            RequirePositiveFinite(returnSeconds, nameof(returnSeconds));

            if (pointsPerCapture < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(pointsPerCapture), pointsPerCapture, "pointsPerCapture must be at least 1");
            }

            ScoreLimit = scoreLimit;
            TimeLimitSeconds = timeLimitSeconds;
            RespawnSeconds = respawnSeconds;
            PickupRadius = pickupRadius;
            CaptureRadius = captureRadius;
            ReturnSeconds = returnSeconds;
            PointsPerCapture = pointsPerCapture;
        }

        private static void RequirePositiveFinite(float value, string name)
        {
            if (!(value > 0f) || float.IsInfinity(value))
            {
                throw new ArgumentOutOfRangeException(name, value, name + " must be positive and finite");
            }
        }
    }
}
