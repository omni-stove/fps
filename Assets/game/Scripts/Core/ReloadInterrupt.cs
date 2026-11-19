namespace Shooter.Core
{
    // ActionRules.ReloadInterruptedBy に並べた動作は、始めたときにリロードを取り消し、している間はリロードを始めさせない。
    public enum ReloadInterrupt
    {
        Melee,

        Grenade,
    }
}
