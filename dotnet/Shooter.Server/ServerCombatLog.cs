using System.Globalization;
using Shooter.Core;

namespace Shooter.Server
{
    internal static class ServerCombatLog
    {
        public static string Dealt(int? attackerId, int victimId, string weaponId, int amount, HitZone zone, DamageOutcome outcome)
        {
            return ServerCombat.LogTag + " dealt attacker=" + (attackerId.HasValue ? Text(attackerId.Value) : "none")
                + " victim=" + Text(victimId) + " weapon=" + (string.IsNullOrEmpty(weaponId) ? "none" : weaponId)
                + " zone=" + zone + " dmg=" + Text(amount) + " outcome=" + outcome;
        }

        public static string ReloadInterrupted(int playerId, ReloadInterrupt by)
        {
            return ServerCombat.LogTag + " reload-cancelled player=" + Text(playerId) + " by=" + by;
        }

        public static string ShieldBlocked(int holderId, int shooterId, string weaponId, int damage)
        {
            return ServerCombat.LogTag + " riot-shield blocked id=" + Text(holderId) + " attacker=" + Text(shooterId)
                + " weapon=" + (string.IsNullOrEmpty(weaponId) ? "none" : weaponId) + " dmg=" + Text(damage);
        }

        private static string Text(int value) => value.ToString(CultureInfo.InvariantCulture);
    }
}
