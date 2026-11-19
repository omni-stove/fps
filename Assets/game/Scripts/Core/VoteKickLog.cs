using System;
using System.Globalization;

namespace Shooter.Core
{
    // 書式を 1 か所に置くのは、サーバーとテストがログの行を同じ関数で作って突き合わせるため (MapVoteLog と同じ)。
    public static class VoteKickLog
    {
        public const string Tag = "[VoteKick]";

        public static string Settings(VoteKickSpec spec)
        {
            if (spec == null)
            {
                throw new ArgumentNullException(nameof(spec));
            }

            if (!spec.Enabled)
            {
                return Tag + " off";
            }

            return Tag + " on yesFraction=" + spec.RequiredYesFraction.ToString("0.###", CultureInfo.InvariantCulture)
                + " seconds=" + Seconds(spec.VoteSeconds)
                + " cooldown=" + Seconds(spec.CooldownSeconds);
        }

        public static string Called(int callerId, int targetId, VoteKickCall result)
        {
            return Tag + " call caller=" + Id(callerId) + " target=" + Id(targetId) + " result=" + result;
        }

        public static string Opened(VoteKick vote)
        {
            if (vote == null)
            {
                throw new ArgumentNullException(nameof(vote));
            }

            return Tag + " opened target=" + Id(vote.TargetId) + " caller=" + Id(vote.CallerId)
                + " voters=" + Id(vote.VoterCount) + " required=" + Id(vote.RequiredVotes)
                + " seconds=" + Seconds(vote.RemainingSeconds);
        }

        public static string Voted(int voterId, int targetId, bool inFavor, VoteKickBallot result)
        {
            return Tag + " vote player=" + Id(voterId) + " target=" + Id(targetId)
                + " inFavor=" + (inFavor ? "true" : "false") + " result=" + result;
        }

        public static string Left(int playerId)
        {
            return Tag + " left player=" + Id(playerId);
        }

        public static string Decided(VoteKickResult result)
        {
            return Tag + " decided target=" + Id(result.TargetId) + " caller=" + Id(result.CallerId)
                + " yes=" + Id(result.YesVotes) + " no=" + Id(result.NoVotes) + " required=" + Id(result.RequiredVotes)
                + " end=" + result.End;
        }

        public static string Kicked(int playerId, bool barred)
        {
            return Tag + " kicked player=" + Id(playerId)
                + (barred ? " barred until the next match" : " (no rejoin key; cannot bar a rejoin)");
        }

        public static string KickedLogText(VoteKickResult result)
        {
            return "vote kicked (yes " + Id(result.YesVotes) + "/" + Id(result.RequiredVotes) + ")";
        }

        public static string RefusedRejoin(string matchId)
        {
            return "vote kicked from match " + (string.IsNullOrEmpty(matchId) ? "none" : matchId) + "; refused until the next match";
        }

        public static string Unbarred(int count)
        {
            return Tag + " next match; " + Id(count) + " vote-kicked account(s) may join again";
        }

        private static string Id(int value)
        {
            return value.ToString(CultureInfo.InvariantCulture);
        }

        private static string Seconds(float seconds)
        {
            return seconds.ToString("0.0", CultureInfo.InvariantCulture);
        }
    }
}
