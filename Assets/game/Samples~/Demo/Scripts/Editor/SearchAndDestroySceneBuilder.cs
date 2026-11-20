using System;
using System.Collections.Generic;
using Shooter.Runtime;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Shooter.Editor
{
    // 地図が左右対称なので、攻守が入れ替わっても出現点は陣営のまま変えない。
    public sealed class SearchAndDestroySceneBuilder : ModeSceneBuilder
    {
        public static string ScenePathValue => BuildSceneList.SearchAndDestroyScene;
        public static string RulesPathValue => GameRulesAssetBuilder.SearchAndDestroyPath;

        public static string DefinitionPath => ModeDefinitionAssets.PathOf(GameRulesAssetBuilder.SearchAndDestroyName);

        public const string EconomyName = "SearchAndDestroyEconomy";

        public static string EconomyPath => ModeDefinitionAssets.PathOf(EconomyName);

        public const string MenuPath = SceneBuildSteps.MenuPath;

        public const string SitesRootName = "BombSites";
        public const string BombObjectName = "Bomb";

        public const float SiteRadius = BombSite.DefaultRadius;

        public static readonly Vector3 BombSize = new Vector3(0.4f, 0.25f, 0.3f);

        // どちらも両陣営の出現点 (z = 0 と z = 40) の真ん中の z = 20 に置き、既定の遮蔽物 (中央の高い壁と、x = ±10 の低い壁) に掛からないようにする。
        public static readonly IReadOnlyList<Vector3> SiteCenters = new[]
        {
            new Vector3(-14f, 0f, 20f),
            new Vector3(14f, 0f, 20f),
        };

        public SearchAndDestroySceneBuilder()
            : base(ScenePathValue, RulesPathValue)
        {
        }

        public static void Build()
        {
            new SearchAndDestroySceneBuilder().BuildSceneFromMenu();
        }

        public static string SiteObjectName(int index)
        {
            return "BombSite-" + Shooter.Core.BombSiteName.Of(index);
        }

        protected override ModeRunnerBase AddRunner(GameObject host, ModeSceneContext context)
        {
            // NewScene(Single) は参照されていないアセットをアンロードするので、シーンを作った後のここで読む。
            var definition = LoadOrCreateDefinition(DefinitionPath);
            WireEconomy(definition, LoadOrCreateEconomy(EconomyPath, context.Catalog));

            var runner = host.AddComponent<SearchAndDestroyRunner>();
            runner.Definition = definition;
            return runner;
        }

        public static SearchAndDestroyDefinition LoadOrCreateDefinition(string path)
        {
            return ModeDefinitionAssets.LoadOrCreate<SearchAndDestroyDefinition>(
                path, d => d.CopyFrom(new Shooter.Core.SearchAndDestroySpec()));
        }

        // 額と品は Resources/Tuning/economy.json から読むので、アセットには武器のカタログだけを入れる。
        public static EconomyDefinition LoadOrCreateEconomy(string path, WeaponCatalogAsset catalog)
        {
            return ModeDefinitionAssets.LoadOrCreate<EconomyDefinition>(path, e => e.WeaponCatalog = catalog);
        }

        // 経済の欄が無かった頃に作った Definition にも経済を付けるため、空のときだけ埋める。ゲームが別の経済を入れていれば触らない。
        public static bool WireEconomy(SearchAndDestroyDefinition definition, EconomyDefinition economy)
        {
            if (definition == null)
            {
                throw new ArgumentNullException(nameof(definition));
            }

            if (definition.Economy != null || economy == null)
            {
                return false;
            }

            definition.Economy = economy;
            EditorUtility.SetDirty(definition);
            AssetDatabase.SaveAssets();
            Debug.Log($"{ModeDefinitionAssets.LogTag} wired the economy '{AssetDatabase.GetAssetPath(economy)}' to '{AssetDatabase.GetAssetPath(definition)}'");
            return true;
        }

        protected override void AddModeObjects(ModeSceneContext context)
        {
            if (!(context.Runner is SearchAndDestroyRunner runner))
            {
                throw new InvalidOperationException($"{GetType().Name}.AddRunner must add a SearchAndDestroyRunner");
            }

            SceneBuildHelpers.RequireOnNavMesh(SiteCenters, NavMeshCheckRadius, "bomb site");
            SceneBuildHelpers.RequireReachable(PlayerFeet, SiteCenters, NavMeshCheckRadius, "bomb site");

            var root = new GameObject(SitesRootName);
            SceneManager.MoveGameObjectToScene(root, context.Scene);

            var sites = new BombSite[SiteCenters.Count];
            for (var i = 0; i < sites.Length; i++)
            {
                var go = new GameObject(SiteObjectName(i));
                go.transform.SetParent(root.transform, false);
                go.transform.position = SiteCenters[i];

                var site = go.AddComponent<BombSite>();
                site.Index = i;
                site.Radius = SiteRadius;
                sites[i] = site;
            }

            runner.SetSites(sites);
            runner.Bomb = CreateBomb(context.Scene);
        }

        private static BombObject CreateBomb(Scene scene)
        {
            var bomb = GameObject.CreatePrimitive(PrimitiveType.Cube);
            bomb.name = BombObjectName;
            SceneManager.MoveGameObjectToScene(bomb, scene);

            // 持ち主の背中に付いて動くので、当たり判定があると持ち主の CharacterController や弾を遮る。
            UnityEngine.Object.DestroyImmediate(bomb.GetComponent<Collider>());
            bomb.transform.localScale = BombSize;
            bomb.transform.position = PlayerFeet + (Vector3.up * (BombSize.y * 0.5f));

            var renderer = bomb.GetComponent<Renderer>();
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return bomb.AddComponent<BombObject>();
        }
    }
}
