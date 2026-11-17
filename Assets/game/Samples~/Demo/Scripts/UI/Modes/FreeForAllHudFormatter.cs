using System.Collections.Generic;
using Shooter.Core;
using UnityEngine;

namespace Shooter.Runtime
{
    public sealed class FreeForAllHudFormatter : ModeHudFormatterBase
    {
        // 差の符号は表の文の側が持つので、差は符号を除いた数を引数に入れる。
        public static LocalizedText Rank(FreeForAllStanding standing)
        {
            var text = new LocalizedText("mode.freeForAll.rank", standing.Rank, standing.SideCount);
            if (!standing.Margin.HasValue)
            {
                return text;
            }

            var margin = standing.Margin.Value;
            if (margin > 0)
            {
                return new LocalizedText("mode.freeForAll.aheadOfSecond", text, margin);
            }

            return margin == 0
                ? new LocalizedText("mode.freeForAll.tiedForFirst", text)
                : new LocalizedText("mode.freeForAll.behindFirst", text, -margin);
        }

        protected override void AppendModeLines(ModeStatus status, List<LocalizedText> lines)
        {
            if (status.Detail is FreeForAllStanding standing)
            {
                lines.Add(Rank(standing));
            }
        }

        // Edit Mode (テストやシーン生成) では Play の初期化が走らないので、エディターの読み込みでも登録する。
#if UNITY_EDITOR
        [UnityEditor.InitializeOnLoadMethod]
#endif
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Register()
        {
            ModeHudFormatters.Register(typeof(FreeForAllStanding), new FreeForAllHudFormatter());
        }
    }
}
