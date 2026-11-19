using System;
using Shooter.Core;

namespace Shooter.Runtime
{
    public static class VehicleBoardPrompt
    {
        public const string DriverKey = "hud.vehicleBoard.driver";

        public const string PassengerKey = "hud.vehicleBoard.passenger";

        public const string GunnerKey = "hud.vehicleBoard.gunner";

        public static LocalizedText Describe(VehicleBoardHint hint, string keyLabel)
        {
            return new LocalizedText(KeyOf(hint.Role), keyLabel ?? string.Empty);
        }

        public static string KeyOf(VehicleSeatRole role)
        {
            switch (role)
            {
                case VehicleSeatRole.Driver:
                    return DriverKey;
                case VehicleSeatRole.Passenger:
                    return PassengerKey;
                case VehicleSeatRole.Gunner:
                    return GunnerKey;
                default:
                    throw new ArgumentOutOfRangeException(nameof(role), role, "role is not a defined VehicleSeatRole");
            }
        }
    }
}
