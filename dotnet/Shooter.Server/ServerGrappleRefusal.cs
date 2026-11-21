namespace Shooter.Server
{
    // 縄を撃つ前に断る理由。撃って外れた理由 (届かない・掴めない) は Core の GrappleMiss で表す
    internal enum ServerGrappleRefusal
    {
        None,
        RuleOff,
        CannotAct,
        NotEquipped,
        CoolingDown,
        NoCharges,
    }
}
