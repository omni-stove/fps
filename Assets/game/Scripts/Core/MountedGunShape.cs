using System;

namespace Shooter.Core
{
    // 固定銃座は乗り物の土台に乗せるので、別の設定を持たせずに乗り物の設定の形 (座席 1 つ・運転席なし・その座席の武器あり・動き方なし) で見分ける。
    public static class MountedGunShape
    {
        public static string Problem(VehicleSpec spec, bool hasMotor)
        {
            if (spec == null)
            {
                throw new ArgumentNullException(nameof(spec));
            }

            if (spec.Seats.Count != 1)
            {
                return MountedGunLog.ReasonSeatCount;
            }

            if (spec.DriverSeatIndex >= 0)
            {
                return MountedGunLog.ReasonDriverSeat;
            }

            if (spec.WeaponsOfSeat(0).Count == 0)
            {
                return MountedGunLog.ReasonNoWeapon;
            }

            return hasMotor ? MountedGunLog.ReasonHasMotor : null;
        }
    }
}
