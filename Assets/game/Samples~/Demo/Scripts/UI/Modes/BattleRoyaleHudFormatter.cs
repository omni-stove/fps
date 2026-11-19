using System;
using System.Collections.Generic;
using Shooter.Core;
using UnityEngine;

namespace Shooter.Runtime
{
    public sealed class BattleRoyaleHudFormatter : IModeHudFormatter
    {
        public static LocalizedText NoWeapon => new LocalizedText("mode.br.noWeapon");

        public static LocalizedText SquadsLeft(int remaining, int total)
        {
            if (remaining < 0 || remaining > total)
            {
                throw new ArgumentOutOfRangeException(nameof(remaining), remaining, "remaining must be between 0 and total");
            }

            return new LocalizedText("mode.br.squadsLeft", remaining, total);
        }

        public static LocalizedText Placement(int? place, int remaining)
        {
            if (place.HasValue)
            {
                if (place.Value < 1)
                {
                    throw new ArgumentOutOfRangeException(nameof(place), place.Value, "place must be at least 1");
                }

                return new LocalizedText("mode.br.placement", place.Value);
            }

            if (remaining < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(remaining), remaining, "remaining must be at least 1 while the squad is still in");
            }

            return new LocalizedText("mode.br.placementAtLeast", remaining);
        }

        public static LocalizedText Squad(int standing, int downed)
        {
            if (standing < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(standing), standing, "standing must not be negative");
            }

            if (downed < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(downed), downed, "downed must not be negative");
            }

            var text = new LocalizedText("mode.br.squad", standing);
            return downed == 0 ? text : ModeHudText.WithDowned(text, downed);
        }

        public static IReadOnlyList<ModeHudScoreLine> Squads(IBattleRoyaleView session, Side? ownSide)
        {
            if (session == null)
            {
                throw new ArgumentNullException(nameof(session));
            }

            var lines = new List<ModeHudScoreLine>();
            foreach (var side in session.Sides)
            {
                var place = session.GetPlacement(side);
                if (!place.HasValue && session.CountMembers(side) == 0)
                {
                    continue;
                }

                var text = place.HasValue
                    ? new LocalizedText("mode.br.squadPlace", ModeHudText.SideName(side), place.Value)
                    : ModeHudText.Remaining(side, session.CountRemaining(side));
                var line = new ModeHudScoreLine(side, text, side == ownSide, place == 1);
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

        public static IReadOnlyList<LocalizedText> Lines(
            IBattleRoyaleView session, int? localPlayerId, int? reviveTargetId, LocalizedText zoneLine, bool unarmed)
        {
            if (session == null)
            {
                throw new ArgumentNullException(nameof(session));
            }

            var lines = new List<LocalizedText>();
            if (session.EntrantCount > 0)
            {
                lines.Add(SquadsLeft(session.SquadsRemaining, session.EntrantCount));
            }

            var local = localPlayerId.HasValue && session.HasPlayer(localPlayerId.Value) ? localPlayerId : null;
            if (local.HasValue && session.EntrantCount > 0)
            {
                var squad = session.GetSide(local.Value);
                var place = session.GetPlacement(squad);
                if (place.HasValue || session.SquadsRemaining > 0)
                {
                    lines.Add(Placement(place, session.SquadsRemaining));
                }
            }

            if (!zoneLine.IsEmpty)
            {
                lines.Add(zoneLine);
            }

            if (!local.HasValue || session.State != GameModeState.Playing)
            {
                return lines;
            }

            var ownSquad = session.GetSide(local.Value);
            lines.Add(Squad(session.CountStanding(ownSquad), session.CountDowned(ownSquad)));

            if (session.IsDowned(local.Value))
            {
                var bleedout = session.GetBleedoutRemaining(local.Value) ?? 0f;
                lines.Add(HudText.Downed(HudFormat.DisplayedSeconds(bleedout), HudFormat.RevivePercent(session.GetReviveProgress(local.Value))));
                return lines;
            }

            if (reviveTargetId.HasValue && session.HasPlayer(reviveTargetId.Value) && session.IsDowned(reviveTargetId.Value))
            {
                lines.Add(HudText.Reviving(HudFormat.RevivePercent(session.GetReviveProgress(reviveTargetId.Value))));
            }

            if (unarmed && session.IsStanding(local.Value))
            {
                lines.Add(NoWeapon);
            }

            return lines;
        }

        public static LocalizedText SupplyFalling(int count)
        {
            if (count < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(count), count, "count must be at least 1");
            }

            return new LocalizedText("mode.br.supplyFalling", count);
        }

        public static LocalizedText SupplyWaiting(int count)
        {
            if (count < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(count), count, "count must be at least 1");
            }

            return new LocalizedText("mode.br.supplyWaiting", count);
        }

        public static LocalizedText SupplyOpenPrompt(string keyLabel)
        {
            var body = new LocalizedText("mode.br.supplyOpen");
            return string.IsNullOrWhiteSpace(keyLabel) ? body : new LocalizedText("mode.br.supplyOpenKey", keyLabel.Trim(), body);
        }

        // 落ちている箱を先に出すのは、着く前に向かえば取り合いで先に着けるため。終わった試合では出さない。
        public static IReadOnlyList<LocalizedText> SupplyLines(BattleRoyaleStatus detail)
        {
            if (detail == null)
            {
                throw new ArgumentNullException(nameof(detail));
            }

            var lines = new List<LocalizedText>();
            if (detail.Game.State != GameModeState.Playing)
            {
                return lines;
            }

            if (detail.FallingSupplyDrops > 0)
            {
                lines.Add(SupplyFalling(detail.FallingSupplyDrops));
            }

            if (detail.LandedSupplyDrops > 0)
            {
                lines.Add(SupplyWaiting(detail.LandedSupplyDrops));
            }

            if (detail.IsLocalPlayerAtSupplyDrop)
            {
                lines.Add(SupplyOpenPrompt(detail.InteractKeyLabel));
            }

            return lines;
        }

        public static LocalizedText RedeployIn(int seconds)
        {
            if (seconds < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(seconds), seconds, "seconds must not be negative");
            }

            return new LocalizedText("mode.br.redeployIn", seconds);
        }

        public static LocalizedText RedeploysLeft(int count)
        {
            if (count < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(count), count, "count must not be negative");
            }

            return new LocalizedText("mode.br.redeploysLeft", count);
        }

        // 戻る待ちは ModeStatus の復活までの秒にも載るが、空から戻ることを言うため、このモードではこちらの行で出す。終わった試合では出さない。
        public static IReadOnlyList<LocalizedText> RedeployLines(BattleRoyaleStatus detail)
        {
            if (detail == null)
            {
                throw new ArgumentNullException(nameof(detail));
            }

            var lines = new List<LocalizedText>();
            if (detail.Game.State != GameModeState.Playing)
            {
                return lines;
            }

            if (detail.LocalRedeploySeconds.HasValue)
            {
                lines.Add(RedeployIn(HudFormat.DisplayedSeconds(detail.LocalRedeploySeconds.Value)));
            }

            if (detail.LocalRedeploysLeft.HasValue)
            {
                lines.Add(RedeploysLeft(detail.LocalRedeploysLeft.Value));
            }

            return lines;
        }

        public static LocalizedText Result(IBattleRoyaleView session)
        {
            if (session == null)
            {
                throw new ArgumentNullException(nameof(session));
            }

            return ModeHudText.Result(session.State, session.Winner);
        }

        public LocalizedText Time(ModeStatus status)
        {
            if (!(status.Detail is BattleRoyaleStatus detail))
            {
                return ModeStatusFormat.Remaining(status);
            }

            var game = detail.Game;
            return game.HasTimeLimit ? ModeStatusFormat.Clock(game.RemainingSeconds) : ModeStatusFormat.Clock(game.ElapsedSeconds);
        }

        public IReadOnlyList<ModeHudScoreLine> Scores(ModeStatus status)
        {
            return status.Detail is BattleRoyaleStatus detail ? Squads(detail.Game, status.OwnSide) : ModeStatusFormat.Scores(status);
        }

        public void AppendLines(ModeStatus status, List<LocalizedText> lines)
        {
            if (status.Detail is BattleRoyaleStatus detail)
            {
                lines.AddRange(RedeployLines(detail));
                var zoneLine = detail.Zone.HasValue ? ZoneHudFormatter.Line(detail.Zone.Value) : default;
                lines.AddRange(Lines(detail.Game, detail.LocalPlayerId, detail.ReviveTargetId, zoneLine, detail.IsLocalPlayerUnarmed));
                lines.AddRange(SupplyLines(detail));
            }
            else
            {
                ModeStatusFormat.AppendRespawn(status, lines);
            }
        }

        public LocalizedText Result(ModeStatus status)
        {
            return status.Detail is BattleRoyaleStatus detail ? Result(detail.Game) : ModeStatusFormat.Result(status);
        }

        // SubsystemRegistration は BeforeSceneLoad より先に走るので、ゲームが BeforeSceneLoad で登録した書式役がこれを置き換えられる
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void RegisterDefault()
        {
            ModeHudFormatters.Register<BattleRoyaleStatus>(new BattleRoyaleHudFormatter());
        }
    }
}
