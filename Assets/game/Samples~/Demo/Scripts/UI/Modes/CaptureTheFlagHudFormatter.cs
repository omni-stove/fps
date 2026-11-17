using System;
using System.Collections.Generic;
using Shooter.Core;
using UnityEngine;

namespace Shooter.Runtime
{
    /// <summary>
    /// 旗取り (<see cref="CaptureTheFlag"/>) の HUD の書式役。<see cref="ModeStatus.Detail"/> の <see cref="CaptureTheFlagStatus"/> から、
    /// リスポーンの行の後に、Alpha・Bravo の順に旗の状態の行を出し、この画面のプレイヤーが旗を運んでいればその案内の行を足す (<see cref="HudLines"/>)。
    /// 文は表のキー <c>mode.ctf.*</c> で返し、陣営名は <see cref="ModeHudText.SideName"/> を引数に入れる。
    /// </summary>
    public sealed class CaptureTheFlagHudFormatter : ModeHudFormatterBase
    {
        /// <summary>flag の陣営の旗の状態の行。落ちていれば台に戻るまでの秒数 (切り上げ) を添える。</summary>
        /// <example>
        /// <c>Flag(Side.Alpha, FlagState.AtBase, null)</c> は <c>ALPHA の旗: 台にある</c>、
        /// <c>Flag(Side.Bravo, FlagState.Carried, null)</c> は <c>BRAVO の旗: 運ばれている</c>、
        /// <c>Flag(Side.Alpha, FlagState.Dropped, 15.2f)</c> は <c>ALPHA の旗: 落ちている (戻るまで 16 秒)</c>
        /// </example>
        /// <exception cref="ArgumentException">Dropped なのに returnSeconds が null。</exception>
        /// <exception cref="ArgumentOutOfRangeException">state が定義されていない値。</exception>
        public static LocalizedText Flag(Side flag, FlagState state, float? returnSeconds)
        {
            var name = ModeHudText.SideName(flag);
            switch (state)
            {
                case FlagState.AtBase:
                    return new LocalizedText("mode.ctf.atBase", name);
                case FlagState.Carried:
                    return new LocalizedText("mode.ctf.carried", name);
                case FlagState.Dropped:
                    if (!returnSeconds.HasValue)
                    {
                        throw new ArgumentException("a dropped flag needs the seconds until it returns", nameof(returnSeconds));
                    }

                    return new LocalizedText("mode.ctf.dropped", name, HudFormat.DisplayedSeconds(returnSeconds.Value));
                default:
                    throw new ArgumentOutOfRangeException(nameof(state), state, "state is not a defined FlagState");
            }
        }

        /// <summary>
        /// この画面のプレイヤーが carried の陣営の旗を運んでいるときの案内。自陣の旗が台にあれば自陣の台へ運ぶよう促し、
        /// 無ければ台に戻るまで得点できないことを伝える (<see cref="CaptureTheFlag.ReachBase"/> は自陣の旗が台に無いと点を入れない)。
        /// </summary>
        /// <example>
        /// <c>Carrying(Side.Bravo, true)</c> は <c>BRAVO の旗を運んでいる: 自陣の台へ</c>、
        /// <c>Carrying(Side.Bravo, false)</c> は <c>BRAVO の旗を運んでいる: 自陣の旗が台に戻ると得点できる</c>
        /// </example>
        public static LocalizedText Carrying(Side carried, bool ownFlagAtBase)
        {
            return new LocalizedText(
                ownFlagAtBase ? "mode.ctf.carrying" : "mode.ctf.carryingWaitForFlag",
                ModeHudText.SideName(carried));
        }

        /// <summary>status の HUD の行 (Alpha・Bravo の旗の状態と、運んでいればその案内)。</summary>
        /// <exception cref="ArgumentNullException">status が null。</exception>
        public static LocalizedText[] HudLines(CaptureTheFlagStatus status)
        {
            if (status == null)
            {
                throw new ArgumentNullException(nameof(status));
            }

            var lines = new List<LocalizedText>(CaptureTheFlag.FlagSides.Count + 1);
            foreach (var side in CaptureTheFlag.FlagSides)
            {
                lines.Add(Flag(side, status.GetState(side), status.GetReturnSeconds(side)));
            }

            if (status.CarriedFlag.HasValue)
            {
                var carried = status.CarriedFlag.Value;
                var ownFlag = CaptureTheFlag.Opponent(carried);
                lines.Add(Carrying(carried, status.GetState(ownFlag) == FlagState.AtBase));
            }

            return lines.ToArray();
        }

        protected override void AppendModeLines(ModeStatus status, List<LocalizedText> lines)
        {
            if (status.Detail is CaptureTheFlagStatus flags)
            {
                lines.AddRange(HudLines(flags));
            }
        }

        // Edit Mode (テストやシーン生成) では Play の初期化が走らないので、エディターの読み込みでも登録する。
#if UNITY_EDITOR
        [UnityEditor.InitializeOnLoadMethod]
#endif
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Register()
        {
            ModeHudFormatters.Register<CaptureTheFlagStatus>(new CaptureTheFlagHudFormatter());
        }
    }
}
