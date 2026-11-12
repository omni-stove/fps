using System;

namespace Fps.Core
{
    /// <summary>
    /// チームデスマッチの設定。生成時に値を検査し、以後は変わらない。
    /// </summary>
    public sealed class TeamDeathmatchSpec
    {
        /// <summary>どちらかのチームがこの点数に届いたら試合が終わる。1 以上。</summary>
        public int ScoreLimit { get; }

        /// <summary>試合の制限時間 (秒)。0 より大きい有限値。</summary>
        public float TimeLimitSeconds { get; }

        /// <summary>倒されてからリスポーンできるまでの秒数。0 以上の有限値。0 なら倒された時点ですぐ出る。</summary>
        public float RespawnSeconds { get; }

        /// <param name="scoreLimit">勝利に必要な点数。1 以上。</param>
        /// <param name="timeLimitSeconds">制限時間 (秒)。0 より大きい有限値。</param>
        /// <param name="respawnSeconds">リスポーンまでの秒数。0 以上の有限値。</param>
        public TeamDeathmatchSpec(int scoreLimit, float timeLimitSeconds, float respawnSeconds)
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

            ScoreLimit = scoreLimit;
            TimeLimitSeconds = timeLimitSeconds;
            RespawnSeconds = respawnSeconds;
        }
    }
}
