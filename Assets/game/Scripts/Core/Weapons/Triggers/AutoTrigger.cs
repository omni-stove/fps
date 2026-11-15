using System;

namespace Fps.Core
{
    public sealed class AutoTrigger : ITriggerModel
    {
        private float _cooldownRemaining;
        private bool _held;

        public AutoTrigger(float fireInterval)
        {
            MinShotSpacingSeconds = SpecGuard.NonNegativeFinite(fireInterval, nameof(fireInterval));
        }

        public float MinShotSpacingSeconds { get; }

        public bool BlocksAds => false;

        public float ChargeFraction => 0f;

        public int Press()
        {
            _held = true;
            return TryShoot();
        }

        public int Release()
        {
            _held = false;
            return 0;
        }

        public int Tick(float deltaSeconds)
        {
            SpecGuard.NonNegativeFinite(deltaSeconds, nameof(deltaSeconds));
            _cooldownRemaining = Math.Max(0f, _cooldownRemaining - deltaSeconds);
            return _held ? TryShoot() : 0;
        }

        // 連射の残りを持たない撃ち方なので取り消すものが無い。
        public void Cancel()
        {
        }

        private int TryShoot()
        {
            if (_cooldownRemaining > 0f)
            {
                return 0;
            }

            _cooldownRemaining = MinShotSpacingSeconds;
            return 1;
        }
    }
}
