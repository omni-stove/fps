namespace Fps.Core
{
    /// <summary>
    /// 武器の種別。ロードアウトを選ぶときの制約と表示にだけ使い、射撃の処理はこの値で分岐しない。
    /// </summary>
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
    }
}
