using System;

namespace Fps.Core
{
    // 盾を構えているか下ろしているかだけを持つ。何で下ろすか (近接・爆発・怯み) と何秒かは呼ぶ側が決める。
    public sealed class ShieldGuard
    {
        private float _remaining;

        public bool IsRaised => _remaining <= 0f;

        public float SecondsRemaining => _remaining;

        public string Cause { get; private set; } = string.Empty;

        // 下ろしている最中にまた受けたら、残りの長いほうに延ばす。短い理由で早く構え直させないように。
        public bool Lower(float seconds, string cause)
        {
            if (!(seconds >= 0f) || float.IsInfinity(seconds))
            {
                throw new ArgumentOutOfRangeException(nameof(seconds), seconds, "seconds must be non-negative and finite");
            }

            if (string.IsNullOrWhiteSpace(cause))
            {
                throw new ArgumentException("cause must not be empty", nameof(cause));
            }

            if (seconds <= _remaining)
            {
                return false;
            }

            var wasRaised = IsRaised;
            _remaining = seconds;
            if (wasRaised)
            {
                Cause = cause;
            }

            return wasRaised;
        }

        public bool Tick(float deltaSeconds)
        {
            if (!(deltaSeconds >= 0f) || float.IsInfinity(deltaSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(deltaSeconds), deltaSeconds, "deltaSeconds must be non-negative and finite");
            }

            if (IsRaised)
            {
                return false;
            }

            _remaining = Math.Max(0f, _remaining - deltaSeconds);
            if (!IsRaised)
            {
                return false;
            }

            Cause = string.Empty;
            return true;
        }

        public void Reset()
        {
            _remaining = 0f;
            Cause = string.Empty;
        }
    }
}
