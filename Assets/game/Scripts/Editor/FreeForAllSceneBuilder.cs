using System;
using System.Collections.Generic;
using Fps.Core;
using Fps.Runtime;
using UnityEditor;
using UnityEngine;

namespace Fps.Editor
{
    /// <summary>
    /// フリーフォーオールのシーン FreeForAll を組む。Runner は <see cref="FreeForAllRunner"/>、ルールは <see cref="GameRulesAssetBuilder.FreeForAllPath"/>。
    /// プレイヤー (陣営 0) と、それぞれ別の陣営 (1〜<see cref="BotCount"/>) の bot 1 体ずつが戦う。Editor を閉じた状態で次のように呼べる:
    /// <code>unity -batchmode -quit -projectPath . -executeMethod Fps.Editor.FreeForAllSceneBuilder.Build</code>
    /// </summary>
    /// <remarks>
    /// 出現点は床の中央 (<see cref="ArenaCenter"/>) を囲む <see cref="SpawnPool"/> の 6 か所で、どれも中央を向く。全陣営がこの 6 か所を出現点に持ち、
    /// 陣営 n は n 番目の点を先頭 (最初に立つ点) にする。倒れた後は 6 か所のうち生きている敵からいちばん遠い点に戻る。
    /// bot は最初の点と、中央を挟んだ向かいの点を往復するので、中央の遮蔽物の周りで互いに出会う。
    /// </remarks>
    public sealed class FreeForAllSceneBuilder : ModeSceneBuilder
    {
        public const string ScenePathValue = BuildSceneList.FreeForAllScene;
        public const string RulesPathValue = GameRulesAssetBuilder.FreeForAllPath;
        public const string MenuPath = "Fps/Build Free For All Scene";

        /// <summary>bot の数。それぞれが自分だけの陣営を持つ。</summary>
        public const int BotCount = 5;

        /// <summary>出現点が囲む床の中央。<see cref="ModeSceneBuilder.DefaultFloorCenter"/> と同じ。</summary>
        public static readonly Vector3 ArenaCenter = DefaultFloorCenter;

        /// <summary>
        /// 出現点 (足元)。添字 i と 5 - i が中央を挟んで向かい合う。先頭はプレイヤーが出る <see cref="ModeSceneBuilder.PlayerFeet"/>
        /// (中央を向くと +Z になる)。どれも外周の壁と遮蔽物 (<see cref="ModeSceneBuilder.DefaultObstacles"/>) から離してある。
        /// </summary>
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

        [MenuItem(MenuPath)]
        public static void Build()
        {
            new FreeForAllSceneBuilder().BuildSceneFromMenu();
        }

        protected override IReadOnlyList<ModeSideLayout> Sides => Layout(BotCount);

        /// <summary>
        /// プレイヤーの陣営 0 (bot なし) と、bot 1 体ずつの陣営 1〜botCount。陣営 n の出現点は <see cref="SpawnPool"/> を n 番目から順に回したもの。
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">botCount が負か、出現点の数 - 1 より多い。</exception>
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

        /// <summary>中央を挟んで point の向かいの点。<see cref="SpawnPool"/> の点なら向かいの点も SpawnPool にある。</summary>
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
