using System;
using System.Collections.Generic;
using System.Globalization;

namespace Shooter.Core
{
    // 通信の手順の状態からも作れるよう、Domination そのものは持たない。
    public sealed class DominationStatus
    {
        public DominationStatus(
            float alphaScore,
            float bravoScore,
            float remainingSeconds,
            IReadOnlyList<ControlPointStatus> points,
            bool isEnded,
            Team? winner)
        {
            if (!(remainingSeconds >= 0f))
            {
                throw new ArgumentOutOfRangeException(nameof(remainingSeconds), remainingSeconds, "remainingSeconds must be zero or positive");
            }

            AlphaScore = alphaScore;
            BravoScore = bravoScore;
            RemainingSeconds = remainingSeconds;
            Points = points ?? Array.Empty<ControlPointStatus>();
            IsEnded = isEnded;
            Winner = isEnded ? winner : null;
        }

        public float AlphaScore { get; }

        public float BravoScore { get; }

        public float RemainingSeconds { get; }

        public IReadOnlyList<ControlPointStatus> Points { get; }

        public bool IsEnded { get; }

        public Team? Winner { get; }

        public float GetScore(Team team)
        {
            switch (team)
            {
                case Team.Alpha:
                    return AlphaScore;
                case Team.Bravo:
                    return BravoScore;
                default:
                    throw new ArgumentOutOfRangeException(nameof(team), team, "team must be Alpha or Bravo");
            }
        }

        public static DominationStatus Of(Domination domination)
        {
            if (domination == null)
            {
                throw new ArgumentNullException(nameof(domination));
            }

            // 出した後も UI が持つので、並びは使い回さずに毎回作る。
            var points = new ControlPointStatus[domination.PointCount];
            for (var i = 0; i < points.Length; i++)
            {
                points[i] = new ControlPointStatus(
                    domination.GetOwner(i),
                    ControlPointStatus.PercentOf(domination.GetControl(i)),
                    domination.IsContested(i));
            }

            var ended = domination.State == GameModeState.Ended;
            return new DominationStatus(
                domination.GetScore(Team.Alpha),
                domination.GetScore(Team.Bravo),
                Math.Max(0f, domination.RemainingSeconds),
                points,
                ended,
                ended ? domination.Winner : null);
        }

        public override string ToString()
        {
            return "A=" + AlphaScore.ToString("0.##", CultureInfo.InvariantCulture)
                + " B=" + BravoScore.ToString("0.##", CultureInfo.InvariantCulture)
                + " remaining=" + RemainingSeconds.ToString("0.##", CultureInfo.InvariantCulture)
                + " points=" + Points.Count
                + " ended=" + IsEnded
                + " winner=" + (Winner.HasValue ? Winner.Value.ToString() : "-");
        }
    }
}
