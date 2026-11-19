using System;
using System.Globalization;

namespace Shooter.Core
{
    public static class VehicleLog
    {
        public const string Tag = "[Vehicle]";

        public const string ReasonNoFreeSeat = "no_free_seat";

        public const string ReasonOutOfRange = "out_of_range";

        public const string ReasonWrecked = "wrecked";

        public const string ReasonAlreadyRiding = "already_riding";

        public const string ReasonRiderUnavailable = "rider_unavailable";

        public const string ReasonSeatTaken = "seat_taken";

        public const string ReasonSeatRemoved = "seat_removed";

        public const string ReasonUnknownVehicle = "unknown_vehicle";

        public const string ReasonNotRiding = "not_riding";

        public const string ReasonExitBlocked = "exit_blocked";

        public static string Board(string vehicle, string rider, int seat, VehicleSeatRole role)
        {
            return $"{Tag} '{vehicle}' board rider={rider} seat={seat} role={role}";
        }

        public static string BoardRefused(string vehicle, string rider, string reason)
        {
            return $"{Tag} '{vehicle}' board refused rider={rider} reason={reason}";
        }

        public static string Switch(string vehicle, string rider, int from, int to, VehicleSeatRole role)
        {
            return $"{Tag} '{vehicle}' switch rider={rider} from={from} to={to} role={role}";
        }

        public static string SwitchRefused(string vehicle, string rider, string reason)
        {
            return $"{Tag} '{vehicle}' switch refused rider={rider} reason={reason}";
        }

        public static string Leave(string vehicle, string rider, int seat, VehicleLeaveReason reason, int exitIndex)
        {
            var exit = exitIndex >= 0 ? exitIndex.ToString(CultureInfo.InvariantCulture) : "-";
            return $"{Tag} '{vehicle}' leave rider={rider} seat={seat} reason={reason} exit={exit}";
        }

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

        public static string PartBroken(string vehicle, string part, AircraftDamageEffect effect, AircraftCondition condition)
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "{0} '{1}' part broken part={2} effect={3} lift={4:0.00}/{5:0.00} control={6:0.00} thrust={7:0.00}",
                Tag,
                vehicle,
                part,
                effect,
                condition.LeftLift,
                condition.RightLift,
                condition.Control,
                condition.Thrust);
        }

        public static string PartDamaged(string vehicle, string part, int damage, int total)
        {
            return string.Format(CultureInfo.InvariantCulture, "{0} '{1}' part damaged part={2} dmg={3} total={4}", Tag, vehicle, part, damage, total);
        }

        public static string TooManyNetParts(string vehicle, string part, int max)
        {
            return string.Format(
                CultureInfo.InvariantCulture, "{0} '{1}' part '{2}' is not sent; at most {3} parts per vehicle are synced", Tag, vehicle, part, max);
        }

        public static string NetCondition(string vehicle, AircraftCondition condition)
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "{0} '{1}' net condition lift={2:0.00}/{3:0.00} control={4:0.00} thrust={5:0.00}",
                Tag,
                vehicle,
                condition.LeftLift,
                condition.RightLift,
                condition.Control,
                condition.Thrust);
        }

        public static string InvalidAircraftDamage(string vehicle, string error)
        {
            return $"{Tag} '{vehicle}' aircraft damage settings are invalid; parts take no flight damage: {error}";
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

        public static string EffectsMissingMotor(string vehicle)
        {
            return $"{Tag} '{vehicle}' has no SimpleAircraftMotor; the aircraft effects show nothing";
        }

        public static string VaporUnswitchable(string vehicle, string prefab)
        {
            return $"{Tag} '{vehicle}' vapor prefab '{prefab}' has no TrailRenderer or ParticleSystem; it is placed but never switched";
        }

        public static string VaporShaderMissing(string shader)
        {
            return $"{Tag} the built-in shader '{shader}' is missing; the code-built vapor trail uses the default material";
        }

        public static string Righted(string vehicle, float tiltDegrees)
        {
            return string.Format(CultureInfo.InvariantCulture, "{0} '{1}' righted tilt={2:0}", Tag, vehicle, tiltDegrees);
        }

        public static string Wrecked(string vehicle, int riders)
        {
            if (riders < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(riders), riders, "riders must not be negative");
            }

            return $"{Tag} '{vehicle}' wrecked riders={riders}";
        }

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

        // heat は熱で撃つ武器だけが持つ。弾倉の武器の行を変えず、読んでいる側をそのままにするため末尾に足す。
        public static string WeaponFired(
            string vehicle,
            string rider,
            int seat,
            string weaponId,
            float yawDegrees,
            float pitchDegrees,
            string hit,
            int damage,
            int ammo,
            float? heat = null)
        {
            var yaw = yawDegrees.ToString("0.0", CultureInfo.InvariantCulture);
            var pitch = pitchDegrees.ToString("0.0", CultureInfo.InvariantCulture);
            var line = $"{Tag} '{vehicle}' fire rider={rider} seat={seat} weapon={weaponId} yaw={yaw} pitch={pitch} hit={hit ?? "-"} dmg={damage} ammo={ammo}";
            return heat.HasValue ? line + " heat=" + heat.Value.ToString("0.00", CultureInfo.InvariantCulture) : line;
        }

        public static string Overheated(string vehicle, int seat, string weaponId, float lockSeconds)
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "{0} '{1}' overheated seat={2} weapon={3} lock={4:0.00}",
                Tag,
                vehicle,
                seat,
                weaponId,
                lockSeconds);
        }

        public static string Cooled(string vehicle, int seat, string weaponId)
        {
            return $"{Tag} '{vehicle}' cooled seat={seat} weapon={weaponId}";
        }
    }
}
