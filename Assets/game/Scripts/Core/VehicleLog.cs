using System;
using System.Collections.Generic;
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

        public static string TransitionBegin(string vehicle, string rider, int seat, VehicleSeatTransitionKind kind, float seconds)
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "{0} '{1}' {2} begin rider={3} seat={4} seconds={5:0.00}",
                Tag,
                vehicle,
                KindName(kind),
                rider,
                seat,
                seconds);
        }

        public static string TransitionEnd(string vehicle, string rider, int seat, VehicleSeatTransitionKind kind)
        {
            return $"{Tag} '{vehicle}' {KindName(kind)} end rider={rider} seat={seat}";
        }

        private static string KindName(VehicleSeatTransitionKind kind)
        {
            switch (kind)
            {
                case VehicleSeatTransitionKind.Entering:
                    return "enter";
                case VehicleSeatTransitionKind.Exiting:
                    return "exit";
                default:
                    throw new ArgumentOutOfRangeException(nameof(kind), kind, "kind must be Entering or Exiting");
            }
        }

        public static string Leave(string vehicle, string rider, int seat, VehicleLeaveReason reason, int exitIndex)
        {
            var exit = exitIndex >= 0 ? exitIndex.ToString(CultureInfo.InvariantCulture) : "-";
            return $"{Tag} '{vehicle}' leave rider={rider} seat={seat} reason={reason} exit={exit}";
        }

        public static string BailOut(string vehicle, string rider, float inheritedSpeed, ParachuteOpenVerdict? parachute)
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "{0} '{1}' bail out rider={2} speed={3:0.0} parachute={4}",
                Tag,
                vehicle,
                rider,
                inheritedSpeed,
                parachute.HasValue ? parachute.Value.ToString() : "-");
        }

        public static string DiedInSeat(string vehicle, string rider, int seat)
        {
            return $"{Tag} '{vehicle}' died in seat rider={rider} seat={seat}";
        }

        public static string ExitBlockedBy(string vehicle, int exitIndex, string blocker)
        {
            return $"{Tag} '{vehicle}' exit={exitIndex} blocked by={blocker}";
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

        public static string PartDamaged(string vehicle, string part, int damage, int total)
        {
            return string.Format(CultureInfo.InvariantCulture, "{0} '{1}' part damaged part={2} dmg={3} total={4}", Tag, vehicle, part, damage, total);
        }

        public static string TooManyNetParts(string vehicle, string part, int max)
        {
            return string.Format(
                CultureInfo.InvariantCulture, "{0} '{1}' part '{2}' is not sent; at most {3} parts per vehicle are synced", Tag, vehicle, part, max);
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

        public static string MissingAnimatorParameters(string vehicle, string controller, IReadOnlyList<string> parameters)
        {
            if (parameters == null)
            {
                throw new ArgumentNullException(nameof(parameters));
            }

            var names = parameters.Count > 0 ? string.Join(", ", parameters) : "-";
            return $"{Tag} '{vehicle}' animator controller '{controller}' has no parameter {names}; the gait animator does not set them";
        }

        public static string BotDriverMissingVehicle(string driver)
        {
            return $"{Tag} bot driver '{driver}' has no Vehicle; it drives nothing";
        }

        public static string BotDriverMissingDriverSeat(string vehicle)
        {
            return $"{Tag} '{vehicle}' has no driver seat; the bot driver drives nothing";
        }

        public static string BotDriverMissingRoute(string vehicle)
        {
            return $"{Tag} '{vehicle}' bot driver has no route with points; it stays put";
        }

        public static string BotDriverPhase(string vehicle, VehicleRoutePhase phase, int targetIndex)
        {
            return string.Format(CultureInfo.InvariantCulture, "{0} '{1}' bot driver phase={2} target={3}", Tag, vehicle, phase, targetIndex);
        }

        public static string BotTakesControls(string vehicle, string rider, bool flies, string route)
        {
            return $"{Tag} '{vehicle}' bot takes controls rider={rider} as={(flies ? "pilot" : "driver")} route={(string.IsNullOrEmpty(route) ? "none" : route)}";
        }

        public static string BotPilotTypeInvalid(string vehicle, string pilotType)
        {
            return $"{Tag} '{vehicle}' bot pilot type '{(string.IsNullOrEmpty(pilotType) ? "none" : pilotType)}' is not a MonoBehaviour with IBotPilot; the bot drives it as a car";
        }

        public static string BotPilotNotAdded(string vehicle, string pilotType)
        {
            return $"{Tag} '{vehicle}' bot pilot '{pilotType}' could not be added; the bot drives it as a car";
        }

        public static string BotGivesUpControls(string vehicle, string rider)
        {
            return $"{Tag} '{vehicle}' bot gives up controls rider={rider}";
        }

        public static string BotRouteOrdered(string rider, string route)
        {
            return $"{Tag} bot route order rider={rider} route={(string.IsNullOrEmpty(route) ? "none" : route)}";
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
