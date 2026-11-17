using System.Collections.Generic;
using Shooter.Core;
using Shooter.Runtime;
using UnityEditor;
using UnityEngine;

namespace Shooter.Editor
{
    /// <summary>
    /// ガーディアン (VIP の護衛) のシーン Guardian を組む。Runner は <see cref="GuardianRunner"/>、ルールは <see cref="GameRulesAssetBuilder.GuardianPath"/>。
    /// 配置は土台の既定の向かい合う 2 チームで、どちらも <see cref="TeamSize"/> 人 (プレイヤーの Alpha は bot が 1 体少ない)。
    /// Editor を閉じた状態で次のように呼べる:
    /// <code>unity -batchmode -quit -projectPath . -executeMethod Shooter.Editor.GuardianSceneBuilder.Build</code>
    /// </summary>
    public sealed class GuardianSceneBuilder : ModeSceneBuilder
    {
        public static string ScenePathValue => BuildSceneList.GuardianScene;
        public static string RulesPathValue => GameRulesAssetBuilder.GuardianPath;
        public const string MenuPath = "Shooter/Build Guardian Scene";

        /// <summary>1 チームの人数。リーダーが倒れた後に残りを倒し切れる程度に抑える。</summary>
        public const int TeamSize = 4;

        public GuardianSceneBuilder()
            : base(ScenePathValue, RulesPathValue)
        {
        }

        protected override IReadOnlyList<ModeSideLayout> Sides => TwoSides(TeamSize - 1, TeamSize);

        [MenuItem(MenuPath)]
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
