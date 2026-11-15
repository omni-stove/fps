using System;

namespace Fps.Core
{
    public sealed class BurstTrigger : ITriggerModel
    {
        private readonly ITriggerModel _inner;

        public BurstTrigger(BurstSpec burst, ITriggerModel inner)
        {
            Spec = burst ?? throw new ArgumentNullException(nameof(burst));
            _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        }

        public BurstSpec Spec { get; }

        public float MinShotSpacingSeconds => _inner.MinShotSpacingSeconds;

        public bool BlocksAds => _inner.BlocksAds;

        public float ChargeFraction => _inner.ChargeFraction;

        public int Press()
        {
            return _inner.Press();
        }

        public int Release()
        {
            return _inner.Release();
        }

        public int Tick(float deltaSeconds)
        {
            return _inner.Tick(deltaSeconds);
        }
    }
}
