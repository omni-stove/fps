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

        public MountedGunLeaveCause? Leave { get; }
    }
}
