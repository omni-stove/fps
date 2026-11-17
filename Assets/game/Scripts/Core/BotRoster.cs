using System;
using System.Globalization;

namespace Shooter.Core
{
    public static class BotRoster
    {
        // 人のプレイヤー Id はクライアント Id をそのまま使うので、大きな値から数えて接続の数がここに届くまで重ならないようにする
        // (届いたクライアントはサーバーが試合に入れない)。
        public const int FirstBotPlayerId = 1_000_000_000;

        public const string Tag = "[BOT]";

        public static bool IsBotId(int playerId)
        {
            return playerId >= FirstBotPlayerId;
        }

        // オンラインの成績の行はクライアント Id の欄に Bot の Id を入れるので、ulong でも引けるようにする。
        public static bool IsBotId(ulong playerId)
        {
            return playerId >= FirstBotPlayerId && playerId <= int.MaxValue;
        }

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

        // 人が 1 人もいない間は Bot だけで試合を回さないよう、Bot も置かない。
        public static int DesiredBots(int launchBots, int minPlayers, int humans)
        {
            var target = TargetPlayers(launchBots, minPlayers);
            if (humans < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(humans), humans, "humans must not be negative");
            }

            return humans == 0 ? 0 : Math.Max(0, target - humans);
        }

        // 人の入ったチーム (preferred) から先に抜くのは、人数の少ない側に入れた人の分だけ人数がそろったままにするため。
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

        public static int BotNumber(int playerId)
        {
            if (!IsBotId(playerId))
            {
                throw new ArgumentOutOfRangeException(nameof(playerId), playerId, "not a bot player id");
            }

            return playerId - FirstBotPlayerId + 1;
        }

        public static Shooter.Core.ScoreboardName ScoreboardName(int playerId)
        {
            return IsBotId(playerId)
                ? Shooter.Core.ScoreboardName.Bot(BotNumber(playerId))
                : Shooter.Core.ScoreboardName.Player(playerId);
        }

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
