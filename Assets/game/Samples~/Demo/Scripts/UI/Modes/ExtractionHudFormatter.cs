using System;
using System.Collections.Generic;
using Shooter.Core;
using UnityEngine;

namespace Shooter.Runtime
{
    // 脱出は陣営の得点を競わないので、得点の行は出さない。勝ち負けも陣営の名前でなく、持ち帰れたかで書く。
    public sealed class ExtractionHudFormatter : IModeHudFormatter
    {
        public static LocalizedText ClosedExit => new LocalizedText("extraction.hud.closedExit");

        // 専用サーバーは断った理由を送らないので、クライアントに分かる 2 つ (待っても開かなかった・箱が消えた) だけを文にする
        public static LocalizedText OpenRefused => new LocalizedText("extraction.hud.openRefused");

        public static LocalizedText OpenGone => new LocalizedText("extraction.hud.openGone");

        public static LocalizedText ExitsOpenIn(float seconds)
        {
            return new LocalizedText("extraction.hud.exitsOpenIn", HudFormat.DisplayedSeconds(seconds));
        }

        public static LocalizedText ExitsOpen(int count)
        {
            return new LocalizedText("extraction.hud.exitsOpen", Math.Max(0, count));
        }

        public static LocalizedText Extracting(float remainingSeconds)
        {
            return new LocalizedText("extraction.hud.extracting", HudFormat.DisplayedSeconds(remainingSeconds));
        }

        public static LocalizedText Carrying(int count)
        {
            return new LocalizedText("extraction.hud.carrying", Math.Max(0, count));
        }

        public static LocalizedText OpenPrompt(string keyLabel)
        {
            var body = new LocalizedText("extraction.hud.open");
            return string.IsNullOrWhiteSpace(keyLabel) ? body : new LocalizedText("extraction.hud.openKey", keyLabel.Trim(), body);
        }

        public static LocalizedText Summary(ExtractionStatus detail)
        {
            if (detail == null)
            {
                throw new ArgumentNullException(nameof(detail));
            }

            switch (detail.Outcome)
            {
                case ExtractionOutcome.Extracted:
                    return detail.LostCount > 0
                        ? new LocalizedText("extraction.hud.extractedStashFull", detail.StoredCount, detail.LostCount)
                        : new LocalizedText("extraction.hud.extracted", detail.StoredCount);
                case ExtractionOutcome.Died:
                    if (detail.StoredCount > 0)
                    {
                        return detail.LostCount > 0
                            ? new LocalizedText("extraction.hud.diedKeptStashFull", detail.StoredCount, detail.LostCount)
                            : new LocalizedText("extraction.hud.diedKept", detail.StoredCount);
                    }

                    return new LocalizedText("extraction.hud.died", detail.LostCount);
                case ExtractionOutcome.TimedOut:
                    return new LocalizedText("extraction.hud.timedOut", detail.LostCount);
                default:
                    return default;
            }
        }

        public static LocalizedText OutcomeTitle(ExtractionOutcome outcome)
        {
            switch (outcome)
            {
                case ExtractionOutcome.Extracted:
                    return new LocalizedText("extraction.result.extracted");
                case ExtractionOutcome.Died:
                    return new LocalizedText("extraction.result.died");
                case ExtractionOutcome.TimedOut:
                    return new LocalizedText("extraction.result.timedOut");
                default:
                    return default;
            }
        }

        public LocalizedText Time(ModeStatus status)
        {
            return Detail(status) is { } detail ? ModeStatusFormat.Clock(detail.RemainingSeconds) : ModeStatusFormat.Remaining(status);
        }

        public IReadOnlyList<ModeHudScoreLine> Scores(ModeStatus status)
        {
            return Detail(status) != null ? Array.Empty<ModeHudScoreLine>() : ModeStatusFormat.Scores(status);
        }

        public void AppendLines(ModeStatus status, List<LocalizedText> lines)
        {
            if (lines == null)
            {
                throw new ArgumentNullException(nameof(lines));
            }

            ModeStatusFormat.AppendRespawn(status, lines);
            var detail = Detail(status);
            if (detail == null)
            {
                return;
            }

            if (detail.IsOver)
            {
                lines.Add(Summary(detail));
                return;
            }

            lines.Add(detail.PointsOpen ? ExitsOpen(detail.OpenPointCount) : ExitsOpenIn(detail.SecondsUntilOpen));
            if (detail.IsHolding)
            {
                lines.Add(Extracting(detail.HoldRemainingSeconds));
            }
            else if (detail.StandingOnClosedPoint)
            {
                lines.Add(ClosedExit);
            }

            lines.Add(Carrying(detail.CarriedCount));
            if (detail.LootInReach)
            {
                lines.Add(OpenPrompt(detail.InteractKeyLabel));
            }
        }

        public LocalizedText Result(ModeStatus status)
        {
            var detail = Detail(status);
            if (detail == null || !status.IsEnded)
            {
                return ModeStatusFormat.Result(status);
            }

            return OutcomeTitle(detail.Outcome);
        }

        private static ExtractionStatus Detail(ModeStatus status)
        {
            if (status == null)
            {
                throw new ArgumentNullException(nameof(status));
            }

            return status.Detail as ExtractionStatus;
        }

        // SubsystemRegistration は BeforeSceneLoad より先に走るので、ゲームが BeforeSceneLoad で登録した書式役がこれを置き換えられる
        // Edit Mode (テストやシーン生成) では Play の初期化が走らないので、エディターの読み込みでも登録する。
#if UNITY_EDITOR
        [UnityEditor.InitializeOnLoadMethod]
#endif
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void RegisterDefault()
        {
            ModeHudFormatters.Register<ExtractionStatus>(new ExtractionHudFormatter());
        }
    }
}
