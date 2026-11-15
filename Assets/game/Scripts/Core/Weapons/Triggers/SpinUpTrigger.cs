using System;

namespace Fps.Core
{
    public sealed class SpinUpTrigger : ITriggerModel
    {
        private readonly ITriggerModel _inner;

        public SpinUpTrigger(SpinUpSpec spinUp, ITriggerModel inner)
        {
            Spec = spinUp ?? throw new ArgumentNullException(nameof(spinUp));
            _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        }

        public SpinUpSpec Spec { get; }

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
