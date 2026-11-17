using System;
using System.Globalization;

namespace Shooter.Core
{
    public readonly struct MatchResult : IEquatable<MatchResult>
    {
        public MatchResult(MatchOutcome outcome, int kills, int deaths, int ownScore)
            : this(outcome, kills, deaths, ownScore, false, null)
        {
        }

        public MatchResult(MatchOutcome outcome, int kills, int deaths, int ownScore, bool rated, int? opponentRating = null)
        {
            if (!Enum.IsDefined(typeof(MatchOutcome), outcome))
            {
                throw new ArgumentOutOfRangeException(nameof(outcome), outcome, "outcome must be a MatchOutcome value");
            }

            if (kills < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(kills), kills, "kills must not be negative");
            }

            if (deaths < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(deaths), deaths, "deaths must not be negative");
            }

            if (ownScore < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(ownScore), ownScore, "ownScore must not be negative");
            }

            if (opponentRating.HasValue && opponentRating.Value < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(opponentRating), opponentRating, "opponentRating must not be negative");
            }

            if (opponentRating.HasValue && !rated)
            {
                throw new ArgumentException("opponentRating is only for a rated match", nameof(opponentRating));
            }

            Outcome = outcome;
            Kills = kills;
            Deaths = deaths;
            OwnScore = ownScore;
            Rated = rated;
            OpponentRating = opponentRating;
        }

        public MatchOutcome Outcome { get; }

        public int Kills { get; }

        public int Deaths { get; }

        public int OwnScore { get; }

        public bool Rated { get; }

        public int? OpponentRating { get; }

        public bool Equals(MatchResult other)
        {
            return Outcome == other.Outcome
                && Kills == other.Kills
                && Deaths == other.Deaths
                && OwnScore == other.OwnScore
                && Rated == other.Rated
                && OpponentRating == other.OpponentRating;
        }

        public override bool Equals(object obj)
        {
            return obj is MatchResult other && Equals(other);
        }

        public static bool operator ==(MatchResult left, MatchResult right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(MatchResult left, MatchResult right)
        {
            return !left.Equals(right);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Outcome, Kills, Deaths, OwnScore, Rated, OpponentRating);
        }

        public override string ToString()
        {
            var text = Outcome + " " + Kills.ToString(CultureInfo.InvariantCulture) + "/" + Deaths.ToString(CultureInfo.InvariantCulture)
                + " score " + OwnScore.ToString(CultureInfo.InvariantCulture);
            if (!Rated)
            {
                return text;
            }

            return text + " rated vs " + (OpponentRating.HasValue ? OpponentRating.Value.ToString(CultureInfo.InvariantCulture) : "?");
        }
    }
}
