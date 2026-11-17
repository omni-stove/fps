using System;

namespace Shooter.Core
{
    public readonly struct ScoreboardEntry : IEquatable<ScoreboardEntry>
    {
        public int PlayerId { get; }

        public ScoreboardName Name { get; }

        public Team Team { get; }

        public int Kills { get; }

        public int Deaths { get; }

        public ScoreboardConnection Connection { get; }

        public bool IsLocal { get; }

        public ScoreboardEntry(int playerId, ScoreboardName name, Team team, int kills, int deaths, ScoreboardConnection connection, bool isLocal)
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
