using System;

namespace Shooter.Core
{
    public readonly struct StaminaStatus : IEquatable<StaminaStatus>
    {
        // 尽きているかを量から求めず受け取るのは、尽きた後は決まった量まで戻るまで走れないなど、量が 0 より上でも尽きた扱いを続ける作りを許すため。
        public StaminaStatus(float current, float max, bool depleted)
        {
            if (!(max > 0f) || float.IsInfinity(max))
            {
                throw new ArgumentOutOfRangeException(nameof(max), max, "max must be positive and finite");
            }

            if (!(current >= 0f && current <= max))
            {
                throw new ArgumentOutOfRangeException(nameof(current), current, "current must be between 0 and max");
            }

            Current = current;
            Max = max;
            Depleted = depleted;
        }

        public float Current { get; }

        public float Max { get; }

        public bool Depleted { get; }

        // default (Max が 0) でも 0 除算で NaN を返さないよう、Max を確かめてから割る。
        public float Fraction => Max > 0f ? Current / Max : 0f;

        public bool Equals(StaminaStatus other)
        {
            return Current.Equals(other.Current) && Max.Equals(other.Max) && Depleted == other.Depleted;
        }

        public override bool Equals(object obj)
        {
            return obj is StaminaStatus other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Current, Max, Depleted);
        }

        public static bool operator ==(StaminaStatus left, StaminaStatus right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(StaminaStatus left, StaminaStatus right)
        {
            return !left.Equals(right);
        }
    }
}
