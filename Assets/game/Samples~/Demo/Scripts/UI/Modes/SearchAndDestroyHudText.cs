using System;
using System.Collections.Generic;
using Shooter.Core;

namespace Shooter.Runtime
{
    public static class SearchAndDestroyHudText
    {
        public static LocalizedText AttackRole => new LocalizedText("mode.snd.attackRole");
        public static LocalizedText DefendRole => new LocalizedText("mode.snd.defendRole");

        // 時計の書式は言語に依らないので、表を引かずにそのまま運ぶ。
        public static LocalizedText Clock(ISearchAndDestroyView game)
        {
            Require(game);
            if (game.Phase == RoundPhase.Preparing)
            {
                return LocalizedText.Literal(HudNumberFormat.RemainingTime(game.PreparationRemaining));
            }

            return LocalizedText.Literal(HudNumberFormat.RemainingTime(IsPlantedOrAfter(game.Bomb) ? game.FuseRemaining : game.RoundRemaining));
        }

        public static IReadOnlyList<ModeHudScoreLine> Scores(ISearchAndDestroyView game, Side? ownSide)
        {
            Require(game);
            var attacker = game.Attacker;
            var defender = game.Defender;
            var attackerWins = game.GetRoundWins(attacker);
            var defenderWins = game.GetRoundWins(defender);
            var sides = attacker < defender ? new[] { attacker, defender } : new[] { defender, attacker };

            var lines = new List<ModeHudScoreLine>(2);
            foreach (var side in sides)
            {
                var isAttacker = side == attacker;
                var wins = isAttacker ? attackerWins : defenderWins;
                var other = isAttacker ? defenderWins : attackerWins;
                var text = new LocalizedText("mode.snd.score", ModeHudText.SideName(side), wins, isAttacker ? AttackRole : DefendRole);
                var line = new ModeHudScoreLine(side, text, side == ownSide, wins > other);
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

        public static IReadOnlyList<LocalizedText> Lines(ISearchAndDestroyView game, int? localPlayerId)
        {
            Require(game);
            var lines = new List<LocalizedText>();
            if (game.Phase == RoundPhase.Waiting)
            {
                return lines;
            }

            lines.Add(new LocalizedText("mode.snd.round", game.RoundNumber, game.WinsToWin));

            var local = localPlayerId.HasValue && game.HasPlayer(localPlayerId.Value) ? localPlayerId : null;
            if (local.HasValue && game.Phase != RoundPhase.Ended)
            {
                lines.Add(new LocalizedText(game.IsAttacker(local.Value) ? "mode.snd.youAttack" : "mode.snd.youDefend"));
            }

            if (game.Phase == RoundPhase.Preparing)
            {
                if (game.LastRoundEnd.HasValue)
                {
                    lines.Add(new LocalizedText("mode.snd.previousRound", RoundResult(game.LastRoundWinner, game.LastRoundEnd.Value)));
                }

                lines.Add(new LocalizedText("mode.snd.startsIn", HudFormat.DisplayedSeconds(game.PreparationRemaining)));
                return lines;
            }

            if (game.Phase == RoundPhase.Ended)
            {
                if (game.LastRoundEnd.HasValue)
                {
                    lines.Add(new LocalizedText("mode.snd.lastRound", RoundResult(game.LastRoundWinner, game.LastRoundEnd.Value)));
                }

                return lines;
            }

            switch (game.Bomb)
            {
                case BombState.Carried:
                    lines.Add(new LocalizedText(local.HasValue && game.CarrierId == local
                        ? "mode.snd.youCarry"
                        : "mode.snd.carried"));
                    if (game.PlantingSite.HasValue)
                    {
                        lines.Add(new LocalizedText("mode.snd.planting", BombSiteName.Of(game.PlantingSite.Value), Percent(game.PlantFraction)));
                    }

                    break;
                case BombState.Dropped:
                    lines.Add(new LocalizedText("mode.snd.dropped"));
                    break;
                case BombState.Planted:
                    lines.Add(Fuse(game.PlantedSite.Value, game.FuseRemaining));
                    if (game.DefuserId.HasValue)
                    {
                        lines.Add(new LocalizedText("mode.snd.defusing", Percent(game.DefuseFraction)));
                    }

                    break;
                case BombState.Defused:
                    lines.Add(new LocalizedText("mode.snd.defused"));
                    break;
                case BombState.Detonated:
                    lines.Add(new LocalizedText("mode.snd.detonated"));
                    break;
            }

            return lines;
        }

        public static LocalizedText Fuse(int site, float fuseRemaining)
        {
            return new LocalizedText("mode.snd.fuse", BombSiteName.Of(site), HudFormat.DisplayedSeconds(fuseRemaining));
        }

        public static LocalizedText RoundResult(Side? winner, RoundEndReason reason)
        {
            var head = winner.HasValue ? HudText.Wins(ModeHudText.SideName(winner.Value)) : HudText.Draw;
            return new LocalizedText("mode.snd.roundResult", head, ReasonText(reason));
        }

        public static LocalizedText ReasonText(RoundEndReason reason)
        {
            switch (reason)
            {
                case RoundEndReason.Detonated:
                    return new LocalizedText("mode.snd.reason.detonated");
                case RoundEndReason.Defused:
                    return new LocalizedText("mode.snd.reason.defused");
                case RoundEndReason.DefendersEliminated:
                    return new LocalizedText("mode.snd.reason.defendersEliminated");
                case RoundEndReason.AttackersEliminated:
                    return new LocalizedText("mode.snd.reason.attackersEliminated");
                case RoundEndReason.TimeUp:
                    return new LocalizedText("mode.snd.reason.timeUp");
                case RoundEndReason.BothEliminated:
                    return new LocalizedText("mode.snd.reason.bothEliminated");
                default:
                    throw new ArgumentOutOfRangeException(nameof(reason), reason, "reason is not a valid RoundEndReason");
            }
        }

        public static LocalizedText Result(ISearchAndDestroyView game)
        {
            Require(game);
            return game.Phase == RoundPhase.Ended && game.Winner.HasValue
                ? HudText.Wins(ModeHudText.SideName(game.Winner.Value))
                : default;
        }

        private static bool IsPlantedOrAfter(BombState bomb)
        {
            return bomb == BombState.Planted || bomb == BombState.Defused || bomb == BombState.Detonated;
        }

        private static int Percent(float fraction)
        {
            var clamped = fraction > 0f ? Math.Min(1f, fraction) : 0f;
            return (int)Math.Floor(clamped * 100f);
        }

        private static void Require(ISearchAndDestroyView game)
        {
            if (game == null)
            {
                throw new ArgumentNullException(nameof(game));
            }
        }
    }
}
