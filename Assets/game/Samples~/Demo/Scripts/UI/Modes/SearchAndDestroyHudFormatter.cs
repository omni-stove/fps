using System;
using System.Collections.Generic;
using Fps.Core;
using UnityEngine;

namespace Fps.Runtime
{
    /// <summary>
    /// 爆破の HUD の書式役。<see cref="ModeStatus.Detail"/> の <see cref="SearchAndDestroyStatus"/> を <see cref="SearchAndDestroyHudText"/> で文にする。
    /// 時計は待ち・ラウンドの残り・起爆までの秒数、得点の欄はラウンドの勝ち数と役割、その下に爆弾の様子、結果は試合が終わったときだけ出す。
    /// </summary>
    public sealed class SearchAndDestroyHudFormatter : IModeHudFormatter
    {
        /// <exception cref="ArgumentException">status の Detail が <see cref="SearchAndDestroyStatus"/> でない。</exception>
        public LocalizedText Time(ModeStatus status) => SearchAndDestroyHudText.Clock(DetailOf(status).Game);

        /// <exception cref="ArgumentException">status の Detail が <see cref="SearchAndDestroyStatus"/> でない。</exception>
        public IReadOnlyList<ModeHudScoreLine> Scores(ModeStatus status) => SearchAndDestroyHudText.Scores(DetailOf(status).Game, status.OwnSide);

        /// <exception cref="ArgumentException">status の Detail が <see cref="SearchAndDestroyStatus"/> でない。</exception>
        public void AppendLines(ModeStatus status, List<LocalizedText> lines)
        {
            if (lines == null)
            {
                throw new ArgumentNullException(nameof(lines));
            }

            var detail = DetailOf(status);
            lines.AddRange(SearchAndDestroyHudText.Lines(detail.Game, detail.LocalPlayerId));
        }

        /// <exception cref="ArgumentException">status の Detail が <see cref="SearchAndDestroyStatus"/> でない。</exception>
        public LocalizedText Result(ModeStatus status) => SearchAndDestroyHudText.Result(DetailOf(status).Game);

        private static SearchAndDestroyStatus DetailOf(ModeStatus status)
        {
            if (status == null)
            {
                throw new ArgumentNullException(nameof(status));
            }

            return status.Detail as SearchAndDestroyStatus
                ?? throw new ArgumentException(
                    $"{nameof(SearchAndDestroyHudFormatter)} formats a {nameof(SearchAndDestroyStatus)} detail, not {status.Detail?.GetType().Name ?? "null"}",
                    nameof(status));
        }

        // Edit Mode (テストやシーン生成) では Play の初期化が走らないので、エディターの読み込みでも登録する。
#if UNITY_EDITOR
        [UnityEditor.InitializeOnLoadMethod]
#endif
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Register()
        {
            ModeHudFormatters.Register<SearchAndDestroyStatus>(new SearchAndDestroyHudFormatter());
        }
    }
}
