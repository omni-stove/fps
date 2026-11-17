using System;
using System.Globalization;

namespace Shooter.Core
{
    public readonly struct Wave : IEquatable<Wave>
    {
        public int EnemyCount { get; }

        public float StrengthMultiplier { get; }

        public int MaxAlive { get; }

        public float RestSeconds { get; }

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
