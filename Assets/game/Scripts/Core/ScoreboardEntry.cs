using System;

namespace Shooter.Core
{
    /// <summary>
    /// スコアボードの 1 行。参加者 1 人分の Id・名前・チーム・キル数・デス数・アシスト数・接続状態で、文は持たない。並べ方は <see cref="ScoreboardRows"/> が決める。
    /// </summary>
    public readonly struct ScoreboardEntry : IEquatable<ScoreboardEntry>
    {
        /// <summary>参加者の Id。同じ一覧の中で重ならない。キル数・アシスト数・デス数が同じ行はこの小さい順に並べる。</summary>
        public int PlayerId { get; }

        /// <summary>
        /// 誰の行か。文にするのはスコアボードの UI。言葉の入らない名前 (敵の名前) は <see cref="ScoreboardName.Literal"/> で渡す。
        /// <c>default(ScoreboardEntry)</c> では空の名前。
        /// </summary>
        public ScoreboardName Name { get; }

        public Team Team { get; }

        public int Kills { get; }

        public int Deaths { get; }

        /// <summary>別のチームの人を倒すのを手伝った (アシストした) 回数。</summary>
        public int Assists { get; }

        public ScoreboardConnection Connection { get; }

        /// <summary>この画面のプレイヤー自身の行か。ビューが目立たせる。</summary>
        public bool IsLocal { get; }

        /// <exception cref="ArgumentOutOfRangeException">kills・deaths・assists が負。</exception>
        public ScoreboardEntry(
            int playerId, ScoreboardName name, Team team, int kills, int deaths, int assists, ScoreboardConnection connection, bool isLocal)
        {
            if (kills < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(kills), kills, "kills must not be negative");
            }

            if (deaths < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(deaths), deaths, "deaths must not be negative");
            }

            if (assists < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(assists), assists, "assists must not be negative");
            }

            PlayerId = playerId;
            Name = name;
            Team = team;
            Kills = kills;
            Deaths = deaths;
            Assists = assists;
            Connection = connection;
            IsLocal = isLocal;
        }

        public bool Equals(ScoreboardEntry other)
        {
            return PlayerId == other.PlayerId
                && Name == other.Name
                && Team == other.Team
                && Kills == other.Kills
                && Deaths == other.Deaths
                && Assists == other.Assists
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
            return HashCode.Combine(PlayerId, Name, (int)Team, Kills, Deaths, Assists, (int)Connection, IsLocal);
        }

        public override string ToString()
        {
            return "id=" + PlayerId + " name=" + Name + " team=" + Team + " K=" + Kills + " D=" + Deaths + " A=" + Assists
                + " connection=" + Connection + " local=" + IsLocal;
        }
    }
}
