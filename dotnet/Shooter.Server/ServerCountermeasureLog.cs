using System.Globalization;
using Shooter.Core;

namespace Shooter.Server
{
    // フレアとロックオンの警告の行。フレアの行は Unity の CountermeasureLog と同じ形にし、撒いた人 (rider=) を足す。
    internal static class ServerCountermeasureLog
    {
        public const string ReasonNotDriver = "not_driver";

        public const string ReasonNoFlares = "no_flares";

        public const string ReasonEmpty = "empty";

        public const string ReasonCoolingDown = "cooling_down";

        public static string Deployed(string vehicle, int rider, int flareId, int remaining, int count)
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "{0} '{1}' flare deployed rider={2} id={3} remaining={4}/{5}",
                VehicleLog.Tag,
                vehicle,
                rider,
                flareId,
                remaining,
                count);
        }

        public static string Refused(string vehicle, int rider, string reason)
        {
            return string.Format(CultureInfo.InvariantCulture, "{0} '{1}' flare refused rider={2} reason={3}", VehicleLog.Tag, vehicle, rider, reason);
        }

        public static string Refused(string vehicle, int rider, CountermeasureVerdict verdict, int remaining, int count, float cooldownLeft)
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "{0} '{1}' flare refused rider={2} reason={3} remaining={4}/{5} wait={6:F2}",
                VehicleLog.Tag,
                vehicle,
                rider,
                verdict == CountermeasureVerdict.Empty ? ReasonEmpty : ReasonCoolingDown,
                remaining,
                count,
                cooldownLeft);
        }

        public static string Lured(int projectileId, string weaponId, int flareId)
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "{0} net projectile id={1} weapon={2} lured flare={3}",
                ServerCombat.LogTag,
                projectileId,
                weaponId,
                flareId);
        }

        public static string LockOn(int shooterId, ServerLockTarget target, string rejection)
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "{0} net lock-on shooter={1} target={2} {3}",
                ServerCombat.LogTag,
                shooterId,
                target,
                rejection == null ? "accepted" : "rejected reason=" + rejection);
        }

        public static string Warning(int playerId, int projectileId, ServerLockTarget target)
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "{0} net lock-on warning player={1} stage=locked projectile={2} target={3}",
                ServerCombat.LogTag,
                playerId,
                projectileId,
                target);
        }

        public static string WarningCleared(int playerId)
        {
            return string.Format(CultureInfo.InvariantCulture, "{0} net lock-on warning player={1} cleared", ServerCombat.LogTag, playerId);
        }
    }
}
