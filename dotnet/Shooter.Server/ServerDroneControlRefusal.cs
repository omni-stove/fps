namespace Shooter.Server
{
    internal enum ServerDroneControlRefusal
    {
        None,
        NotFound,
        NotDrone,
        NotOwner,
        CannotAct,
        Stale,
    }
}
