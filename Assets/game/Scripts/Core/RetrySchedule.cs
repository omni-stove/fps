using System;

namespace Shooter.Core
{
    public sealed class RetrySchedule
    {
        private readonly float _intervalSeconds;
        private readonly float _timeoutSeconds;
        private float _deadline;
        private float _next;
        private bool _started;

        public RetrySchedule(float intervalSeconds, float timeoutSeconds)
        {
            if (!(intervalSeconds > 0f) || float.IsInfinity(intervalSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(intervalSeconds), intervalSeconds, "must be a positive finite number");
            }

            if (!(timeoutSeconds > 0f) || float.IsInfinity(timeoutSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(timeoutSeconds), timeoutSeconds, "must be a positive finite number");
            }

            _intervalSeconds = intervalSeconds;
            _timeoutSeconds = timeoutSeconds;
        }

        public float TimeoutSeconds => _timeoutSeconds;

        public float IntervalSeconds => _intervalSeconds;

        public void Start(float now)
        {
            _started = true;
            _deadline = now + _timeoutSeconds;
            _next = now;
        }

        public bool IsExpired(float now)
        {
            return _started && now >= _deadline;
        }

        public bool IsDue(float now)
        {
            return _started && now >= _next;
        }

        public void Wait(float now)
        {
            _next = now + _intervalSeconds;
        }
    }
}
