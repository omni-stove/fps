using System;

namespace Fps.Core
{
    /// <summary>
    /// スコアボードの 1 行。参加者 1 人分の名前・チーム・キル数・デス数・接続状態。並べ方は <see cref="ScoreboardRows"/> が決める。
    /// </summary>
    public readonly struct ScoreboardEntry : IEquatable<ScoreboardEntry>
    {
        /// <summary>参加者の Id。同じ一覧の中で重ならない。キル数・デス数が同じ行はこの小さい順に並べる。</summary>
        public int PlayerId { get; }

        /// <summary>
        /// 表示する名前。スコアボードが今の言語で文にする。言葉の入らない名前 (Bot や敵の名前) は <see cref="LocalizedText.Literal"/> で渡す。
        /// <c>default(ScoreboardEntry)</c> では空の文。
        /// </summary>
        public LocalizedText Name { get; }

        public Team Team { get; }

        public int Kills { get; }

        public int Deaths { get; }

        public ScoreboardConnection Connection { get; }

        /// <summary>この画面のプレイヤー自身の行か。ビューが目立たせる。</summary>
        public bool IsLocal { get; }

        /// <exception cref="ArgumentOutOfRangeException">kills か deaths が負。</exception>
        public ScoreboardEntry(int playerId, LocalizedText name, Team team, int kills, int deaths, ScoreboardConnection connection, bool isLocal)
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
            Name = name;
            Team = team;
            Kills = kills;
            Deaths = deaths;
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
            return HashCode.Combine(PlayerId, Name, (int)Team, Kills, Deaths, (int)Connection, IsLocal);
        }

        public override string ToString()
        {
            return "id=" + PlayerId + " name=" + Name + " team=" + Team + " K=" + Kills + " D=" + Deaths
                + " connection=" + Connection + " local=" + IsLocal;
        }
    }
}
