using System;
using System.Globalization;

namespace Fps.Core
{
    /// <summary>二人で使う物 (<see cref="CoopHold"/>) の <c>[Coop]</c> 付きログの文言。</summary>
    public static class CoopHoldLog
    {
        public const string Tag = "[Coop]";

        /// <summary>人数と経過を並べた説明。</summary>
        /// <example><c>holding=1/2 elapsed=0.50/3.00</c></example>
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
