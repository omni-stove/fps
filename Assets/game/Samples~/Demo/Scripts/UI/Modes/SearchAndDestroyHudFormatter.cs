using System;
using System.Collections.Generic;
using Shooter.Core;
using UnityEngine;

namespace Shooter.Runtime
{
    public sealed class SearchAndDestroyHudFormatter : IModeHudFormatter
    {
        public LocalizedText Time(ModeStatus status) => SearchAndDestroyHudText.Clock(DetailOf(status).Game);

        public IReadOnlyList<ModeHudScoreLine> Scores(ModeStatus status) => SearchAndDestroyHudText.Scores(DetailOf(status).Game, status.OwnSide);

        public void AppendLines(ModeStatus status, List<LocalizedText> lines)
        {
            if (lines == null)
            {
                throw new ArgumentNullException(nameof(lines));
            }

            var detail = DetailOf(status);
            lines.AddRange(SearchAndDestroyHudText.Lines(detail.Game, detail.LocalPlayerId));
        }

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
