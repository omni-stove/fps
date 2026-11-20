using System.Globalization;
using Shooter.Core;

namespace Shooter.Server
{
    internal static class ServerViolationLog
    {
        private const string _tag = AntiCheatSettings.LogTag + " violations ";

        public static string Off()
        {
            return _tag + "off (" + ServerCombatTuning.AntiCheatFileName + " violations.enabled is false); rejected shots and inputs over budget are not counted";
        }

        public static string On(AntiCheatTuning.ViolationSettings settings)
        {
            return _tag + "on window=" + Seconds(settings.WindowSeconds) + "s"
                + " shotRejectionLimit=" + Count(settings.ShotRejectionLimit)
                + " overBudgetLimit=" + Count(settings.OverBudgetLimit);
        }

        public static string Kicking(int playerId, ServerViolationKind kind, int count, int limit, float windowSeconds)
        {
            return _tag + "kicking id=" + Count(playerId) + ": " + Reason(kind, count, limit, windowSeconds);
        }

        // ServerAntiCheat の Kick と同じく、ServerHost が切るときの行で何の検知で切ったかが分かるよう頭に付ける
        public static string KickText(ServerViolationKind kind, int count, int limit, float windowSeconds)
        {
            return AntiCheatSettings.RejectedDetail + ": " + Reason(kind, count, limit, windowSeconds);
        }

        private static string Reason(ServerViolationKind kind, int count, int limit, float windowSeconds)
        {
            return "reason=" + kind + " count=" + Count(count) + " limit=" + Count(limit) + " window=" + Seconds(windowSeconds) + "s";
        }

        private static string Count(int value)
        {
            return value.ToString(CultureInfo.InvariantCulture);
        }

        private static string Seconds(float value)
        {
            return value.ToString("0.###", CultureInfo.InvariantCulture);
        }
    }
}
