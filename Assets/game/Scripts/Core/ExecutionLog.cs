using System;
using System.Globalization;

namespace Fps.Core
{
    /// <summary>
    /// エクスキューション (<see cref="ExecutionSession"/>) の <c>[Mode]</c> 付きログの文言。参加者の出入りや状態は <see cref="ModeLog"/> のものを使い、
    /// ここにはダウン・蘇生・とどめ・出血とラウンドの区切りを置く。人は ID、陣営は ID の数字で書き、分からない人は <c>-</c>。
    /// </summary>
    public static class ExecutionLog
    {
        public const string Tag = ModeLog.Tag;

        /// <example><c>[Mode] downed player=2 by=0</c>、ダウンさせた人が分からなければ <c>[Mode] downed player=2 by=-</c></example>
        public static string Downed(int playerId, int? downerId)
        {
            return Tag + " downed player=" + Number(playerId) + " by=" + Optional(downerId);
        }

        /// <example><c>[Mode] revived player=2 by=3</c></example>
        public static string Revived(int reviverId, int playerId)
        {
            return Tag + " revived player=" + Number(playerId) + " by=" + Number(reviverId);
        }

        /// <example><c>[Mode] executed victim=2 by=0</c></example>
        public static string Executed(int executorId, int victimId)
        {
            return Tag + " executed victim=" + Number(victimId) + " by=" + Number(executorId);
        }

        /// <example><c>[Mode] bledout player=2 credit=0</c>、倒したことにする敵がいなければ <c>credit=-</c></example>
        public static string BledOut(int playerId, int? creditId)
        {
            return Tag + " bledout player=" + Number(playerId) + " credit=" + Optional(creditId);
        }

        /// <example><c>[Mode] round=2 preparing</c></example>
        public static string Preparing(int roundNumber)
        {
            return Tag + " round=" + Number(roundNumber) + " preparing";
        }

        /// <example><c>[Mode] round=2 live</c></example>
        public static string Live(int roundNumber)
        {
            return Tag + " round=" + Number(roundNumber) + " live";
        }

        /// <summary>ラウンドの終わりと、その時点の両チームのラウンドの勝ち数。</summary>
        /// <example><c>[Mode] round=2 ended winner=1 wins=0:1,1:1</c>、引き分けなら <c>winner=draw</c></example>
        public static string RoundEnded(RoundSession round, Side? winner)
        {
            if (round == null)
            {
                throw new ArgumentNullException(nameof(round));
            }

            return Tag + " round=" + Number(round.RoundNumber) + " ended winner=" + (winner.HasValue ? Number(winner.Value.Id) : ModeLog.Draw)
                + " wins=" + Number(Side.Alpha.Id) + ":" + Number(round.GetRoundWins(Side.Alpha))
                + "," + Number(Side.Bravo.Id) + ":" + Number(round.GetRoundWins(Side.Bravo));
        }

        private static string Optional(int? value)
        {
            return value.HasValue ? Number(value.Value) : "-";
        }

        private static string Number(int value)
        {
            return value.ToString(CultureInfo.InvariantCulture);
        }
    }
}
