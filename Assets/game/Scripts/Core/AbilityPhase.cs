namespace Fps.Core
{
    /// <summary><see cref="AbilitySlot"/> の能力が今どこにいるか。</summary>
    public enum AbilityPhase
    {
        /// <summary>使っていない。</summary>
        Idle,

        /// <summary>使い始めて、効果が出るのを待っている (<see cref="AbilitySpec.WindupSeconds"/>)。</summary>
        WindingUp,

        /// <summary>効果が出ている。</summary>
        Active,
    }
}
