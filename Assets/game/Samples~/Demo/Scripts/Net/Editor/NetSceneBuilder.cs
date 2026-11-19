using System;
using System.Linq;
using Shooter.Core;
using Shooter.Runtime;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Shooter.Editor
{
    public static class NetSceneBuilder
    {
        public static string MatchDefinitionPath => ShooterKitRoot.OutputPath("Rules/NetMatch.asset");

        public static string ScenePath => BuildSceneList.NetTestScene;

        public const float PlayerHeight = NetMotorSettings.DefaultStandingHeight;
        public const float PlayerRadius = NetMotorSettings.DefaultRadius;

        // Plane は 10m 四方なので、両陣地と東西の地点が収まる 60m 四方になるよう 6 倍にする。
        public static readonly Vector3 GroundScale = new Vector3(6f, 1f, 6f);

        // 出現地点はカプセルの中心を y=0 に置くので、足元が床に接するよう半分下げる。
        public const float GroundY = -PlayerHeight / 2f;

        public static string NavMeshDataPath => ModeSceneBuilder.NavMeshDataPathFor(ScenePath);

        // 出現地点はカプセルの中心 (床から PlayerHeight の半分上) なので、NavMesh を探す半径はそれより広くとる。
        public const float NavMeshCheckRadius = 1.5f;

        public static readonly (string Name, Vector3 Position, NetSpawnPoint.SpawnTeam Team)[] SpawnPoints =
        {
            ("Spawn Alpha 1", new Vector3(-4f, 0f, -22f), NetSpawnPoint.SpawnTeam.Alpha),
            ("Spawn Alpha 2", new Vector3(4f, 0f, -22f), NetSpawnPoint.SpawnTeam.Alpha),
            ("Spawn Bravo 1", new Vector3(-4f, 0f, 22f), NetSpawnPoint.SpawnTeam.Bravo),
            ("Spawn Bravo 2", new Vector3(4f, 0f, 22f), NetSpawnPoint.SpawnTeam.Bravo),
            ("Spawn Any West", new Vector3(-22f, 0f, 0f), NetSpawnPoint.SpawnTeam.Any),
            ("Spawn Any East", new Vector3(22f, 0f, 0f), NetSpawnPoint.SpawnTeam.Any),
        };

        // 低い壁の上端 (床から 0.8m) は MotorSpec.DefaultCoverLowWallHeight 未満にして低いカバーとして扱わせ、高い壁は端から覗けるようにする。
        // 出現地点と陣地の間の通り道は塞がない位置に置く。
        public static readonly (string Name, Vector3 Center, Vector3 Size)[] CoverWalls =
        {
            ("Cover Low Alpha", new Vector3(-9f, GroundY + 0.4f, -10f), new Vector3(4f, 0.8f, 0.4f)),
            ("Cover High Alpha", new Vector3(9f, GroundY + 1.25f, -10f), new Vector3(2f, 2.5f, 0.4f)),
            ("Cover Low Bravo", new Vector3(9f, GroundY + 0.4f, 10f), new Vector3(4f, 0.8f, 0.4f)),
            ("Cover High Bravo", new Vector3(-9f, GroundY + 1.25f, 10f), new Vector3(2f, 2.5f, 0.4f)),
        };

        // 範囲の円柱は位置から上下に ControlPointZone.DefaultHeight の半分ずつ広がるので、位置を足元 (床の高さ) にすれば床に立つカプセルの中心 (y=0) が入る。
        public static readonly (string Name, Vector3 Position, int Index)[] ControlPoints =
        {
            ("Control Point A", new Vector3(0f, GroundY, 0f), 0),
        };

        public const string FlagBasesRootName = CaptureTheFlagSceneBuilder.BasesRootName;

        // 旗取りのサーバーは両チームの台が無いマップを断るので、どのルールでも置いておく。CaptureTheFlag.FlagSides の順 (Alpha、Bravo) に、自陣の出現地点の奥の中央へ置く。
        // 台は足元 (床の高さ) に置き、範囲の円盤を床に出す。上下の差は CaptureTheFlag.ReachHeight 以内なので、床に立つカプセルの中心 (y=0) も届く。
        public static readonly (Side Side, Vector3 Position)[] FlagBases =
        {
            (Side.Alpha, new Vector3(0f, GroundY, -26f)),
            (Side.Bravo, new Vector3(0f, GroundY, 26f)),
        };

        [MenuItem("Shooter/Net/Build NetTest Scene")]
        public static void Build()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                Debug.Log("[Net] NetSceneBuilder: cancelled");
                return;
            }

            var match = LoadOrBuildMatch();
            BuildScene(CaptureRadius(match));
            BuildSceneList.Register(ScenePath);
            AssetDatabase.SaveAssets();
            Debug.Log("[Net] NetSceneBuilder: wrote " + ScenePath + " (match " + MatchDefinitionPath + ", loadout "
                + AssetDatabase.GetAssetPath(match.Loadout) + ", catalog " + AssetDatabase.GetAssetPath(match.Catalog) + ")");
        }

        // 既にある試合の設定はルールの並びなどを上書きしないのは、ゲームが変えた値を残すため。
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

        // 自分のプレイヤーのカメラは手前に描くので、プレイヤーが出てきてもこのカメラは消さずに置いたままでよい。
        public static Camera CreateSpectatorCamera(Scene scene, Vector3 position, Quaternion rotation)
        {
            var cameraObject = new GameObject("Main Camera") { tag = "MainCamera" };
            SceneManager.MoveGameObjectToScene(cameraObject, scene);
            var camera = cameraObject.AddComponent<Camera>();
            cameraObject.transform.SetPositionAndRotation(position, rotation);
            return camera;
        }

        public static Light CreateSun(Scene scene)
        {
            var lightObject = new GameObject("Directional Light");
            SceneManager.MoveGameObjectToScene(lightObject, scene);
            var light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            lightObject.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            return light;
        }

        public static string FlagBaseName(Side side)
        {
            return CaptureTheFlagSceneBuilder.BaseObjectName(side);
        }

        // 判定の半径はサーバーが書き出したルールの値で決めるので、シーンの台の半径はクライアントに見せる円盤の大きさにしか使われない。サーバーへ渡すのと同じ値に揃える。
        public static float CaptureRadius(NetMatchDefinition match)
        {
            if (match == null)
            {
                throw new ArgumentNullException(nameof(match));
            }

            return match.ToSettings().CaptureTheFlag.CaptureRadius;
        }

        // 旗そのもの (FlagObject) はオフラインのランナーが動かすもので、オンラインでは置いても台に立ったまま動かないので、台だけを置く。
        public static GameObject CreateFlagBases(Scene scene, System.Collections.Generic.IReadOnlyList<(Side Side, Vector3 Position)> bases, float radius)
        {
            SceneBuildHelpers.RequireLoaded(scene);
            if (bases == null)
            {
                throw new ArgumentNullException(nameof(bases));
            }

            foreach (var flagSide in CaptureTheFlag.FlagSides)
            {
                if (bases.Count(b => b.Side == flagSide) != 1)
                {
                    throw new ArgumentException("needs exactly one flag base for " + flagSide, nameof(bases));
                }
            }

            var root = new GameObject(FlagBasesRootName);
            SceneManager.MoveGameObjectToScene(root, scene);
            try
            {
                foreach (var (side, position) in bases)
                {
                    var baseObject = new GameObject(FlagBaseName(side));
                    baseObject.transform.SetParent(root.transform, false);
                    baseObject.transform.SetPositionAndRotation(position, Quaternion.identity);
                    var flagBase = baseObject.AddComponent<FlagBase>();
                    flagBase.Side = side;
                    flagBase.Radius = radius;
                }

                return root;
            }
            catch
            {
                UnityEngine.Object.DestroyImmediate(root);
                throw;
            }
        }

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

        private static void BuildScene(float captureRadius)
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

            CreateFlagBases(scene, FlagBases, captureRadius);

            BakeNavMesh(ground);

            if (!EditorSceneManager.SaveScene(scene, scenePath))
            {
                throw new InvalidOperationException("failed to save " + scenePath);
            }
        }

        // サーバーへの書き出し NavMeshExporter は床の NavMeshSurface から読むので、焼いたデータをそこに持たせる。
        private static void BakeNavMesh(GameObject ground)
        {
            var surface = SceneBuildHelpers.AddNavMeshSurface(ground, CollectObjects.All);
            SceneBuildHelpers.EnsureFolder(System.IO.Path.GetDirectoryName(NavMeshDataPath));
            SceneBuildHelpers.BakeNavMesh(surface);
            SceneBuildHelpers.SaveNavMeshData(surface, NavMeshDataPath);

            var spawns = SpawnPoints.Select(point => point.Position).ToList();
            SceneBuildHelpers.RequireOnNavMesh(spawns, NavMeshCheckRadius, "spawn point");
            SceneBuildHelpers.RequireReachable(spawns[0], spawns, NavMeshCheckRadius, "spawn point");

            var bases = FlagBases.Select(flagBase => flagBase.Position).ToList();
            SceneBuildHelpers.RequireOnNavMesh(bases, NavMeshCheckRadius, "flag base");
            SceneBuildHelpers.RequireReachable(spawns[0], bases, NavMeshCheckRadius, "flag base");
        }
    }
}
