using System.Collections.Generic;
using Shooter.Core;
using Shooter.Runtime;
using UnityEditor;
using UnityEngine;

namespace Shooter.Editor
{
    public sealed class GuardianSceneBuilder : ModeSceneBuilder
    {
        public static string ScenePathValue => BuildSceneList.GuardianScene;
        public static string RulesPathValue => GameRulesAssetBuilder.GuardianPath;
        public const string MenuPath = SceneBuildSteps.MenuPath;

        // リーダーが倒れた後に残りを倒し切れる程度に抑える。
        public const int TeamSize = 4;

        public GuardianSceneBuilder()
            : base(ScenePathValue, RulesPathValue)
        {
        }

        protected override IReadOnlyList<ModeSideLayout> Sides => TwoSides(TeamSize - 1, TeamSize);

        public static void Build()
        {
            new GuardianSceneBuilder().BuildSceneFromMenu();
        }

        protected override ModeRunnerBase AddRunner(GameObject host, ModeSceneContext context)
        {
            var runner = host.AddComponent<GuardianRunner>();
            runner.TimeLimitSeconds = GuardianRunner.DefaultTimeLimitSeconds;
            runner.RespawnSeconds = GuardianRunner.DefaultRespawnSeconds;
            return runner;
        }
    }
}
