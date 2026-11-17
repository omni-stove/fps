using System;
using System.Globalization;

namespace Shooter.Core
{
    public static class FreeForAllMode
    {
        public const float DefaultTimeLimitSeconds = 300f;
        public const int DefaultScoreLimit = 15;
        public const float DefaultRespawnSeconds = 3f;
        public const int DefaultPointsPerKill = 1;

        public static ModeSession Create(float timeLimitSeconds, int scoreLimit, float respawnSeconds, int pointsPerKill = DefaultPointsPerKill)
        {
            if (scoreLimit < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(scoreLimit), scoreLimit, "scoreLimit must be at least 1");
            }

            // KillScore は double を取れるが、順位と HUD は得点を整数で数える (DisplayScore) ので整数に絞る。
            if (pointsPerKill < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(pointsPerKill), pointsPerKill, "pointsPerKill must be at least 1");
            }

            return new ModeSession(
                new FreeForAll(),
                timeLimitSeconds,
                new IScoreRule[] { new KillScore(pointsPerKill) },
                new IndividualRespawn(respawnSeconds),
                new IWinCondition[] { new ScoreLimitWin(scoreLimit), new TimeLimitWin() });
        }

        // HUD に出す得点と順位がずれないよう、得点は HUD と同じく HudFormat.DisplayedScore で数える。
        public static FreeForAllStanding Standing(ModeSession session, Side side)
        {
            if (session == null)
            {
                throw new ArgumentNullException(nameof(session));
            }

            var sides = session.Sides;
            var own = -1;
            var found = false;
            foreach (var s in sides)
            {
                if (s == side)
                {
                    own = HudFormat.DisplayedScore(session.GetScore(s));
                    found = true;
                    break;
                }
            }

            if (!found)
            {
                throw new ArgumentException($"{side} has not joined the mode", nameof(side));
            }

            var rank = 1;
            var bestOther = (int?)null;
            foreach (var s in sides)
            {
                if (s == side)
                {
                    continue;
                }

                var score = HudFormat.DisplayedScore(session.GetScore(s));
                if (score > own)
                {
                    rank++;
                }

                if (!bestOther.HasValue || score > bestOther.Value)
                {
                    bestOther = score;
                }
            }

            return new FreeForAllStanding(rank, sides.Count, own, bestOther.HasValue ? own - bestOther.Value : (int?)null);
        }
    }

    public readonly struct FreeForAllStanding : IEquatable<FreeForAllStanding>
    {
        public int Rank { get; }

        public int SideCount { get; }

        public int Score { get; }

        public int? Margin { get; }

        public bool IsSoleLeader => Margin.HasValue ? Margin.Value > 0 : SideCount == 1;

        public FreeForAllStanding(int rank, int sideCount, int score, int? margin)
        {
            if (sideCount < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(sideCount), sideCount, "sideCount must be at least 1");
            }

            if (rank < 1 || rank > sideCount)
            {
                throw new ArgumentOutOfRangeException(nameof(rank), rank, "rank must be between 1 and sideCount");
            }

            Rank = rank;
            SideCount = sideCount;
            Score = score;
            Margin = margin;
        }

        public bool Equals(FreeForAllStanding other)
        {
            return Rank == other.Rank && SideCount == other.SideCount && Score == other.Score && Margin == other.Margin;
        }

        public override bool Equals(object obj) => obj is FreeForAllStanding other && Equals(other);

        public static bool operator ==(FreeForAllStanding left, FreeForAllStanding right) => left.Equals(right);

        public static bool operator !=(FreeForAllStanding left, FreeForAllStanding right) => !left.Equals(right);

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = Rank;
                hash = (hash * 397) ^ SideCount;
                hash = (hash * 397) ^ Score;
                return (hash * 397) ^ (Margin ?? int.MinValue);
            }
        }

        public override string ToString()
        {
            return $"rank {Rank}/{SideCount} score {Score} margin {(Margin.HasValue ? Margin.Value.ToString(CultureInfo.InvariantCulture) : "-")}";
        }
    }
}
