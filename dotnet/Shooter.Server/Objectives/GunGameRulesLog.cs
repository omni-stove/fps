using System.Globalization;
using Shooter.Core;

namespace Shooter.Server
{
    internal static class GunGameRulesLog
    {
        public static string Started(GunGameSpec spec, FriendlyFire friendlyFire)
        {
            return GunGameText.LogPrefix + " net tiers=" + Number(spec.TierWeaponIds.Count)
                + " timeLimit=" + Seconds(spec.TimeLimitSeconds) + "s"
                + " respawn=" + Seconds(spec.RespawnSeconds) + "s"
                + " melee=" + Number(spec.MeleeWeaponIds.Count)
                + " demotesOnMelee=" + (spec.DemotesOnMelee ? "true" : "false")
                + " friendlyFire=" + friendlyFire;
        }

        public static string Restart()
        {
            return GunGameText.LogPrefix + " restart";
        }

        public static string Finished(int playerId, Team? team)
        {
            return GunGameText.LogPrefix + " ended by=finish winner=" + Number(playerId) + " team=" + TeamName(team);
        }

        public static string TimeUp(int? leaderId, Team? team)
        {
            return GunGameText.LogPrefix + " ended by=time winner=" + (leaderId.HasValue ? Number(leaderId.Value) : ModeLog.Draw)
                + " team=" + TeamName(team);
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
