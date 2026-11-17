using System;
using System.Collections.Generic;
using Shooter.Core;
using UnityEngine;

namespace Shooter.Runtime
{
    public sealed class ExecutionHudFormatter : IModeHudFormatter
    {
        public LocalizedText Time(ModeStatus status) => ModeHudText.Time(DetailOf(status).Session.Mode);

        public IReadOnlyList<ModeHudScoreLine> Scores(ModeStatus status) => ExecutionHudText.RoundWins(DetailOf(status).Session, status.OwnSide);

        public void AppendLines(ModeStatus status, List<LocalizedText> lines)
        {
            if (lines == null)
            {
                throw new ArgumentNullException(nameof(lines));
            }

            var detail = DetailOf(status);
            lines.AddRange(ExecutionHudText.Lines(detail.Session, detail.LocalPlayerId, detail.InteractTargetId));
        }

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
