using System;

namespace Fps.Core
{
    /// <summary>
    /// 波の番号 (1 から) から <see cref="Wave"/> を決める式。敵の数と強さの倍率は番号に比例して増え、同時に出す上限と休憩は一定。
    /// <list type="bullet">
    /// <item>敵の数 = <see cref="FirstEnemyCount"/> + round(<see cref="EnemiesPerWave"/> × (番号 - 1))。<see cref="MaxEnemyCount"/> で頭打ち。</item>
    /// <item>強さの倍率 = <see cref="FirstStrength"/> + <see cref="StrengthPerWave"/> × (番号 - 1)。<see cref="MaxStrength"/> で頭打ち。</item>
    /// </list>
    /// </summary>
    public sealed class WaveFormula
    {
        /// <summary>1 つの波で出す敵の数の上限。式がどれだけ大きくなっても、これ以上は出さない。</summary>
        public const int MaxEnemyCount = 10000;

        /// <summary>1 波目の敵の数。1 以上。</summary>
        public int FirstEnemyCount { get; }

        /// <summary>波が 1 つ進むごとに増える敵の数。0 以上の有限値。端数は波ごとに四捨五入する。</summary>
        public float EnemiesPerWave { get; }

        /// <summary>1 波目の強さの倍率。0 より大きい有限値。</summary>
        public float FirstStrength { get; }

        /// <summary>波が 1 つ進むごとに増える強さの倍率。0 以上の有限値。</summary>
        public float StrengthPerWave { get; }

        /// <summary>強さの倍率の上限。<see cref="FirstStrength"/> 以上 (無限大なら上限なし)。</summary>
        public float MaxStrength { get; }

        /// <summary>同時に出しておく敵の上限。1 以上。</summary>
        public int MaxAlive { get; }

        /// <summary>波の間の休憩 (秒)。0 以上の有限値。</summary>
        public float RestSeconds { get; }

        /// <param name="firstEnemyCount">1 波目の敵の数。1 以上 <see cref="MaxEnemyCount"/> 以下。</param>
        /// <param name="enemiesPerWave">波ごとに増える敵の数。0 以上の有限値。</param>
        /// <param name="firstStrength">1 波目の強さの倍率。0 より大きい有限値。</param>
        /// <param name="strengthPerWave">波ごとに増える強さの倍率。0 以上の有限値。</param>
        /// <param name="maxAlive">同時に出しておく敵の上限。1 以上。</param>
        /// <param name="restSeconds">波の間の休憩 (秒)。0 以上の有限値。</param>
        /// <param name="maxStrength">強さの倍率の上限。firstStrength 以上。既定は上限なし。</param>
        public WaveFormula(
            int firstEnemyCount,
            float enemiesPerWave,
            float firstStrength,
            float strengthPerWave,
            int maxAlive,
            float restSeconds,
            float maxStrength = float.PositiveInfinity)
        {
            if (firstEnemyCount < 1 || firstEnemyCount > MaxEnemyCount)
            {
                throw new ArgumentOutOfRangeException(nameof(firstEnemyCount), firstEnemyCount, "firstEnemyCount must be between 1 and " + MaxEnemyCount);
            }

            RequireNonNegativeFinite(enemiesPerWave, nameof(enemiesPerWave));
            if (!(firstStrength > 0f) || float.IsInfinity(firstStrength))
            {
                throw new ArgumentOutOfRangeException(nameof(firstStrength), firstStrength, "firstStrength must be positive and finite");
            }

            RequireNonNegativeFinite(strengthPerWave, nameof(strengthPerWave));
            if (!(maxStrength >= firstStrength))
            {
                throw new ArgumentOutOfRangeException(nameof(maxStrength), maxStrength, "maxStrength must not be less than firstStrength");
            }

            if (maxAlive < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(maxAlive), maxAlive, "maxAlive must be at least 1");
            }

            RequireNonNegativeFinite(restSeconds, nameof(restSeconds));

            FirstEnemyCount = firstEnemyCount;
            EnemiesPerWave = enemiesPerWave;
            FirstStrength = firstStrength;
            StrengthPerWave = strengthPerWave;
            MaxStrength = maxStrength;
            MaxAlive = maxAlive;
            RestSeconds = restSeconds;
        }

        /// <summary>number 番目 (1 から) の波。number が 1 未満なら <see cref="ArgumentOutOfRangeException"/>。</summary>
        public Wave At(int number)
        {
            if (number < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(number), number, "number must be at least 1");
            }

            var steps = (double)(number - 1);
            var count = Math.Min(MaxEnemyCount, FirstEnemyCount + Math.Round(EnemiesPerWave * steps, MidpointRounding.AwayFromZero));
            var strength = Math.Min(MaxStrength, FirstStrength + (StrengthPerWave * steps));

            // float に丸めると上限が無いときに無限大になり得るので、float の最大値で止める。
            return new Wave((int)count, (float)Math.Min(strength, float.MaxValue), MaxAlive, RestSeconds);
        }

        private static void RequireNonNegativeFinite(float value, string name)
        {
            if (!(value >= 0f) || float.IsInfinity(value))
            {
                throw new ArgumentOutOfRangeException(name, value, name + " must be non-negative and finite");
            }
        }
    }
}
