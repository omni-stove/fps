using System;
using System.Collections.Generic;
using Shooter.Core;

namespace Shooter.Runtime
{
    public static class ModeHudText
    {
        public static LocalizedText SideName(Side side)
        {
            return side.TryGetTeam(out var team)
                ? HudText.TeamName(team)
                : new LocalizedText("hud.side", side.Id);
        }

        // 時間で入る点は小数になるので、届いていない点を出さないよう HudFormat.DisplayedScore で切り捨てて出す。
        public static LocalizedText ScoreText(Side side, double score)
        {
            return HudText.Score(SideName(side), HudFormat.DisplayedScore(score));
        }

        public static LocalizedText Remaining(Side side, int remaining)
        {
            return new LocalizedText("hud.mode.remaining", SideName(side), remaining);
        }

        public static LocalizedText WithDowned(LocalizedText text, int downed)
        {
            return new LocalizedText("hud.mode.withDowned", text, downed);
        }

        // 残り時間の書式は言語に依らないので、表を引かずにそのまま運ぶ。
        public static LocalizedText Time(ModeSession session)
        {
            if (session == null)
            {
                throw new ArgumentNullException(nameof(session));
            }

            return LocalizedText.Literal(HudNumberFormat.RemainingTime(session.RemainingSeconds));
        }

        public static IReadOnlyList<ModeHudScoreLine> Scores(ModeSession session, Side? ownSide)
        {
            if (session == null)
            {
                throw new ArgumentNullException(nameof(session));
            }

            var sides = session.Sides;
            var leader = session.LeadingSide;
            var lines = new List<ModeHudScoreLine>(sides.Count);
            foreach (var side in sides)
            {
                var line = new ModeHudScoreLine(side, ScoreText(side, session.GetScore(side)), side == ownSide, side == leader);
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

        public static LocalizedText Result(ModeSession session)
        {
            if (session == null)
            {
                throw new ArgumentNullException(nameof(session));
            }

            return Result(session.State, session.Winner);
        }

        public static LocalizedText Result(GameModeState state, Side? winner)
        {
            if (state != GameModeState.Ended)
            {
                return default;
            }

            return winner.HasValue ? HudText.Wins(SideName(winner.Value)) : HudText.Draw;
        }
    }
}
