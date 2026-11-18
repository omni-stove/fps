using System;
using System.Collections.Generic;

namespace Shooter.Core
{
    // プレイヤーの Id や名前を持たないのは、同意したのは手元の人だけで、同じ試合のほかの人の分まで残せないため。
    public sealed class TelemetryMatchSummary
    {
        public TelemetryMatchSummary(
            string matchId,
            string mode,
            string map,
            float durationSeconds,
            IReadOnlyList<SideScore> sideScores,
            Side? winner,
            Side? ownSide = null,
            int? humans = null,
            int? bots = null)
        {
            if (float.IsNaN(durationSeconds) || float.IsInfinity(durationSeconds) || durationSeconds < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(durationSeconds), durationSeconds, "durationSeconds must be finite and not negative");
            }

            if (humans < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(humans), humans, "humans must not be negative");
            }

            if (bots < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(bots), bots, "bots must not be negative");
            }

            MatchId = matchId?.Trim() ?? string.Empty;
            Mode = mode?.Trim() ?? string.Empty;
            Map = map?.Trim() ?? string.Empty;
            DurationSeconds = durationSeconds;
            SideScores = sideScores ?? Array.Empty<SideScore>();
            Winner = winner;
            OwnSide = ownSide;
            Humans = humans;
            Bots = bots;
        }

        public string MatchId { get; }

        public string Mode { get; }

        public string Map { get; }

        public float DurationSeconds { get; }

        public IReadOnlyList<SideScore> SideScores { get; }

        public Side? Winner { get; }

        public Side? OwnSide { get; }

        public int? Humans { get; }

        public int? Bots { get; }
    }
}
