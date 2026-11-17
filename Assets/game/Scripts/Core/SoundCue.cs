using System;
using System.Numerics;

namespace Shooter.Core
{
    public readonly struct SoundCue
    {
        public const float FarDistance = 40f;

        public SoundCue(SoundCueKind kind, SoundCueDirection direction, float distance)
        {
            Kind = kind;
            Direction = direction;
            Distance = distance;
        }

        public SoundCueKind Kind { get; }

        public SoundCueDirection Direction { get; }

        public float Distance { get; }

        public bool IsFar => !float.IsNaN(Distance) && Distance >= FarDistance;

        public static bool TryLocate(
            SoundCueKind kind,
            Vector3 listener,
            float listenerYaw,
            Vector3? source,
            float audibleDistance,
            out SoundCue cue)
        {
            if (!source.HasValue)
            {
                cue = new SoundCue(kind, SoundCueDirection.None, float.NaN);
                return true;
            }

            var distance = Vector3.Distance(listener, source.Value);
            if (float.IsNaN(distance) || float.IsInfinity(distance) || !(distance <= audibleDistance))
            {
                cue = default;
                return false;
            }

            var direction = DamageDirection.TryScreenAngle(listener, listenerYaw, source.Value, out var degrees)
                ? DirectionOf(degrees)
                : SoundCueDirection.None;
            cue = new SoundCue(kind, direction, distance);
            return true;
        }

        public static SoundCueDirection DirectionOf(float screenDegrees)
        {
            if (float.IsNaN(screenDegrees) || float.IsInfinity(screenDegrees))
            {
                return SoundCueDirection.None;
            }

            var wrapped = screenDegrees % 360f;
            if (wrapped < 0f)
            {
                wrapped += 360f;
            }

            var sector = (int)Math.Floor((wrapped + 22.5f) / 45f) % 8;
            return (SoundCueDirection)((int)SoundCueDirection.Front + sector);
        }
    }
}
