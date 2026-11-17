using System;

namespace Shooter.Core
{
    public sealed class RespawnCountdown
    {
        public float DelaySeconds { get; }

        public bool IsRunning { get; private set; }

        public float RemainingSeconds { get; private set; }

        public RespawnCountdown(float delaySeconds)
        {
            if (!(delaySeconds >= 0f) || float.IsInfinity(delaySeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(delaySeconds), delaySeconds, "delaySeconds must be a finite, non-negative value");
            }

            DelaySeconds = delaySeconds;
        }

        public void Start()
        {
            IsRunning = true;
            RemainingSeconds = DelaySeconds;
        }

        public void Cancel()
        {
            IsRunning = false;
            RemainingSeconds = 0f;
        }

        public bool Tick(float deltaSeconds)
        {
            if (!(deltaSeconds >= 0f) || float.IsInfinity(deltaSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(deltaSeconds), deltaSeconds, "deltaSeconds must be a finite, non-negative value");
            }

            if (!IsRunning)
            {
                return false;
            }

            RemainingSeconds -= deltaSeconds;
            if (RemainingSeconds > 0f)
            {
                return false;
            }

            Cancel();
            return true;
        }
    }
}
