using System;
using System.Collections.Generic;
using System.Globalization;

namespace Shooter.Core
{
    public enum LeaderboardStage
    {
        Loading,
        Ready,
        Unavailable,
    }

    public readonly struct LeaderboardRow : IEquatable<LeaderboardRow>
    {
        public LeaderboardRow(int rank, string playerName, long score, bool isLocal)
        {
            if (rank < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(rank), rank, "rank must be at least 1");
            }

            Rank = rank;
            PlayerName = playerName ?? string.Empty;
            Score = score;
            IsLocal = isLocal;
        }

        public int Rank { get; }

        public string PlayerName { get; }

        // EOS の統計は 64 ビットまで積み上がるので、int に縮めない。
        public long Score { get; }

        public bool IsLocal { get; }

        public bool Equals(LeaderboardRow other)
        {
            return Rank == other.Rank
                && string.Equals(PlayerName, other.PlayerName, StringComparison.Ordinal)
                && Score == other.Score
                && IsLocal == other.IsLocal;
        }

        public override bool Equals(object obj)
        {
            return obj is LeaderboardRow other && Equals(other);
        }

        public static bool operator ==(LeaderboardRow left, LeaderboardRow right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(LeaderboardRow left, LeaderboardRow right)
        {
            return !left.Equals(right);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Rank, PlayerName == null ? 0 : StringComparer.Ordinal.GetHashCode(PlayerName), Score, IsLocal);
        }

        public override string ToString()
        {
            return "#" + Rank.ToString(CultureInfo.InvariantCulture) + " " + PlayerName + " " + Score.ToString(CultureInfo.InvariantCulture);
        }
    }

    public readonly struct LeaderboardStatus : IEquatable<LeaderboardStatus>
    {
        // 渡された一覧を後から書き換えられても変わらないよう写しを持つ。default ではコンストラクタを通らず null になるので、読むときに空へ寄せる。
        private readonly LeaderboardRow[] _rows;

        public LeaderboardStatus(string boardId, LeaderboardStage stage, IReadOnlyList<LeaderboardRow> rows)
        {
            if (string.IsNullOrWhiteSpace(boardId))
            {
                throw new ArgumentException("boardId must not be empty", nameof(boardId));
            }

            if (!Enum.IsDefined(typeof(LeaderboardStage), stage))
            {
                throw new ArgumentOutOfRangeException(nameof(stage), stage, "stage is not a defined LeaderboardStage");
            }

            BoardId = boardId.Trim();
            Stage = stage;
            var copy = new LeaderboardRow[rows?.Count ?? 0];
            for (var i = 0; i < copy.Length; i++)
            {
                copy[i] = rows[i];
            }

            _rows = copy;
        }

        public string BoardId { get; }

        public LeaderboardStage Stage { get; }

        public IReadOnlyList<LeaderboardRow> Rows => _rows ?? Array.Empty<LeaderboardRow>();

        public bool Equals(LeaderboardStatus other)
        {
            if (!string.Equals(BoardId, other.BoardId, StringComparison.Ordinal) || Stage != other.Stage)
            {
                return false;
            }

            var mine = Rows;
            var theirs = other.Rows;
            if (mine.Count != theirs.Count)
            {
                return false;
            }

            for (var i = 0; i < mine.Count; i++)
            {
                if (!mine[i].Equals(theirs[i]))
                {
                    return false;
                }
            }

            return true;
        }

        public override bool Equals(object obj)
        {
            return obj is LeaderboardStatus other && Equals(other);
        }

        public static bool operator ==(LeaderboardStatus left, LeaderboardStatus right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(LeaderboardStatus left, LeaderboardStatus right)
        {
            return !left.Equals(right);
        }

        public override int GetHashCode()
        {
            var hash = HashCode.Combine(BoardId == null ? 0 : StringComparer.Ordinal.GetHashCode(BoardId), Stage);
            foreach (var row in Rows)
            {
                hash = HashCode.Combine(hash, row);
            }

            return hash;
        }

        public override string ToString()
        {
            return BoardId + " " + Stage + " rows " + Rows.Count.ToString(CultureInfo.InvariantCulture);
        }
    }
}
