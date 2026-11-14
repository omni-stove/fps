using System;
using System.Numerics;

namespace Fps.Core
{
    public sealed class PhotoModeSpec
    {
        public const float DefaultRadius = 8f;
        public const float DefaultMoveSpeed = 3f;
        public const float DefaultMinFieldOfView = 20f;
        public const float DefaultMaxFieldOfView = 100f;
        public const float DefaultFieldOfViewStep = 5f;
        public const float DefaultMaxRoll = 45f;
        public const float DefaultRollSpeed = 45f;
        public const float DefaultStartHeight = 0.6f;
        public const float DefaultStartBackDistance = 2.5f;

        public static PhotoModeSpec Default { get; } = new PhotoModeSpec();

        public PhotoModeSpec()
            : this(
                DefaultRadius,
                DefaultMoveSpeed,
                DefaultMinFieldOfView,
                DefaultMaxFieldOfView,
                DefaultFieldOfViewStep,
                DefaultMaxRoll,
                DefaultRollSpeed,
                DefaultStartHeight,
                DefaultStartBackDistance)
        {
        }

        public PhotoModeSpec(
            float radius,
            float moveSpeed,
            float minFieldOfView,
            float maxFieldOfView,
            float fieldOfViewStep,
            float maxRoll,
            float rollSpeed,
            float startHeight,
            float startBackDistance)
        {
            if (!IsFinite(radius) || radius <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(radius), radius, "radius must be positive and finite");
            }

            RequireFiniteNonNegative(moveSpeed, nameof(moveSpeed));
            RequireFieldOfView(minFieldOfView, nameof(minFieldOfView));
            RequireFieldOfView(maxFieldOfView, nameof(maxFieldOfView));
            if (minFieldOfView > maxFieldOfView)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(minFieldOfView), minFieldOfView, "minFieldOfView must not be greater than maxFieldOfView (" + maxFieldOfView + ")");
            }

            RequireFiniteNonNegative(fieldOfViewStep, nameof(fieldOfViewStep));
            RequireFiniteNonNegative(maxRoll, nameof(maxRoll));
            if (maxRoll > 180f)
            {
                throw new ArgumentOutOfRangeException(nameof(maxRoll), maxRoll, "maxRoll must not exceed 180 degrees");
            }

            RequireFiniteNonNegative(rollSpeed, nameof(rollSpeed));
            RequireFiniteNonNegative(startHeight, nameof(startHeight));
            RequireFiniteNonNegative(startBackDistance, nameof(startBackDistance));

            Radius = radius;
            MoveSpeed = moveSpeed;
            MinFieldOfView = minFieldOfView;
            MaxFieldOfView = maxFieldOfView;
            FieldOfViewStep = fieldOfViewStep;
            MaxRoll = maxRoll;
            RollSpeed = rollSpeed;
            StartHeight = startHeight;
            StartBackDistance = startBackDistance;
        }

        public float Radius { get; }

        public float MoveSpeed { get; }

        public float MinFieldOfView { get; }

        public float MaxFieldOfView { get; }

        public float FieldOfViewStep { get; }

        public float MaxRoll { get; }

        public float RollSpeed { get; }

        public float StartHeight { get; }

        public float StartBackDistance { get; }

        public float ClampFieldOfView(float fieldOfView)
        {
            // 壊れた値の上限・下限のどちらかに寄せると、極端な望遠か広角で始まってしまう
            if (!IsFinite(fieldOfView))
            {
                return (MinFieldOfView + MaxFieldOfView) * 0.5f;
            }

            return Math.Max(MinFieldOfView, Math.Min(MaxFieldOfView, fieldOfView));
        }

        public float StepFieldOfView(float current, int zoomInSteps)
        {
            return ClampFieldOfView(ClampFieldOfView(current) - (zoomInSteps * FieldOfViewStep));
        }

        public float ClampRoll(float roll)
        {
            if (!IsFinite(roll))
            {
                return 0f;
            }

            return Math.Max(-MaxRoll, Math.Min(MaxRoll, roll));
        }

        public float StepRoll(float current, float input, float deltaSeconds)
        {
            RequireFiniteNonNegative(deltaSeconds, nameof(deltaSeconds));
            var direction = IsFinite(input) ? Math.Max(-1f, Math.Min(1f, input)) : 0f;
            return ClampRoll(ClampRoll(current) + (direction * RollSpeed * deltaSeconds));
        }

        public static Vector3 ClampWithin(Vector3 position, Vector3 center, float radius)
        {
            RequireFiniteNonNegative(radius, nameof(radius));
            var offset = position - center;
            var lengthSquared = offset.LengthSquared();
            if (!IsFinite(lengthSquared))
            {
                return center;
            }

            if (lengthSquared <= radius * radius)
            {
                return position;
            }

            return center + (offset * (radius / (float)Math.Sqrt(lengthSquared)));
        }

        private static void RequireFieldOfView(float value, string name)
        {
            if (!IsFinite(value) || value <= 0f || value >= 180f)
            {
                throw new ArgumentOutOfRangeException(name, value, name + " must be between 0 and 180 degrees");
            }
        }

        private static void RequireFiniteNonNegative(float value, string name)
        {
            if (!IsFinite(value) || value < 0f)
            {
                throw new ArgumentOutOfRangeException(name, value, name + " must be finite and not negative");
            }
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
