using System;
using System.Collections.Generic;
using Shooter.Core;

namespace Shooter.Runtime
{
    public static class ModeStatusFormat
    {
        public static LocalizedText Clock(double seconds)
        {
            return LocalizedText.Literal(HudNumberFormat.RemainingTime((float)seconds));
        }

        public static LocalizedText Remaining(ModeStatus status)
        {
            if (status == null)
            {
                throw new ArgumentNullException(nameof(status));
            }

            return Clock(status.RemainingSeconds ?? float.PositiveInfinity);
        }

        public static IReadOnlyList<ModeHudScoreLine> Scores(ModeStatus status)
        {
            if (status == null)
            {
                throw new ArgumentNullException(nameof(status));
            }

            var leader = LeadingSide(status.SideScores);
            var lines = new List<ModeHudScoreLine>(status.SideScores.Count);
            foreach (var score in status.SideScores)
            {
                var line = new ModeHudScoreLine(
                    score.Side, ModeHudText.ScoreText(score.Side, score.Score), score.Side == status.OwnSide, score.Side == leader);
                if (line.IsOwn)
                {
                    lines.Insert(0, line);
                }
                else
                {
                    lines.Add(line);
                }
            }

            return lines;
        }

        public static void AppendRespawn(ModeStatus status, List<LocalizedText> lines)
        {
            if (status == null)
            {
                throw new ArgumentNullException(nameof(status));
            }

            if (lines == null)
            {
                throw new ArgumentNullException(nameof(lines));
            }

            if (status.RespawnSeconds.HasValue)
            {
                lines.Add(HudText.Respawn(HudFormat.DisplayedSeconds(status.RespawnSeconds.Value)));
            }
        }

        public static LocalizedText Result(ModeStatus status)
        {
            if (status == null)
            {
                throw new ArgumentNullException(nameof(status));
            }

            if (!status.IsEnded)
            {
                return default;
            }

            return status.Winner.HasValue ? HudText.Wins(ModeHudText.SideName(status.Winner.Value)) : HudText.Draw;
        }

        private static Side? LeadingSide(IReadOnlyList<SideScore> scores)
        {
            Side? leader = null;
            var best = double.NegativeInfinity;
            var tied = false;
            foreach (var score in scores)
            {
                if (score.Score > best)
                {
                    best = score.Score;
                    leader = score.Side;
                    tied = false;
                }
                else if (score.Score == best)
                {
                    tied = true;
                }
            }

            return tied ? null : leader;
        }
    }
}
