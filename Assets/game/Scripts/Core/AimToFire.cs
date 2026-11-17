namespace Shooter.Core
{
    public enum AimToFire
    {
        Off,

        // ブラインドファイアは構えずに撃つものなので、On でもブラインドファイアには構えを求めない。
        On,
    }
}
