using System;

namespace Shooter.Core
{
    public readonly struct TutorialObservation
    {
        public TutorialObservation(
            float deltaSeconds,
            float movedMeters,
            bool aiming,
            int hits,
            bool reloading,
            bool crouching,
            bool inCover)
        {
            if (!(deltaSeconds >= 0f) || float.IsInfinity(deltaSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(deltaSeconds), deltaSeconds, "deltaSeconds must be a non-negative finite number");
            }

            if (!(movedMeters >= 0f) || float.IsInfinity(movedMeters))
            {
                throw new ArgumentOutOfRangeException(nameof(movedMeters), movedMeters, "movedMeters must be a non-negative finite number");
            }

            if (hits < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(hits), hits, "hits must not be negative");
            }

            DeltaSeconds = deltaSeconds;
            MovedMeters = movedMeters;
            Aiming = aiming;
            Hits = hits;
            Reloading = reloading;
            Crouching = crouching;
            InCover = inCover;
        }

        public float DeltaSeconds { get; }

        public float MovedMeters { get; }

        public bool Aiming { get; }

        public int Hits { get; }

        public bool Reloading { get; }

        public bool Crouching { get; }

        public bool InCover { get; }
    }
}
