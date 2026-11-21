using System.Globalization;
using System.Numerics;
using Shooter.Core;

namespace Shooter.Server
{
    internal static class ServerBotLog
    {
        public static string Goal(int playerId, Vector3 goal)
        {
            return ServerBots.LogTag + " goal player=" + Text(playerId) + " x=" + F2(goal.X) + " y=" + F2(goal.Y) + " z=" + F2(goal.Z);
        }

        public static string GoalCleared(int playerId)
        {
            return ServerBots.LogTag + " goal player=" + Text(playerId) + " cleared";
        }

        public static string CoverFound(int playerId, int points)
        {
            return ServerBots.LogTag + " cover player=" + Text(playerId) + " points=" + Text(points);
        }

        public static string GrenadeThrown(int playerId, EnemyState state, Vector3 target)
        {
            return ServerBots.LogTag + " grenade player=" + Text(playerId) + " state=" + state
                + " x=" + F2(target.X) + " y=" + F2(target.Y) + " z=" + F2(target.Z);
        }

        public static string Phase(int playerId, EnemyArchetype archetype, string previous, string next)
        {
            return ServerBots.LogTag + " " + Lower(archetype) + " player=" + Text(playerId) + " phase=" + next + " prev=" + previous;
        }

        public static string ChargeHit(int playerId, int victimId, int damage, float travelled, DamageOutcome outcome)
        {
            return ServerBots.LogTag + " charger player=" + Text(playerId) + " hit victim=" + Text(victimId) + " dmg=" + Text(damage)
                + " travelled=" + F2(travelled) + " outcome=" + outcome;
        }

        public static string ChargeEnded(int playerId, ChargeEnd end, float travelled)
        {
            return ServerBots.LogTag + " charger player=" + Text(playerId) + " missed cause=" + end.ToString().ToLowerInvariant() + " travelled=" + F2(travelled);
        }

        public static string ChargeCancelled(int playerId, string reason)
        {
            return ServerBots.LogTag + " charger player=" + Text(playerId) + " cancelled reason=" + reason;
        }

        public static string Exploded(int playerId, string cause, float radius, int damaged, int killed)
        {
            return ServerBots.LogTag + " bomber player=" + Text(playerId) + " exploded cause=" + cause + " radius=" + F2(radius)
                + " damaged=" + Text(damaged) + " killed=" + Text(killed);
        }

        public static string ShieldLowered(int playerId, DamageKind kind, float seconds)
        {
            return ServerBots.LogTag + " shield player=" + Text(playerId) + " lowered cause=" + kind.ToString().ToLowerInvariant() + " seconds=" + F2(seconds);
        }

        public static string ShieldRaised(int playerId)
        {
            return ServerBots.LogTag + " shield player=" + Text(playerId) + " raised";
        }

        public static string ShieldBash(int playerId)
        {
            return ServerBots.LogTag + " shield player=" + Text(playerId) + " bash";
        }

        public static string SniperFired(int playerId, float damageMultiplier)
        {
            return ServerBots.LogTag + " sniper player=" + Text(playerId) + " fired multiplier=" + F2(damageMultiplier);
        }

        private static string Lower(EnemyArchetype archetype) => archetype.ToString().ToLowerInvariant();

        private static string Text(int value) => value.ToString(CultureInfo.InvariantCulture);

        private static string F2(float value) => value.ToString("F2", CultureInfo.InvariantCulture);
    }
}
