using System;
using System.Globalization;

namespace Fps.Core
{
    /// <summary>乗り物の乗り降りのログの書式。どれも <see cref="Tag"/> で始まる。</summary>
    public static class VehicleLog
    {
        public const string Tag = "[Vehicle]";

        /// <summary>乗り込めなかった理由: 空いている座席が無い。</summary>
        public const string ReasonNoFreeSeat = "no_free_seat";

        /// <summary>乗り込めなかった理由: 座席の乗る位置まで遠い。</summary>
        public const string ReasonOutOfRange = "out_of_range";

        /// <summary>乗り込めなかった理由: 乗り物が壊れている。</summary>
        public const string ReasonWrecked = "wrecked";

        /// <summary>乗り込めなかった理由: もうどこかの乗り物に乗っている。</summary>
        public const string ReasonAlreadyRiding = "already_riding";

        /// <summary>乗り込めなかった理由: 乗る人が乗れる状態でない (倒れているなど)。</summary>
        public const string ReasonRiderUnavailable = "rider_unavailable";

        /// <summary>乗り込めなかった・移れなかった理由: 指定した座席が塞がっている。</summary>
        public const string ReasonSeatTaken = "seat_taken";

        /// <summary>乗り込めなかった・移れなかった理由: 指定した座席が破棄されている。</summary>
        public const string ReasonSeatRemoved = "seat_removed";

        public const string ReasonUnknownVehicle = "unknown_vehicle";

        public const string ReasonNotRiding = "not_riding";

        /// <summary>降りられなかった理由: 降りる場所がすべて塞がっている。</summary>
        public const string ReasonExitBlocked = "exit_blocked";

        /// <example><c>[Vehicle] 'Jeep' board rider=Player seat=0 role=Driver</c></example>
        public static string Board(string vehicle, string rider, int seat, VehicleSeatRole role)
        {
            return $"{Tag} '{vehicle}' board rider={rider} seat={seat} role={role}";
        }

        /// <example><c>[Vehicle] 'Jeep' board refused rider=Player reason=no_free_seat</c></example>
        public static string BoardRefused(string vehicle, string rider, string reason)
        {
            return $"{Tag} '{vehicle}' board refused rider={rider} reason={reason}";
        }

        /// <example><c>[Vehicle] 'Jeep' switch rider=Player from=0 to=1 role=Gunner</c></example>
        public static string Switch(string vehicle, string rider, int from, int to, VehicleSeatRole role)
        {
            return $"{Tag} '{vehicle}' switch rider={rider} from={from} to={to} role={role}";
        }

        /// <example><c>[Vehicle] 'Jeep' switch refused rider=Player reason=no_free_seat</c></example>
        public static string SwitchRefused(string vehicle, string rider, string reason)
        {
            return $"{Tag} '{vehicle}' switch refused rider={rider} reason={reason}";
        }

        /// <summary>降りたときのログ。exitIndex は使った降りる場所の候補の番号で、塞がっていても降ろしたなら -1。</summary>
        /// <example><c>[Vehicle] 'Jeep' leave rider=Player seat=0 reason=Request exit=1</c></example>
        public static string Leave(string vehicle, string rider, int seat, VehicleLeaveReason reason, int exitIndex)
        {
            var exit = exitIndex >= 0 ? exitIndex.ToString(CultureInfo.InvariantCulture) : "-";
            return $"{Tag} '{vehicle}' leave rider={rider} seat={seat} reason={reason} exit={exit}";
        }

        /// <example><c>[Vehicle] 'Jeep' leave refused rider=Player reason=exit_blocked</c></example>
        public static string LeaveRefused(string vehicle, string rider, string reason)
        {
            return $"{Tag} '{vehicle}' leave refused rider={rider} reason={reason}";
        }

        public static string Damaged(string vehicle, int damage, int health, int maxHealth)
        {
            return string.Format(CultureInfo.InvariantCulture, "{0} '{1}' damaged dmg={2} hp={3}/{4}", Tag, vehicle, damage, health, maxHealth);
        }

        public static string Stage(string vehicle, VehicleDamageStage stage, int health, int maxHealth)
        {
            return string.Format(CultureInfo.InvariantCulture, "{0} '{1}' stage={2} hp={3}/{4}", Tag, vehicle, stage, health, maxHealth);
        }

        public static string Exploded(string vehicle, float x, float y, float z, int riders, int damaged, int killed)
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "{0} '{1}' exploded at=({2:0.0}, {3:0.0}, {4:0.0}) riders={5} damaged={6} killed={7}",
                Tag,
                vehicle,
                x,
                y,
                z,
                riders,
                damaged,
                killed);
        }

        public static string UnknownPart(string vehicle, string part)
        {
            return $"{Tag} '{vehicle}' part '{part}' is not in the spec; it takes damage unscaled";
        }

        public static string Ram(string vehicle, string target, int damage, float speed, string driver, int health)
        {
            var by = string.IsNullOrEmpty(driver) ? "-" : driver;
            return string.Format(
                CultureInfo.InvariantCulture,
                "{0} '{1}' ram target={2} dmg={3} speed={4:0.0} by={5} hp={6}",
                Tag,
                vehicle,
                target,
                damage,
                speed,
                by,
                health);
        }

        public static string MissingMotor(string vehicle)
        {
            return $"{Tag} '{vehicle}' has no IVehicleMotor; the driver's input moves nothing";
        }

        public static string Righted(string vehicle, float tiltDegrees)
        {
            return string.Format(CultureInfo.InvariantCulture, "{0} '{1}' righted tilt={2:0}", Tag, vehicle, tiltDegrees);
        }

        /// <example><c>[Vehicle] 'Jeep' wrecked riders=2</c></example>
        public static string Wrecked(string vehicle, int riders)
        {
            if (riders < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(riders), riders, "riders must not be negative");
            }

            return $"{Tag} '{vehicle}' wrecked riders={riders}";
        }

        /// <example><c>[Vehicle] 'jeep#0' corrected driver=1 reason=TooFast generation=1 at=(1.0, 0.5, 2.0)</c></example>
        public static string Corrected(string vehicle, string driver, VehicleMoveRejection reason, int generation, float x, float y, float z)
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "{0} '{1}' corrected driver={2} reason={3} generation={4} at=({5:0.0}, {6:0.0}, {7:0.0})",
                Tag,
                vehicle,
                driver,
                reason,
                generation,
                x,
                y,
                z);
        }

        public static string MoveDropped(string vehicle, string rider, VehicleMoveRejection reason)
        {
            return $"{Tag} '{vehicle}' move dropped rider={rider} reason={reason}";
        }

        public static string NetName(string vehicleId, int index)
        {
            return vehicleId + "#" + index.ToString(CultureInfo.InvariantCulture);
        }

        public static string Firing(string vehicle, string rider, int seat, VehicleSeatFiring firing)
        {
            return $"{Tag} '{vehicle}' firing rider={rider} seat={seat} with={firing}";
        }

        public static string WeaponFired(
            string vehicle, string rider, int seat, string weaponId, float yawDegrees, float pitchDegrees, string hit, int damage, int ammo)
        {
            var yaw = yawDegrees.ToString("0.0", CultureInfo.InvariantCulture);
            var pitch = pitchDegrees.ToString("0.0", CultureInfo.InvariantCulture);
            return $"{Tag} '{vehicle}' fire rider={rider} seat={seat} weapon={weaponId} yaw={yaw} pitch={pitch} hit={hit ?? "-"} dmg={damage} ammo={ammo}";
        }
    }
}
