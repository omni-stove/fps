using System.Globalization;

namespace Fps.Core
{
    // 盾持ち・狙撃兵と同じく、敵の状態のログと並べて読めるよう [AI] に出す。
    public static class BomberLog
    {
        public const string LogTag = "[AI]";

        public const string CauseFuse = "fuse";
        public const string CauseKilled = "killed";
        public const string CauseKilledArmed = "killed-armed";

        public static string Phase(string id, BomberPhase previous, BomberPhase next)
        {
            return $"{LogTag} id={id} bomber phase={next} prev={previous}";
        }

        public static string Fuse(string id, string target, float seconds)
        {
            return string.Format(CultureInfo.InvariantCulture, "{0} id={1} bomber fuse target={2} seconds={3:F2}", LogTag, id, target, seconds);
        }

        public static string Exploded(string id, string cause, float radius, int damaged, int killed)
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "{0} id={1} bomber exploded cause={2} radius={3:F1} damaged={4} killed={5}",
                LogTag,
                id,
                cause,
                radius,
                damaged,
                killed);
        }

        public static string Hit(string id, string target, int damage, float distance)
        {
            return string.Format(
                CultureInfo.InvariantCulture, "{0} id={1} bomber hit target={2} dmg={3} distance={4:F1}", LogTag, id, target, damage, distance);
        }
    }
}
