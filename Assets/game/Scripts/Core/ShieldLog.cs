using System.Globalization;

namespace Fps.Core
{
    // 敵の状態のログ (EnemyAgent.LogTag) と同じ [AI] に出す。盾持ちの出来事を、同じ敵の状態の移り変わりと並べて読めるように。
    public static class ShieldLog
    {
        public const string LogTag = "[AI]";

        public static string Blocked(string id, int damage, string shooter)
        {
            return $"{LogTag} id={id} shield blocked dmg={damage} by={(string.IsNullOrEmpty(shooter) ? "unknown" : shooter)}";
        }

        public static string Lowered(string id, string cause, float seconds)
        {
            return string.Format(CultureInfo.InvariantCulture, "{0} id={1} shield lowered cause={2} seconds={3:F2}", LogTag, id, cause, seconds);
        }

        public static string Raised(string id)
        {
            return $"{LogTag} id={id} shield raised";
        }

        public static string Bash(string id)
        {
            return $"{LogTag} id={id} shield bash";
        }
    }
}
