using System.Collections.Generic;
using Fps.Runtime;
using UnityEditor;
using UnityEngine;

namespace Fps.Editor
{
    /// <summary>
    /// エクスキューションのシーン (<see cref="BuildSceneList.ExecutionScene"/>) を組む。Runner は <see cref="ExecutionRunner"/>、
    /// ルールは <see cref="GameRulesAssetBuilder.ExecutionPath"/> (ラウンド中は戻らない・ダウンして味方が起こせる)。
    /// 地形と遮蔽物は土台の既定のまま、2 チームが 3 人ずつ (プレイヤーの陣営 0 に bot 2 体、陣営 1 に bot 3 体) で向かい合う。
    /// Editor を閉じた状態で次のように呼べる:
    /// <code>unity -batchmode -quit -projectPath . -executeMethod Fps.Editor.ExecutionSceneBuilder.Build</code>
    /// </summary>
    public sealed class ExecutionSceneBuilder : ModeSceneBuilder
    {
        public static string ScenePathValue => BuildSceneList.ExecutionScene;
        public static string RulesPathValue => GameRulesAssetBuilder.ExecutionPath;
        public const string MenuPath = "Fps/Build Execution Scene";

        /// <summary>プレイヤーの陣営の bot の数。プレイヤーを合わせて <see cref="EnemySideBots"/> と同じ人数になる。</summary>
        public const int PlayerSideBots = 2;

        public const int EnemySideBots = 3;

        public ExecutionSceneBuilder()
            : base(ScenePathValue, RulesPathValue)
        {
        }

        protected override IReadOnlyList<ModeSideLayout> Sides => TwoSides(PlayerSideBots, EnemySideBots);

        [MenuItem(MenuPath)]
        public static void Build()
        {
            new ExecutionSceneBuilder().BuildSceneFromMenu();
        }

        protected override ModeRunnerBase AddRunner(GameObject host, ModeSceneContext context)
        {
            var runner = host.AddComponent<ExecutionRunner>();
            runner.WinsToWin = ExecutionRunner.DefaultWinsToWin;
            runner.PreparationSeconds = ExecutionRunner.DefaultPreparationSeconds;
            runner.RoundTimeLimitSeconds = ExecutionRunner.DefaultRoundTimeLimitSeconds;
            runner.ExecuteSeconds = ExecutionRunner.DefaultExecuteSeconds;
            runner.InteractRange = ExecutionRunner.DefaultInteractRange;
            return runner;
        }
    }
}
