using System;

namespace Fps.Core
{
    /// <summary>
    /// キルコンファームド (<see cref="KillConfirmed"/>) の設定。生成時に値を検査し、以後は変わらない。
    /// </summary>
    public sealed class KillConfirmedSpec
    {
        /// <summary>どちらかのチームがこの確定数 (得点) に届いたら試合が終わる。1 以上。</summary>
        public int ScoreLimit { get; }

        /// <summary>試合の制限時間 (秒)。0 より大きい有限値。</summary>
        public float TimeLimitSeconds { get; }

        /// <summary>倒れてから戻るまでの秒数。0 以上の有限値。</summary>
        public float RespawnSeconds { get; }

        /// <summary>落ちたタグが誰にも拾われずに消えるまでの秒数。0 より大きい有限値。</summary>
        public float TagLifetimeSeconds { get; }

        /// <param name="scoreLimit">勝利に必要な確定数。1 以上。</param>
        /// <param name="timeLimitSeconds">制限時間 (秒)。0 より大きい有限値。</param>
        /// <param name="respawnSeconds">倒れてから戻るまでの秒数。0 以上の有限値。</param>
        /// <param name="tagLifetimeSeconds">タグが消えるまでの秒数。0 より大きい有限値。</param>
        public KillConfirmedSpec(int scoreLimit, float timeLimitSeconds, float respawnSeconds, float tagLifetimeSeconds)
        {
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

            if (!(tagLifetimeSeconds > 0f) || float.IsInfinity(tagLifetimeSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(tagLifetimeSeconds), tagLifetimeSeconds, "tagLifetimeSeconds must be positive and finite");
            }

            ScoreLimit = scoreLimit;
            TimeLimitSeconds = timeLimitSeconds;
            RespawnSeconds = respawnSeconds;
            TagLifetimeSeconds = tagLifetimeSeconds;
        }
    }
}
