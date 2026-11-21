namespace Shooter.Protocol
{
    // 人の Id と乗り物の番号は別々に数えるので、同じ数でもどちらを指すかを分ける。
    public enum LockTargetKind
    {
        Player = 0,
        Vehicle = 1,
    }
}
