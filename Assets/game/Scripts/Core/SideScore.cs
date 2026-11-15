using System;

namespace Fps.Core
{
    /// <summary>陣営 1 つの得点。<see cref="ModeStatus.SideScores"/> に陣営の数だけ並ぶ。</summary>
    public readonly struct SideScore : IEquatable<SideScore>
    {
        public SideScore(Side side, double score)
        {
            Side = side;
            Score = score;
        }

        public Side Side { get; }

        /// <summary>得点。拠点のように時間で入る点は小数を含む。</summary>
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
