using System;

namespace Shooter.Core
{
    public readonly struct SideScore : IEquatable<SideScore>
    {
        public SideScore(Side side, double score)
        {
            Side = side;
            Score = score;
        }

        public Side Side { get; }

        // 拠点のように時間で入る点があるので、小数で持つ。
        public double Score { get; }

        public bool Equals(SideScore other)
        {
            return Side == other.Side && Score.Equals(other.Score);
        }

        public override bool Equals(object obj)
        {
            return obj is SideScore other && Equals(other);
        }

        public static bool operator ==(SideScore left, SideScore right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(SideScore left, SideScore right)
        {
            return !left.Equals(right);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Side, Score);
        }

        public override string ToString()
        {
            return Side + "=" + Score;
        }
    }
}
