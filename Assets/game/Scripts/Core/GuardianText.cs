using System.Globalization;

namespace Fps.Core
{
    /// <summary>
    /// ガーディアンの <c>[Mode]</c> のログ (<see cref="GuardianRespawn"/> のリーダーの生死)。
    /// </summary>
    public static class GuardianText
    {
        /// <example><c>[Mode] leader side=1 player=4</c></example>
        public static string LeaderChosenLog(Side side, int playerId)
        {
            return ModeLog.Tag + " leader side=" + Number(side.Id) + " player=" + Number(playerId);
        }

        /// <example><c>[Mode] leader-down side=1 player=4 respawn=off</c></example>
        public static string LeaderFellLog(Side side, int playerId)
        {
            return ModeLog.Tag + " leader-down side=" + Number(side.Id) + " player=" + Number(playerId) + " respawn=off";
        }

        private static string Number(int value)
        {
            return value.ToString(CultureInfo.InvariantCulture);
        }
    }
}
