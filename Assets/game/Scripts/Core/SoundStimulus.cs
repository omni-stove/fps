using System;
using System.Numerics;

namespace Shooter.Core
{
    // 上がり方を音に持たせず聞いた側の AwarenessSpec で決めるのは、同じ銃声でも敵の種類ごとに耳の良さを変えられるようにするため。
    public readonly struct SoundStimulus
    {
        public SoundStimulus(SoundCueKind kind, Vector3 position, float radius, bool draws)
        {
            if (!Enum.IsDefined(typeof(SoundCueKind), kind))
            {
                throw new ArgumentOutOfRangeException(nameof(kind), kind, "unknown sound cue kind");
            }

            if (!IsFinite(position.X) || !IsFinite(position.Y) || !IsFinite(position.Z))
            {
                throw new ArgumentOutOfRangeException(nameof(position), position, "position must be finite");
            }

            if (!(radius >= 0f) || float.IsInfinity(radius))
            {
                throw new ArgumentOutOfRangeException(nameof(radius), radius, "radius must not be negative and must be finite");
            }

            Kind = kind;
            Position = position;
            Radius = radius;
            Draws = draws;
        }

        public SoundCueKind Kind { get; }

        public Vector3 Position { get; }

        public float Radius { get; }

        public bool Draws { get; }

        public float LoudnessAt(Vector3 listener)
        {
            var distance = Vector3.Distance(listener, Position);
            if (!IsFinite(distance) || !(Radius > 0f) || !(distance <= Radius))
            {
                return 0f;
            }

            return 1f - (distance / Radius);
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
