using System;
using System.Collections.Generic;
using Shooter.Core;
using UnityEngine;

namespace Shooter.Runtime
{
    // 陣営を ALPHA・BRAVO ではなく生存者・感染者と呼ぶので、得点と勝敗の行も ModeStatusFormat を使わずにここで組む。
    public sealed class InfectedHudFormatter : IModeHudFormatter
    {
        public static LocalizedText SurvivorsName => new LocalizedText("mode.infected.survivors");

        public static LocalizedText InfectedName => new LocalizedText("mode.infected.infected");

        public static LocalizedText YouAreSurvivor => new LocalizedText("mode.infected.youAreSurvivor");

        public static LocalizedText YouAreInfected => new LocalizedText("mode.infected.youAreInfected");

        public static LocalizedText SideName(Side side)
        {
            if (side == Infected.SurvivorSide)
            {
                return SurvivorsName;
            }

            return side == Infected.InfectedSide ? InfectedName : ModeHudText.SideName(side);
        }

        public static LocalizedText OutbreakIn(int seconds)
        {
            return new LocalizedText("mode.infected.outbreakIn", seconds);
        }

        public static LocalizedText Counts(int survivors, int infected)
        {
            return new LocalizedText("mode.infected.counts", survivors, infected);
        }

        public static IReadOnlyList<LocalizedText> HudLines(InfectedStatus status)
        {
            if (status == null)
            {
                throw new ArgumentNullException(nameof(status));
            }

            var lines = new List<LocalizedText>();
            if (!status.HasBrokenOut)
            {
                lines.Add(OutbreakIn(HudFormat.DisplayedSeconds(status.SecondsUntilOutbreak)));
            }

            lines.Add(Counts(status.SurvivorCount, status.InfectedCount));
            if (status.HasBrokenOut && status.IsLocalPlayerInfected.HasValue)
            {
                lines.Add(status.IsLocalPlayerInfected.Value ? YouAreInfected : YouAreSurvivor);
            }

            return lines;
        }

        public LocalizedText Time(ModeStatus status) => ModeStatusFormat.Remaining(status);

        public IReadOnlyList<ModeHudScoreLine> Scores(ModeStatus status)
        {
            if (status == null)
            {
                throw new ArgumentNullException(nameof(status));
            }

            var leader = LeadingSide(status.SideScores);
            var lines = new List<ModeHudScoreLine>(status.SideScores.Count);
            foreach (var score in status.SideScores)
            {
                var text = HudText.Score(SideName(score.Side), HudFormat.DisplayedScore(score.Score));
                var line = new ModeHudScoreLine(score.Side, text, score.Side == status.OwnSide, score.Side == leader);
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
            ModeStatusFormat.AppendRespawn(status, lines);
            if (status.Detail is InfectedStatus detail)
            {
                lines.AddRange(HudLines(detail));
            }
        }

        public LocalizedText Result(ModeStatus status)
        {
            if (status == null)
            {
                throw new ArgumentNullException(nameof(status));
            }

            if (!status.IsEnded)
            {
                return default;
            }

            return status.Winner.HasValue ? HudText.Wins(SideName(status.Winner.Value)) : HudText.Draw;
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

        // Edit Mode (テストやシーン生成) では Play の初期化が走らないので、エディターの読み込みでも登録する。
        // SubsystemRegistration は BeforeSceneLoad より先に走るので、ゲームが BeforeSceneLoad で登録した書式役がこれを置き換えられる。
#if UNITY_EDITOR
        [UnityEditor.InitializeOnLoadMethod]
#endif
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Register()
        {
            ModeHudFormatters.Register<InfectedStatus>(new InfectedHudFormatter());
        }
    }
}
