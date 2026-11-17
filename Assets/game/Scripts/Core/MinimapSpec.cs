using System;

namespace Shooter.Core
{
    public sealed class MinimapSpec
    {
        public const float DefaultRadius = 50f;
        public const float DefaultScale = 2f;
        public const float DefaultUpdateIntervalSeconds = 0.1f;
        public const float DefaultSpottedSeconds = 3f;
        public const bool DefaultRotateWithPlayer = true;

        public static MinimapSpec Default { get; } = new MinimapSpec();

        public float Radius { get; }

        public float Scale { get; }

        public float UpdateIntervalSeconds { get; }

        public float SpottedSeconds { get; }

        public bool RotateWithPlayer { get; }

        public float MapRadius => Radius * Scale;

        public MinimapSpec(
            float radius = DefaultRadius,
            float scale = DefaultScale,
            float updateIntervalSeconds = DefaultUpdateIntervalSeconds,
            float spottedSeconds = DefaultSpottedSeconds,
            bool rotateWithPlayer = DefaultRotateWithPlayer)
        {
            RequireInRange(radius, nameof(radius));
            RequireInRange(scale, nameof(scale));
            RequireInRange(updateIntervalSeconds, nameof(updateIntervalSeconds));
            RequireInRange(spottedSeconds, nameof(spottedSeconds));

            Radius = radius;
            Scale = scale;
            UpdateIntervalSeconds = updateIntervalSeconds;
            SpottedSeconds = spottedSeconds;
            RotateWithPlayer = rotateWithPlayer;
        }

        public static bool IsInRange(float value)
        {
            return value > 0f && !float.IsInfinity(value);
        }

        public override string ToString()
        {
            return FormattableString.Invariant(
                $"radius={Radius:0.##} scale={Scale:0.##} update={UpdateIntervalSeconds:0.###}s spotted={SpottedSeconds:0.##}s rotate={(RotateWithPlayer ? "true" : "false")}");
        }

        private static void RequireInRange(float value, string name)
        {
            if (!IsInRange(value))
            {
                throw new ArgumentOutOfRangeException(name, value, name + " must be positive and finite");
            }
        }
    }
}
