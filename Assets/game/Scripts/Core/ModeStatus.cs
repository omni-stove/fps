using System;
using System.Collections.Generic;
using System.Globalization;

namespace Shooter.Core
{
    public sealed class ModeStatus
    {
        public ModeStatus(
            float? remainingSeconds,
            IReadOnlyList<SideScore> sideScores,
            Side? ownSide,
            float? respawnSeconds,
            bool isEnded,
            Side? winner,
            object detail)
        {
            RemainingSeconds = RequireNonNegative(remainingSeconds, nameof(remainingSeconds));
            RespawnSeconds = RequireNonNegative(respawnSeconds, nameof(respawnSeconds));
            SideScores = sideScores ?? Array.Empty<SideScore>();
            OwnSide = ownSide;
            IsEnded = isEnded;
            Winner = winner;
            Detail = detail;
        }

        public float? RemainingSeconds { get; }

        public IReadOnlyList<SideScore> SideScores { get; }

        public Side? OwnSide { get; }

        public float? RespawnSeconds { get; }

        public bool IsEnded { get; }

        public Side? Winner { get; }

        // object で持つのは、Core が文言を作らず、UI が型ごとの書式役で文にするため。
        public object Detail { get; }

        public override string ToString()
        {
            return "remaining=" + (RemainingSeconds.HasValue ? RemainingSeconds.Value.ToString("0.##", CultureInfo.InvariantCulture) : "-")
                + " sides=" + SideScores.Count
                + " own=" + (OwnSide.HasValue ? OwnSide.Value.ToString() : "-")
                + " ended=" + IsEnded
                + " winner=" + (Winner.HasValue ? Winner.Value.ToString() : "-")
                + " detail=" + (Detail != null ? Detail.GetType().Name : "-");
        }

        private static float? RequireNonNegative(float? seconds, string paramName)
        {
            if (seconds.HasValue && !(seconds.Value >= 0f))
            {
                throw new ArgumentOutOfRangeException(paramName, seconds.Value, paramName + " must be zero or positive");
            }

            return seconds;
        }
    }
}
