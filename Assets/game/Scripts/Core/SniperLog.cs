using System.Globalization;
using System.Numerics;

namespace Fps.Core
{
    // 盾持ちと同じく、敵の状態のログと並べて読めるよう [AI] に出す。
    public static class SniperLog
    {
        public const string LogTag = "[AI]";

        public static string Phase(string id, SniperPhase previous, SniperPhase next)
        {
            return $"{LogTag} id={id} sniper phase={next} prev={previous}";
        }

        public static string Aim(string id, string target, float seconds)
        {
            return string.Format(CultureInfo.InvariantCulture, "{0} id={1} sniper aim target={2} seconds={3:F2}", LogTag, id, target, seconds);
        }

        public static string AimCancelled(string id, float elapsed)
        {
            return string.Format(CultureInfo.InvariantCulture, "{0} id={1} sniper aim cancelled after={2:F2}s", LogTag, id, elapsed);
        }

        // 当たったかは撃った後に EnemyAgent の fire のログが出すので、ここは狙った一発を撃ったことと倍率だけを出す。
        public static string Fire(string id, float damageMultiplier)
        {
            return string.Format(CultureInfo.InvariantCulture, "{0} id={1} sniper fire multiplier={2:F2}", LogTag, id, damageMultiplier);
        }

        public static string Relocate(string id, int perch, Vector3 position)
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "{0} id={1} sniper relocate perch={2} to=({3:F1}, {4:F1}, {5:F1})",
                LogTag,
                id,
                perch,
                position.X,
                position.Y,
                position.Z);
        }

        public static string Arrived(string id, int perch, bool gaveUp)
        {
            return $"{LogTag} id={id} sniper {(gaveUp ? "relocate gave-up" : "arrived")} perch={perch}";
        }

        public static string Stay(string id)
        {
            return $"{LogTag} id={id} sniper relocate none";
        }
    }
}
