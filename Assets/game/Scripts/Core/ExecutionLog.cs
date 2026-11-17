using System;
using System.Globalization;

namespace Shooter.Core
{
    public static class ExecutionLog
    {
        public const string Tag = ModeLog.Tag;

        public static string Downed(int playerId, int? downerId)
        {
            return Tag + " downed player=" + Number(playerId) + " by=" + Optional(downerId);
        }

        public static string Revived(int reviverId, int playerId)
        {
            return Tag + " revived player=" + Number(playerId) + " by=" + Number(reviverId);
        }

        public static string Executed(int executorId, int victimId)
        {
            return Tag + " executed victim=" + Number(victimId) + " by=" + Number(executorId);
        }

        public static string BledOut(int playerId, int? creditId)
        {
            return Tag + " bledout player=" + Number(playerId) + " credit=" + Optional(creditId);
        }

        public static string Preparing(int roundNumber)
        {
            return Tag + " round=" + Number(roundNumber) + " preparing";
        }

        public static string Live(int roundNumber)
        {
            return Tag + " round=" + Number(roundNumber) + " live";
        }

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
