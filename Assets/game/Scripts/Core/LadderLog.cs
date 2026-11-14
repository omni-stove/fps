using System;
using System.Globalization;
using System.Numerics;

namespace Fps.Core
{
    /// <summary>はしごのログの文。どれも <see cref="LogTag"/> で始まる。</summary>
    public static class LadderLog
    {
        public const string LogTag = "[Ladder]";

        /// <summary><see cref="Grab"/> の文の頭。</summary>
        public const string GrabPrefix = LogTag + " grab ";

        /// <summary><see cref="Leave"/> の文の頭。</summary>
        public const string LeavePrefix = LogTag + " leave ";

        /// <summary>例: <c>[Ladder] grab ladder=Ladder entry=Front height=0.00</c>。</summary>
        public static string Grab(string ladder, LadderEntry entry, float height)
        {
            return string.Format(
                CultureInfo.InvariantCulture, "{0}ladder={1} entry={2} height={3:0.00}", GrabPrefix, NameOf(ladder), entry, height);
        }

        /// <summary>例: <c>[Ladder] leave ladder=Ladder reason=ExitTop height=3.00</c>。</summary>
        /// <exception cref="ArgumentOutOfRangeException">reason が <see cref="LadderOutcome.Climbing"/>。</exception>
        public static string Leave(string ladder, LadderOutcome reason, float height)
        {
            if (reason == LadderOutcome.Climbing)
            {
                throw new ArgumentOutOfRangeException(nameof(reason), reason, "Climbing is not a reason to leave a ladder");
            }

            return string.Format(
                CultureInfo.InvariantCulture, "{0}ladder={1} reason={2} height={3:0.00}", LeavePrefix, NameOf(ladder), reason, height);
        }

        /// <summary>reason で離れたときの <see cref="Leave"/> の文が始まる部分。高さを問わずに探すのに使う。</summary>
        public static string LeavePrefixFor(string ladder, LadderOutcome reason)
        {
            return $"{LeavePrefix}ladder={NameOf(ladder)} reason={reason} ";
        }

        /// <summary>例: <c>[Ladder] link ladder=Ladder from=(0.00, 0.00, -1.00) to=(0.00, 3.00, 1.00) agentTypes=1</c>。</summary>
        public static string Link(string ladder, Vector3 from, Vector3 to, int agentTypes)
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "{0} link ladder={1} from={2} to={3} agentTypes={4}",
                LogTag,
                NameOf(ladder),
                Format(from),
                Format(to),
                agentTypes);
        }

        private static string NameOf(string ladder)
        {
            return string.IsNullOrEmpty(ladder) ? "-" : ladder;
        }

        private static string Format(Vector3 v)
        {
            return string.Format(CultureInfo.InvariantCulture, "({0:0.00}, {1:0.00}, {2:0.00})", v.X, v.Y, v.Z);
        }
    }
}
