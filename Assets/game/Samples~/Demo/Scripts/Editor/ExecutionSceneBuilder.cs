using System.Collections.Generic;
using Shooter.Runtime;
using UnityEditor;
using UnityEngine;

namespace Shooter.Editor
{
    public sealed class ExecutionSceneBuilder : ModeSceneBuilder
    {
        public static string ScenePathValue => BuildSceneList.ExecutionScene;
        public static string RulesPathValue => GameRulesAssetBuilder.ExecutionPath;
        public const string MenuPath = SceneBuildSteps.MenuPath;

        // プレイヤーを合わせて EnemySideBots と同じ人数にする。
        public const int PlayerSideBots = 2;

        public const int EnemySideBots = 3;

        public ExecutionSceneBuilder()
            : base(ScenePathValue, RulesPathValue)
        {
        }

        protected override IReadOnlyList<ModeSideLayout> Sides => TwoSides(PlayerSideBots, EnemySideBots);

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
            runner.ExecuteRange = ExecutionRunner.DefaultExecuteRange;
            return runner;
        }
    }
}
