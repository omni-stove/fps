using System;
using System.Collections.Generic;
using Fps.Core;
using UnityEngine;

namespace Fps.Runtime
{
    /// <summary>
    /// キルコンファームド (<see cref="KillConfirmedRunner"/>) の HUD の書式役。<see cref="ModeStatus.Detail"/> の <see cref="KillConfirmed"/> から、
    /// リスポーンの行の後に、陣営ごとの確定数・阻止数と落ちているタグの数の 3 行 (<see cref="HudLines"/>) を出す。
    /// 文は表のキー <c>mode.killConfirmed.*</c> で返し、陣営名は <see cref="ModeHudText.SideName"/> を引数に入れる。
    /// </summary>
    public sealed class KillConfirmedHudFormatter : ModeHudFormatterBase
    {
        /// <summary>陣営ごとの確定数の行。</summary>
        /// <example><c>Confirmed(3, 2)</c> は <c>確定 ALPHA 3 / BRAVO 2</c></example>
        public static LocalizedText Confirmed(int alpha, int bravo)
        {
            return new LocalizedText("mode.killConfirmed.confirmed", PerSide(alpha, bravo));
        }

        /// <summary>陣営ごとの阻止数の行。</summary>
        /// <example><c>Denied(1, 0)</c> は <c>阻止 ALPHA 1 / BRAVO 0</c></example>
        public static LocalizedText Denied(int alpha, int bravo)
        {
            return new LocalizedText("mode.killConfirmed.denied", PerSide(alpha, bravo));
        }

        /// <summary>落ちていて拾われるのを待っているタグの数の行。</summary>
        /// <example><c>TagsOnField(2)</c> は <c>落ちているタグ 2</c></example>
        public static LocalizedText TagsOnField(int count)
        {
            return new LocalizedText("mode.killConfirmed.tagsOnField", count);
        }

        /// <summary>killConfirmed の HUD の 3 行 (確定数・阻止数・落ちているタグの数)。</summary>
        /// <exception cref="ArgumentNullException">killConfirmed が null。</exception>
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
