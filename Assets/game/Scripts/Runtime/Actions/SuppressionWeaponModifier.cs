using System;
using Fps.Core;

namespace Fps.Runtime
{
    public sealed class SuppressionWeaponModifier : IWeaponModifier
    {
        public SuppressionWeaponModifier(SuppressionReceiver receiver)
        {
            if (receiver == null)
            {
                throw new ArgumentNullException(nameof(receiver));
            }

            Receiver = receiver;
        }

        public SuppressionReceiver Receiver { get; }

        public float ModifyConeDegrees(WeaponSpec spec, float coneDegrees)
        {
            return coneDegrees + (ActiveLevel() * Receiver.Spec.MaxSpreadDegrees);
        }

        public float ModifySpreadFraction(float spreadFraction)
        {
            return spreadFraction + (ActiveLevel() * Receiver.Spec.MaxCrosshairSpread);
        }

        // 外し忘れた武器に残っても、受け皿が Off なら何も変えない。
        private float ActiveLevel()
        {
            return Receiver != null && Receiver.IsActive ? Receiver.Level : 0f;
        }
    }
}
