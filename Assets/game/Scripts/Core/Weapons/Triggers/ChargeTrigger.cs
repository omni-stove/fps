using System;

namespace Fps.Core
{
    public sealed class ChargeTrigger : ITriggerModel
    {
        private readonly ITriggerModel _inner;

        public ChargeTrigger(ChargeSpec charge, ITriggerModel inner)
        {
            Spec = charge ?? throw new ArgumentNullException(nameof(charge));
            _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        }

        public ChargeSpec Spec { get; }

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
