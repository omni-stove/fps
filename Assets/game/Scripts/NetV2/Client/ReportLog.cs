using System;
using System.Globalization;
using Shooter.Core;

namespace Shooter.NetV2
{
    public static class ReportLog
    {
        public const string Tag = "[Report]";

        public static string Sent(int participantId, ReportReason reason, bool sent)
        {
            if (!Enum.IsDefined(typeof(ReportReason), reason))
            {
                throw new ArgumentOutOfRangeException(nameof(reason), reason, "reason must be a defined ReportReason");
            }

            return string.Format(
                CultureInfo.InvariantCulture,
                sent ? "{0} sent target={1} reason={2}" : "{0} could not send target={1} reason={2} (not connected)",
                Tag,
                participantId,
                reason);
        }

        public static string NotSent(int participantId, ReportReason reason)
        {
            return string.Format(
                CultureInfo.InvariantCulture, "{0} could not send target={1} reason={2} (left the match)", Tag, participantId, reason);
        }
    }
}
