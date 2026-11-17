using System.Numerics;

namespace Shooter.Core
{
    public readonly struct BotPickupOption
    {
        public BotPickupOption(Vector3 position, bool givesWeapon)
        {
            Position = position;
            GivesWeapon = givesWeapon;
        }

        public Vector3 Position { get; }

        public bool GivesWeapon { get; }
    }
}
