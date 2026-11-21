using System.Globalization;
using Shooter.Core;

namespace Shooter.Server
{
    // オフラインと同じ行 (GrappleLog) の後ろに、誰のどの入力かを足す。当たりには名前が無いので、面は当たりの Id で示す
    internal static class ServerGrappleLog
    {
        public const string RefusedPrefix = GrappleLog.Tag + " refused ";

        public static string Loaded(GrappleType grapple, string path)
        {
            return GrappleLog.Tag + " grapple id=" + grapple.Id
                + " range=" + Text(grapple.Spec.MinRange) + "-" + Text(grapple.Spec.MaxRange)
                + " charges=" + Text(grapple.Ability.MaxCharges)
                + " from " + (path ?? "-");
        }

        public static string Attached(int playerId, int tick, GrappleTarget target)
        {
            return GrappleLog.Attached(Surface(target.ColliderId), target.Distance, target.SnappedToEdge) + Who(playerId, tick);
        }

        public static string Miss(int playerId, int tick, GrappleMiss reason, GrappleTarget target)
        {
            var hit = reason == GrappleMiss.TooClose || reason == GrappleMiss.NotGrabbable;
            return GrappleLog.Miss(reason, hit ? Surface(target.ColliderId) : null, hit ? target.Distance : (float?)null) + Who(playerId, tick);
        }

        public static string Arrived(int playerId, int tick, float seconds, bool wall)
        {
            return GrappleLog.Arrived(seconds, wall) + Who(playerId, tick);
        }

        public static string Released(int playerId, int tick, GrappleEnd reason, float speed)
        {
            return GrappleLog.Released(reason, speed) + Who(playerId, tick);
        }

        public static string Refused(int playerId, int tick, ServerGrappleRefusal reason)
        {
            return RefusedPrefix + "reason=" + reason + Who(playerId, tick);
        }

        private static string Surface(int colliderId)
        {
            return "collider-" + Text(colliderId);
        }

        private static string Who(int playerId, int tick)
        {
            return " owner=" + Text(playerId) + " tick=" + Text(tick);
        }

        private static string Text(int value)
        {
            return value.ToString(CultureInfo.InvariantCulture);
        }

        private static string Text(float value)
        {
            return value.ToString("0.##", CultureInfo.InvariantCulture);
        }
    }
}
