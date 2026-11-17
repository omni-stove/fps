using System;
using System.Globalization;

namespace Shooter.Core
{
    // 書式を 1 か所に置くのは、サーバーとテストがログの行を同じ関数で作って突き合わせるため。
    public static class MapVoteLog
    {
        public const string Tag = "[MapVote]";

        public static string Opened(MapVote vote)
        {
            if (vote == null)
            {
                throw new ArgumentNullException(nameof(vote));
            }

            return Tag + " opened current=" + Token(vote.CurrentMapId)
                + " candidates=[" + string.Join(", ", vote.Candidates) + "]"
                + " seconds=" + vote.RemainingSeconds.ToString("0.0", CultureInfo.InvariantCulture);
        }

        public static string Cast(int playerId, string mapId, MapVoteCast result)
        {
            return Tag + " cast player=" + playerId.ToString(CultureInfo.InvariantCulture) + " map=" + Token(mapId) + " result=" + result;
        }

        public static string Left(int playerId, string droppedMapId)
        {
            return Tag + " left player=" + playerId.ToString(CultureInfo.InvariantCulture) + " dropped=" + Token(droppedMapId);
        }

        public static string Decided(MapVoteDecision decision)
        {
            return Tag + " decided map=" + Token(decision.MapId) + " reason=" + decision.Reason
                + " votes=" + decision.Votes.ToString(CultureInfo.InvariantCulture);
        }

        public static string ClientChanged(MapVoteStatus? status)
        {
            return status.HasValue ? Tag + " net " + status.Value : Tag + " net ended";
        }

        public static string ClientMalformed(int candidateCount)
        {
            return Tag + " net dropped a malformed vote candidates=" + candidateCount.ToString(CultureInfo.InvariantCulture);
        }

        public static string ClientRequested(int candidateIndex, string mapId, MapVoteCast result, bool sent)
        {
            var line = Tag + " net request index=" + candidateIndex.ToString(CultureInfo.InvariantCulture)
                + " map=" + Token(mapId) + " result=" + result;
            if (result != MapVoteCast.Cast && result != MapVoteCast.Changed)
            {
                return line + " (not sent)";
            }

            return sent ? line : line + " (not connected; not sent)";
        }

        public static string ClientMapChanging(string fromMapId, string toMapId)
        {
            return Tag + " net next match map " + Token(fromMapId) + " -> " + Token(toMapId);
        }

        // 送られてきたマップ Id に空白があってもログの区切りが崩れないよう、空白は _ に、空なら none にする。
        private static string Token(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return "none";
            }

            var chars = text.Trim().ToCharArray();
            for (var i = 0; i < chars.Length; i++)
            {
                if (char.IsWhiteSpace(chars[i]))
                {
                    chars[i] = '_';
                }
            }

            return new string(chars);
        }
    }
}
