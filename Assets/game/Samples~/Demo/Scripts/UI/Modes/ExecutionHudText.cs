using System;
using System.Collections.Generic;
using Shooter.Core;

namespace Shooter.Runtime
{
    public static class ExecutionHudText
    {
        public static LocalizedText Round(int roundNumber, int winsToWin)
        {
            if (roundNumber < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(roundNumber), roundNumber, "roundNumber must be at least 1");
            }

            if (winsToWin < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(winsToWin), winsToWin, "winsToWin must be at least 1");
            }

            return new LocalizedText("mode.execution.round", roundNumber, winsToWin);
        }

        public static LocalizedText Preparation(int seconds)
        {
            if (seconds < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(seconds), seconds, "seconds must not be negative");
            }

            return new LocalizedText("mode.execution.startsIn", seconds);
        }

        public static LocalizedText RoundResult(Side? winner)
        {
            return new LocalizedText(
                "mode.execution.previousRound",
                winner.HasValue ? HudText.Wins(ModeHudText.SideName(winner.Value)) : HudText.Draw);
        }

        public static LocalizedText Remaining(Side side, int remaining, int downed)
        {
            if (remaining < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(remaining), remaining, "remaining must not be negative");
            }

            if (downed < 0 || downed > remaining)
            {
                throw new ArgumentOutOfRangeException(nameof(downed), downed, "downed must be between 0 and remaining");
            }

            var text = ModeHudText.Remaining(side, remaining);
            return downed == 0 ? text : ModeHudText.WithDowned(text, downed);
        }

        public static LocalizedText Executing(int percent)
        {
            if (percent < 0 || percent > 100)
            {
                throw new ArgumentOutOfRangeException(nameof(percent), percent, "percent must be between 0 and 100");
            }

            return new LocalizedText("mode.execution.executing", percent);
        }

        public static LocalizedText Time(IExecutionView view)
        {
            if (view == null)
            {
                throw new ArgumentNullException(nameof(view));
            }

            return LocalizedText.Literal(HudNumberFormat.RemainingTime(view.RoundRemaining));
        }

        public static IReadOnlyList<ModeHudScoreLine> RoundWins(IExecutionView view, Side? ownSide)
        {
            if (view == null)
            {
                throw new ArgumentNullException(nameof(view));
            }

            var alpha = view.GetRoundWins(Side.Alpha);
            var bravo = view.GetRoundWins(Side.Bravo);
            Side? leader = alpha == bravo ? (Side?)null : alpha > bravo ? Side.Alpha : Side.Bravo;
            var lines = new List<ModeHudScoreLine>(2);
            foreach (var side in new[] { Side.Alpha, Side.Bravo })
            {
                var wins = side == Side.Alpha ? alpha : bravo;
                var line = new ModeHudScoreLine(side, ModeHudText.ScoreText(side, wins), side == ownSide, side == leader);
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

        public static IReadOnlyList<LocalizedText> Lines(IExecutionView view, int? localPlayerId, int? interactTargetId)
        {
            if (view == null)
            {
                throw new ArgumentNullException(nameof(view));
            }

            var lines = new List<LocalizedText>();
            if (view.RoundNumber >= 1)
            {
                lines.Add(Round(view.RoundNumber, view.WinsToWin));
            }

            if (view.Phase == RoundPhase.Preparing)
            {
                if (view.HasFinishedRound)
                {
                    lines.Add(RoundResult(view.LastRoundWinner));
                }

                lines.Add(Preparation(HudFormat.DisplayedSeconds(view.PreparationRemaining)));
            }

            var local = localPlayerId.HasValue && view.HasPlayer(localPlayerId.Value) ? localPlayerId : null;
            var ownSide = local.HasValue ? view.GetSide(local.Value) : (Side?)null;
            var first = ownSide ?? Side.Alpha;
            var second = first == Side.Alpha ? Side.Bravo : Side.Alpha;
            lines.Add(Remaining(first, view.CountRemaining(first), view.CountDowned(first)));
            lines.Add(Remaining(second, view.CountRemaining(second), view.CountDowned(second)));

            if (local.HasValue && view.IsDowned(local.Value))
            {
                var bleedout = view.GetBleedoutRemaining(local.Value) ?? 0f;
                lines.Add(HudText.Downed(HudFormat.DisplayedSeconds(bleedout), HudFormat.RevivePercent(view.GetReviveProgress(local.Value))));
            }
            else if (local.HasValue && interactTargetId.HasValue && view.HasPlayer(interactTargetId.Value) && view.IsDowned(interactTargetId.Value))
            {
                var target = interactTargetId.Value;
                lines.Add(view.GetSide(target) == ownSide
                    ? HudText.Reviving(HudFormat.RevivePercent(view.GetReviveProgress(target)))
                    : Executing(HudFormat.RevivePercent(view.GetExecuteProgress(target))));
            }

            return lines;
        }

        public static LocalizedText Result(IExecutionView view)
        {
            if (view == null)
            {
                throw new ArgumentNullException(nameof(view));
            }

            var winner = view.Winner;
            return winner.HasValue ? HudText.Wins(ModeHudText.SideName(winner.Value)) : default;
        }
    }
}
