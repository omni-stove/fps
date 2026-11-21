using System;
using System.Globalization;
using Shooter.Core;

namespace Shooter.Server
{
    internal static class ExecutionRulesLog
    {
        public const string Tag = "[Execution]";

        public static string Started(int winsToWin, float preparationSeconds, float roundTimeLimitSeconds, ExecutionSpec spec)
        {
            ArgumentNullException.ThrowIfNull(spec);
            return Tag + " net winsToWin=" + Text(winsToWin)
                + " preparation=" + Text(preparationSeconds)
                + "s roundTimeLimit=" + Text(roundTimeLimitSeconds)
                + "s executeSeconds=" + Text(spec.ExecuteSeconds)
                + " executeRange=" + Text(spec.ExecuteRange);
        }

        public static string DownedForced(Downed configured)
        {
            return Tag + " rules downed=" + configured + " is played as Revive; Execution needs downed players";
        }

        public static string MeleeOff(Melee configured)
        {
            return Tag + " rules melee=" + configured + "; no one can execute, so downed players can be shot down instead";
        }

        public static string Restart()
        {
            return Tag + " restart";
        }

        public static string State(EliminationObjective rounds, Func<Team, int> standing, Func<Team, int> downed)
        {
            ArgumentNullException.ThrowIfNull(rounds);
            ArgumentNullException.ThrowIfNull(standing);
            ArgumentNullException.ThrowIfNull(downed);
            var text = Tag + " state round=" + Text(rounds.Rounds.RoundNumber)
                + " phase=" + rounds.Phase
                + " wins=" + Text(rounds.GetRoundWins(Team.Alpha)) + "-" + Text(rounds.GetRoundWins(Team.Bravo))
                + " standing=" + Text(standing(Team.Alpha)) + "-" + Text(standing(Team.Bravo))
                + " downed=" + Text(downed(Team.Alpha)) + "-" + Text(downed(Team.Bravo));
            var winner = rounds.DecideWinner();
            return text + " winner=" + (winner.HasValue ? winner.Value.ToString() : MatchLog.Draw);
        }

        private static string Text(int value) => value.ToString(CultureInfo.InvariantCulture);

        private static string Text(float value) => value.ToString(CultureInfo.InvariantCulture);
    }
}
