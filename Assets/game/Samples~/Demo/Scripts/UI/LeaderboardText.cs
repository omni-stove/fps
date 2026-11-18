using System;
using System.Globalization;
using Shooter.Core;

namespace Shooter.Runtime
{
    public static class LeaderboardText
    {
        public const string TitleKey = "leaderboard.title";
        public const string LoadingKey = "leaderboard.loading";
        public const string UnavailableKey = "leaderboard.unavailable";
        public const string EmptyKey = "leaderboard.empty";
        public const string NoBoardsKey = "leaderboard.noBoards";
        public const string RankKey = "leaderboard.rank";
        public const string UnknownPlayerKey = "leaderboard.unknownPlayer";
        public const string YouKey = "leaderboard.you";
        public const string RefreshKey = "leaderboard.refresh";
        public const string BackKey = "leaderboard.back";

        public static LocalizedText Title => new LocalizedText(TitleKey);

        public static LocalizedText Refresh => new LocalizedText(RefreshKey);

        public static LocalizedText Back => new LocalizedText(BackKey);

        // 名前のキーを書かなかったボードは、ポータルと同じ Id をそのまま出す
        public static LocalizedText BoardName(LeaderboardDefinition board)
        {
            if (board == null)
            {
                throw new ArgumentNullException(nameof(board));
            }

            return board.DisplayNameKey.Length == 0 ? LocalizedText.Literal(board.Id) : new LocalizedText(board.DisplayNameKey);
        }

        // 一覧の代わりに出す一文。行を出すときは空を返す。読み直しの間 (Loading で行がある) は前の行を出したままにする。
        public static LocalizedText Notice(LeaderboardStatus? status, bool hasControl, bool hasBoards)
        {
            if (!hasBoards)
            {
                return new LocalizedText(NoBoardsKey);
            }

            if (!hasControl)
            {
                return new LocalizedText(UnavailableKey);
            }

            if (!status.HasValue)
            {
                return new LocalizedText(LoadingKey);
            }

            var value = status.Value;
            switch (value.Stage)
            {
                case LeaderboardStage.Loading:
                    return value.Rows.Count == 0 ? new LocalizedText(LoadingKey) : default;
                case LeaderboardStage.Unavailable:
                    return new LocalizedText(UnavailableKey);
                default:
                    return value.Rows.Count == 0 ? new LocalizedText(EmptyKey) : default;
            }
        }

        public static LocalizedText Rank(LeaderboardRow row)
        {
            return new LocalizedText(RankKey, row.Rank);
        }

        // 名前はプレイヤーが付けたものなので、表を引かずにそのまま出す
        public static LocalizedText PlayerName(LeaderboardRow row)
        {
            var name = row.PlayerName.Length == 0 ? new LocalizedText(UnknownPlayerKey) : LocalizedText.Literal(row.PlayerName);
            return row.IsLocal ? new LocalizedText(YouKey, name) : name;
        }

        public static LocalizedText Score(LeaderboardRow row)
        {
            return LocalizedText.Literal(row.Score.ToString("N0", CultureInfo.InvariantCulture));
        }
    }
}
