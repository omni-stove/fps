using System;
using System.Globalization;

namespace Fps.Core
{
    /// <summary>
    /// <see cref="FootstepCadence.Advance"/> の結果。鳴らす音の種類と音量。
    /// <c>default</c> は <see cref="None"/> と同じ (何も鳴らさない)。
    /// </summary>
    public readonly struct FootstepCue : IEquatable<FootstepCue>
    {
        /// <summary>何も鳴らさない結果。</summary>
        public static readonly FootstepCue None;

        /// <summary>鳴らす音の種類。</summary>
        public FootstepCueKind Kind { get; }

        /// <summary>音量 ([0, 1])。<see cref="FootstepCueKind.None"/> では 0。</summary>
        public float Volume { get; }

        /// <summary>何か鳴らすか。</summary>
        public bool HasSound => Kind != FootstepCueKind.None;

        /// <param name="kind">定義済みの種類。</param>
        /// <param name="volume">[0, 1] の音量。kind が <see cref="FootstepCueKind.None"/> なら 0。</param>
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
