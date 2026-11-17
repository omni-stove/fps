using System.Globalization;

namespace Shooter.Core
{
    public static class MountedGunLog
    {
        public const string Tag = "[MountedGun]";

        public const string ReasonSeatCount = "seat_count";

        public const string ReasonDriverSeat = "driver_seat";

        public const string ReasonNoWeapon = "no_weapon";

        public const string ReasonHasMotor = "has_motor";

        public static string Invalid(string gun, string reason)
        {
            return $"{Tag} '{gun}' is not a mounted gun reason={reason}";
        }

        public static string ShieldFallback(string gun, string problem)
        {
            return $"{Tag} '{gun}' {problem}; using the placeholder shield";
        }

        public static string Seek(string enemy, string gun, float distance)
        {
            return string.Format(CultureInfo.InvariantCulture, "{0} '{1}' seek gun={2} distance={3:0.0}", Tag, enemy, gun, distance);
        }

        public static string Man(string enemy, string gun)
        {
            return $"{Tag} '{enemy}' man gun={gun}";
        }

        public static string Leave(string enemy, string gun, MountedGunLeaveCause cause)
        {
            return $"{Tag} '{enemy}' leave gun={gun} cause={cause}";
        }
    }
}
