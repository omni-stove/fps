namespace Shooter.Core
{
    // ロードアウトを選ぶときの制約と表示にだけ使い、射撃の処理はこの値で分岐しない。
    public enum WeaponCategory
    {
        AssaultRifle,
        SubmachineGun,
        Shotgun,
        MarksmanRifle,
        SniperRifle,
        LightMachineGun,
        Handgun,
        Launcher,
        Melee,
    }
}
