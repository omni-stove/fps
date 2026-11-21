using System;
using Shooter.Protocol;

namespace Shooter.NetV2
{
    // 捕捉 (LockOnTracker) は相手を 1 つの int で持つので、人の Id と乗り物の番号を同じ並びに置く。
    // 人の Id は 0 から数えるので、乗り物は人と重ならない大きな数から数え、撃つときに送る形 (LockTargetKind と番号) に戻す。
    public static class ClientLockTargetIds
    {
        public const int VehicleBase = 1 << 30;

        public static int OfVehicle(int vehicle)
        {
            if (vehicle < 0 || vehicle >= int.MaxValue - VehicleBase)
            {
                throw new ArgumentOutOfRangeException(nameof(vehicle), vehicle, "vehicle must be a non-negative vehicle index");
            }

            return VehicleBase + vehicle;
        }

        public static bool IsVehicle(int targetId)
        {
            return targetId >= VehicleBase;
        }

        public static LockTargetKind KindOf(int targetId)
        {
            return IsVehicle(targetId) ? LockTargetKind.Vehicle : LockTargetKind.Player;
        }

        // 乗り物なら番号、人なら Id のまま返す。
        public static int WireIdOf(int targetId)
        {
            return IsVehicle(targetId) ? targetId - VehicleBase : targetId;
        }
    }
}
