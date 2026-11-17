using System;
using System.Collections.Generic;
using Shooter.Core;

namespace Shooter.Runtime
{
    public abstract class ModeHudFormatterBase : IModeHudFormatter
    {
        public virtual LocalizedText Time(ModeStatus status)
        {
            var remaining = Require(status).RemainingSeconds;
            return remaining.HasValue ? LocalizedText.Literal(HudNumberFormat.RemainingTime(remaining.Value)) : default;
        }

        public virtual IReadOnlyList<ModeHudScoreLine> Scores(ModeStatus status)
        {
            var sideScores = Require(status).SideScores;
            var leader = LeadingSide(sideScores);
            var lines = new List<ModeHudScoreLine>(sideScores.Count);
            foreach (var sideScore in sideScores)
            {
                var side = sideScore.Side;
                var line = new ModeHudScoreLine(side, ModeHudText.ScoreText(side, sideScore.Score), side == status.OwnSide, side == leader);
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

        public void AppendLines(ModeStatus status, List<LocalizedText> lines)
        {
            Require(status);
            if (lines == null)
            {
                throw new ArgumentNullException(nameof(lines));
            }

            if (status.RespawnSeconds.HasValue)
            {
                lines.Add(HudText.Respawn(HudFormat.DisplayedSeconds(status.RespawnSeconds.Value)));
            }

            AppendModeLines(status, lines);
        }

        public virtual LocalizedText Result(ModeStatus status)
        {
            if (!Require(status).IsEnded)
            {
                return default;
            }

            return status.Winner.HasValue ? HudText.Wins(ModeHudText.SideName(status.Winner.Value)) : HudText.Draw;
        }

        protected virtual void AppendModeLines(ModeStatus status, List<LocalizedText> lines)
        {
        }

        private static ModeStatus Require(ModeStatus status)
        {
            return status ?? throw new ArgumentNullException(nameof(status));
        }

        private static Side? LeadingSide(IReadOnlyList<SideScore> sideScores)
        {
            Side? leader = null;
            var best = double.NegativeInfinity;
            var tied = false;
            foreach (var sideScore in sideScores)
            {
                if (sideScore.Score > best)
                {
                    best = sideScore.Score;
                    leader = sideScore.Side;
                    tied = false;
                }
                else if (sideScore.Score == best)
                {
                    tied = true;
                }
            }

            return tied ? null : leader;
        }
    }
}
