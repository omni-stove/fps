using System;
using System.Linq;
using Fps.Core;
using Fps.Runtime;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Fps.Editor
{
    /// <summary>
    /// ネット対戦の既定のマップ NetTest と、試合の設定 (<see cref="NetMatchDefinition"/>、<see cref="MatchDefinitionPath"/>) を生成し、
    /// シーンをビルド設定に加える。.NET のサーバーは書き出し (MapCollisionExporter・ServerRulesExporter) を読み、
    /// クライアントはつないだサーバーのマップとしてこのシーンへ移る。何度実行しても同じ結果になる。batchmode では
    /// <c>-batchmode -quit -projectPath . -executeMethod Fps.Editor.NetSceneBuilder.Build</c> で呼ぶ。
    /// </summary>
    public static class NetSceneBuilder
    {
        /// <summary>試合の設定。ServerRulesExporter がルールのアセットごとに server-data/rules に書き出す。</summary>
        public static string MatchDefinitionPath => FpsKitRoot.OutputPath("Rules/NetMatch.asset");

        /// <summary>
        /// 生成して保存するシーン。出力先ルート (<see cref="FpsKitRoot.Output"/>) の下の <see cref="BuildSceneList.NetTestScene"/>。
        /// </summary>
        public static string ScenePath => BuildSceneList.NetTestScene;

        /// <summary>プレイヤーのカプセルの高さと半径 (m)。<see cref="NetMotorSettings"/> の既定の寸法と同じ。</summary>
        public const float PlayerHeight = NetMotorSettings.DefaultStandingHeight;
        public const float PlayerRadius = NetMotorSettings.DefaultRadius;

        /// <summary>床 (Plane は 10m 四方) の拡大率。両陣地と東西の地点が収まる 60m 四方にする。</summary>
        public static readonly Vector3 GroundScale = new Vector3(6f, 1f, 6f);

        /// <summary>床の高さ。カプセルの中心が y=0 のとき足元が床に接する。</summary>
        public const float GroundY = -PlayerHeight / 2f;

        /// <summary>サーバーの Bot が経路を引く、NetTest の焼いた NavMesh (<see cref="ModeSceneBuilder.NavMeshDataPathFor"/> と同じ置き場所)。</summary>
        public static string NavMeshDataPath => ModeSceneBuilder.NavMeshDataPathFor(ScenePath);

        /// <summary>
        /// 出現地点が NavMesh に乗っているかを確かめるときに探す半径 (m)。出現地点はカプセルの中心 (床から <see cref="PlayerHeight"/> の半分上) なので、
        /// それより広くとる。
        /// </summary>
        public const float NavMeshCheckRadius = 1.5f;

        /// <summary>
        /// 出現地点。チームごとの陣地を南北に 2 か所ずつ、どちらも使える地点を東西に 1 か所ずつ置く。
        /// カプセルの中心が y=0 に来るので、<see cref="GroundY"/> の床にちょうど立つ。
        /// </summary>
        public static readonly (string Name, Vector3 Position, NetSpawnPoint.SpawnTeam Team)[] SpawnPoints =
        {
            ("Spawn Alpha 1", new Vector3(-4f, 0f, -22f), NetSpawnPoint.SpawnTeam.Alpha),
            ("Spawn Alpha 2", new Vector3(4f, 0f, -22f), NetSpawnPoint.SpawnTeam.Alpha),
            ("Spawn Bravo 1", new Vector3(-4f, 0f, 22f), NetSpawnPoint.SpawnTeam.Bravo),
            ("Spawn Bravo 2", new Vector3(4f, 0f, 22f), NetSpawnPoint.SpawnTeam.Bravo),
            ("Spawn Any West", new Vector3(-22f, 0f, 0f), NetSpawnPoint.SpawnTeam.Any),
            ("Spawn Any East", new Vector3(22f, 0f, 0f), NetSpawnPoint.SpawnTeam.Any),
        };

        /// <summary>
        /// カバーの確かめ用の壁 (ルールで Cover を On にしたとき張り付ける)。両陣地の前に、低い壁 (上端が床から 0.8m、
        /// <see cref="CoverProbe.LowWallHeight"/> 未満) と、端から覗ける高い壁 (2.5m) を 1 枚ずつ、相手の陣地に向けて置く。
        /// 位置は床の上の中心、大きさは Cube の拡大率。出現地点と陣地の間の通り道は塞がない。
        /// </summary>
        public static readonly (string Name, Vector3 Center, Vector3 Size)[] CoverWalls =
        {
            ("Cover Low Alpha", new Vector3(-9f, GroundY + 0.4f, -10f), new Vector3(4f, 0.8f, 0.4f)),
            ("Cover High Alpha", new Vector3(9f, GroundY + 1.25f, -10f), new Vector3(2f, 2.5f, 0.4f)),
            ("Cover Low Bravo", new Vector3(9f, GroundY + 0.4f, 10f), new Vector3(4f, 0.8f, 0.4f)),
            ("Cover High Bravo", new Vector3(-9f, GroundY + 1.25f, 10f), new Vector3(2f, 2.5f, 0.4f)),
        };

        /// <summary>
        /// 拠点制圧 (サーバーを <c>-rules Domination</c> で起動したとき) の拠点。両陣地の間、マップの中央に 1 つ置く。
        /// 位置は足元 (床の高さ) で、範囲の円柱はそこから上下に <see cref="ControlPointZone.DefaultHeight"/> の半分ずつ広がるので、
        /// 床に立つプレイヤーのカプセルの中心 (y=0) が入る。番号は HUD とログで 0 = A。
        /// </summary>
        public static readonly (string Name, Vector3 Position, int Index)[] ControlPoints =
        {
            ("Control Point A", new Vector3(0f, GroundY, 0f), 0),
        };

        [MenuItem("Fps/Net/Build NetTest Scene")]
        public static void Build()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                Debug.Log("[Net] NetSceneBuilder: cancelled");
                return;
            }

            var match = LoadOrBuildMatch();
            BuildScene();
            BuildSceneList.Register(ScenePath);
            AssetDatabase.SaveAssets();
            Debug.Log("[Net] NetSceneBuilder: wrote " + ScenePath + " (match " + MatchDefinitionPath + ", loadout "
                + AssetDatabase.GetAssetPath(match.Loadout) + ", catalog " + AssetDatabase.GetAssetPath(match.Catalog) + ")");
        }

        /// <summary>
        /// 試合の設定 (<see cref="MatchDefinitionPath"/>) を読む。無ければ既定の値に、デフォルトロードアウト
        /// (<see cref="WeaponAssetBuilder.LoadoutPath"/>) と武器の一覧 (<see cref="WeaponAssetBuilder.CatalogPath"/>) を配線して作る。
        /// 新しく作るときだけ、ルールの並びにチームデスマッチとドミネーション (<see cref="SampleRulesPaths"/>) を入れる。
        /// ほかの中身は上書きしない (ゲームが変えた値を残す)。ロードアウトが試合で使えなければ例外を投げる。
        /// </summary>
        public static NetMatchDefinition LoadOrBuildMatch()
        {
            var match = AssetDatabase.LoadAssetAtPath<NetMatchDefinition>(MatchDefinitionPath);
            if (match == null)
            {
                if (AssetDatabase.LoadAssetAtPath<LoadoutDefinition>(WeaponAssetBuilder.LoadoutPath) == null)
                {
                    Debug.Log("[Net] NetSceneBuilder: " + WeaponAssetBuilder.LoadoutPath + " is missing; running WeaponAssetBuilder.Build");
                    WeaponAssetBuilder.Build();
                }

                match = ScriptableObject.CreateInstance<NetMatchDefinition>();
                match.Loadout = WeaponAssetBuilder.LoadDefaultLoadout();
                match.Catalog = WeaponAssetBuilder.LoadCatalog();
                match.SetRules(LoadSampleRules());
                SceneBuildHelpers.EnsureFolder(System.IO.Path.GetDirectoryName(MatchDefinitionPath));
                AssetDatabase.CreateAsset(match, MatchDefinitionPath);
                AssetDatabase.SaveAssets();
                Debug.Log("[Net] NetSceneBuilder: created " + MatchDefinitionPath + " with the rules " + string.Join(", ", SampleRulesPaths));
            }

            if (!match.TryCreateLoadout(out _, out var error))
            {
                throw new InvalidOperationException("[Net] NetSceneBuilder: " + MatchDefinitionPath + " cannot be used by the match: " + error);
            }

            return match;
        }

        public static string[] SampleRulesPaths => new[] { GameRulesAssetBuilder.TeamDeathmatchPath, GameRulesAssetBuilder.DominationPath };

        private static GameRulesDefinition[] LoadSampleRules()
        {
            if (SampleRulesPaths.Any(p => AssetDatabase.LoadAssetAtPath<GameRulesDefinition>(p) == null))
            {
                Debug.Log("[Net] NetSceneBuilder: the sample rules are missing; running GameRulesAssetBuilder.Build");
                GameRulesAssetBuilder.Build();
            }

            return SampleRulesPaths.Select(GameRulesAssetBuilder.LoadRules).ToArray();
        }

        /// <summary>
        /// 自分のプレイヤーが居ない間 (つなぐ前やマップを読んだ直後) に映す、見下ろしのカメラを scene に置く。
        /// 自分のプレイヤーのカメラは手前に描くので、置いたままでよい。
        /// </summary>
        public static Camera CreateSpectatorCamera(Scene scene, Vector3 position, Quaternion rotation)
        {
            var cameraObject = new GameObject("Main Camera") { tag = "MainCamera" };
            SceneManager.MoveGameObjectToScene(cameraObject, scene);
            var camera = cameraObject.AddComponent<Camera>();
            cameraObject.transform.SetPositionAndRotation(position, rotation);
            return camera;
        }

        /// <summary>NetTest と同じ向きの平行光源を scene に置く。</summary>
        public static Light CreateSun(Scene scene)
        {
            var lightObject = new GameObject("Directional Light");
            SceneManager.MoveGameObjectToScene(lightObject, scene);
            var light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            lightObject.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            return light;
        }

        /// <summary>出現地点の目印を parent の下に置く。position はカプセルの中心 (足元は <see cref="PlayerHeight"/> の半分下)。</summary>
        public static NetSpawnPoint CreateSpawnPoint(Transform parent, string pointName, Vector3 position, NetSpawnPoint.SpawnTeam team)
        {
            if (parent == null)
            {
                throw new ArgumentNullException(nameof(parent));
            }

            var pointObject = new GameObject(pointName);
            pointObject.transform.SetParent(parent, false);
            pointObject.transform.localPosition = position;
            var point = pointObject.AddComponent<NetSpawnPoint>();
            point.Team = team;
            return point;
        }

        private static void BuildScene()
        {
            var scenePath = ScenePath;
            SceneBuildHelpers.EnsureFolder(System.IO.Path.GetDirectoryName(scenePath));

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // 両陣地のカプセルが見えるよう上から見下ろす
            CreateSpectatorCamera(scene, new Vector3(0f, 38f, -38f), Quaternion.Euler(45f, 0f, 0f));
            CreateSun(scene);

            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.position = new Vector3(0f, GroundY, 0f);
            ground.transform.localScale = GroundScale;

            var coverRoot = new GameObject("CoverWalls");
            foreach (var (wallName, center, size) in CoverWalls)
            {
                var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
                wall.name = wallName;
                wall.transform.SetParent(coverRoot.transform, false);
                wall.transform.localPosition = center;
                wall.transform.localScale = size;
            }

            var spawnRoot = new GameObject("SpawnPoints");
            foreach (var (pointName, position, team) in SpawnPoints)
            {
                CreateSpawnPoint(spawnRoot.transform, pointName, position, team);
            }

            foreach (var (pointName, position, index) in ControlPoints)
            {
                var pointObject = new GameObject(pointName);
                pointObject.transform.position = position;
                pointObject.AddComponent<ControlPointZone>().Index = index;
            }

            BakeNavMesh(ground);

            if (!EditorSceneManager.SaveScene(scene, scenePath))
            {
                throw new InvalidOperationException("failed to save " + scenePath);
            }
        }

        /// <summary>
        /// サーバーの Bot が経路を引く NavMesh を、床と壁から焼いて <see cref="NavMeshDataPath"/> に保存し、床の NavMeshSurface に持たせる
        /// (書き出し NavMeshExporter が読む)。どの出現地点も NavMesh に乗っていて、最初の出現地点から歩いて行けることを確かめる。
        /// </summary>
        private static void BakeNavMesh(GameObject ground)
        {
            var surface = SceneBuildHelpers.AddNavMeshSurface(ground, CollectObjects.All);
            SceneBuildHelpers.EnsureFolder(System.IO.Path.GetDirectoryName(NavMeshDataPath));
            SceneBuildHelpers.BakeNavMesh(surface);
            SceneBuildHelpers.SaveNavMeshData(surface, NavMeshDataPath);

            var spawns = SpawnPoints.Select(point => point.Position).ToList();
            SceneBuildHelpers.RequireOnNavMesh(spawns, NavMeshCheckRadius, "spawn point");
            SceneBuildHelpers.RequireReachable(spawns[0], spawns, NavMeshCheckRadius, "spawn point");
        }
    }
}
