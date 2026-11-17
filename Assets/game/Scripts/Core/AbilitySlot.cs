using System;

namespace Shooter.Core
{
    public sealed class AbilitySlot
    {
        public AbilitySlot(AbilitySpec spec)
        {
            Spec = spec ?? throw new ArgumentNullException(nameof(spec));
            Charges = spec.MaxCharges;
        }

        public AbilitySpec Spec { get; }

        public event Action Changed;

        public int Charges { get; private set; }

        public AbilityPhase Phase { get; private set; }

        public float CooldownRemaining { get; private set; }

        public float WindupRemaining { get; private set; }

        public float RechargeElapsed { get; private set; }

        public float RechargeRemaining
        {
            get
            {
                if (Charges >= Spec.MaxCharges)
                {
                    return 0f;
                }

                return Spec.Recharges ? Math.Max(0f, Spec.RechargeSeconds - RechargeElapsed) : float.PositiveInfinity;
            }
        }

        public bool IsRunning => Phase != AbilityPhase.Idle;

        public AbilityUseBlock Block
        {
            get
            {
                if (IsRunning)
                {
                    return AbilityUseBlock.Running;
                }

                if (CooldownRemaining > 0f)
                {
                    return AbilityUseBlock.Cooldown;
                }

                return Charges <= 0 ? AbilityUseBlock.NoCharges : AbilityUseBlock.None;
            }
        }

        public bool CanStart => Block == AbilityUseBlock.None;

        public float SecondsUntilReady
        {
            get
            {
                var wait = CooldownRemaining;
                if (Charges <= 0)
                {
                    wait = Math.Max(wait, RechargeRemaining);
                }

                return wait;
            }
        }

        public bool TryStart()
        {
            if (!CanStart)
            {
                return false;
            }

            var before = Snapshot();
            Charges--;
            CooldownRemaining = Spec.CooldownSeconds;
            if (Spec.WindupSeconds > 0f)
            {
                Phase = AbilityPhase.WindingUp;
                WindupRemaining = Spec.WindupSeconds;
            }
            else
            {
                Phase = AbilityPhase.Active;
                WindupRemaining = 0f;
            }

            NotifyIfChanged(before);
            return true;
        }

        public AbilitySlotStep Tick(float deltaSeconds)
        {
            if (!(deltaSeconds >= 0f) || float.IsInfinity(deltaSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(deltaSeconds), deltaSeconds, "deltaSeconds must not be negative and must be finite");
            }

            var before = Snapshot();
            var step = AbilitySlotStep.None;
            CooldownRemaining = Math.Max(0f, CooldownRemaining - deltaSeconds);

            if (Phase == AbilityPhase.WindingUp)
            {
                WindupRemaining -= deltaSeconds;
                if (WindupRemaining <= 0f)
                {
                    WindupRemaining = 0f;
                    Phase = AbilityPhase.Active;
                    step |= AbilitySlotStep.EffectStarted;
                }
            }

            if (Charges < Spec.MaxCharges && Spec.Recharges)
            {
                RechargeElapsed += deltaSeconds;
                while (RechargeElapsed >= Spec.RechargeSeconds && Charges < Spec.MaxCharges)
                {
                    RechargeElapsed -= Spec.RechargeSeconds;
                    Charges++;
                    step |= AbilitySlotStep.Recharged;
                }
            }

            if (Charges >= Spec.MaxCharges)
            {
                RechargeElapsed = 0f;
            }

            NotifyIfChanged(before);
            return step;
        }

        public bool End()
        {
            if (!IsRunning)
            {
                return false;
            }

            var before = Snapshot();
            Phase = AbilityPhase.Idle;
            WindupRemaining = 0f;
            NotifyIfChanged(before);
            return true;
        }

        // 回数は使い始めた時点で減るので、置けない所で離したなど効果を出さずに終えた能力が、その 1 回を返すのに使う。
        public void Refund()
        {
            var before = Snapshot();
            Charges = Math.Min(Spec.MaxCharges, Charges + 1);
            CooldownRemaining = 0f;
            if (Charges >= Spec.MaxCharges)
            {
                RechargeElapsed = 0f;
            }

            NotifyIfChanged(before);
        }

        public void Reset()
        {
            var before = Snapshot();
            Charges = Spec.MaxCharges;
            Phase = AbilityPhase.Idle;
            CooldownRemaining = 0f;
            WindupRemaining = 0f;
            RechargeElapsed = 0f;
            NotifyIfChanged(before);
        }

        private (int Charges, AbilityPhase Phase, float Cooldown, float Windup, float Recharge) Snapshot()
        {
            return (Charges, Phase, CooldownRemaining, WindupRemaining, RechargeElapsed);
        }

        private void NotifyIfChanged((int Charges, AbilityPhase Phase, float Cooldown, float Windup, float Recharge) before)
        {
            if (before != Snapshot())
            {
                Changed?.Invoke();
            }
        }
    }
}
