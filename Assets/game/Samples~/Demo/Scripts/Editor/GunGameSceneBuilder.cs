using System;
using System.Collections.Generic;
using Shooter.Core;
using Shooter.Runtime;
using UnityEditor;
using UnityEngine;

namespace Shooter.Editor
{
    public sealed class GunGameSceneBuilder : ModeSceneBuilder
    {
        public static string ScenePathValue => BuildSceneList.GunGameScene;
        public static string RulesPathValue => GameRulesAssetBuilder.GunGamePath;
        public static string LadderPath => ShooterKitRoot.OutputPath("Weapons/GunGameLadder.asset");
        public const string MenuPath = "Shooter/Build Gun Game Scene";

        public const int BotCount = 5;

        // 0 段目は bot が最初に持つ武器 (ModeSceneBuilder.BotWeaponId の rifle) と揃える。
        public static IReadOnlyList<string> LadderWeaponPaths => new[]
        {
            WeaponAssetBuilder.RiflePath,
            WeaponAssetBuilder.LauncherPath,
            WeaponAssetBuilder.HandgunPath,
        };

        public static IReadOnlyList<string> MeleeWeaponPaths => new[]
        {
            WeaponAssetBuilder.KnifePath,
        };

        // 出現点を全陣営で共有するのは、Runner が自陣営の点のうち敵からいちばん離れた点に戻すので、共有していれば生きている全員から離れた点に戻るため。
        // 最初の点は陣営ごとに並びをずらしてばらけさせる。
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

        public static GunGameLadderDefinition BuildLadder(string path)
        {
            path = SceneBuildHelpers.ValidateAssetPath(path);

            var tiers = new WeaponDefinition[LadderWeaponPaths.Count];
            for (var i = 0; i < tiers.Length; i++)
            {
                tiers[i] = WeaponAssetBuilder.LoadDefinition(LadderWeaponPaths[i]);
            }

            var melee = new WeaponDefinition[MeleeWeaponPaths.Count];
            for (var i = 0; i < melee.Length; i++)
            {
                melee[i] = WeaponAssetBuilder.LoadDefinition(MeleeWeaponPaths[i]);
            }

            var ladder = SceneBuildHelpers.CreateOrUpdateAsset<GunGameLadderDefinition>(
                path, d => Configure(d, tiers, melee, true));
            Debug.Log($"[{nameof(GunGameSceneBuilder)}] saved '{path}' ({tiers.Length} tier(s), {melee.Length} melee weapon(s))");
            return ladder;
        }

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
