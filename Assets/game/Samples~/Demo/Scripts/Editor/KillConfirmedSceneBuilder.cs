using System;
using System.Collections.Generic;
using Shooter.Runtime;
using UnityEditor;
using UnityEngine;

namespace Shooter.Editor
{
    // タグは Runner が始めたときにフックで足し、実行時に作られるプレイヤーの DropOnDeath も Runner が付けるので、ここでは bot の DropOnDeath だけを置く。
    public sealed class KillConfirmedSceneBuilder : ModeSceneBuilder
    {
        public static string ScenePathValue => BuildSceneList.KillConfirmedScene;
        public static string RulesPathValue => GameRulesAssetBuilder.KillConfirmedPath;

        public static string DefinitionPath => ModeDefinitionAssets.PathOf(GameRulesAssetBuilder.KillConfirmedName);

        public const string MenuPath = SceneBuildSteps.MenuPath;

        // プレイヤーを合わせて EnemySideBots と同じ人数にする。
        public const int PlayerSideBots = 2;

        public const int EnemySideBots = 3;

        public KillConfirmedSceneBuilder()
            : base(ScenePathValue, RulesPathValue)
        {
        }

        protected override IReadOnlyList<ModeSideLayout> Sides => TwoSides(PlayerSideBots, EnemySideBots);

        public static void Build()
        {
            new KillConfirmedSceneBuilder().BuildSceneFromMenu();
        }

        // 消えるまでの秒数は Runner がタグには使わない (モードが数える) が、Scene でタグ以外の物を足したときの値になるので入れておく。
        public static void ConfigureBotDrop(DropOnDeath drop)
        {
            if (drop == null)
            {
                throw new ArgumentNullException(nameof(drop));
            }

            drop.Drops = Array.Empty<PickupContents>();
            drop.CollectMode = PickupCollectMode.Touch;
            drop.LifetimeSeconds = KillConfirmedRunner.DefaultTagLifetimeSeconds;
        }

        protected override ModeRunnerBase AddRunner(GameObject host, ModeSceneContext context)
        {
            // NewScene(Single) は参照されていないアセットをアンロードするので、シーンを作った後のここで読む。
            var definition = LoadOrCreateDefinition(DefinitionPath);

            var runner = host.AddComponent<KillConfirmedRunner>();
            runner.Definition = definition;
            runner.ScoreLimit = KillConfirmedRunner.DefaultScoreLimit;
            runner.TimeLimitSeconds = KillConfirmedRunner.DefaultTimeLimitSeconds;
            runner.RespawnSeconds = KillConfirmedRunner.DefaultRespawnSeconds;
            runner.TagLifetimeSeconds = KillConfirmedRunner.DefaultTagLifetimeSeconds;
            runner.PointsPerConfirm = KillConfirmedRunner.DefaultPointsPerConfirm;
            runner.TagRadius = KillConfirmedRunner.DefaultTagRadius;
            return runner;
        }

        public static KillConfirmedDefinition LoadOrCreateDefinition(string path)
        {
            return ModeDefinitionAssets.LoadOrCreate<KillConfirmedDefinition>(
                path,
                d => d.CopyFrom(new Shooter.Core.KillConfirmedSpec(
                    KillConfirmedRunner.DefaultScoreLimit,
                    KillConfirmedRunner.DefaultTimeLimitSeconds,
                    KillConfirmedRunner.DefaultRespawnSeconds,
                    KillConfirmedRunner.DefaultTagLifetimeSeconds,
                    KillConfirmedRunner.DefaultPointsPerConfirm,
                    KillConfirmedRunner.DefaultTagRadius)));
        }

        protected override void AddModeObjects(ModeSceneContext context)
        {
            foreach (var bot in context.Bots)
            {
                var drop = bot.GetComponent<DropOnDeath>();
                if (drop == null)
                {
                    drop = bot.gameObject.AddComponent<DropOnDeath>();
                }

                ConfigureBotDrop(drop);
            }
        }
    }
}
