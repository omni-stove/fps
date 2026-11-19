using System;

namespace Shooter.Core
{
    public sealed class MeleeCharge
    {
        private float _heldSeconds;
        private bool _holding;

        public MeleeCharge(MeleeWeaponSpec spec)
        {
            Spec = spec ?? throw new ArgumentNullException(nameof(spec));
        }

        public MeleeWeaponSpec Spec { get; }

        public bool IsHolding => _holding;

        public float HeldSeconds => _holding ? _heldSeconds : 0f;

        public float Fraction => _holding ? Spec.ChargeFraction(_heldSeconds) : 0f;

        public void Press()
        {
            _holding = true;
            _heldSeconds = 0f;
        }

        public void Advance(float deltaSeconds)
        {
            if (!(deltaSeconds >= 0f) || float.IsInfinity(deltaSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(deltaSeconds), deltaSeconds, "deltaSeconds must be a non-negative finite number");
            }

            if (_holding)
            {
                _heldSeconds += deltaSeconds;
            }
        }

        public bool TryRelease(out float fraction)
        {
            if (!_holding)
            {
                fraction = 0f;
                return false;
            }

            fraction = Spec.ChargeFraction(_heldSeconds);
            Drop();
            return true;
        }

        public void Drop()
        {
            _holding = false;
            _heldSeconds = 0f;
        }
    }
}
