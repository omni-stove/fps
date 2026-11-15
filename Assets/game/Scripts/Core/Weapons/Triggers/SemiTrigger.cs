using System;

namespace Fps.Core
{
    public sealed class SemiTrigger : ITriggerModel
    {
        private float _cooldownRemaining;

        public SemiTrigger(float fireInterval)
        {
            MinShotSpacingSeconds = SpecGuard.NonNegativeFinite(fireInterval, nameof(fireInterval));
        }

        public float MinShotSpacingSeconds { get; }

        public bool BlocksAds => false;

        public float ChargeFraction => 0f;

        // 待ちの間に押した分を溜めて後から撃つと、押した覚えの無い時に弾が出る。
        public int Press()
        {
            if (_cooldownRemaining > 0f)
            {
                return 0;
            }

            _cooldownRemaining = MinShotSpacingSeconds;
            return 1;
        }

        public int Release()
        {
            return 0;
        }

        public int Tick(float deltaSeconds)
        {
            SpecGuard.NonNegativeFinite(deltaSeconds, nameof(deltaSeconds));
            _cooldownRemaining = Math.Max(0f, _cooldownRemaining - deltaSeconds);
            return 0;
        }

        // 連射の残りを持たない撃ち方なので取り消すものが無い。
        public void Cancel()
        {
        }
    }
}
