using System;
using System.Globalization;

namespace Shooter.Core
{
    public readonly struct FootstepCue : IEquatable<FootstepCue>
    {
        public static readonly FootstepCue None;

        public FootstepCueKind Kind { get; }

        public float Volume { get; }

        public bool HasSound => Kind != FootstepCueKind.None;

        public FootstepCue(FootstepCueKind kind, float volume)
        {
            if (!Enum.IsDefined(typeof(FootstepCueKind), kind))
            {
                throw new ArgumentOutOfRangeException(nameof(kind), kind, "kind is not a defined FootstepCueKind");
            }

            if (!(volume >= 0f && volume <= 1f))
            {
                throw new ArgumentOutOfRangeException(nameof(volume), volume, "volume must be within [0, 1]");
            }

            if (kind == FootstepCueKind.None && volume != 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(volume), volume, "volume must be 0 when kind is None");
            }

            Kind = kind;
            Volume = volume;
        }

        public bool Equals(FootstepCue other)
        {
            return Kind == other.Kind && Volume.Equals(other.Volume);
        }

        public override bool Equals(object obj)
        {
            return obj is FootstepCue other && Equals(other);
        }

        public static bool operator ==(FootstepCue left, FootstepCue right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(FootstepCue left, FootstepCue right)
        {
            return !left.Equals(right);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Kind, Volume);
        }

        public override string ToString()
        {
            return string.Format(CultureInfo.InvariantCulture, "{0} volume={1:0.00}", Kind, Volume);
        }
    }
}
