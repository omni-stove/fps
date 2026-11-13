using System;

namespace Fps.Core
{
    /// <summary>
    /// スコアボードの 1 行。参加者 1 人分の名前・チーム・キル数・デス数・接続状態。並べ方は <see cref="ScoreboardRows"/> が決める。
    /// </summary>
    public readonly struct ScoreboardEntry : IEquatable<ScoreboardEntry>
    {
        // default(ScoreboardEntry) ではコンストラクタを通らず null になるので、読むときに空文字へ寄せる。
        private readonly string _name;

        /// <summary>参加者の Id。同じ一覧の中で重ならない。キル数・デス数が同じ行はこの小さい順に並べる。</summary>
        public int PlayerId { get; }

        /// <summary>表示する名前。null にならない。</summary>
        public string Name => _name ?? string.Empty;

        public Team Team { get; }

        public int Kills { get; }

        public int Deaths { get; }

        public ScoreboardConnection Connection { get; }

        /// <summary>この画面のプレイヤー自身の行か。ビューが目立たせる。</summary>
        public bool IsLocal { get; }

        /// <exception cref="ArgumentOutOfRangeException">kills か deaths が負。</exception>
        public ScoreboardEntry(int playerId, string name, Team team, int kills, int deaths, ScoreboardConnection connection, bool isLocal)
        {
            if (kills < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(kills), kills, "kills must not be negative");
            }

            if (deaths < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(deaths), deaths, "deaths must not be negative");
            }

            PlayerId = playerId;
            _name = name ?? string.Empty;
            Team = team;
            Kills = kills;
            Deaths = deaths;
            Connection = connection;
            IsLocal = isLocal;
        }

        public bool Equals(ScoreboardEntry other)
        {
            return PlayerId == other.PlayerId
                && string.Equals(Name, other.Name, StringComparison.Ordinal)
                && Team == other.Team
                && Kills == other.Kills
                && Deaths == other.Deaths
                && Connection == other.Connection
                && IsLocal == other.IsLocal;
        }

        public override bool Equals(object obj)
        {
            return obj is ScoreboardEntry other && Equals(other);
        }

        public static bool operator ==(ScoreboardEntry left, ScoreboardEntry right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(ScoreboardEntry left, ScoreboardEntry right)
        {
            return !left.Equals(right);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(PlayerId, StringComparer.Ordinal.GetHashCode(Name), (int)Team, Kills, Deaths, (int)Connection, IsLocal);
        }

        public override string ToString()
        {
            return "id=" + PlayerId + " name=" + Name + " team=" + Team + " K=" + Kills + " D=" + Deaths
                + " connection=" + Connection + " local=" + IsLocal;
        }
    }
}
