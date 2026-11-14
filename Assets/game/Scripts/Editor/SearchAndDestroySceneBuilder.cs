using System;
using System.Collections.Generic;
using Fps.Runtime;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Fps.Editor
{
    /// <summary>
    /// 爆破 (<see cref="SearchAndDestroyRunner"/>) のシーン SearchAndDestroy を組む。ルールは <see cref="GameRulesAssetBuilder.SearchAndDestroyPath"/>
    /// (リスポーンなし・ダウンなし)。配置は土台の既定 (プレイヤーの陣営 0 に bot 2 体、陣営 1 に bot 3 体で 3 対 3) に、
    /// 両陣営の出現点から同じ距離の左右に設置地点 A・B (<see cref="SiteCenters"/>) と、爆弾の見た目 (<see cref="BombObject"/>) を足す。
    /// 地図が左右対称なので、攻守が入れ替わっても出現点は陣営のまま変えない。爆弾の秒数や勝ち数は <see cref="DefinitionPath"/> のアセットを Runner に配線して読ませる
    /// (無ければ既定値で作り、あれば値を変えない)。Editor を閉じた状態で次のように呼べる:
    /// <code>unity -batchmode -quit -projectPath . -executeMethod Fps.Editor.SearchAndDestroySceneBuilder.Build</code>
    /// </summary>
    public sealed class SearchAndDestroySceneBuilder : ModeSceneBuilder
    {
        public static string ScenePathValue => BuildSceneList.SearchAndDestroyScene;
        public static string RulesPathValue => GameRulesAssetBuilder.SearchAndDestroyPath;

        /// <summary>Runner に配線する調整値のアセット (<see cref="SearchAndDestroyDefinition"/>)。</summary>
        public static string DefinitionPath => ModeDefinitionAssets.PathOf(GameRulesAssetBuilder.SearchAndDestroyName);

        public const string MenuPath = "Fps/Build Search And Destroy Scene";

        public const string SitesRootName = "BombSites";
        public const string BombObjectName = "Bomb";

        /// <summary>設置地点の範囲の半径 (m)。</summary>
        public const float SiteRadius = BombSite.DefaultRadius;

        /// <summary>爆弾の見た目の大きさ (m)。</summary>
        public static readonly Vector3 BombSize = new Vector3(0.4f, 0.25f, 0.3f);

        /// <summary>
        /// 設置地点 A (西) と B (東) の中心 (足元)。どちらも両陣営の出現点 (z = 0 と z = 40) の真ん中の z = 20 で、
        /// 既定の遮蔽物 (中央の高い壁と、x = ±10 の低い壁) に掛からない。
        /// </summary>
        public static readonly IReadOnlyList<Vector3> SiteCenters = new[]
        {
            new Vector3(-14f, 0f, 20f),
            new Vector3(14f, 0f, 20f),
        };

        public SearchAndDestroySceneBuilder()
            : base(ScenePathValue, RulesPathValue)
        {
        }

        [MenuItem(MenuPath)]
        public static void Build()
        {
            new SearchAndDestroySceneBuilder().BuildSceneFromMenu();
        }

        /// <summary>設置地点の GameObject の名前。</summary>
        /// <example><c>SiteObjectName(1)</c> は <c>BombSite-B</c></example>
        public static string SiteObjectName(int index)
        {
            return "BombSite-" + Fps.Core.SearchAndDestroyText.SiteName(index);
        }

        protected override ModeRunnerBase AddRunner(GameObject host, ModeSceneContext context)
        {
            // NewScene(Single) は参照されていないアセットをアンロードするので、シーンを作った後のここで読む。
            var definition = LoadOrCreateDefinition(DefinitionPath);

            var runner = host.AddComponent<SearchAndDestroyRunner>();
            runner.Definition = definition;
            return runner;
        }

        /// <summary>
        /// path の調整値のアセットを返す。無ければ <see cref="Fps.Core.SearchAndDestroySpec"/> の既定値で作る。既にあれば値は変えない。
        /// </summary>
        public static SearchAndDestroyDefinition LoadOrCreateDefinition(string path)
        {
            return ModeDefinitionAssets.LoadOrCreate<SearchAndDestroyDefinition>(
                path, d => d.CopyFrom(new Fps.Core.SearchAndDestroySpec()));
        }

        protected override void AddModeObjects(ModeSceneContext context)
        {
            if (!(context.Runner is SearchAndDestroyRunner runner))
            {
                throw new InvalidOperationException($"{GetType().Name}.AddRunner must add a SearchAndDestroyRunner");
            }

            // 焼き終えた NavMesh で、bot が地点へ歩いて行けることを確かめる (行けなければ保存しない)。
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
