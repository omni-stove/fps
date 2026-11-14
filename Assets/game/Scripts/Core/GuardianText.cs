using System;
using System.Collections.Generic;
using System.Globalization;

namespace Fps.Core
{
    /// <summary>
    /// ガーディアンの HUD の行 (表のキー <c>mode.guardian.*</c> の <see cref="LocalizedText"/>) と <c>[Mode]</c> のログ
    /// (<see cref="GuardianRespawn"/> のリーダーの生死)。
    /// </summary>
    public static class GuardianText
    {
        public static LocalizedText Alive => new LocalizedText("mode.guardian.alive");
        public static LocalizedText Fallen => new LocalizedText("mode.guardian.fallen");

        /// <summary>自分がリーダーのときに出す行。</summary>
        public static LocalizedText YouAreLeader => new LocalizedText("mode.guardian.youAreLeader");

        /// <summary>自分の陣営のリーダーが倒れていて、自分も倒れているときに出す行。</summary>
        public static LocalizedText NoRespawnLeaderDown => new LocalizedText("mode.guardian.noRespawnLeaderDown");

        /// <example><c>Leader(Side.Alpha, false)</c> は <c>ALPHA リーダー 生存</c></example>
        public static LocalizedText Leader(Side side, bool fallen)
        {
            return new LocalizedText("mode.guardian.leader", ModeHudText.SideName(side), fallen ? Fallen : Alive);
        }

        /// <summary>
        /// リーダーのいる陣営ごとの生死の行。ownSide があればその行を先頭に、残りは陣営の ID の昇順に並べる。
        /// </summary>
        public static IReadOnlyList<LocalizedText> Leaders(GuardianRespawn leaders, Side? ownSide)
        {
            if (leaders == null)
            {
                throw new ArgumentNullException(nameof(leaders));
            }

            var lines = new List<LocalizedText>();
            foreach (var side in leaders.LeaderSides)
            {
                var line = Leader(side, leaders.HasLeaderFallen(side));
                if (side == ownSide)
                {
                    lines.Insert(0, line);
                }
                else
                {
                    lines.Add(line);
                }
            }

            return lines;
        }

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
