using System.Globalization;
using System.Numerics;

namespace Fps.Core
{
    // 盾持ち・狙撃兵と同じく、敵の状態のログと並べて読めるよう [AI] に出す。
    public static class ChargerLog
    {
        public const string LogTag = "[AI]";

        public static string Phase(string id, ChargerPhase previous, ChargerPhase next)
        {
            return $"{LogTag} id={id} charger phase={next} prev={previous}";
        }

        public static string Windup(string id, string target, float seconds)
        {
            return string.Format(CultureInfo.InvariantCulture, "{0} id={1} charger windup target={2} seconds={3:F2}", LogTag, id, target, seconds);
        }

        public static string Charge(string id, Vector3 direction, float distance)
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "{0} id={1} charger charge dir=({2:F2}, {3:F2}) distance={4:F1}",
                LogTag,
                id,
                direction.X,
                direction.Z,
                distance);
        }

        public static string Hit(string id, string target, int damage, float travelled)
        {
            return string.Format(
                CultureInfo.InvariantCulture, "{0} id={1} charger hit target={2} dmg={3} travelled={4:F1}", LogTag, id, target, damage, travelled);
        }

        public static string Missed(string id, ChargeEnd cause, float travelled, float stunSeconds)
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "{0} id={1} charger missed cause={2} travelled={3:F1} stun={4:F2}",
                LogTag,
                id,
                cause.ToString().ToLowerInvariant(),
                travelled,
                stunSeconds);
        }

        public static string Blocked(string id, string obstacle)
        {
            return $"{LogTag} id={id} charger blocked by={(string.IsNullOrEmpty(obstacle) ? "unknown" : obstacle)}";
        }

        public static string Cancelled(string id, string reason)
        {
            return $"{LogTag} id={id} charger cancelled reason={reason}";
        }

        public static string Ready(string id)
        {
            return $"{LogTag} id={id} charger ready";
        }
    }
}
