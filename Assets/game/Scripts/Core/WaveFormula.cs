using System;

namespace Shooter.Core
{
    public sealed class WaveFormula
    {
        public const int MaxEnemyCount = 10000;

        public int FirstEnemyCount { get; }

        public float EnemiesPerWave { get; }

        public float FirstStrength { get; }

        public float StrengthPerWave { get; }

        public float MaxStrength { get; }

        public int MaxAlive { get; }

        public float RestSeconds { get; }

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
