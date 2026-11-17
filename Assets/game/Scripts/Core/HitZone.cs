namespace Shooter.Core
{
    public enum HitZone
    {
        Body,

        Head,

        Limb,

        // 通信では byte に詰めて送るので、既存の値をずらさないよう末尾に足す。
        WeakPoint,
    }
}
