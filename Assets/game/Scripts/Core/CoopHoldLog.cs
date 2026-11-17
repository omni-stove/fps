using System;
using System.Globalization;

namespace Shooter.Core
{
    public static class CoopHoldLog
    {
        public const string Tag = "[Coop]";

        public static string Describe(CoopHold hold)
        {
            if (hold == null)
            {
                throw new ArgumentNullException(nameof(hold));
            }

            return string.Format(
                CultureInfo.InvariantCulture,
                "holding={0}/{1} elapsed={2:0.00}/{3:0.00}",
                hold.HoldingCount,
                hold.Spec.RequiredCount,
                hold.ElapsedSeconds,
                hold.Spec.HoldSeconds);
        }
    }
}
