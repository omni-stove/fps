using System;
using System.Globalization;

namespace Shooter.Core
{
    public static class StaminaLog
    {
        public const string LogTag = "[Stamina]";

        public static string FormatConfigured(Stamina rule, StaminaSpec spec)
        {
            if (spec == null)
            {
                throw new ArgumentNullException(nameof(spec));
            }

            return string.Format(
                CultureInfo.InvariantCulture,
                "{0} rule={1} max={2:0.00} sprintPerSecond={3:0.00} slide={4:0.00} dodge={5:0.00} regenPerSecond={6:0.00} regenDelay={7:0.00} recover={8:0.00}",
                LogTag,
                rule,
                spec.Max,
                spec.SprintCostPerSecond,
                spec.SlideCost,
                spec.DodgeCost,
                spec.RegenPerSecond,
                spec.RegenDelaySeconds,
                spec.RecoverAmount);
        }

        public static string FormatDepleted(StaminaStatus status)
        {
            return LogTag + " depleted " + FormatStatus(status);
        }

        public static string FormatRecovered(StaminaStatus status)
        {
            return LogTag + " recovered " + FormatStatus(status);
        }

        public static string FormatFull(StaminaStatus status)
        {
            return LogTag + " full " + FormatStatus(status);
        }

        public static string FormatRefilled(StaminaStatus status)
        {
            return LogTag + " refilled " + FormatStatus(status);
        }

        public static string FormatSpent(StaminaUse use, float cost, StaminaStatus status)
        {
            return string.Format(CultureInfo.InvariantCulture, "{0} spent use={1} cost={2:0.00} ", LogTag, use, cost) + FormatStatus(status);
        }

        public static string FormatRefused(StaminaUse use, float cost, StaminaStatus status)
        {
            return string.Format(CultureInfo.InvariantCulture, "{0} refused use={1} cost={2:0.00} ", LogTag, use, cost) + FormatStatus(status);
        }

        private static string FormatStatus(StaminaStatus status)
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "current={0:0.00} max={1:0.00} depleted={2}",
                status.Current,
                status.Max,
                status.Depleted ? "true" : "false");
        }
    }
}
