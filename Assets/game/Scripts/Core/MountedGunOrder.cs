namespace Shooter.Core
{
    public readonly struct MountedGunOrder
    {
        public MountedGunOrder(bool fire, MountedGunLeaveCause? leave)
        {
            Fire = fire;
            Leave = leave;
        }

        public bool Fire { get; }

        // 降りるならその理由。乗ったままなら null。
        public MountedGunLeaveCause? Leave { get; }
    }
}
