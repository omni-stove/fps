using System;
using System.Collections.Generic;
using Fps.Core;
using Fps.Runtime;
using UnityEditor;
using UnityEngine;

namespace Fps.Editor
{
    /// <summary>
    /// ガンゲームのシーン GunGame を組む。全員が別の陣営 (プレイヤーが陣営 0、bot が 1 から <see cref="BotCount"/>) で撃ち合い、
    /// Runner は <see cref="GunGameRunner"/>、ルールは <see cref="GameRulesAssetBuilder.GunGamePath"/> (予備弾は無限)。
    /// 武器の並び (<see cref="GunGameLadderDefinition"/>) は <see cref="LadderPath"/> に作るか上書きして Runner に配線する。
    /// 並びは <see cref="LadderWeaponPaths"/> の順 (rifle → launcher → handgun) で、近接の武器はまだ無いので空 (戻す設定はオン)。
    /// 出現点は <see cref="SpawnPositions"/> の 8 か所を全陣営で共有し、陣営ごとに並びをずらして最初の点をばらけさせる
    /// (Runner は自陣営の点のうち敵からいちばん離れた点に戻すので、共有していれば生きている全員から離れた点に戻る)。
    /// Editor を閉じた状態で次のように呼べる (先に WeaponAssetBuilder.Build と GameRulesAssetBuilder.Build を走らせる):
    /// <code>unity -batchmode -quit -projectPath . -executeMethod Fps.Editor.GunGameSceneBuilder.Build</code>
    /// </summary>
    public sealed class GunGameSceneBuilder : ModeSceneBuilder
    {
        public static string ScenePathValue => BuildSceneList.GunGameScene;
        public static string RulesPathValue => GameRulesAssetBuilder.GunGamePath;
        public static string LadderPath => FpsKitRoot.OutputPath("Weapons/GunGameLadder.asset");
        public const string MenuPath = "Fps/Build Gun Game Scene";

        /// <summary>bot の数。1 体ずつ別の陣営に立てる。</summary>
        public const int BotCount = 5;

        /// <summary>武器の並び。0 段目は bot が最初に持つ武器 (<see cref="ModeSceneBuilder.BotWeaponId"/> の rifle) と揃える。</summary>
        public static IReadOnlyList<string> LadderWeaponPaths => new[]
        {
            WeaponAssetBuilder.RiflePath,
            WeaponAssetBuilder.LauncherPath,
            WeaponAssetBuilder.HandgunPath,
        };

        /// <summary>
        /// 全陣営で共有する出現点。先頭はプレイヤーの出現点 (原点で +Z 向き)。どれも遮蔽物 (<see cref="ModeSceneBuilder.DefaultObstacles"/>) と外周の壁から離し、
        /// 床の中ほど (z = 20) を向く。
        /// </summary>
        public static readonly IReadOnlyList<ModeSpawnPlacement> SpawnPositions = new[]
        {
            new ModeSpawnPlacement(new Vector3(0f, 0f, 0f), Vector3.forward),
            new ModeSpawnPlacement(new Vector3(-15f, 0f, 5f), Vector3.forward),
            new ModeSpawnPlacement(new Vector3(15f, 0f, 5f), Vector3.forward),
            new ModeSpawnPlacement(new Vector3(-15f, 0f, 35f), Vector3.back),
            new ModeSpawnPlacement(new Vector3(15f, 0f, 35f), Vector3.back),
            new ModeSpawnPlacement(new Vector3(0f, 0f, 40f), Vector3.back),
            new ModeSpawnPlacement(new Vector3(-16f, 0f, 20f), Vector3.right),
            new ModeSpawnPlacement(new Vector3(16f, 0f, 20f), Vector3.left),
        };

        public GunGameSceneBuilder()
            : base(ScenePathValue, RulesPathValue)
        {
        }

        protected override IReadOnlyList<ModeSideLayout> Sides => FreeForAllSides(BotCount);

        [MenuItem(MenuPath)]
        public static void Build()
        {
            new GunGameSceneBuilder().BuildSceneFromMenu();
        }

        /// <summary>
        /// プレイヤーの陣営 0 (bot なし) と、bot 1 体ずつの陣営 1 から botCount。どの陣営も <see cref="SpawnPositions"/> の全点を、
        /// 陣営の ID だけずらした順に持つ (陣営 n の最初の点は n 番目)。botCount は 0 以上、出現点の数未満。
        /// </summary>
        public static IReadOnlyList<ModeSideLayout> FreeForAllSides(int botCount)
        {
            if (botCount < 0 || botCount >= SpawnPositions.Count)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(botCount), botCount, $"botCount must be between 0 and {SpawnPositions.Count - 1} so that every side starts at its own spawn point");
            }

            var sides = new ModeSideLayout[botCount + 1];
            for (var s = 0; s < sides.Length; s++)
            {
                var spawns = new ModeSpawnPlacement[SpawnPositions.Count];
                for (var i = 0; i < spawns.Length; i++)
                {
                    spawns[i] = SpawnPositions[(s + i) % SpawnPositions.Count];
                }

                sides[s] = new ModeSideLayout(new Side(s), spawns, s == 0 ? 0 : 1);
            }

            return sides;
        }

        /// <summary>
        /// path に <see cref="LadderWeaponPaths"/> の並び (近接の武器なし・近接で戻す設定はオン) を作るか上書きして保存し、返す。
        /// 武器のアセットが無いか不正なら、何も保存せずに例外を投げる。
        /// </summary>
        public static GunGameLadderDefinition BuildLadder(string path)
        {
            path = SceneBuildHelpers.ValidateAssetPath(path);

            var tiers = new WeaponDefinition[LadderWeaponPaths.Count];
            for (var i = 0; i < tiers.Length; i++)
            {
                tiers[i] = WeaponAssetBuilder.LoadDefinition(LadderWeaponPaths[i]);
            }

            var ladder = SceneBuildHelpers.CreateOrUpdateAsset<GunGameLadderDefinition>(
                path, d => Configure(d, tiers, Array.Empty<WeaponDefinition>(), true));
            Debug.Log($"[{nameof(GunGameSceneBuilder)}] saved '{path}' ({tiers.Length} tier(s))");
            return ladder;
        }

        /// <summary>
        /// ladder の中身を tiers・meleeWeapons・demotesOnMelee にする。先にすべての武器を検査し、不正なら ladder を変えずに例外を投げる。
        /// </summary>
        /// <exception cref="ArgumentException">tiers が空か、tiers・meleeWeapons に null がある。</exception>
        /// <exception cref="WeaponDefinitionException">どれかの武器の定義が不正。</exception>
        public static void Configure(
            GunGameLadderDefinition ladder,
            IReadOnlyList<WeaponDefinition> tiers,
            IReadOnlyList<WeaponDefinition> meleeWeapons,
            bool demotesOnMelee)
        {
            if (ladder == null)
            {
                throw new ArgumentNullException(nameof(ladder));
            }

            if (tiers == null)
            {
                throw new ArgumentNullException(nameof(tiers));
            }

            if (meleeWeapons == null)
            {
                throw new ArgumentNullException(nameof(meleeWeapons));
            }

            if (tiers.Count == 0)
            {
                throw new ArgumentException("a gun game ladder needs at least one tier", nameof(tiers));
            }

            RequireValid(tiers, nameof(tiers));
            RequireValid(meleeWeapons, nameof(meleeWeapons));

            ladder.SetTiers(tiers);
            ladder.SetMeleeWeapons(meleeWeapons);
            ladder.DemotesOnMelee = demotesOnMelee;

            // 各武器は検査済みだが、アセットの形で読み直せることも確かめる。
            ladder.ToLadder();
        }

        protected override ModeRunnerBase AddRunner(GameObject host, ModeSceneContext context)
        {
            // NewScene(Single) は参照されていないアセットをアンロードするので、シーンを作った後のここで読む。
            var ladder = BuildLadder(LadderPath);

            var runner = host.AddComponent<GunGameRunner>();
            runner.Ladder = ladder;
            return runner;
        }

        private static void RequireValid(IReadOnlyList<WeaponDefinition> weapons, string parameterName)
        {
            for (var i = 0; i < weapons.Count; i++)
            {
                if (weapons[i] == null)
                {
                    throw new ArgumentException($"{parameterName} #{i} is empty", parameterName);
                }

                weapons[i].ToSpec();
            }
        }
    }
}
