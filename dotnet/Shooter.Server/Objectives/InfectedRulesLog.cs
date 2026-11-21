using System;
using System.Globalization;
using Shooter.Core;

namespace Shooter.Server
{
    internal static class InfectedRulesLog
    {
        public const string Tag = "[Infected]";

        public const string SurvivorName = "survivor";

        public const string InfectedName = "infected";

        public static string Started(InfectedSpec spec)
        {
            ArgumentNullException.ThrowIfNull(spec);
            return Tag + " net initialInfected=" + Text(spec.InitialInfected)
                + " startDelay=" + Text(spec.StartDelaySeconds)
                + " roundSeconds=" + Text(spec.RoundSeconds)
                + " respawn=" + Text(spec.InfectedRespawnSeconds)
                + " scorePerInfection=" + Text(spec.ScorePerInfection)
                + " scorePerSurvivedRound=" + Text(spec.ScorePerSurvivedRound);
        }

        // 体の値と武器はまだサーバーが替えないので、ファイルに書いた値が効いていないことを起動のときに見せる
        public static string NotApplied(InfectedSpec spec)
        {
            ArgumentNullException.ThrowIfNull(spec);
            return Tag + " not applied online yet infectedMaxHealth=" + Text(spec.InfectedMaxHealth)
                + " infectedSpeedMultiplier=" + Text(spec.InfectedSpeedMultiplier)
                + " infectedWeaponId=" + (spec.HasInfectedWeapon ? spec.InfectedWeaponId : InfectedLog.Nobody);
        }

        public static string Joined(int playerId, Side side)
        {
            return Tag + " joined player=" + Text(playerId) + " side=" + SideName(side);
        }

        public static string NoOneInfected(int playerCount)
        {
            return Tag + " outbreak with " + Text(playerCount) + " player(s); no one is infected, so the survivors win when time runs out";
        }

        public static string Score(int survivors, int infected)
        {
            return Tag + " score survivors=" + Text(survivors) + " infected=" + Text(infected);
        }

        public static string Restart()
        {
            return Tag + " restart";
        }

        public static string State(Infected infected)
        {
            ArgumentNullException.ThrowIfNull(infected);
            var session = infected.Session;
            var text = Tag + " state=" + session.State
                + " survivors=" + Text(infected.SurvivorCount)
                + " infected=" + Text(infected.InfectedCount)
                + " scoreSurvivors=" + Text(HudFormat.DisplayedScore(session.GetScore(Infected.SurvivorSide)))
                + " scoreInfected=" + Text(HudFormat.DisplayedScore(session.GetScore(Infected.InfectedSide)))
                + " remaining=" + Math.Max(0f, session.RemainingSeconds).ToString("F1", CultureInfo.InvariantCulture);
            if (session.State != GameModeState.Ended)
            {
                return text;
            }

            return text + " winner=" + (session.Winner.HasValue ? SideName(session.Winner.Value) : MatchLog.Draw);
        }

        public static string SideName(Side side)
        {
            return side == Infected.InfectedSide ? InfectedName : SurvivorName;
        }

        private static string Text(int value) => value.ToString(CultureInfo.InvariantCulture);

        private static string Text(float value) => value.ToString(CultureInfo.InvariantCulture);
    }
}
