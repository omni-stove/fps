using System.Collections.Generic;
using Shooter.Core;
using Shooter.Runtime;
using UnityEditor;
using UnityEngine;

namespace Shooter.Editor
{
    // まだ入口に並べないモードなので、モード名の一覧 (GameRulesAssetBuilder.ModeNames) と BuildSceneList.Scenes には載せず、ルールもここで作る。
    // bot はみな生存者の陣営に置く。感染者の陣営の出現点は、感染した人が戻る先として置く。
    public sealed class InfectedSceneBuilder : ModeSceneBuilder
    {
        public const string ModeName = "Infected";

        public const string MenuPath = "Shooter/Build Infected Scene";

        // プレイヤーを合わせて 5 人の生存者から始める。
        public const int SurvivorBots = 4;

        // 生存者が持つ rifle と分け、感染者は handgun に持ち替える。
        public const string InfectedWeaponId = WeaponAssetBuilder.HandgunId;

        public InfectedSceneBuilder()
            : base(ScenePathValue, RulesPathValue)
        {
        }

        public static string ScenePathValue => ShooterKitRoot.OutputScene(ModeName);

        public static string RulesPathValue => GameRulesAssetBuilder.Folder + "/" + ModeName + ".asset";

        public static string DefinitionPath => ModeDefinitionAssets.PathOf(ModeName);

        protected override IReadOnlyList<ModeSideLayout> Sides => TwoSides(SurvivorBots, 0);

        [MenuItem(MenuPath)]
        public static void Build()
        {
            LoadOrBuildRules(RulesPathValue);
            new InfectedSceneBuilder().BuildSceneFromMenu();
        }

        // 倒された人はすぐ感染者として戻るので、起こしてもらうダウンは使わない。
        public static GameRules InfectedRules()
        {
            return GameRulesAssetBuilder.TeamDeathmatchRules()
                .WithActions(GameRulesAssetBuilder.TeamDeathmatchActions().WithDowned(Downed.Off));
        }

        // ゲーム側で調整した値を残すよう、既にあれば値は変えない。
        public static GameRulesDefinition LoadOrBuildRules(string path)
        {
            path = SceneBuildHelpers.ValidateAssetPath(path);
            if (AssetDatabase.LoadAssetAtPath<GameRulesDefinition>(path) != null)
            {
                return GameRulesAssetBuilder.LoadRules(path);
            }

            var rules = InfectedRules();
            var built = SceneBuildHelpers.CreateOrUpdateAsset<GameRulesDefinition>(path, d => GameRulesAssetBuilder.Configure(d, rules));
            Debug.Log($"[{nameof(InfectedSceneBuilder)}] saved '{path}'");
            return built;
        }

        public static InfectedDefinition LoadOrCreateDefinition(string path, WeaponCatalogAsset catalog)
        {
            return ModeDefinitionAssets.LoadOrCreate<InfectedDefinition>(path, d =>
            {
                d.CopyFrom(InfectedSpec.Default);
                d.InfectedWeapon = WeaponAssetBuilder.FindInCatalog(catalog, ModeName, InfectedWeaponId);
            });
        }

        protected override ModeRunnerBase AddRunner(GameObject host, ModeSceneContext context)
        {
            // NewScene(Single) は参照されていないアセットをアンロードするので、シーンを作った後のここで読む。
            var definition = LoadOrCreateDefinition(DefinitionPath, context.Catalog);

            var runner = host.AddComponent<InfectedRunner>();
            runner.Definition = definition;
            runner.WeaponCatalog = context.Catalog;
            return runner;
        }
    }
}
