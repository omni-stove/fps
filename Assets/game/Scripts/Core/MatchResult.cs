using System;
using System.Globalization;

namespace Shooter.Core
{
    /// <summary>終わった試合の、この画面のプレイヤーの結果。</summary>
    public readonly struct MatchResult : IEquatable<MatchResult>
    {
        /// <param name="outcome">勝敗。</param>
        /// <param name="kills">倒した数。</param>
        /// <param name="deaths">倒れた数。</param>
        /// <param name="ownScore">自分の得点。</param>
        /// <exception cref="ArgumentOutOfRangeException">outcome が <see cref="MatchOutcome"/> に無いか、kills・deaths・ownScore が負。</exception>
        public MatchResult(MatchOutcome outcome, int kills, int deaths, int ownScore)
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

            Outcome = outcome;
            Kills = kills;
            Deaths = deaths;
            OwnScore = ownScore;
        }

        public MatchOutcome Outcome { get; }

        public int Kills { get; }

        public int Deaths { get; }

        public int OwnScore { get; }

        public bool Equals(MatchResult other)
        {
            return Outcome == other.Outcome && Kills == other.Kills && Deaths == other.Deaths && OwnScore == other.OwnScore;
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
            return HashCode.Combine(Outcome, Kills, Deaths, OwnScore);
        }

        public override string ToString()
        {
            return Outcome + " " + Kills.ToString(CultureInfo.InvariantCulture) + "/" + Deaths.ToString(CultureInfo.InvariantCulture)
                + " score " + OwnScore.ToString(CultureInfo.InvariantCulture);
        }
    }
}
