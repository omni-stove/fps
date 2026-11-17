using System.Collections.Generic;
using Shooter.Core;
using UnityEngine;

namespace Shooter.Runtime
{
    /// <summary>
    /// フリーフォーオール (<see cref="FreeForAllRunner"/>) の HUD の書式役。<see cref="ModeStatus.Detail"/> の自分の順位 (<see cref="FreeForAllStanding"/>) を、
    /// リスポーンの行の後に順位の行 (<see cref="Rank"/>) にして出す。
    /// </summary>
    public sealed class FreeForAllHudFormatter : ModeHudFormatterBase
    {
        /// <summary>順位の行。表のキー <c>mode.freeForAll.*</c> の文で、差は符号を除いた数を引数に入れる。</summary>
        /// <example>
        /// 6 人中 3 位で 1 位と 4 点差なら <c>順位 3/6  1位と -4</c>、単独 1 位で 2 位と 2 点差なら <c>順位 1/6  2位と +2</c>、
        /// 1 位が並んでいれば <c>順位 1/6  1位と同点</c>、1 人だけなら <c>順位 1/1</c>
        /// </example>
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
