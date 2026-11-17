using System;
using System.Collections.Generic;
using Fps.Core;
using UnityEngine;

namespace Fps.Runtime
{
    /// <summary>
    /// エクスキューションの HUD の書式役。<see cref="ModeStatus.Detail"/> の <see cref="ExecutionStatus"/> を <see cref="ExecutionHudText"/> で文にする。
    /// 時計はラウンドの残り、得点の欄は両チームのラウンドの勝ち数、その下にラウンドの番号・待ちの残り・両チームの残り人数・自分のダウンや長押しの進み、
    /// 結果は先取数に届いたときだけ出す。
    /// </summary>
    public sealed class ExecutionHudFormatter : IModeHudFormatter
    {
        /// <exception cref="ArgumentException">status の Detail が <see cref="ExecutionStatus"/> でない。</exception>
        public LocalizedText Time(ModeStatus status) => ModeHudText.Time(DetailOf(status).Session.Mode);

        /// <exception cref="ArgumentException">status の Detail が <see cref="ExecutionStatus"/> でない。</exception>
        public IReadOnlyList<ModeHudScoreLine> Scores(ModeStatus status) => ExecutionHudText.RoundWins(DetailOf(status).Session, status.OwnSide);

        /// <exception cref="ArgumentException">status の Detail が <see cref="ExecutionStatus"/> でない。</exception>
        public void AppendLines(ModeStatus status, List<LocalizedText> lines)
        {
            if (lines == null)
            {
                throw new ArgumentNullException(nameof(lines));
            }

            var detail = DetailOf(status);
            lines.AddRange(ExecutionHudText.Lines(detail.Session, detail.LocalPlayerId, detail.InteractTargetId));
        }

        /// <exception cref="ArgumentException">status の Detail が <see cref="ExecutionStatus"/> でない。</exception>
        public LocalizedText Result(ModeStatus status) => ExecutionHudText.Result(DetailOf(status).Session);

        private static ExecutionStatus DetailOf(ModeStatus status)
        {
            if (status == null)
            {
                throw new ArgumentNullException(nameof(status));
            }

            return status.Detail as ExecutionStatus
                ?? throw new ArgumentException(
                    $"{nameof(ExecutionHudFormatter)} formats an {nameof(ExecutionStatus)} detail, not {status.Detail?.GetType().Name ?? "null"}",
                    nameof(status));
        }

        // Edit Mode (テストやシーン生成) では Play の初期化が走らないので、エディターの読み込みでも登録する。
#if UNITY_EDITOR
        [UnityEditor.InitializeOnLoadMethod]
#endif
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Register()
        {
            ModeHudFormatters.Register<ExecutionStatus>(new ExecutionHudFormatter());
        }
    }
}
