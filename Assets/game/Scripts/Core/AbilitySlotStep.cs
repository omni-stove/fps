using System;

namespace Fps.Core
{
    /// <summary><see cref="AbilitySlot.Tick"/> の 1 回で起きたこと。</summary>
    [Flags]
    public enum AbilitySlotStep
    {
        None = 0,

        /// <summary>回数が 1 つ以上戻った。</summary>
        Recharged = 1 << 0,

        /// <summary>使い始めてから効果が出るまでの秒数が過ぎ、効果が出た (<see cref="AbilityPhase.Active"/> になった)。</summary>
        EffectStarted = 1 << 1,
    }
}
