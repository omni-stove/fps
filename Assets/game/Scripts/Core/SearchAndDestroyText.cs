using System.Globalization;

namespace Shooter.Core
{
    /// <summary>
    /// 爆破 (<see cref="SearchAndDestroy"/>) の <c>[SearchAndDestroy]</c> 付きのログの文言と、設置地点の名前。
    /// 設置地点の名前は <see cref="DominationHudFormat.PointName"/> (0 = A, 1 = B) と同じ。
    /// </summary>
    public static class SearchAndDestroyText
    {
        public const string Tag = "[SearchAndDestroy]";

        /// <example><c>SiteName(1)</c> は <c>B</c></example>
        public static string SiteName(int site) => DominationHudFormat.PointName(site);

        /// <example><c>[SearchAndDestroy] round=3 attacker=1 defender=0</c></example>
        public static string RoundLog(int round, Side attacker, Side defender)
        {
            return Tag + " round=" + Number(round) + " attacker=" + Number(attacker.Id) + " defender=" + Number(defender.Id);
        }

        /// <example><c>[SearchAndDestroy] live round=3</c></example>
        public static string LiveLog(int round)
        {
            return Tag + " live round=" + Number(round);
        }

        /// <example><c>[SearchAndDestroy] round_end round=3 winner=1 reason=Detonated wins=0:1,1:2</c>、引き分けなら <c>winner=draw</c></example>
        public static string RoundEndLog(int round, Side? winner, RoundEndReason reason, int alphaWins, int bravoWins)
        {
            return Tag + " round_end round=" + Number(round)
                + " winner=" + (winner.HasValue ? Number(winner.Value.Id) : ModeLog.Draw)
                + " reason=" + reason
                + " wins=" + Number(Side.Alpha.Id) + ":" + Number(alphaWins) + "," + Number(Side.Bravo.Id) + ":" + Number(bravoWins);
        }

        /// <example><c>[SearchAndDestroy] swap attacker=1 defender=0</c></example>
        public static string SwapLog(Side attacker, Side defender)
        {
            return Tag + " swap attacker=" + Number(attacker.Id) + " defender=" + Number(defender.Id);
        }

        /// <example><c>[SearchAndDestroy] pickup player=2</c></example>
        public static string PickUpLog(int playerId) => Tag + " pickup player=" + Number(playerId);

        /// <example><c>[SearchAndDestroy] drop player=2</c></example>
        public static string DropLog(int playerId) => Tag + " drop player=" + Number(playerId);

        /// <example><c>[SearchAndDestroy] planted player=0 site=A</c></example>
        public static string PlantedLog(int playerId, int site) => Tag + " planted player=" + Number(playerId) + " site=" + SiteName(site);

        /// <example><c>[SearchAndDestroy] defused player=4</c></example>
        public static string DefusedLog(int playerId) => Tag + " defused player=" + Number(playerId);

        /// <example><c>[SearchAndDestroy] detonated site=B</c></example>
        public static string DetonatedLog(int site) => Tag + " detonated site=" + SiteName(site);

        /// <example><c>[SearchAndDestroy] ended winner=0</c></example>
        public static string EndedLog(Side winner) => Tag + " ended winner=" + Number(winner.Id);

        private static string Number(int value) => value.ToString(CultureInfo.InvariantCulture);
    }
}
