namespace Shooter.Core
{
    public enum PickupSideRule
    {
        // 0 にしてあるのは、default(PickupEligibility) を誰でも拾える条件にするため。
        Anyone = 0,

        OnlySide = 1,

        ExceptSide = 2,
    }
}
