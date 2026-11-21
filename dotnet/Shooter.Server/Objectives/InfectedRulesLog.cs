using System;
using System.Collections.Generic;
using System.Globalization;
using Shooter.Core;

namespace Shooter.Server
{
    internal static class InfectedRulesLog
    {
        public const string Tag = "[Infected]";

        public const string SurvivorName = "survivor";

        public const string InfectedName = "infected";

        public const string KeepsWeapons = "loadout";

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

        public static string Body(InfectedSpec spec)
        {
            ArgumentNullException.ThrowIfNull(spec);
            return Tag + " body maxHealth=" + Text(spec.InfectedMaxHealth)
                + " speedMultiplier=" + Text(spec.InfectedSpeedMultiplier)
                + " weapon=" + WeaponText(spec);
        }

        public static string Turned(int playerId, InfectedSpec spec)
        {
            ArgumentNullException.ThrowIfNull(spec);
            return Tag + " infected body player=" + Text(playerId)
                + " maxHealth=" + Text(spec.InfectedMaxHealth)
                + " speedMultiplier=" + Text(spec.InfectedSpeedMultiplier);
        }

        public static string Restored(IReadOnlyList<int> playerIds)
        {
            ArgumentNullException.ThrowIfNull(playerIds);
            var ids = new string[playerIds.Count];
            for (var i = 0; i < ids.Length; i++)
            {
                ids[i] = Text(playerIds[i]);
            }

            return Tag + " survivor bodies restored players=" + string.Join(",", ids);
        }

        public static string Weapon(string weaponId, string rulesName)
        {
            return Tag + " infected weapon=" + (weaponId ?? string.Empty) + " rules=" + (rulesName ?? string.Empty);
        }

        public static string Rearm(int playerId, string weaponId)
        {
            return Tag + " rearm player=" + Text(playerId) + " weapon=" + (weaponId ?? string.Empty);
        }

        public static string MissingWeapon(string weaponId, string rulesName)
        {
            return "cannot start the " + MatchObjective.Infected + " match: the infected weapon " + (weaponId ?? string.Empty)
                + " is not in the loadout or the catalog of rules " + (rulesName ?? string.Empty);
        }

        // 空なら感染しても持っている武器のままなので、武器が無いのと見分けて書く
        private static string WeaponText(InfectedSpec spec)
        {
            return spec.HasInfectedWeapon ? spec.InfectedWeaponId : KeepsWeapons;
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
