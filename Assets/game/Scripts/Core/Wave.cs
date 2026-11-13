using System;
using System.Globalization;

namespace Fps.Core
{
    /// <summary>
    /// PvE の 1 つの波の値。何体出すか・敵をどれだけ強くするか・同時に何体まで出しておくか・全滅させた後に何秒休むか。
    /// </summary>
    public readonly struct Wave : IEquatable<Wave>
    {
        /// <summary>この波で出す敵の数。1 以上。</summary>
        public int EnemyCount { get; }

        /// <summary>敵の強さの倍率 (<see cref="WaveStrength"/>)。0 より大きい有限値。1 なら元の強さのまま。</summary>
        public float StrengthMultiplier { get; }

        /// <summary>同時に出しておく敵の上限。1 以上。倒されて減った分だけ残りを出す。</summary>
        public int MaxAlive { get; }

        /// <summary>この波を全滅させてから次の波を出すまでの休憩 (秒)。0 以上の有限値。</summary>
        public float RestSeconds { get; }

        /// <param name="enemyCount">この波で出す敵の数。1 以上。</param>
        /// <param name="strengthMultiplier">敵の強さの倍率。0 より大きい有限値。</param>
        /// <param name="maxAlive">同時に出しておく敵の上限。1 以上。</param>
        /// <param name="restSeconds">全滅させてから次の波までの休憩 (秒)。0 以上の有限値。</param>
        public Wave(int enemyCount, float strengthMultiplier, int maxAlive, float restSeconds)
        {
            if (enemyCount < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(enemyCount), enemyCount, "enemyCount must be at least 1");
            }

            if (!(strengthMultiplier > 0f) || float.IsInfinity(strengthMultiplier))
            {
                throw new ArgumentOutOfRangeException(nameof(strengthMultiplier), strengthMultiplier, "strengthMultiplier must be positive and finite");
            }

            if (maxAlive < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(maxAlive), maxAlive, "maxAlive must be at least 1");
            }

            if (!(restSeconds >= 0f) || float.IsInfinity(restSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(restSeconds), restSeconds, "restSeconds must be non-negative and finite");
            }

            EnemyCount = enemyCount;
            StrengthMultiplier = strengthMultiplier;
            MaxAlive = maxAlive;
            RestSeconds = restSeconds;
        }

        public bool Equals(Wave other)
        {
            return EnemyCount == other.EnemyCount
                && StrengthMultiplier.Equals(other.StrengthMultiplier)
                && MaxAlive == other.MaxAlive
                && RestSeconds.Equals(other.RestSeconds);
        }

        public override bool Equals(object obj) => obj is Wave other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = EnemyCount;
                hash = (hash * 397) ^ StrengthMultiplier.GetHashCode();
                hash = (hash * 397) ^ MaxAlive;
                hash = (hash * 397) ^ RestSeconds.GetHashCode();
                return hash;
            }
        }

        public override string ToString()
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "Wave(enemies={0}, strength={1}, maxAlive={2}, rest={3})",
                EnemyCount,
                StrengthMultiplier,
                MaxAlive,
                RestSeconds);
        }

        public static bool operator ==(Wave left, Wave right) => left.Equals(right);

        public static bool operator !=(Wave left, Wave right) => !left.Equals(right);
    }
}
