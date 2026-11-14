using System;
using System.Collections.Generic;
using Fps.Runtime;
using UnityEditor;
using UnityEngine;

namespace Fps.Editor
{
    /// <summary>
    /// キルコンファームドのシーン (<see cref="BuildSceneList.KillConfirmedScene"/>) を組む。Runner は <see cref="KillConfirmedRunner"/>、
    /// ルールは <see cref="GameRulesAssetBuilder.KillConfirmedPath"/>。地形と遮蔽物は土台の既定のまま、2 チームが 3 人ずつ
    /// (プレイヤーの陣営 0 に bot 2 体、陣営 1 に bot 3 体) で向かい合う。
    /// bot にはタグだけを落とす <see cref="DropOnDeath"/> (<see cref="ConfigureBotDrop"/>) を付ける。タグは Runner が始めたときにフックで足し、
    /// 実行時に作られるプレイヤーの DropOnDeath も Runner が付ける。Editor を閉じた状態で次のように呼べる:
    /// <code>unity -batchmode -quit -projectPath . -executeMethod Fps.Editor.KillConfirmedSceneBuilder.Build</code>
    /// </summary>
    public sealed class KillConfirmedSceneBuilder : ModeSceneBuilder
    {
        public static string ScenePathValue => BuildSceneList.KillConfirmedScene;
        public static string RulesPathValue => GameRulesAssetBuilder.KillConfirmedPath;
        public const string MenuPath = "Fps/Build Kill Confirmed Scene";

        /// <summary>プレイヤーの陣営の bot の数。プレイヤーを合わせて <see cref="EnemySideBots"/> と同じ人数になる。</summary>
        public const int PlayerSideBots = 2;

        public const int EnemySideBots = 3;

        public KillConfirmedSceneBuilder()
            : base(ScenePathValue, RulesPathValue)
        {
        }

        protected override IReadOnlyList<ModeSideLayout> Sides => TwoSides(PlayerSideBots, EnemySideBots);

        [MenuItem(MenuPath)]
        public static void Build()
        {
            new KillConfirmedSceneBuilder().BuildSceneFromMenu();
        }

        /// <summary>
        /// bot の drop を、落とす物なし (タグは Runner が足す)・範囲に入ったら拾う・<see cref="KillConfirmedRunner.DefaultTagLifetimeSeconds"/> 秒で消える設定にする。
        /// 消えるまでの秒数は Runner がタグには使わない (モードが数える) が、Scene でタグ以外の物を足したときの値になる。
        /// </summary>
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
            var runner = host.AddComponent<KillConfirmedRunner>();
            runner.ScoreLimit = KillConfirmedRunner.DefaultScoreLimit;
            runner.TimeLimitSeconds = KillConfirmedRunner.DefaultTimeLimitSeconds;
            runner.RespawnSeconds = KillConfirmedRunner.DefaultRespawnSeconds;
            runner.TagLifetimeSeconds = KillConfirmedRunner.DefaultTagLifetimeSeconds;
            runner.TagRadius = KillConfirmedRunner.DefaultTagRadius;
            return runner;
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
