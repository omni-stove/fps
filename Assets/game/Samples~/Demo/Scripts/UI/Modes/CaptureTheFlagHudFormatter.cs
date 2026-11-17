using System;
using System.Collections.Generic;
using Shooter.Core;
using UnityEngine;

namespace Shooter.Runtime
{
    public sealed class CaptureTheFlagHudFormatter : ModeHudFormatterBase
    {
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

        // CaptureTheFlag.ReachBase は自陣の旗が台に無いと点を入れないので、そのときは台へ運ぶよう促さず、旗が戻るまで得点できないと伝える。
        public static LocalizedText Carrying(Side carried, bool ownFlagAtBase)
        {
            return new LocalizedText(
                ownFlagAtBase ? "mode.ctf.carrying" : "mode.ctf.carryingWaitForFlag",
                ModeHudText.SideName(carried));
        }

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
