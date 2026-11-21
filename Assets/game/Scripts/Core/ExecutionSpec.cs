using System;
using System.Collections.Generic;

namespace Shooter.Core
{
    // ラウンドの勝ち数・待ち・制限時間は Elimination と同じ NetMatchSettings の round* を使うので持たない。
    public sealed class ExecutionSpec
    {
        public const float DefaultExecuteSeconds = ExecutionSession.DefaultExecuteSeconds;

        // ダウン中の体は倒れて横に伸びるので、立った人どうしで測るネット対戦の蘇生 (ReviveReach.DefaultRange) より少し長くする。
        public const float DefaultExecuteRange = 2f;

        public const string ExecuteSecondsItem = "executeSeconds";
        public const string ExecuteRangeItem = "executeRange";

        public static IReadOnlyList<string> ItemNames { get; } = new[] { ExecuteSecondsItem, ExecuteRangeItem };

        public static ExecutionSpec Default { get; } = new ExecutionSpec();

        public float ExecuteSeconds { get; }

        public float ExecuteRange { get; }

        public ExecutionSpec(float executeSeconds = DefaultExecuteSeconds, float executeRange = DefaultExecuteRange)
        {
            if (!(executeSeconds > 0f) || float.IsInfinity(executeSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(executeSeconds), executeSeconds, "executeSeconds must be positive and finite");
            }

            if (!(executeRange > 0f) || float.IsInfinity(executeRange))
            {
                throw new ArgumentOutOfRangeException(nameof(executeRange), executeRange, "executeRange must be positive and finite");
            }

            ExecuteSeconds = executeSeconds;
            ExecuteRange = executeRange;
        }

        public bool SameAs(ExecutionSpec other)
        {
            return other != null && ExecuteSeconds == other.ExecuteSeconds && ExecuteRange == other.ExecuteRange;
        }
    }
}
