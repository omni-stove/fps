using System;

namespace Shooter.Core
{
    public sealed class HealthRegenerator
    {
        public Health Health { get; }

        public float RegenDelaySeconds { get; }

        public float RegenPerSecond { get; }

        public float DelayRemainingSeconds => _delayRemaining;

        private float _delayRemaining;

        // float だと大きな Tick を足したときに桁落ちしやすいので double でためる
        private double _pending;

        public HealthRegenerator(Health health, GameRules rules)
            : this(
                health,
                (rules ?? throw new ArgumentNullException(nameof(rules))).RegenDelaySeconds,
                rules.RegenPerSecond)
        {
        }

        public HealthRegenerator(Health health, float regenDelaySeconds, float regenPerSecond)
        {
            if (!(regenDelaySeconds >= 0f) || float.IsInfinity(regenDelaySeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(regenDelaySeconds), regenDelaySeconds, "regenDelaySeconds must be a non-negative finite number");
            }

            if (!(regenPerSecond >= 0f) || float.IsInfinity(regenPerSecond))
            {
                throw new ArgumentOutOfRangeException(nameof(regenPerSecond), regenPerSecond, "regenPerSecond must be a non-negative finite number");
            }

            Health = health ?? throw new ArgumentNullException(nameof(health));
            RegenDelaySeconds = regenDelaySeconds;
            RegenPerSecond = regenPerSecond;
        }

        public void NotifyDamaged()
        {
            _delayRemaining = RegenDelaySeconds;
            _pending = 0d;
        }

        public int Tick(float deltaSeconds)
        {
            if (!(deltaSeconds >= 0f))
            {
                throw new ArgumentOutOfRangeException(nameof(deltaSeconds), deltaSeconds, "deltaSeconds must not be negative");
            }

            if (RegenPerSecond == 0f || Health.IsDead || Health.Current >= Health.Max)
            {
                // 端数を捨てるのは、満タンになった後に持ち越すと次の被弾直後の回復が早まるため。
                _pending = 0d;
                return 0;
            }

            if (deltaSeconds <= _delayRemaining)
            {
                _delayRemaining -= deltaSeconds;
                return 0;
            }

            var regenSeconds = (double)deltaSeconds - _delayRemaining;
            _delayRemaining = 0f;

            _pending += regenSeconds * RegenPerSecond;
            var missing = Health.Max - Health.Current;
            if (_pending >= missing)
            {
                _pending = 0d;
                Health.Heal(missing);
                return missing;
            }

            var whole = (int)Math.Floor(_pending);
            if (whole == 0)
            {
                return 0;
            }

            _pending -= whole;
            Health.Heal(whole);
            return whole;
        }
    }
}
