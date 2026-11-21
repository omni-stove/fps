using System;
using System.Collections.Generic;
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

        public const string LogTag = "[Net] NetSceneBuilder:";

        // -map はシーンのファイル名でマップを探すので、ファイル名と同じにする
        public const string BattleRoyaleMapName = "NetBattleRoyale";

        public static string BattleRoyaleScenePath => BuildSceneList.NetBattleRoyaleScene;

        public static string BattleRoyaleNavMeshDataPath => ModeSceneBuilder.NavMeshDataPathFor(BattleRoyaleScenePath);

        public const string BattleRoyaleSpawnRootName = "SpawnPoints";
        public const string BattleRoyaleCoverRootName = "CoverWalls";
        public const string LootContainersRootName = "LootContainers";
        public const string ExtractionPointsRootName = "ExtractionPoints";
        public const string ZoneObjectName = "Zone";

        // Plane は 10m 四方なので、16 倍で 160m 四方にする。
        public const float BattleRoyaleHalfExtent = 80f;

        public static readonly Vector3 BattleRoyaleGroundScale = new Vector3(BattleRoyaleHalfExtent / 5f, 1f, BattleRoyaleHalfExtent / 5f);

        // どの出現点 (中心から 60m 前後) も中に入り、床の四隅 (約 113m) は外に出る大きさにする。
        // 縮み方の段階はサーバーが調整値から決め、地点ファイルにはこの最初の円だけを書き出す。
        public const float BattleRoyaleZoneRadius = 75f;

        public static readonly Vector3 BattleRoyaleZoneCenter = new Vector3(0f, GroundY, 0f);

        // 分隊は試合の目的が決めるので、どの出現点もチームを決めない。
        public static readonly IReadOnlyList<(string Name, Vector3 Position)> BattleRoyaleSpawnPoints = new[]
        {
            ("Spawn South", new Vector3(0f, 0f, -60f)),
            ("Spawn South East", new Vector3(42.5f, 0f, -42.5f)),
            ("Spawn East", new Vector3(60f, 0f, 0f)),
            ("Spawn North East", new Vector3(42.5f, 0f, 42.5f)),
            ("Spawn North", new Vector3(0f, 0f, 60f)),
            ("Spawn North West", new Vector3(-42.5f, 0f, 42.5f)),
            ("Spawn West", new Vector3(-60f, 0f, 0f)),
            ("Spawn South West", new Vector3(-42.5f, 0f, -42.5f)),
        };

        // 表を新しく作らずに Demo の Extraction のシーンの表 (ExtractionSceneBuilder) を引くので、その Id を使う。
        public static readonly IReadOnlyList<(string Id, Vector3 Position, string TableId)> BattleRoyaleLootContainers = new[]
        {
            ("net-br-box-01", new Vector3(0f, GroundY, -30f), ExtractionSceneBuilder.SuppliesTable),
            ("net-br-box-02", new Vector3(21.2f, GroundY, -21.2f), ExtractionSceneBuilder.SuppliesTable),
            ("net-br-box-03", new Vector3(30f, GroundY, 0f), ExtractionSceneBuilder.SuppliesTable),
            ("net-br-box-04", new Vector3(21.2f, GroundY, 21.2f), ExtractionSceneBuilder.SuppliesTable),
            ("net-br-box-05", new Vector3(0f, GroundY, 30f), ExtractionSceneBuilder.SuppliesTable),
            ("net-br-box-06", new Vector3(-21.2f, GroundY, 21.2f), ExtractionSceneBuilder.SuppliesTable),
            ("net-br-box-07", new Vector3(-30f, GroundY, 0f), ExtractionSceneBuilder.SuppliesTable),
            ("net-br-box-08", new Vector3(-21.2f, GroundY, -21.2f), ExtractionSceneBuilder.SuppliesTable),
            ("net-br-box-09", new Vector3(8f, GroundY, 8f), ExtractionSceneBuilder.ValuablesTable),
            ("net-br-box-10", new Vector3(-8f, GroundY, 8f), ExtractionSceneBuilder.ValuablesTable),
            ("net-br-box-11", new Vector3(-8f, GroundY, -8f), ExtractionSceneBuilder.ValuablesTable),
            ("net-br-box-12", new Vector3(8f, GroundY, -8f), ExtractionSceneBuilder.ValuablesTable),
        };

        // 脱出地点は出現点の輪より外の 4 方に、床の印として置く。どれが開いても箱から歩かせる。
        public static readonly IReadOnlyList<(string Id, Vector3 Position)> BattleRoyaleExtractionPoints = new[]
        {
            ("net-br-exit-north", new Vector3(0f, GroundY, 70f)),
            ("net-br-exit-east", new Vector3(70f, GroundY, 0f)),
            ("net-br-exit-south", new Vector3(0f, GroundY, -70f)),
            ("net-br-exit-west", new Vector3(-70f, GroundY, 0f)),
        };

        // 出現点と箱の間で見通しを切る。低い壁は NetTest と同じく MotorSpec.DefaultCoverLowWallHeight より低くする。
        public static readonly IReadOnlyList<(string Name, Vector3 Center, Vector3 Size)> BattleRoyaleCoverWalls = new[]
        {
            ("Cover High North", new Vector3(0f, GroundY + 1.25f, 45f), new Vector3(6f, 2.5f, 0.5f)),
            ("Cover High East", new Vector3(45f, GroundY + 1.25f, 0f), new Vector3(0.5f, 2.5f, 6f)),
            ("Cover High South", new Vector3(0f, GroundY + 1.25f, -45f), new Vector3(6f, 2.5f, 0.5f)),
            ("Cover High West", new Vector3(-45f, GroundY + 1.25f, 0f), new Vector3(0.5f, 2.5f, 6f)),
            ("Cover Low North East", new Vector3(32f, GroundY + 0.4f, 32f), new Vector3(4f, 0.8f, 0.4f)),
            ("Cover Low North West", new Vector3(-32f, GroundY + 0.4f, 32f), new Vector3(4f, 0.8f, 0.4f)),
            ("Cover Low South West", new Vector3(-32f, GroundY + 0.4f, -32f), new Vector3(4f, 0.8f, 0.4f)),
            ("Cover Low South East", new Vector3(32f, GroundY + 0.4f, -32f), new Vector3(4f, 0.8f, 0.4f)),
        };

        public static string MapName => MapCollisionExporter.MapName(ScenePath);

        public static string FeatureLayoutPath => NetMapFeatureBuilder.LayoutPath(MapName);

        public static string BattleRoyaleFeatureLayoutPath => NetMapFeatureBuilder.LayoutPath(BattleRoyaleMapName);

        // 出現地点の上に塔や乗り物が重ならないよう、カプセルの半径より広く空ける。
        public const float FeatureSpawnClearance = 2f;

        // NetTest は北西にはしごの塔、北東に水場、南西に車を置き、陣地の間の通り道と拠点・旗の台からは離す。
        // 位置はマップの JSON (FeatureLayoutPath) を作るときの既定で、作った後は JSON の値を使う。
        public static NetMapFeatureLayout DefaultFeatures => new NetMapFeatureLayout(
            new[] { new NetMapLadderTower("Ladder Tower West", new Vector3(-20f, GroundY, 15f), new Vector3(4f, 3f, 4f), NetMapFeatureLayout.East, 0.8f) },
            new[] { new NetMapWaterPool("Pond East", new Vector3(20f, GroundY, 15f), new Vector3(8f, 3f, 8f), 2.5f) },
            new[] { new NetMapVehicleSpot("Car West", NetMapFeatureBuilder.CarKind, new Vector3(-20f, GroundY, -15f), 0f) });

        // BR は出現点の輪と箱の輪の間に、北東にはしごの塔、南西に池、南と北西に車を置く。
        public static NetMapFeatureLayout BattleRoyaleDefaultFeatures => new NetMapFeatureLayout(
            new[]
            {
                new NetMapLadderTower("Ladder Tower North East", new Vector3(15f, GroundY, 52f), new Vector3(4f, 3f, 4f), NetMapFeatureLayout.South, 0.8f),
            },
            new[] { new NetMapWaterPool("Lake South West", new Vector3(-50f, GroundY, -20f), new Vector3(12f, 3f, 12f), 2.5f) },
            new[]
            {
                new NetMapVehicleSpot("Car South", NetMapFeatureBuilder.CarKind, new Vector3(20f, GroundY, -50f), 0f),
                new NetMapVehicleSpot("Car North West", NetMapFeatureBuilder.CarKind, new Vector3(-20f, GroundY, 50f), 180f),
            });

        public static readonly Vector3 BattleRoyaleSpectatorPosition = new Vector3(0f, 90f, -90f);

        public static readonly Quaternion BattleRoyaleSpectatorRotation = Quaternion.Euler(45f, 0f, 0f);

        public static void Build()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                Debug.Log("[Net] NetSceneBuilder: cancelled");
                return;
            }

            var match = LoadOrBuildMatch();
            var captureRadius = CaptureRadius(match);
            var features = LoadFeatures(captureRadius);
            BuildScene(captureRadius, features);
            BuildSceneList.Register(ScenePath);
            AssetDatabase.SaveAssets();
            Debug.Log("[Net] NetSceneBuilder: wrote " + ScenePath + " (match " + MatchDefinitionPath + ", loadout "
                + AssetDatabase.GetAssetPath(match.Loadout) + ", catalog " + AssetDatabase.GetAssetPath(match.Catalog) + ", "
                + features.LadderTowers.Count + " ladder tower(s), " + features.WaterPools.Count + " water pool(s), "
                + features.Vehicles.Count + " vehicle(s) from " + FeatureLayoutPath + ")");
        }

        public static void BuildBattleRoyale()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                Debug.Log(LogTag + " cancelled");
                return;
            }

            BuildBattleRoyaleAt(BattleRoyaleScenePath, BattleRoyaleNavMeshDataPath);
        }

        public static void BuildBattleRoyaleAt(string scenePath, string navMeshDataPath)
        {
            scenePath = BootstrapSceneBuilder.ValidateScenePath(scenePath);
            navMeshDataPath = SceneBuildHelpers.ValidateAssetPath(navMeshDataPath);
            var problems = BattleRoyaleLayoutProblems();
            if (problems.Count > 0)
            {
                throw new InvalidOperationException(LogTag + " the " + BattleRoyaleMapName + " layout is invalid: " + string.Join("; ", problems));
            }

            // NewScene(Single) は参照されていないアセットをアンロードするので、置き方の JSON はシーンを作る前に読んで値だけ持つ
            var features = LoadBattleRoyaleFeatures();

            SceneBuildHelpers.EnsureFolder(System.IO.Path.GetDirectoryName(scenePath));
            SceneBuildHelpers.EnsureFolder(System.IO.Path.GetDirectoryName(navMeshDataPath));
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // 箱・脱出地点・エリアは当たりも形も持たない印なので、地形だけを置いて焼いてから足す
            var ground = CreateBattleRoyaleLevel(scene);
            var surface = SceneBuildHelpers.AddNavMeshSurface(ground, CollectObjects.All);
            NetMapFeatureBuilder.PlaceTerrain(scene, NetMapFeatureBuilder.TerrainParent(surface), features);
            SceneBuildHelpers.BakeNavMesh(surface);
            SceneBuildHelpers.SaveNavMeshData(surface, navMeshDataPath);
            RequireBattleRoyaleNavigable(features);

            PopulateBattleRoyale(scene);
            NetMapFeatureBuilder.PlaceVehicles(scene, null, features);
            if (!EditorSceneManager.SaveScene(scene, scenePath))
            {
                throw new InvalidOperationException(LogTag + " failed to save " + scenePath);
            }

            BuildSceneList.Register(scenePath);
            AssetDatabase.SaveAssets();
            Debug.Log(LogTag + " wrote " + scenePath + " and " + navMeshDataPath + " (" + BattleRoyaleSpawnPoints.Count + " spawn point(s), "
                + BattleRoyaleLootContainers.Count + " loot container(s), " + BattleRoyaleExtractionPoints.Count + " extraction point(s), zone radius "
                + BattleRoyaleZoneRadius + " m, " + features.LadderTowers.Count + " ladder tower(s), " + features.WaterPools.Count + " water pool(s), "
                + features.Vehicles.Count + " vehicle(s)); start a server with " + NetLaunchOptions.MapFlag + " " + BattleRoyaleMapName);
        }

        public static NetMapFeatureLayout LoadFeatures(float captureRadius)
        {
            var features = NetMapFeatureBuilder.LoadOrBuild(FeatureLayoutPath, DefaultFeatures);
            var problems = FeatureProblems(features, captureRadius);
            if (problems.Count > 0)
            {
                throw new InvalidOperationException(LogTag + " " + FeatureLayoutPath + " does not fit " + MapName + ": " + string.Join("; ", problems));
            }

            return features;
        }

        public static NetMapFeatureLayout LoadBattleRoyaleFeatures()
        {
            var features = NetMapFeatureBuilder.LoadOrBuild(BattleRoyaleFeatureLayoutPath, BattleRoyaleDefaultFeatures);
            var problems = BattleRoyaleFeatureProblems(features);
            if (problems.Count > 0)
            {
                throw new InvalidOperationException(
                    LogTag + " " + BattleRoyaleFeatureLayoutPath + " does not fit " + BattleRoyaleMapName + ": " + string.Join("; ", problems));
            }

            return features;
        }

        public static List<string> FeatureProblems(NetMapFeatureLayout features, float captureRadius)
        {
            var keepClear = SpawnPoints.Select(p => (p.Name, Feet(p.Position), FeatureSpawnClearance))
                .Concat(ControlPoints.Select(p => (p.Name, p.Position, ControlPointZone.DefaultRadius + PlayerRadius)))
                .Concat(FlagBases.Select(b => (FlagBaseName(b.Side), b.Position, captureRadius + PlayerRadius)));
            return NetMapFeatureBuilder.Problems(features, SceneBuildHelpers.PlaneHalfExtents(GroundScale).x, CoverWalls, keepClear);
        }

        public static List<string> BattleRoyaleFeatureProblems(NetMapFeatureLayout features)
        {
            var keepClear = BattleRoyaleSpawnPoints.Select(p => (p.Name, Feet(p.Position), FeatureSpawnClearance))
                .Concat(BattleRoyaleLootContainers.Select(c => (c.Id, c.Position, LootContainer.DefaultReach + PlayerRadius)))
                .Concat(BattleRoyaleExtractionPoints.Select(p => (p.Id, p.Position, ExtractionZone.DefaultRadius + PlayerRadius)));
            return NetMapFeatureBuilder.Problems(features, BattleRoyaleHalfExtent, BattleRoyaleCoverWalls, keepClear);
        }

        // はしごの足元に Bot が歩いて行けないと、サーバーの Bot がはしごを使えないので、焼いた後に確かめる。
        public static List<Vector3> LadderApproachPoints(NetMapFeatureLayout features)
        {
            if (features == null)
            {
                throw new ArgumentNullException(nameof(features));
            }

            var points = new List<Vector3>();
            foreach (var tower in features.LadderTowers)
            {
                var (bottom, facing) = LadderTowerBuilder.LadderPlacement(tower.BaseCenter, tower.Size, tower.SideDirection);
                points.Add(bottom - (facing * NetMapFeatureBuilder.LadderApproachDepth));
            }

            return points;
        }

        private static Vector3 Feet(Vector3 spawnPosition)
        {
            return spawnPosition + new Vector3(0f, GroundY, 0f);
        }

        public static GameObject CreateBattleRoyaleLevel(Scene scene)
        {
            SceneBuildHelpers.RequireLoaded(scene);
            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            SceneManager.MoveGameObjectToScene(ground, scene);
            ground.transform.position = new Vector3(0f, GroundY, 0f);
            ground.transform.localScale = BattleRoyaleGroundScale;
            SceneBuildHelpers.CreatePerimeterWalls(scene, null, ground.transform.position, SceneBuildHelpers.PlaneHalfExtents(BattleRoyaleGroundScale));

            var coverRoot = new GameObject(BattleRoyaleCoverRootName);
            SceneManager.MoveGameObjectToScene(coverRoot, scene);
            foreach (var (wallName, center, size) in BattleRoyaleCoverWalls)
            {
                var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
                wall.name = wallName;
                wall.transform.SetParent(coverRoot.transform, false);
                wall.transform.localPosition = center;
                wall.transform.localScale = size;
            }

            return ground;
        }

        public static void PopulateBattleRoyale(Scene scene)
        {
            SceneBuildHelpers.RequireLoaded(scene);
            CreateSpectatorCamera(scene, BattleRoyaleSpectatorPosition, BattleRoyaleSpectatorRotation);
            CreateSun(scene);

            var spawnRoot = new GameObject(BattleRoyaleSpawnRootName);
            SceneManager.MoveGameObjectToScene(spawnRoot, scene);
            foreach (var (pointName, position) in BattleRoyaleSpawnPoints)
            {
                CreateSpawnPoint(spawnRoot.transform, pointName, position, NetSpawnPoint.SpawnTeam.Any);
            }

            var lootRoot = new GameObject(LootContainersRootName);
            SceneManager.MoveGameObjectToScene(lootRoot, scene);
            foreach (var (id, position, tableId) in BattleRoyaleLootContainers)
            {
                CreateLootContainerMarker(lootRoot.transform, id, position, tableId);
            }

            var exitRoot = new GameObject(ExtractionPointsRootName);
            SceneManager.MoveGameObjectToScene(exitRoot, scene);
            foreach (var (id, position) in BattleRoyaleExtractionPoints)
            {
                CreateExtractionPointMarker(exitRoot.transform, id, position);
            }

            CreateZoneMarker(scene, BattleRoyaleZoneCenter, BattleRoyaleZoneRadius);
            EditorSceneManager.MarkSceneDirty(scene);
        }

        // クライアントの箱は NetV2LootPart がスナップショットから出すので、シーンの箱は書き出しの印にとどめる。
        // 空の見た目を先に置くのは、遊ぶときの Awake に形と当たりを作らせず、サーバーの知らない当たりで手元の予測をずらさないため
        public static LootContainer CreateLootContainerMarker(Transform parent, string id, Vector3 position, string tableId)
        {
            if (parent == null)
            {
                throw new ArgumentNullException(nameof(parent));
            }

            if (string.IsNullOrWhiteSpace(id))
            {
                throw new ArgumentException("a loot container needs an id", nameof(id));
            }

            var marker = new GameObject("Loot " + id);
            marker.transform.SetParent(parent, false);
            marker.transform.position = position;
            new GameObject(LootContainer.VisualName).transform.SetParent(marker.transform, false);
            var container = marker.AddComponent<LootContainer>();
            container.ContainerId = id;
            container.TableId = tableId;
            return container;
        }

        // 箱と同じく、脱出地点の見た目もクライアントの NetV2LootPart が出すので、閉じた・開いた見た目を空にしておく
        public static ExtractionZone CreateExtractionPointMarker(Transform parent, string id, Vector3 position)
        {
            if (parent == null)
            {
                throw new ArgumentNullException(nameof(parent));
            }

            if (string.IsNullOrWhiteSpace(id))
            {
                throw new ArgumentException("an extraction point needs an id", nameof(id));
            }

            var marker = new GameObject("Exit " + id);
            marker.transform.SetParent(parent, false);
            marker.transform.position = position;
            new GameObject(ExtractionZone.VisualName).transform.SetParent(marker.transform, false);
            new GameObject(ExtractionZone.VisualName + "-Open").transform.SetParent(marker.transform, false);
            var zone = marker.AddComponent<ExtractionZone>();
            zone.PointId = id;
            return zone;
        }

        // 縮めるのはサーバーなので、シーンのエリアは自分では始めない (BeginsOnStart を切る) 最初の円の印にする
        public static ShrinkingZone CreateZoneMarker(Scene scene, Vector3 center, float radius)
        {
            SceneBuildHelpers.RequireLoaded(scene);
            var zoneObject = new GameObject(ZoneObjectName);
            SceneManager.MoveGameObjectToScene(zoneObject, scene);
            zoneObject.transform.position = center;
            var zone = zoneObject.AddComponent<ShrinkingZone>();
            zone.BeginsOnStart = false;
            zone.InitialRadius = radius;
            return zone;
        }

        // NavMesh を焼いた後で書き出しが Id の重なりで止まらないよう、シーンを作る前に確かめる
        public static List<string> BattleRoyaleLayoutProblems()
        {
            var problems = new List<string>();
            AddDuplicates(problems, "loot container id", BattleRoyaleLootContainers.Select(c => c.Id));
            AddDuplicates(problems, "extraction point id", BattleRoyaleExtractionPoints.Select(p => p.Id));
            AddDuplicates(problems, "spawn point name", BattleRoyaleSpawnPoints.Select(p => p.Name));

            var inside = BattleRoyaleHalfExtent - 1f;
            var points = BattleRoyaleSpawnPoints.Select(p => (p.Name, p.Position))
                .Concat(BattleRoyaleLootContainers.Select(c => (c.Id, c.Position)))
                .Concat(BattleRoyaleExtractionPoints.Select(p => (p.Id, p.Position)));
            foreach (var (pointName, position) in points)
            {
                if (Mathf.Abs(position.x) > inside || Mathf.Abs(position.z) > inside)
                {
                    problems.Add(pointName + " at " + position + " is outside the floor");
                }

                foreach (var (wallName, center, size) in BattleRoyaleCoverWalls)
                {
                    if (Mathf.Abs(position.x - center.x) < (size.x / 2f) + PlayerRadius
                        && Mathf.Abs(position.z - center.z) < (size.z / 2f) + PlayerRadius)
                    {
                        problems.Add(pointName + " at " + position + " is inside " + wallName);
                    }
                }
            }

            foreach (var (pointName, position) in BattleRoyaleSpawnPoints)
            {
                var flat = new Vector2(position.x - BattleRoyaleZoneCenter.x, position.z - BattleRoyaleZoneCenter.z);
                if (flat.magnitude >= BattleRoyaleZoneRadius)
                {
                    problems.Add(pointName + " starts outside the zone (radius " + BattleRoyaleZoneRadius + " m)");
                }
            }

            return problems;
        }

        private static void AddDuplicates(List<string> problems, string label, IEnumerable<string> values)
        {
            foreach (var group in values.GroupBy(v => v, StringComparer.Ordinal).Where(g => g.Count() > 1))
            {
                problems.Add("the " + label + " '" + group.Key + "' is used " + group.Count() + " times");
            }
        }

        // サーバーの Bot は箱と脱出地点へ NavMesh で歩くので、焼いた後に出現点から届くかを確かめる
        public static void RequireBattleRoyaleNavigable(NetMapFeatureLayout features)
        {
            var spawns = BattleRoyaleSpawnPoints.Select(p => p.Position).ToList();
            SceneBuildHelpers.RequireOnNavMesh(spawns, NavMeshCheckRadius, "spawn point");
            SceneBuildHelpers.RequireReachable(spawns[0], spawns, NavMeshCheckRadius, "spawn point");

            var boxes = BattleRoyaleLootContainers.Select(c => c.Position).ToList();
            SceneBuildHelpers.RequireOnNavMesh(boxes, NavMeshCheckRadius, "loot container");
            SceneBuildHelpers.RequireReachable(spawns[0], boxes, NavMeshCheckRadius, "loot container");

            var exits = BattleRoyaleExtractionPoints.Select(p => p.Position).ToList();
            SceneBuildHelpers.RequireOnNavMesh(exits, NavMeshCheckRadius, "extraction point");
            SceneBuildHelpers.RequireReachable(spawns[0], exits, NavMeshCheckRadius, "extraction point");

            RequireLaddersReachable(spawns[0], features);
        }

        private static void RequireLaddersReachable(Vector3 from, NetMapFeatureLayout features)
        {
            var ladders = LadderApproachPoints(features);
            if (ladders.Count == 0)
            {
                return;
            }

            SceneBuildHelpers.RequireOnNavMesh(ladders, NavMeshCheckRadius, "ladder foot");
            SceneBuildHelpers.RequireReachable(from, ladders, NavMeshCheckRadius, "ladder foot");
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

        private static void BuildScene(float captureRadius, NetMapFeatureLayout features)
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
            SceneBuildHelpers.CreatePerimeterWalls(scene, null, ground.transform.position, SceneBuildHelpers.PlaneHalfExtents(GroundScale));

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

            NetMapFeatureBuilder.PlaceTerrain(scene, null, features);
            BakeNavMesh(ground, features);
            NetMapFeatureBuilder.PlaceVehicles(scene, null, features);

            if (!EditorSceneManager.SaveScene(scene, scenePath))
            {
                throw new InvalidOperationException("failed to save " + scenePath);
            }
        }

        // サーバーへの書き出し NavMeshExporter は床の NavMeshSurface から読むので、焼いたデータをそこに持たせる。
        private static void BakeNavMesh(GameObject ground, NetMapFeatureLayout features)
        {
            var surface = SceneBuildHelpers.AddNavMeshSurface(ground, CollectObjects.All);
            SceneBuildHelpers.EnsureFolder(System.IO.Path.GetDirectoryName(NavMeshDataPath));
            SceneBuildHelpers.BakeNavMesh(surface);
            SceneBuildHelpers.SaveNavMeshData(surface, NavMeshDataPath);
            RequireNavigable(features);
        }

        public static void RequireNavigable(NetMapFeatureLayout features)
        {
            var spawns = SpawnPoints.Select(point => point.Position).ToList();
            SceneBuildHelpers.RequireOnNavMesh(spawns, NavMeshCheckRadius, "spawn point");
            SceneBuildHelpers.RequireReachable(spawns[0], spawns, NavMeshCheckRadius, "spawn point");

            var bases = FlagBases.Select(flagBase => flagBase.Position).ToList();
            SceneBuildHelpers.RequireOnNavMesh(bases, NavMeshCheckRadius, "flag base");
            SceneBuildHelpers.RequireReachable(spawns[0], bases, NavMeshCheckRadius, "flag base");

            RequireLaddersReachable(spawns[0], features);
        }
    }
}
