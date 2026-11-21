using System;
using System.Collections.Generic;
using System.Globalization;
using Shooter.Core;
using UnityEngine;

namespace Shooter.Runtime
{
    public sealed class HordeHudFormatter : IModeHudFormatter
    {
        public static LocalizedText ReturnsAfterWave => new LocalizedText("mode.horde.returnsAfterWave");

        public static LocalizedText Victory => new LocalizedText("mode.horde.victory");
        public static LocalizedText Defeat => new LocalizedText("mode.horde.defeat");

        public static LocalizedText Wave(int number, int? waveCount)
        {
            var current = number > 0 ? number.ToString(CultureInfo.InvariantCulture) : "-";
            return waveCount.HasValue
                ? new LocalizedText("mode.horde.wave", current, waveCount.Value)
                : new LocalizedText("mode.horde.waveEndless", current);
        }

        public static LocalizedText RemainingEnemies(int remaining)
        {
            return new LocalizedText("mode.horde.remainingEnemies", Math.Max(0, remaining));
        }

        public static LocalizedText NextWave(float restSeconds)
        {
            return new LocalizedText("mode.horde.nextWave", HudFormat.DisplayedSeconds(restSeconds));
        }

        public static LocalizedText Currency(int currency)
        {
            return new LocalizedText("mode.horde.currency", currency);
        }

        public static LocalizedText ActionName(HordeFortificationAction action)
        {
            switch (action)
            {
                case HordeFortificationAction.Build:
                    return new LocalizedText("mode.horde.build");
                case HordeFortificationAction.Repair:
                    return new LocalizedText("mode.horde.repair");
                default:
                    return LocalizedText.Literal("-");
            }
        }

        public static LocalizedText Prompt(HordeFortificationOffer offer)
        {
            var body = new LocalizedText("mode.horde.prompt", Word(offer.StructureName), ActionName(offer.Action), offer.Cost);
            switch (offer.Verdict)
            {
                case HordePurchaseVerdict.Ok:
                    return string.IsNullOrWhiteSpace(offer.KeyLabel) ? body : new LocalizedText("mode.horde.promptKey", offer.KeyLabel.Trim(), body);
                case HordePurchaseVerdict.WaveInProgress:
                    return new LocalizedText("mode.horde.promptBetweenWaves", body);
                case HordePurchaseVerdict.NotEnoughCurrency:
                    return new LocalizedText("mode.horde.promptNotEnoughCurrency", body);
                default:
                    return body;
            }
        }

        public LocalizedText Time(ModeStatus status)
        {
            return status.Detail is HordeStatus detail
                ? ModeStatusFormat.Clock(detail.ElapsedSeconds)
                : ModeStatusFormat.Remaining(status);
        }

        public IReadOnlyList<ModeHudScoreLine> Scores(ModeStatus status) => ModeStatusFormat.Scores(status);

        public void AppendLines(ModeStatus status, List<LocalizedText> lines)
        {
            ModeStatusFormat.AppendRespawn(status, lines);
            if (!(status.Detail is HordeStatus detail))
            {
                return;
            }

            lines.Add(Wave(detail.WaveNumber, detail.WaveCount));
            if (detail.Phase == WavePhase.Fighting)
            {
                lines.Add(RemainingEnemies(detail.RemainingEnemies));
            }
            else if (detail.Phase == WavePhase.Resting)
            {
                lines.Add(NextWave(detail.RestRemaining));
            }

            lines.Add(Currency(detail.Currency));

            if (detail.IsEnded)
            {
                lines.Add(detail.IsVictory ? Victory : Defeat);
                return;
            }

            if (detail.IsAwaitingReturn)
            {
                lines.Add(ReturnsAfterWave);
                return;
            }

            if (detail.Offer.HasValue)
            {
                lines.Add(Prompt(detail.Offer.Value));
            }
        }

        public LocalizedText Result(ModeStatus status) => ModeStatusFormat.Result(status);

        // 空白を含む名前はログと同じく詰めて出す (HUD とログで同じ防衛物が同じ名前に見えるように)
        private static string Word(string value) => string.IsNullOrEmpty(value) ? "-" : value.Replace(' ', '_');

        // SubsystemRegistration は BeforeSceneLoad より先に走るので、ゲームが BeforeSceneLoad で登録した書式役がこれを置き換えられる
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void RegisterDefault()
        {
            ModeHudFormatters.Register<HordeStatus>(new HordeHudFormatter());
        }
    }
}
