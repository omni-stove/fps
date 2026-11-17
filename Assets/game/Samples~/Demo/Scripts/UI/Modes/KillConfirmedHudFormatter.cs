using System;
using System.Collections.Generic;
using Shooter.Core;
using UnityEngine;

namespace Shooter.Runtime
{
    public sealed class KillConfirmedHudFormatter : ModeHudFormatterBase
    {
        public static LocalizedText Confirmed(int alpha, int bravo)
        {
            return new LocalizedText("mode.killConfirmed.confirmed", PerSide(alpha, bravo));
        }

        public static LocalizedText Denied(int alpha, int bravo)
        {
            return new LocalizedText("mode.killConfirmed.denied", PerSide(alpha, bravo));
        }

        public static LocalizedText TagsOnField(int count)
        {
            return new LocalizedText("mode.killConfirmed.tagsOnField", count);
        }

        public static LocalizedText[] HudLines(KillConfirmed killConfirmed)
        {
            if (killConfirmed == null)
            {
                throw new ArgumentNullException(nameof(killConfirmed));
            }

            return new[]
            {
                Confirmed(killConfirmed.GetConfirmed(Side.Alpha), killConfirmed.GetConfirmed(Side.Bravo)),
                Denied(killConfirmed.GetDenied(Side.Alpha), killConfirmed.GetDenied(Side.Bravo)),
                TagsOnField(killConfirmed.Tags.Count),
            };
        }

        protected override void AppendModeLines(ModeStatus status, List<LocalizedText> lines)
        {
            if (status.Detail is KillConfirmed killConfirmed)
            {
                lines.AddRange(HudLines(killConfirmed));
            }
        }

        private static LocalizedText PerSide(int alpha, int bravo)
        {
            return new LocalizedText(
                "mode.killConfirmed.perSide",
                ModeHudText.SideName(Side.Alpha),
                alpha,
                ModeHudText.SideName(Side.Bravo),
                bravo);
        }

        // Edit Mode (テストやシーン生成) では Play の初期化が走らないので、エディターの読み込みでも登録する。
#if UNITY_EDITOR
        [UnityEditor.InitializeOnLoadMethod]
#endif
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Register()
        {
            ModeHudFormatters.Register<KillConfirmed>(new KillConfirmedHudFormatter());
        }
    }
}
