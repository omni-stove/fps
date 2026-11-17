using System;
using System.Collections.Generic;

namespace Shooter.Core
{
    public readonly struct RumbleMotors : IEquatable<RumbleMotors>
    {
        public static RumbleMotors Zero => default;

        public float Low { get; }

        public float High { get; }

        public bool IsZero => Low <= 0f && High <= 0f;

        public RumbleMotors(float low, float high)
        {
            Low = low;
            High = high;
        }

        public bool Equals(RumbleMotors other)
        {
            return Low.Equals(other.Low) && High.Equals(other.High);
        }

        public override bool Equals(object obj)
        {
            return obj is RumbleMotors other && Equals(other);
        }

        public static bool operator ==(RumbleMotors left, RumbleMotors right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(RumbleMotors left, RumbleMotors right)
        {
            return !left.Equals(right);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (Low.GetHashCode() * 397) ^ High.GetHashCode();
            }
        }
    }

    public sealed class RumbleModel
    {
        private readonly List<Active> _active = new List<Active>();

        public int ActiveCount => _active.Count;

        public RumbleMotors Current
        {
            get
            {
                var low = 0f;
                var high = 0f;
                foreach (var active in _active)
                {
                    low = Math.Max(low, active.Pulse.Low);
                    high = Math.Max(high, active.Pulse.High);
                }

                return new RumbleMotors(low, high);
            }
        }

        public static RumblePulse ForExplosion(RumblePulse pulse, float distance, float radius)
        {
            if (!(distance >= 0f) || float.IsInfinity(distance))
            {
                throw new ArgumentOutOfRangeException(nameof(distance), distance, "distance must be a non-negative finite number");
            }

            if (!(radius > 0f) || float.IsInfinity(radius))
            {
                throw new ArgumentOutOfRangeException(nameof(radius), radius, "radius must be a positive finite number");
            }

            if (distance >= radius)
            {
                return RumblePulse.None;
            }

            var factor = 1f - (distance / radius);
            return new RumblePulse(pulse.Low * factor, pulse.High * factor, pulse.Seconds);
        }

        public static RumbleMotors Apply(RumbleMotors raw, bool enabled, float strength)
        {
            if (!(strength >= 0f && strength <= 1f))
            {
                throw new ArgumentOutOfRangeException(nameof(strength), strength, "strength must be between 0 and 1");
            }

            if (!enabled || strength <= 0f)
            {
                return RumbleMotors.Zero;
            }

            return new RumbleMotors(raw.Low * strength, raw.High * strength);
        }

        public bool Add(RumblePulse pulse)
        {
            if (pulse.IsSilent)
            {
                return false;
            }

            _active.Add(new Active(pulse, pulse.Seconds));
            return true;
        }

        public bool Tick(float deltaSeconds)
        {
            if (!(deltaSeconds >= 0f) || float.IsInfinity(deltaSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(deltaSeconds), deltaSeconds, "deltaSeconds must be a non-negative finite number");
            }

            if (_active.Count == 0)
            {
                return false;
            }

            for (var i = _active.Count - 1; i >= 0; i--)
            {
                var remaining = _active[i].Remaining - deltaSeconds;
                if (remaining <= 0f)
                {
                    _active.RemoveAt(i);
                }
                else
                {
                    _active[i] = new Active(_active[i].Pulse, remaining);
                }
            }

            return _active.Count == 0;
        }

        public RumbleMotors Sample(bool enabled, float strength)
        {
            return Apply(Current, enabled, strength);
        }

        public void Reset()
        {
            _active.Clear();
        }

        private readonly struct Active
        {
            public RumblePulse Pulse { get; }

            public float Remaining { get; }

            public Active(RumblePulse pulse, float remaining)
            {
                Pulse = pulse;
                Remaining = remaining;
            }
        }
    }
}
