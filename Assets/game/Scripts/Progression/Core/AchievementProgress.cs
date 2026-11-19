using System;
using System.Globalization;

namespace Shooter.Core
{
    public readonly struct AchievementProgress : IEquatable<AchievementProgress>
    {
        public AchievementProgress(string id, int current, int target, bool unlocked)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                throw new ArgumentException("id must not be empty", nameof(id));
            }

            if (current < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(current), current, "current must not be negative");
            }

            if (target < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(target), target, "target must be at least 1");
            }

            Id = id.Trim();
            Current = current;
            Target = target;

            // 解けた後に目標の数を上げても、解けた記録は取り消さないので、数とは別に持つ。
            Unlocked = unlocked;
        }

        public string Id { get; }

        public int Current { get; }

        public int Target { get; }

        public bool Unlocked { get; }

        public float Fraction => Unlocked ? 1f : Math.Min(1f, (float)Current / Target);

        public bool Equals(AchievementProgress other)
        {
            return string.Equals(Id, other.Id, StringComparison.Ordinal)
                && Current == other.Current
                && Target == other.Target
                && Unlocked == other.Unlocked;
        }

        public override bool Equals(object obj)
        {
            return obj is AchievementProgress other && Equals(other);
        }

        public static bool operator ==(AchievementProgress left, AchievementProgress right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(AchievementProgress left, AchievementProgress right)
        {
            return !left.Equals(right);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Id == null ? 0 : StringComparer.Ordinal.GetHashCode(Id), Current, Target, Unlocked);
        }

        public override string ToString()
        {
            return Id + " " + Current.ToString(CultureInfo.InvariantCulture) + "/" + Target.ToString(CultureInfo.InvariantCulture)
                + (Unlocked ? " unlocked" : string.Empty);
        }
    }
}
