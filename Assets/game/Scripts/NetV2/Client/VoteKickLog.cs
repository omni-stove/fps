using System.Globalization;
using Shooter.Core;
using Shooter.Protocol;

namespace Shooter.NetV2
{
    public static class VoteKickLog
    {
        public const string Tag = "[VoteKick]";

        public static string Changed(VoteKickStatus? status)
        {
            return status.HasValue ? Tag + " net " + status.Value : Tag + " net ended";
        }

        public static string Decided(int targetId, VoteKickOutcome outcome)
        {
            return Tag + " net decided target=" + targetId.ToString(CultureInfo.InvariantCulture) + " outcome=" + outcome;
        }

        public static string Malformed()
        {
            return Tag + " net dropped a malformed vote";
        }

        public static string Called(int targetId, bool sent)
        {
            return Tag + " net call target=" + targetId.ToString(CultureInfo.InvariantCulture) + Sent(sent);
        }

        public static string Voted(int targetId, bool inFavor, bool sent)
        {
            return Tag + " net vote target=" + targetId.ToString(CultureInfo.InvariantCulture)
                + " inFavor=" + (inFavor ? "true" : "false") + Sent(sent);
        }

        public static string NoVote(bool inFavor)
        {
            return Tag + " net vote inFavor=" + (inFavor ? "true" : "false") + " (no vote in progress; not sent)";
        }

        public static string CallAfterLeaving(int targetId)
        {
            return Tag + " net call target=" + targetId.ToString(CultureInfo.InvariantCulture) + " (left the match; not sent)";
        }

        public static string VoteAfterLeaving(bool inFavor)
        {
            return Tag + " net vote inFavor=" + (inFavor ? "true" : "false") + " (left the match; not sent)";
        }

        private static string Sent(bool sent)
        {
            return sent ? string.Empty : " (not connected; not sent)";
        }
    }
}
