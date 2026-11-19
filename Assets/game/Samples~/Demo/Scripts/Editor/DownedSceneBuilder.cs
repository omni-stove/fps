using System.Collections.Generic;
using Shooter.Core;
using Shooter.Runtime;
using UnityEngine;

namespace Shooter.Editor
{
    // ダウンは一人用の試合 (MatchRunner) では起きないので、ダウンを使うエクスキューションのモードの上に組む。
    public sealed class DownedSceneBuilder : ModeSceneBuilder
    {
        public const string SceneName = "Downed";

        public static readonly Vector3 DownedAllyFeet = new Vector3(-2f, 0f, 2f);
        public static readonly Vector3 DownedEnemyFeet = new Vector3(2f, 0f, 4f);
        public static readonly Vector3 StandingEnemyFeet = new Vector3(0f, 0f, DefaultFarSpawnZ);

        public static string ScenePathValue => BuildSceneList.DownedScene;
        public static string RulesPathValue => GameRulesAssetBuilder.ExecutionPath;

        public DownedSceneBuilder()
            : base(ScenePathValue, RulesPathValue)
        {
        }

        // 味方の bot 1 と敵の bot 1 はダウンした状態で始め、敵の bot 2 は立ったまま攻めてきてプレイヤーをダウンさせる。
        public static IReadOnlyList<ModeSideLayout> Layout()
        {
            return new[]
            {
                new ModeSideLayout(
                    new Side(0),
                    new[] { new ModeSpawnPlacement(PlayerFeet, Vector3.forward), new ModeSpawnPlacement(DownedAllyFeet, Vector3.forward) },
                    1),
                new ModeSideLayout(
                    new Side(1),
                    new[] { new ModeSpawnPlacement(DownedEnemyFeet, Vector3.back), new ModeSpawnPlacement(StandingEnemyFeet, Vector3.back) },
                    2),
            };
        }

        public static IReadOnlyList<string> DownedAtStartNames => new[] { BotName(new Side(0), 1), BotName(new Side(1), 1) };

        protected override IReadOnlyList<ModeSideLayout> Sides => Layout();

        public static void Build()
        {
            new DownedSceneBuilder().BuildSceneFromMenu();
        }

        protected override ModeRunnerBase AddRunner(GameObject host, ModeSceneContext context)
        {
            var runner = host.AddComponent<ExecutionRunner>();
            runner.WinsToWin = ExecutionRunner.DefaultWinsToWin;
            runner.PreparationSeconds = ExecutionRunner.DefaultPreparationSeconds;
            runner.RoundTimeLimitSeconds = ExecutionRunner.DefaultRoundTimeLimitSeconds;
            runner.ExecuteSeconds = ExecutionRunner.DefaultExecuteSeconds;
            runner.ExecuteRange = ExecutionRunner.DefaultExecuteRange;
            runner.DownedAtRoundStart = DownedAtStart(context.Bots);
            return runner;
        }

        public static List<SideMember> DownedAtStart(IReadOnlyList<EnemyAgent> bots)
        {
            var names = new HashSet<string>(DownedAtStartNames);
            var members = new List<SideMember>();
            foreach (var bot in bots)
            {
                if (bot != null && names.Contains(bot.name))
                {
                    members.Add(bot.GetComponent<SideMember>());
                }
            }

            if (members.Count != names.Count)
            {
                throw new System.InvalidOperationException(
                    $"the downed scene needs the bots {string.Join(", ", DownedAtStartNames)}, but found {members.Count} of them");
            }

            return members;
        }
    }
}
