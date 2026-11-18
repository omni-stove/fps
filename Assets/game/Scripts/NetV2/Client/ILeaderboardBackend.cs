using System;
using System.Collections.Generic;

namespace Shooter.NetV2
{
    // Shooter.NetV2.Client は EOS の SDK を参照しないので、実装は Shooter.NetEos.EosLeaderboardQuery に置き、この口で ClientLeaderboard に渡す。
    public interface ILeaderboardBackend : IDisposable
    {
        void Tick();

        // done は (ログインした ProductUserId, 失敗の理由) を 1 回だけ受け取る。
        void Login(Action<string, string> done);

        // 上位 maxRows 行と、localUserId の行 (上位に入っていなくても順位が付いていれば) を返す。
        // 同時に 1 つしか呼ばないので、実装は前の問い合わせの結果を持ち続けなくてよい。
        void QueryRanks(string boardId, int maxRows, string localUserId, Action<LeaderboardPage, string> done);
    }

    public readonly struct LeaderboardEntry
    {
        public LeaderboardEntry(int rank, string userId, string displayName, long score)
        {
            Rank = rank;
            UserId = userId ?? string.Empty;
            DisplayName = displayName ?? string.Empty;
            Score = score;
        }

        public int Rank { get; }

        public string UserId { get; }

        public string DisplayName { get; }

        public long Score { get; }
    }

    public sealed class LeaderboardPage
    {
        public static readonly LeaderboardPage Empty = new LeaderboardPage(null, null);

        public LeaderboardPage(IReadOnlyList<LeaderboardEntry> top, LeaderboardEntry? local)
        {
            var copy = new LeaderboardEntry[top?.Count ?? 0];
            for (var i = 0; i < copy.Length; i++)
            {
                copy[i] = top[i];
            }

            Top = copy;
            Local = local;
        }

        public IReadOnlyList<LeaderboardEntry> Top { get; }

        public LeaderboardEntry? Local { get; }
    }
}
