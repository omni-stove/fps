namespace Shooter.Core
{
    public enum InventoryMoveResult
    {
        Moved = 0,

        NoStack = 1,

        NoGrid = 2,

        OutOfGrid = 3,

        Blocked = 4,

        RotationNotAllowed = 5,
    }
}
