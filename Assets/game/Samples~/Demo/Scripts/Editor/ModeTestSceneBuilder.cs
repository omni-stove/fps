using Shooter.Runtime;
using UnityEditor;
using UnityEngine;

namespace Shooter.Editor
{
    public sealed class ModeTestSceneBuilder : ModeSceneBuilder
    {
        public static string ScenePathValue => BuildSceneList.ModeTestScene;
        public static string RulesPathValue => GameRulesAssetBuilder.TeamDeathmatchPath;
        public const string MenuPath = SceneBuildSteps.MenuPath;

        public static string DefinitionPath => ModeDefinitionAssets.PathOf("TeamDeathmatch");

        public ModeTestSceneBuilder()
            : base(ScenePathValue, RulesPathValue)
        {
        }

        public static void Build()
        {
            new ModeTestSceneBuilder().BuildSceneFromMenu();
        }

        protected override ModeRunnerBase AddRunner(GameObject host, ModeSceneContext context)
        {
            // NewScene(Single) は参照されていないアセットをアンロードするので、シーンを作った後のここで読む。
            var definition = LoadOrCreateDefinition(DefinitionPath);

            var runner = host.AddComponent<TeamDeathmatchRunner>();
            runner.SideCount = 2;
            runner.Definition = definition;
            return runner;
        }

        public static TeamDeathmatchDefinition LoadOrCreateDefinition(string path)
        {
            return ModeDefinitionAssets.LoadOrCreate<TeamDeathmatchDefinition>(
                path,
                d => d.CopyFrom(new Shooter.Core.TeamDeathmatchSpec(
                    TeamDeathmatchRunner.DefaultScoreLimit,
                    TeamDeathmatchRunner.DefaultTimeLimitSeconds,
                    TeamDeathmatchRunner.DefaultRespawnSeconds)));
        }
    }
}
