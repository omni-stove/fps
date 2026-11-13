using System;
using System.Globalization;

namespace Fps.Core
{
    /// <summary>
    /// オンラインの試合で、人の足りない分を埋める Bot の数・抜けさせるチーム・プレイヤー Id・名前の決まり。
    /// </summary>
    /// <remarks>
    /// 埋める人数は起動引数 <c>-bots</c> (<see cref="NetLaunchOptions.Bots"/>) と <see cref="GameRules.MinPlayers"/> の大きいほうで、
    /// 人がその人数に届くまでを Bot で埋める。人が 1 人もいない間は Bot だけで試合を回さないよう、Bot も置かない。
    /// <para>
    /// Bot のプレイヤー Id は <see cref="FirstBotPlayerId"/> から数える。人のプレイヤー Id はクライアント Id をそのまま使うので、
    /// 接続の数がこの値に届くまで重ならない (届いたクライアントはサーバーが試合に入れない)。
    /// </para>
    /// </remarks>
    public static class BotRoster
    {
        /// <summary>最初の Bot のプレイヤー Id。これ以上の Id はどれも Bot。</summary>
        public const int FirstBotPlayerId = 1_000_000_000;

        /// <summary>Bot の名前の頭に付ける印。</summary>
        public const string Tag = "[BOT]";

        /// <summary>playerId が Bot の Id か。</summary>
        public static bool IsBotId(int playerId)
        {
            return playerId >= FirstBotPlayerId;
        }

        /// <summary>playerId が Bot の Id か。オンラインの成績の行はクライアント Id の欄に Bot の Id を入れるので、ulong でも引ける。</summary>
        public static bool IsBotId(ulong playerId)
        {
            return playerId >= FirstBotPlayerId && playerId <= int.MaxValue;
        }

        /// <summary>
        /// 埋める人数 (人と Bot を合わせた数)。launchBots と minPlayers の大きいほう。
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">どちらかが負。</exception>
        public static int TargetPlayers(int launchBots, int minPlayers)
        {
            if (launchBots < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(launchBots), launchBots, "launchBots must not be negative");
            }

            if (minPlayers < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(minPlayers), minPlayers, "minPlayers must not be negative");
            }

            return Math.Max(launchBots, minPlayers);
        }

        /// <summary>
        /// 人が humans 人いるときに置く Bot の数。<see cref="TargetPlayers"/> に足りない分で、人がいなければ 0。
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">どれかが負。</exception>
        public static int DesiredBots(int launchBots, int minPlayers, int humans)
        {
            var target = TargetPlayers(launchBots, minPlayers);
            if (humans < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(humans), humans, "humans must not be negative");
            }

            return humans == 0 ? 0 : Math.Max(0, target - humans);
        }

        /// <summary>
        /// Bot を 1 人抜くチーム。preferred (人が入ったチーム) に Bot がいればそこから、いなければ人数 (人と Bot の合計) の多いほうから
        /// (同じなら Bot の多いほう、それも同じなら Alpha)。どちらにも Bot がいなければ null。
        /// 人の入ったチームから抜くと、人数の少ない側に入れた人の分だけ人数がそろったままになる。
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">どれかの数が負か、Bot の数が人数より多い。</exception>
        public static Team? TeamToTrim(Team? preferred, int alphaPlayers, int bravoPlayers, int alphaBots, int bravoBots)
        {
            ValidateCounts(nameof(alphaPlayers), alphaPlayers, nameof(alphaBots), alphaBots);
            ValidateCounts(nameof(bravoPlayers), bravoPlayers, nameof(bravoBots), bravoBots);

            if (preferred.HasValue && BotsOf(preferred.Value, alphaBots, bravoBots) > 0)
            {
                return preferred.Value;
            }

            if (alphaBots == 0 && bravoBots == 0)
            {
                return null;
            }

            if (alphaBots == 0)
            {
                return Team.Bravo;
            }

            if (bravoBots == 0)
            {
                return Team.Alpha;
            }

            if (alphaPlayers != bravoPlayers)
            {
                return alphaPlayers > bravoPlayers ? Team.Alpha : Team.Bravo;
            }

            return bravoBots > alphaBots ? Team.Bravo : Team.Alpha;
        }

        /// <summary>Bot の Id の通し番号 (1 から)。</summary>
        /// <exception cref="ArgumentOutOfRangeException">playerId が Bot の Id でない。</exception>
        public static int BotNumber(int playerId)
        {
            if (!IsBotId(playerId))
            {
                throw new ArgumentOutOfRangeException(nameof(playerId), playerId, "not a bot player id");
            }

            return playerId - FirstBotPlayerId + 1;
        }

        /// <summary>スコアボードの名前。Bot なら <c>[BOT] Bot 1</c>、人なら <see cref="ScoreboardRows.PlayerName"/>。</summary>
        public static string ScoreboardName(int playerId)
        {
            return IsBotId(playerId)
                ? Tag + " Bot " + BotNumber(playerId).ToString(CultureInfo.InvariantCulture)
                : ScoreboardRows.PlayerName(playerId);
        }

        /// <summary>キルフィードの名前。Bot なら <c>[BOT] B1</c>、人なら <see cref="KillFeedFormat.Player"/>。</summary>
        public static string KillFeedName(int playerId)
        {
            return IsBotId(playerId)
                ? Tag + " B" + BotNumber(playerId).ToString(CultureInfo.InvariantCulture)
                : KillFeedFormat.Player(playerId);
        }

        private static int BotsOf(Team team, int alphaBots, int bravoBots)
        {
            return team == Team.Bravo ? bravoBots : alphaBots;
        }

        private static void ValidateCounts(string playersName, int players, string botsName, int bots)
        {
            if (players < 0)
            {
                throw new ArgumentOutOfRangeException(playersName, players, "player count must not be negative");
            }

            if (bots < 0 || bots > players)
            {
                throw new ArgumentOutOfRangeException(botsName, bots, "bot count must be between 0 and the player count");
            }
        }
    }
}
