using System.Globalization;
using Shooter.Core;

namespace Shooter.Server
{
    // 行の頭はオフラインと同じタグ (StaminaLog・SuppressionLog) にし、同じ語で検索できるようにする
    internal static class ServerPlayerStatusLog
    {
        public const string LogTag = "[Status]";
        public const string StaggerTag = "[Stagger]";

        public static string Rules(ActionRules actions)
        {
            return LogTag + " rules stamina=" + actions.Stamina + " stagger=" + actions.Stagger + " suppression=" + actions.Suppression;
        }

        public static string Tuning(string staminaPath, string staggerPath, string suppressionPath)
        {
            return ServerProgram.LogTag + " status tuning"
                + " stamina=" + (staminaPath ?? "default")
                + " stagger=" + (staggerPath ?? "default")
                + " suppression=" + (suppressionPath ?? "default");
        }

        public static string StaminaDepleted(int playerId, StaminaStatus status)
        {
            return StaminaLog.LogTag + " depleted player=" + Text(playerId) + " " + Status(status);
        }

        public static string StaminaRecovered(int playerId, StaminaStatus status)
        {
            return StaminaLog.LogTag + " recovered player=" + Text(playerId) + " " + Status(status);
        }

        public static string Staggered(int playerId, StaggerVerdict verdict, HitZone? zone, DamageKind kind)
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "{0} staggered player={1} kind={2} seconds={3:0.00} zone={4} damage={5}",
                StaggerTag,
                playerId,
                verdict.Kind,
                verdict.Seconds,
                zone.HasValue ? zone.Value.ToString() : "none",
                kind);
        }

        public static string StaggerResisted(int playerId, StaggerKind kind)
        {
            return StaggerTag + " resisted player=" + Text(playerId) + " kind=" + kind;
        }

        public static string StaggerRecovered(int playerId, StaggerKind kind)
        {
            return StaggerTag + " recovered player=" + Text(playerId) + " kind=" + kind;
        }

        public static string SuppressedChanged(int playerId, bool suppressed, float level)
        {
            return SuppressionLog.SuppressedChanged(Text(playerId), suppressed, level);
        }

        private static string Status(StaminaStatus status)
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "current={0:0.00} max={1:0.00}",
                status.Current,
                status.Max);
        }

        private static string Text(int value)
        {
            return value.ToString(CultureInfo.InvariantCulture);
        }
    }
}
