using System.Globalization;

namespace Shooter.Core
{
    public static class SearchAndDestroyLog
    {
        public const string Tag = "[SearchAndDestroy]";

        public static string RoundLog(int round, Side attacker, Side defender)
        {
            return Tag + " round=" + Number(round) + " attacker=" + Number(attacker.Id) + " defender=" + Number(defender.Id);
        }

        public static string LiveLog(int round)
        {
            return Tag + " live round=" + Number(round);
        }

        public static string RoundEndLog(int round, Side? winner, RoundEndReason reason, int alphaWins, int bravoWins)
        {
            return Tag + " round_end round=" + Number(round)
                + " winner=" + (winner.HasValue ? Number(winner.Value.Id) : ModeLog.Draw)
                + " reason=" + reason
                + " wins=" + Number(Side.Alpha.Id) + ":" + Number(alphaWins) + "," + Number(Side.Bravo.Id) + ":" + Number(bravoWins);
        }

        public static string SwapLog(Side attacker, Side defender)
        {
            return Tag + " swap attacker=" + Number(attacker.Id) + " defender=" + Number(defender.Id);
        }

        public static string PickUpLog(int playerId) => Tag + " pickup player=" + Number(playerId);

        public static string DropLog(int playerId) => Tag + " drop player=" + Number(playerId);

        public static string PlantedLog(int playerId, int site) => Tag + " planted player=" + Number(playerId) + " site=" + BombSiteName.Of(site);

        public static string DefusedLog(int playerId) => Tag + " defused player=" + Number(playerId);

        public static string DetonatedLog(int site) => Tag + " detonated site=" + BombSiteName.Of(site);

        public static string EndedLog(Side winner) => Tag + " ended winner=" + Number(winner.Id);

        public static string SettingsLog(SearchAndDestroySpec spec)
        {
            if (spec == null)
            {
                throw new System.ArgumentNullException(nameof(spec));
            }

            return Tag + " net sites=" + Number(spec.SiteCount)
                + " winsToWin=" + Number(spec.WinsToWin)
                + " round=" + Seconds(spec.RoundSeconds) + "s"
                + " plant=" + Seconds(spec.PlantSeconds) + "s"
                + " defuse=" + Seconds(spec.DefuseSeconds) + "s"
                + " fuse=" + Seconds(spec.FuseSeconds) + "s"
                + " preparation=" + Seconds(spec.PreparationSeconds) + "s"
                + " swapEvery=" + Number(spec.SwapEveryRounds);
        }

        private static string Number(int value) => value.ToString(CultureInfo.InvariantCulture);

        private static string Seconds(float value) => value.ToString(CultureInfo.InvariantCulture);
    }
}
