using System;

namespace Shooter.Core
{
    [Flags]
    public enum AbilitySlotStep
    {
        None = 0,

        Recharged = 1 << 0,

        EffectStarted = 1 << 1,
    }
}
