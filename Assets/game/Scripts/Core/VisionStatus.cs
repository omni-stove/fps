using System;

namespace Shooter.Core
{
    // どこから来たかを持つのは、照準器の暗視は覗いている間だけ照準の中に、ゴーグルの暗視は画面全体に掛けるので、UI が覆う範囲を変えるため。
    public readonly struct VisionStatus : IEquatable<VisionStatus>
    {
        public static VisionStatus None => default;

        public VisionStatus(VisionMode mode, VisionSource source)
        {
            if (!Enum.IsDefined(typeof(VisionMode), mode))
            {
                throw new ArgumentOutOfRangeException(nameof(mode), mode, "mode is not a defined VisionMode");
            }

            if (!Enum.IsDefined(typeof(VisionSource), source))
            {
                throw new ArgumentOutOfRangeException(nameof(source), source, "source is not a defined VisionSource");
            }

            if ((mode == VisionMode.None) != (source == VisionSource.None))
            {
                throw new ArgumentException("mode and source must both be None or both be set", nameof(source));
            }

            Mode = mode;
            Source = source;
        }

        public VisionMode Mode { get; }

        public VisionSource Source { get; }

        public bool IsActive => Mode != VisionMode.None;

        public bool Equals(VisionStatus other)
        {
            return Mode == other.Mode && Source == other.Source;
        }

        public override bool Equals(object obj)
        {
            return obj is VisionStatus other && Equals(other);
        }

        public static bool operator ==(VisionStatus left, VisionStatus right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(VisionStatus left, VisionStatus right)
        {
            return !left.Equals(right);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine((int)Mode, (int)Source);
        }

        public override string ToString()
        {
            return "mode=" + Mode + " source=" + Source;
        }
    }
}
