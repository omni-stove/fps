using System;

namespace Fps.Core
{
    [Flags]
    public enum ContextMeleeConditions
    {
        None = 0,
        TargetStaggered = 1 << 0,
        TargetDowned = 1 << 1,

        // 自分が相手の背後にいる。相手が自分の背後にいる意味に取らないよう、相手の向きから見た位置で決める (ContextMeleeSelector.IsBehind)。
        TargetBehind = 1 << 2,
        TargetUnaware = 1 << 3,
        SelfGrounded = 1 << 4,
        SelfCrouching = 1 << 5,
        SelfSprinting = 1 << 6,
    }
}
