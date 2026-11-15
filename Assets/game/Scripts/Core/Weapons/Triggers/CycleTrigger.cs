using System;

namespace Fps.Core
{
    public sealed class CycleTrigger : ITriggerModel
    {
        private readonly ITriggerModel _inner;

        public CycleTrigger(CycleSpec cycle, ITriggerModel inner)
        {
            Spec = cycle ?? throw new ArgumentNullException(nameof(cycle));
            _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        }

        public CycleSpec Spec { get; }

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

        public void Cancel()
        {
            _inner.Cancel();
        }
    }
}
