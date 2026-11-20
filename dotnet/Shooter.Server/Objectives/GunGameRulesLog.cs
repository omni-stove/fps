using System.Globalization;
using Shooter.Core;

namespace Shooter.Server
{
    internal static class GunGameRulesLog
    {
        public static string Started(GunGameSpec spec, FriendlyFire friendlyFire)
        {
            return GunGameLog.LogPrefix + " net tiers=" + Number(spec.TierWeaponIds.Count)
                + " timeLimit=" + Seconds(spec.TimeLimitSeconds) + "s"
                + " respawn=" + Seconds(spec.RespawnSeconds) + "s"
                + " melee=" + Number(spec.MeleeWeaponIds.Count)
                + " demotesOnMelee=" + (spec.DemotesOnMelee ? "true" : "false")
                + " friendlyFire=" + friendlyFire;
        }

        public static string Restart()
        {
            return GunGameLog.LogPrefix + " restart";
        }

        public static string Finished(int playerId, Team? team)
        {
            return GunGameLog.LogPrefix + " ended by=finish winner=" + Number(playerId) + " team=" + TeamName(team);
        }

        public static string TimeUp(int? leaderId, Team? team)
        {
            return GunGameLog.LogPrefix + " ended by=time winner=" + (leaderId.HasValue ? Number(leaderId.Value) : ModeLog.Draw)
                + " team=" + TeamName(team);
        }

        public static string Rearm(int playerId, string weaponId)
        {
            return GunGameLog.LogPrefix + " rearm player=" + Number(playerId) + " weapon=" + (weaponId ?? "");
        }

        public static string MissingTierWeapon(int tier, string weaponId, string rulesName)
        {
            return "cannot start the " + MatchObjective.GunGame + " match: tier " + Number(tier) + " weapon " + (weaponId ?? "")
                + " is not in the loadout or the catalog of rules " + (rulesName ?? "");
        }

        private static string TeamName(Team? team)
        {
            return team.HasValue ? team.Value.ToString() : "-";
        }

        private static string Seconds(float value)
        {
            return value.ToString("0.##", CultureInfo.InvariantCulture);
        }

        private static string Number(int value)
        {
            return value.ToString(CultureInfo.InvariantCulture);
        }
    }
}
