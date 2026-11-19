using System;
using System.Collections.Generic;
using Shooter.Core;
using Shooter.Runtime;
using UnityEditor;
using UnityEngine;

namespace Shooter.Editor
{
    // bot が中央の遮蔽物の周りで互いに出会うよう、最初の点と中央を挟んだ向かいの点を往復させる。
    public sealed class FreeForAllSceneBuilder : ModeSceneBuilder
    {
        public static string ScenePathValue => BuildSceneList.FreeForAllScene;
        public static string RulesPathValue => GameRulesAssetBuilder.FreeForAllPath;
        public const string MenuPath = SceneBuildSteps.MenuPath;

        public const int BotCount = 5;

        public static readonly Vector3 ArenaCenter = DefaultFloorCenter;

        public static readonly IReadOnlyList<Vector3> SpawnPool = new[]
        {
            new Vector3(0f, 0f, 0f),
            new Vector3(-15f, 0f, 6f),
            new Vector3(15f, 0f, 6f),
            new Vector3(-15f, 0f, 34f),
            new Vector3(15f, 0f, 34f),
            new Vector3(0f, 0f, 40f),
        };

        public FreeForAllSceneBuilder()
            : base(ScenePathValue, RulesPathValue)
        {
        }

        public static void Build()
        {
            new FreeForAllSceneBuilder().BuildSceneFromMenu();
        }

        protected override IReadOnlyList<ModeSideLayout> Sides => Layout(BotCount);

        public static IReadOnlyList<ModeSideLayout> Layout(int botCount)
        {
            if (botCount < 0 || botCount >= SpawnPool.Count)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(botCount), botCount, $"botCount must be between 0 and {SpawnPool.Count - 1} so that every side starts at its own spawn point");
            }

            var sides = new ModeSideLayout[botCount + 1];
            for (var id = 0; id < sides.Length; id++)
            {
                var spawns = new ModeSpawnPlacement[SpawnPool.Count];
                for (var k = 0; k < spawns.Length; k++)
                {
                    var feet = SpawnPool[(id + k) % SpawnPool.Count];
                    spawns[k] = new ModeSpawnPlacement(feet, ArenaCenter - feet);
                }

                sides[id] = new ModeSideLayout(new Side(id), spawns, id == 0 ? 0 : 1);
            }

            return sides;
        }

        public static Vector3 Opposite(Vector3 point)
        {
            return new Vector3((2f * ArenaCenter.x) - point.x, point.y, (2f * ArenaCenter.z) - point.z);
        }

        protected override IReadOnlyList<Vector3> BotPatrolRoute(IReadOnlyList<ModeSideLayout> sides, int sideIndex, int spawnIndex)
        {
            var own = sides[sideIndex].Spawns[spawnIndex].Feet;
            return new[] { own, Opposite(own) };
        }

        protected override ModeRunnerBase AddRunner(GameObject host, ModeSceneContext context)
        {
            var runner = host.AddComponent<FreeForAllRunner>();
            runner.TimeLimitSeconds = FreeForAllMode.DefaultTimeLimitSeconds;
            runner.ScoreLimit = FreeForAllMode.DefaultScoreLimit;
            runner.RespawnSeconds = FreeForAllMode.DefaultRespawnSeconds;
            return runner;
        }
    }
}
