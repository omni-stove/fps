using System;

namespace Fps.Core
{
    public sealed class BeamState
    {
        public BeamState(BeamSpec beam)
        {
            Spec = beam ?? throw new ArgumentNullException(nameof(beam));
        }

        public BeamSpec Spec { get; }

        public bool IsFiring { get; private set; }

        public float FiringSeconds { get; private set; }

        public void Start()
        {
            IsFiring = true;
            FiringSeconds = 0f;
        }

        public void Stop()
        {
            IsFiring = false;
        }

        public int Tick(float deltaSeconds)
        {
            SpecGuard.NonNegativeFinite(deltaSeconds, nameof(deltaSeconds));
            if (IsFiring)
            {
                FiringSeconds += deltaSeconds;
            }

            return 0;
        }
    }
}
