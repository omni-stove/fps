namespace Shooter.Core
{
    // ActionRules.ReloadInterruptedBy に並べた動作は、始めたときにリロードを取り消し、している間はリロードを始めさせない。
    // Aim だけは構え始めたときに取り消すのみにする。構えている間もリロードを始めさせないと、構えたままでは弾を込められなくなるため。
    public enum ReloadInterrupt
    {
        Melee,

        Grenade,

        Aim,
    }
}
