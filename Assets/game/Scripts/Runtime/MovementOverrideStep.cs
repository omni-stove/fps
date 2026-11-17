using System;
using UnityEngine;

namespace Shooter.Runtime
{
    public readonly struct MovementOverrideStep
    {
        public MovementOverrideStep(
            Vector3 displacement,
            Vector3 velocity,
            Vector3? position = null,
            float? yawDegrees = null,
            bool? crouched = null)
        {
            RequireFinite(displacement, nameof(displacement));
            RequireFinite(velocity, nameof(velocity));
            if (position.HasValue)
            {
                RequireFinite(position.Value, nameof(position));
            }

            if (yawDegrees.HasValue && !IsFinite(yawDegrees.Value))
            {
                throw new ArgumentException("yawDegrees must be finite", nameof(yawDegrees));
            }

            Displacement = displacement;
            Velocity = velocity;
            Position = position;
            YawDegrees = yawDegrees;
            Crouched = crouched;
        }

        public Vector3 Displacement { get; }

        public Vector3 Velocity { get; }

        public Vector3? Position { get; }

        public float? YawDegrees { get; }

        public bool? Crouched { get; }

        public static MovementOverrideStep Move(Vector3 displacement, Vector3 velocity)
        {
            return new MovementOverrideStep(displacement, velocity);
        }

        private static void RequireFinite(Vector3 value, string paramName)
        {
            if (!IsFinite(value.x) || !IsFinite(value.y) || !IsFinite(value.z))
            {
                throw new ArgumentException($"{paramName} must be finite: {value}", paramName);
            }
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
