using System;
using System.Globalization;
using System.Numerics;

namespace Shooter.Core
{
    public static class LadderLog
    {
        public const string LogTag = "[Ladder]";

        public const string GrabPrefix = LogTag + " grab ";

        public const string LeavePrefix = LogTag + " leave ";

        public static string Grab(string ladder, LadderEntry entry, float height)
        {
            return string.Format(
                CultureInfo.InvariantCulture, "{0}ladder={1} entry={2} height={3:0.00}", GrabPrefix, NameOf(ladder), entry, height);
        }

        public static string Leave(string ladder, LadderOutcome reason, float height)
        {
            if (reason == LadderOutcome.Climbing)
            {
                throw new ArgumentOutOfRangeException(nameof(reason), reason, "Climbing is not a reason to leave a ladder");
            }

            return string.Format(
                CultureInfo.InvariantCulture, "{0}ladder={1} reason={2} height={3:0.00}", LeavePrefix, NameOf(ladder), reason, height);
        }

        // 高さを問わずに探せるよう、高さの前までを返す。
        public static string LeavePrefixFor(string ladder, LadderOutcome reason)
        {
            return $"{LeavePrefix}ladder={NameOf(ladder)} reason={reason} ";
        }

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
