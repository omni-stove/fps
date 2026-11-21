using System;
using System.Collections.Generic;
using System.IO;
using Shooter.Core;
using Shooter.Runtime;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Shooter.Editor
{
    // ルールはサーバーが -rules で選ぶので、拠点は MatchObjective.Domination の試合でしか使われないがどのルールでも置いておく。
    public static class ArenaSceneBuilder
    {
        public const string MapName = "NetArena01";

        public static string ScenePath => BuildSceneList.NetArena01Scene;

        // Unity の慣習どおり、焼いた NavMesh はシーン名のフォルダに置く。
        public static string NavMeshDataPath => ShooterKitRoot.OutputPath("Scenes/" + MapName + "/NavMesh-Arena.asset");

        public const string MenuPath = SceneBuildSteps.MenuPath;
        public const string LogTag = "[Net] ArenaSceneBuilder:";

        public const string LevelName = "Level";
        public const string GroundName = "Ground";
        public const string SpawnPointsRootName = "SpawnPoints";

        public const float HalfExtent = 30f;

        public const float GroundY = NetSceneBuilder.GroundY;

        public const int SpawnsPerTeam = 8;

        // 外周の壁は乗り越えられず外へ出られない高さにする。
        public const float BoundaryHeight = 4f;
        public const float BoundaryThickness = 1f;

        // 乗り越えられる高さ (MotorSpec.DefaultVaultMaxHeight) より高くして、端から覗くカバーにする。
        public const float HighWallHeight = 3f;
        public const float HighWallThickness = 0.5f;

        // MotorSpec.DefaultCoverLowWallHeight 未満にしてしゃがんで隠れる低いカバーにし、MotorSpec.DefaultVaultMaxHeight 以下にして乗り越えられるようにする。
        public const float LowWallHeight = 0.8f;
        public const float LowWallThickness = 0.4f;

        // 段差は MotorSpec.DefaultVaultMinHeight 以上・MotorSpec.DefaultVaultMaxDepth 以下にして乗り越えられるようにする。
        public const float StepHeight = 0.5f;
        public const float StepDepth = 0.8f;

        public const int BuildingWidth = 6;
        public const int BuildingDepth = 6;
        public const int BuildingWallHeight = 4;

        // 出入口はプレイヤー (高さ 2 m) がくぐれる大きさにする。
        public const int DoorWidth = 2;
        public const int DoorHeight = 3;

        public const float PieceSize = 1f;
        public const int PieceMaxHealth = DestructiblePiece.DefaultMaxHealth;

        // 東西の壁は角を南北の壁と二重に数えないよう除く。
        public const int BuildingPieceCount =
            (2 * ((BuildingWidth * BuildingWallHeight) - (DoorWidth * DoorHeight)))
            + (2 * (BuildingDepth - 2) * BuildingWallHeight)
            + (BuildingWidth * BuildingDepth);

        public const float ControlPointRadius = ControlPointZone.DefaultRadius;
        public const float ControlPointHeight = ControlPointZone.DefaultHeight;

        // 拠点の円の縁に立ったプレイヤーのカプセルが遮蔽物にめり込まず、拠点の床で乗り越えや遮蔽の動きが起きないよう、円と遮蔽物の間にプレイヤーの半径を空ける。
        public const float ControlPointClearance = NetSceneBuilder.PlayerRadius;

        public const float NavMeshCheckRadius = 1f;

        // Plane は 10 m 四方なので、半分の幅を 5 で割ると拡大率になる。
        public static readonly Vector3 GroundScale = new Vector3(HalfExtent / 5f, 1f, HalfExtent / 5f);

        // 両陣地と外周まで見下ろせる位置にする。
        public static readonly Vector3 SpectatorPosition = new Vector3(0f, 50f, -42f);
        public static readonly Quaternion SpectatorRotation = Quaternion.Euler(50f, 0f, 0f);

        public static readonly IReadOnlyList<(string Name, Vector3 Center, Vector3 Size)> BoundaryWalls = new[]
        {
            ("Boundary South", new Vector3(0f, GroundY + (BoundaryHeight / 2f), -HalfExtent - (BoundaryThickness / 2f)),
                new Vector3((HalfExtent * 2f) + (BoundaryThickness * 2f), BoundaryHeight, BoundaryThickness)),
            ("Boundary North", new Vector3(0f, GroundY + (BoundaryHeight / 2f), HalfExtent + (BoundaryThickness / 2f)),
                new Vector3((HalfExtent * 2f) + (BoundaryThickness * 2f), BoundaryHeight, BoundaryThickness)),
            ("Boundary West", new Vector3(-HalfExtent - (BoundaryThickness / 2f), GroundY + (BoundaryHeight / 2f), 0f),
                new Vector3(BoundaryThickness, BoundaryHeight, HalfExtent * 2f)),
            ("Boundary East", new Vector3(HalfExtent + (BoundaryThickness / 2f), GroundY + (BoundaryHeight / 2f), 0f),
                new Vector3(BoundaryThickness, BoundaryHeight, HalfExtent * 2f)),
        };

        public static readonly IReadOnlyList<(string Name, Vector3 Center, Vector3 Size)> HighWalls = Concat(
            Mirrored("High Wall", "West", OnGround(new Vector3(-10f, 0f, -14f), HighWallHeight), new Vector3(6f, HighWallHeight, HighWallThickness)),
            Mirrored("High Wall", "East", OnGround(new Vector3(10f, 0f, -14f), HighWallHeight), new Vector3(6f, HighWallHeight, HighWallThickness)),
            new[]
            {
                ("High Wall Center West", OnGround(new Vector3(-6f, 0f, 0f), HighWallHeight), new Vector3(HighWallThickness, HighWallHeight, 6f)),
                ("High Wall Center East", OnGround(new Vector3(6f, 0f, 0f), HighWallHeight), new Vector3(HighWallThickness, HighWallHeight, 6f)),
            });

        public static readonly IReadOnlyList<(string Name, Vector3 Center, Vector3 Size)> LowWalls = Concat(
            Mirrored("Low Wall", "Center", OnGround(new Vector3(0f, 0f, -9f), LowWallHeight), new Vector3(4f, LowWallHeight, LowWallThickness)),
            Mirrored("Low Wall", "West", OnGround(new Vector3(-24f, 0f, -8f), LowWallHeight), new Vector3(4f, LowWallHeight, LowWallThickness)),
            Mirrored("Low Wall", "East", OnGround(new Vector3(24f, 0f, -8f), LowWallHeight), new Vector3(4f, LowWallHeight, LowWallThickness)));

        public static readonly IReadOnlyList<(string Name, Vector3 Center, Vector3 Size)> Steps = Concat(
            Mirrored("Step", "West", OnGround(new Vector3(-17f, 0f, -19f), StepHeight), new Vector3(4f, StepHeight, StepDepth)),
            Mirrored("Step", "East", OnGround(new Vector3(17f, 0f, -19f), StepHeight), new Vector3(4f, StepHeight, StepDepth)));

        public static readonly IReadOnlyList<(string Name, Vector3 GroundCenter)> Buildings = new[]
        {
            ("Building West", new Vector3(-18f, GroundY, 0f)),
            ("Building East", new Vector3(18f, GroundY, 0f)),
        };

        public static readonly IReadOnlyList<(string Name, Vector3 Position, NetSpawnPoint.SpawnTeam Team)> SpawnPoints = CreateSpawnLayout();

        // 範囲の円が ControlPointClearance を空けてどの壁・段差・建物にも掛からない位置にしか置けない (ControlPointOverlaps が確かめる)。
        public static readonly IReadOnlyList<(string Name, Vector3 Position, int Index)> ControlPoints = new[]
        {
            (ControlPointName(0), new Vector3(0f, GroundY, -15f), 0),
            (ControlPointName(1), new Vector3(0f, GroundY, 0f), 1),
            (ControlPointName(2), new Vector3(0f, GroundY, 15f), 2),
        };

        // 旗取りのサーバーは両チームの台が無いマップを断るので、拠点と同じくどのルールでも置く。Alpha の台を自陣の出現地点の奥の中央に置き、Bravo はその鏡にする。
        // 範囲の円が掛からないかは FlagBaseOverlaps が確かめる。
        public static readonly IReadOnlyList<(Side Side, Vector3 Position)> FlagBases = new[]
        {
            (Side.Alpha, new Vector3(0f, GroundY, -27f)),
            (Side.Bravo, MirrorZ(new Vector3(0f, GroundY, -27f))),
        };

        public const string PayloadRootName = "Payload";
        public const string PayloadCartName = "PayloadCart";

        // Payload のサーバーは道筋の無いマップを断るので、拠点・旗の台と同じくどのルールでも置く。Alpha の出現地点の手前から Bravo の出現地点の手前まで押させる。
        // 中央の低い壁 (z = ±9, x = -2〜2) と中央東の高い壁 (x = 6) の間 (x = 4) を通し、幅 2m の台車が壁に掛からないようにする。
        public static readonly IReadOnlyList<Vector3> PayloadRoutePoints = new[]
        {
            new Vector3(0f, GroundY, -18f),
            new Vector3(0f, GroundY, -13f),
            new Vector3(4f, GroundY, -10f),
            new Vector3(4f, GroundY, 0f),
            new Vector3(4f, GroundY, 10f),
            new Vector3(0f, GroundY, 13f),
            new Vector3(0f, GroundY, 18f),
        };

        // 道のりの真ん中 (中央の拠点の横) を中継点にする
        public static readonly IReadOnlyList<int> PayloadCheckpoints = new[] { 3 };

        public static string FeatureLayoutPath => NetMapFeatureBuilder.LayoutPath(MapName);

        // 台車の幅 (2m) の半分に人がすれ違える幅を足し、道筋の上に塔や乗り物を置かないようにする。
        public const float PayloadRouteClearance = 2f;

        // 西の外周沿いに両チームのはしごの塔を鏡に置き、その間に車を 1 台、東の建物の外に水場を置く。どれも出現地点・拠点・旗の台・台車の道筋から離す。
        // 位置はマップの JSON (FeatureLayoutPath) を作るときの既定で、作った後は JSON の値を使う。
        public static NetMapFeatureLayout DefaultFeatures => new NetMapFeatureLayout(
            new[]
            {
                new NetMapLadderTower("Ladder Tower Alpha", new Vector3(-25f, GroundY, -14f), new Vector3(4f, 3f, 4f), NetMapFeatureLayout.East, 0.8f),
                new NetMapLadderTower("Ladder Tower Bravo", MirrorZ(new Vector3(-25f, GroundY, -14f)), new Vector3(4f, 3f, 4f), NetMapFeatureLayout.East, 0.8f),
            },
            new[] { new NetMapWaterPool("Pond East", new Vector3(25f, GroundY, 0f), new Vector3(5f, 3f, 10f), 2.5f) },
            new[] { new NetMapVehicleSpot("Car West", NetMapFeatureBuilder.CarKind, new Vector3(-25f, GroundY, 0f), 0f) });

        public static void Build()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                Debug.Log(LogTag + " cancelled");
                return;
            }

            BuildAt(ScenePath, NavMeshDataPath);
        }

        public static void BuildAt(string scenePath, string navMeshDataPath)
        {
            scenePath = BootstrapSceneBuilder.ValidateScenePath(scenePath);
            navMeshDataPath = SceneBuildHelpers.ValidateAssetPath(navMeshDataPath);

            var overlaps = ControlPointOverlaps();
            if (overlaps.Count > 0)
            {
                throw new InvalidOperationException(LogTag + " control point areas overlap the level: " + string.Join("; ", overlaps));
            }

            // NewScene(Single) は参照されていないアセットをアンロードするので、試合の設定はシーンを作る前に読んで値だけ持つ
            var captureRadius = NetSceneBuilder.CaptureRadius(NetSceneBuilder.LoadOrBuildMatch());
            var baseOverlaps = FlagBaseOverlaps(captureRadius);
            if (baseOverlaps.Count > 0)
            {
                throw new InvalidOperationException(LogTag + " flag base areas (capture radius " + captureRadius + " m) overlap the level: "
                    + string.Join("; ", baseOverlaps));
            }

            // 試合の設定と同じく、置き方の JSON もシーンを作る前に読んで値だけ持つ
            var features = LoadFeatures(captureRadius);

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // NewScene(Single) は参照されていないアセットをアンロードするので、プレハブはシーンを作ってから読む
            var pieceVisualPrefab = DestructiblePieceVisual.Load();

            SceneBuildHelpers.EnsureFolder(Path.GetDirectoryName(scenePath));
            SceneBuildHelpers.EnsureFolder(Path.GetDirectoryName(navMeshDataPath));

            // 建物の部品は NavMeshObstacle で NavMesh を削るので、壊れたところを通れるよう地形だけを置いた状態で焼く
            var surface = CreateLevel(scene);
            NetMapFeatureBuilder.PlaceTerrain(scene, NetMapFeatureBuilder.TerrainParent(surface), features);
            SceneBuildHelpers.BakeNavMesh(surface);
            SceneBuildHelpers.SaveNavMeshData(surface, navMeshDataPath);
            RequireNavigable(features);

            PopulateScene(scene, pieceVisualPrefab, captureRadius);
            NetMapFeatureBuilder.PlaceVehicles(scene, null, features);
            SaveScene(scene, scenePath);

            BuildSceneList.Register(scenePath);
            AssetDatabase.SaveAssets();
            Debug.Log(LogTag + " wrote " + scenePath + " and " + navMeshDataPath + " (" + SpawnPoints.Count + " spawn point(s), "
                + Buildings.Count + " building(s), " + ControlPoints.Count + " control point(s), "
                + FlagBases.Count + " flag base(s), " + features.LadderTowers.Count + " ladder tower(s), "
                + features.WaterPools.Count + " water pool(s), " + features.Vehicles.Count + " vehicle(s)); start a server with "
                + NetLaunchOptions.MapFlag + " " + MapName);
        }

        public static NetMapFeatureLayout LoadFeatures(float captureRadius)
        {
            var features = NetMapFeatureBuilder.LoadOrBuild(FeatureLayoutPath, DefaultFeatures);
            var problems = FeatureProblems(features, captureRadius);
            if (problems.Count > 0)
            {
                throw new InvalidOperationException(LogTag + " " + FeatureLayoutPath + " does not fit the arena: " + string.Join("; ", problems));
            }

            return features;
        }

        // 建物は壊れて形が変わるので、塔や乗り物は建物の外にしか置けない (Obstacles に建物の床の広さを含める)。
        public static List<string> FeatureProblems(NetMapFeatureLayout features, float captureRadius)
        {
            var keepClear = new List<(string Name, Vector3 Position, float Radius)>();
            foreach (var (spawnName, position, _) in SpawnPoints)
            {
                keepClear.Add((spawnName, Feet(position), NetSceneBuilder.FeatureSpawnClearance));
            }

            foreach (var (pointName, position, _) in ControlPoints)
            {
                keepClear.Add((pointName, position, ControlPointRadius + ControlPointClearance));
            }

            foreach (var (side, position) in FlagBases)
            {
                keepClear.Add((NetSceneBuilder.FlagBaseName(side), position, captureRadius + ControlPointClearance));
            }

            for (var i = 0; i < PayloadRoutePoints.Count; i++)
            {
                keepClear.Add(("payload route point " + i, PayloadRoutePoints[i], PayloadRouteClearance));
            }

            return NetMapFeatureBuilder.Problems(features, HalfExtent, Obstacles(), keepClear);
        }

        public static void RequireNavigable(NetMapFeatureLayout features)
        {
            RequireNavigable();
            var ladders = NetSceneBuilder.LadderApproachPoints(features);
            if (ladders.Count == 0)
            {
                return;
            }

            SceneBuildHelpers.RequireOnNavMesh(ladders, NavMeshCheckRadius, "ladder foot");
            SceneBuildHelpers.RequireReachable(Feet(SpawnPoints[0].Position), ladders, NavMeshCheckRadius, "ladder foot");
        }

        public static NavMeshSurface CreateLevel(Scene scene)
        {
            SceneBuildHelpers.RequireEmpty(scene, "the arena level");

            var level = new GameObject(LevelName);
            SceneManager.MoveGameObjectToScene(level, scene);
            try
            {
                SceneBuildHelpers.CreateStaticPlane(scene, level.transform, GroundName, new Vector3(0f, GroundY, 0f), GroundScale);
                AddBoxes(scene, level.transform, "Boundary", BoundaryWalls);
                AddBoxes(scene, level.transform, "HighWalls", HighWalls);
                AddBoxes(scene, level.transform, "LowWalls", LowWalls);
                AddBoxes(scene, level.transform, "Steps", Steps);

                var surface = SceneBuildHelpers.AddNavMeshSurface(level, CollectObjects.Children);
                EditorSceneManager.MarkSceneDirty(scene);
                return surface;
            }
            catch
            {
                UnityEngine.Object.DestroyImmediate(level);
                throw;
            }
        }

        public static void RequireNavigable()
        {
            var points = NavMeshCheckPoints();
            SceneBuildHelpers.RequireOnNavMesh(points, NavMeshCheckRadius, "arena point");
            SceneBuildHelpers.RequireReachable(Feet(SpawnPoints[0].Position), points, NavMeshCheckRadius, "arena point");
        }

        public static List<Vector3> NavMeshCheckPoints()
        {
            var points = new List<Vector3>();
            foreach (var (_, position, _) in SpawnPoints)
            {
                points.Add(Feet(position));
            }

            foreach (var (_, position, _) in ControlPoints)
            {
                points.Add(position);
            }

            foreach (var (_, position) in FlagBases)
            {
                points.Add(position);
            }

            // Bot は台車のまわりを歩いて押すので、道筋の点にも歩いて行けるようにする
            points.AddRange(PayloadRoutePoints);

            var doorOffset = (BuildingDepth * PieceSize / 2f) + 1.5f;
            foreach (var (_, center) in Buildings)
            {
                points.Add(center + new Vector3(0f, 0f, -doorOffset));
                points.Add(center + new Vector3(0f, 0f, doorOffset));
            }

            return points;
        }

        public static void PopulateScene(
            Scene scene, GameObject pieceVisualPrefab = null, float captureRadius = CaptureTheFlagSpec.DefaultCaptureRadius)
        {
            SceneBuildHelpers.RequireLoaded(scene);

            NetSceneBuilder.CreateSpectatorCamera(scene, SpectatorPosition, SpectatorRotation);
            NetSceneBuilder.CreateSun(scene);

            var poolObject = new GameObject(DebrisPool.DefaultObjectName);
            SceneManager.MoveGameObjectToScene(poolObject, scene);
            var pool = poolObject.AddComponent<DebrisPool>();

            foreach (var (buildingName, groundCenter) in Buildings)
            {
                CreateBuilding(scene, buildingName, groundCenter, pool, pieceVisualPrefab);
            }

            var spawnRoot = new GameObject(SpawnPointsRootName);
            SceneManager.MoveGameObjectToScene(spawnRoot, scene);
            foreach (var (pointName, position, team) in SpawnPoints)
            {
                NetSceneBuilder.CreateSpawnPoint(spawnRoot.transform, pointName, position, team);
            }

            foreach (var (pointName, position, index) in ControlPoints)
            {
                CreateControlPoint(scene, pointName, position, index);
            }

            NetSceneBuilder.CreateFlagBases(scene, FlagBases, captureRadius);
            CreatePayloadCart(scene, PayloadRoutePoints, PayloadCheckpoints);

            EditorSceneManager.MarkSceneDirty(scene);
        }

        // 範囲 (ControlPointZone) は付けない。付けるとネットのクライアントがシーンの拠点として数えてしまうので、遊ぶときに PayloadCart.BuildRoute が既定の半径で足す形に任せる。
        // 当たり (Collider) も付けないのは、サーバーへ書き出す当たりに動かない箱として入ってしまうため
        public static PayloadCart CreatePayloadCart(Scene scene, IReadOnlyList<Vector3> route, IReadOnlyList<int> checkpoints)
        {
            SceneBuildHelpers.RequireLoaded(scene);
            if (route == null)
            {
                throw new ArgumentNullException(nameof(route));
            }

            if (route.Count < PayloadRoute.MinPointCount)
            {
                throw new ArgumentException("a payload route needs at least " + PayloadRoute.MinPointCount + " points", nameof(route));
            }

            var root = new GameObject(PayloadRootName);
            SceneManager.MoveGameObjectToScene(root, scene);
            try
            {
                // 点を台車の子にすると台車と一緒に動いてしまうので、台車と並べて置く
                var points = new Transform[route.Count];
                for (var i = 0; i < route.Count; i++)
                {
                    var point = new GameObject("Route Point " + i);
                    point.transform.SetParent(root.transform, false);
                    point.transform.SetPositionAndRotation(route[i], Quaternion.identity);
                    points[i] = point.transform;
                }

                var cartObject = new GameObject(PayloadCartName);
                cartObject.transform.SetParent(root.transform, false);
                cartObject.transform.SetPositionAndRotation(route[0], Quaternion.identity);
                var cart = cartObject.AddComponent<PayloadCart>();
                cart.SetRoute(points, checkpoints ?? Array.Empty<int>());
                return cart;
            }
            catch
            {
                UnityEngine.Object.DestroyImmediate(root);
                throw;
            }
        }

        public static ControlPointZone CreateControlPoint(Scene scene, string pointName, Vector3 position, int index)
        {
            SceneBuildHelpers.RequireLoaded(scene);

            var point = new GameObject(pointName);
            SceneManager.MoveGameObjectToScene(point, scene);
            point.transform.SetPositionAndRotation(position, Quaternion.identity);

            try
            {
                var zone = point.AddComponent<ControlPointZone>();
                zone.Index = index;
                zone.Radius = ControlPointRadius;
                zone.Height = ControlPointHeight;
                DominationSceneBuilder.AddFlag(point.transform);
                return zone;
            }
            catch
            {
                UnityEngine.Object.DestroyImmediate(point);
                throw;
            }
        }

        public static string ControlPointName(int index)
        {
            return "Control Point " + DominationHudFormat.PointName(index);
        }

        public static List<string> ControlPointOverlaps()
        {
            var problems = new List<string>();
            var reach = ControlPointRadius + ControlPointClearance;
            var obstacles = Obstacles();

            for (var i = 0; i < ControlPoints.Count; i++)
            {
                var (pointName, position, _) = ControlPoints[i];
                foreach (var (obstacleName, center, size) in obstacles)
                {
                    if (HorizontalDistanceToBox(position, center, size) < reach)
                    {
                        problems.Add(pointName + " overlaps " + obstacleName);
                    }
                }

                foreach (var (spawnName, spawnPosition, _) in SpawnPoints)
                {
                    if (HorizontalDistance(position, spawnPosition) < reach)
                    {
                        problems.Add(pointName + " covers " + spawnName);
                    }
                }

                for (var j = i + 1; j < ControlPoints.Count; j++)
                {
                    var (otherName, otherPosition, _) = ControlPoints[j];
                    if (HorizontalDistance(position, otherPosition) < 2f * reach)
                    {
                        problems.Add(pointName + " overlaps " + otherName);
                    }
                }
            }

            return problems;
        }

        // 台の円は拠点と同じく、縁に立つプレイヤーが遮蔽物にめり込まないよう ControlPointClearance を空ける。
        // 出現地点を覆うと出てすぐ旗を運び込めてしまい、拠点の円と重なるとどちらの円盤か見分けにくいので、それも断る。
        public static List<string> FlagBaseOverlaps(float captureRadius)
        {
            if (!(captureRadius > 0f) || float.IsInfinity(captureRadius))
            {
                throw new ArgumentOutOfRangeException(nameof(captureRadius), captureRadius, "the capture radius must be positive and finite");
            }

            var problems = new List<string>();
            var reach = captureRadius + ControlPointClearance;
            var obstacles = Obstacles();
            foreach (var (side, position) in FlagBases)
            {
                var baseName = NetSceneBuilder.FlagBaseName(side);
                foreach (var (obstacleName, center, size) in obstacles)
                {
                    if (HorizontalDistanceToBox(position, center, size) < reach)
                    {
                        problems.Add(baseName + " overlaps " + obstacleName);
                    }
                }

                foreach (var (spawnName, spawnPosition, _) in SpawnPoints)
                {
                    if (HorizontalDistance(position, spawnPosition) < reach)
                    {
                        problems.Add(baseName + " covers " + spawnName);
                    }
                }

                foreach (var (pointName, pointPosition, _) in ControlPoints)
                {
                    if (HorizontalDistance(position, pointPosition) < captureRadius + ControlPointRadius + ControlPointClearance)
                    {
                        problems.Add(baseName + " overlaps " + pointName);
                    }
                }
            }

            return problems;
        }

        // 弾で壊れるかはサーバーが試合のルールで決め、壊れた部品はサーバーの知らせでクライアントが外すので、ここでは壊れない状態にしておく。
        public static DestructibleStructure CreateBuilding(
            Scene scene, string buildingName, Vector3 groundCenter, DebrisPool pool, GameObject pieceVisualPrefab = null)
        {
            SceneBuildHelpers.RequireLoaded(scene);

            var root = new GameObject(buildingName);
            SceneManager.MoveGameObjectToScene(root, scene);
            root.transform.SetPositionAndRotation(groundCenter, Quaternion.identity);

            try
            {
                var structure = root.AddComponent<DestructibleStructure>();
                structure.DestructionEnabled = false;
                structure.DebrisPool = pool;

                var minX = -((BuildingWidth - 1) * PieceSize / 2f);
                var minZ = -((BuildingDepth - 1) * PieceSize / 2f);
                var maxX = -minX;
                var maxZ = -minZ;
                var south = CreateGroup(root.transform, "South");
                var north = CreateGroup(root.transform, "North");
                var west = CreateGroup(root.transform, "West");
                var east = CreateGroup(root.transform, "East");
                var roof = CreateGroup(root.transform, "Roof");

                var index = 0;
                for (var y = 0; y < BuildingWallHeight; y++)
                {
                    var height = (y + 0.5f) * PieceSize;
                    var grounded = y == 0;
                    for (var x = 0; x < BuildingWidth; x++)
                    {
                        if (IsDoor(x, y))
                        {
                            continue;
                        }

                        var px = minX + (x * PieceSize);
                        CreatePiece(south, x, y, new Vector3(px, height, minZ), ref index, grounded, pieceVisualPrefab);
                        CreatePiece(north, x, y, new Vector3(px, height, maxZ), ref index, grounded, pieceVisualPrefab);
                    }

                    for (var z = 1; z < BuildingDepth - 1; z++)
                    {
                        var pz = minZ + (z * PieceSize);
                        CreatePiece(west, z, y, new Vector3(minX, height, pz), ref index, grounded, pieceVisualPrefab);
                        CreatePiece(east, z, y, new Vector3(maxX, height, pz), ref index, grounded, pieceVisualPrefab);
                    }
                }

                var roofHeight = (BuildingWallHeight + 0.5f) * PieceSize;
                for (var x = 0; x < BuildingWidth; x++)
                {
                    for (var z = 0; z < BuildingDepth; z++)
                    {
                        CreatePiece(roof, x, z, new Vector3(minX + (x * PieceSize), roofHeight, minZ + (z * PieceSize)), ref index, false, pieceVisualPrefab);
                    }
                }

                return structure;
            }
            catch
            {
                UnityEngine.Object.DestroyImmediate(root);
                throw;
            }
        }

        public static bool IsDoor(int column, int row)
        {
            var first = (BuildingWidth - DoorWidth) / 2;
            return row < DoorHeight && column >= first && column < first + DoorWidth;
        }

        public static Vector3 Feet(Vector3 spawnPosition)
        {
            return spawnPosition + new Vector3(0f, GroundY, 0f);
        }

        public static Vector3 MirrorZ(Vector3 point)
        {
            return new Vector3(point.x, point.y, -point.z);
        }

        private static (string Name, Vector3 Position, NetSpawnPoint.SpawnTeam Team)[] CreateSpawnLayout()
        {
            var columns = new[] { -9f, -3f, 3f, 9f };
            var rows = new[] { -26f, -22f };
            var points = new List<(string, Vector3, NetSpawnPoint.SpawnTeam)>();
            var number = 1;
            foreach (var z in rows)
            {
                foreach (var x in columns)
                {
                    var alpha = new Vector3(x, 0f, z);
                    points.Add(("Spawn Alpha " + number, alpha, NetSpawnPoint.SpawnTeam.Alpha));
                    points.Add(("Spawn Bravo " + number, MirrorZ(alpha), NetSpawnPoint.SpawnTeam.Bravo));
                    number++;
                }
            }

            return points.ToArray();
        }

        private static List<(string Name, Vector3 Center, Vector3 Size)> Obstacles()
        {
            var obstacles = new List<(string Name, Vector3 Center, Vector3 Size)>();
            obstacles.AddRange(BoundaryWalls);
            obstacles.AddRange(HighWalls);
            obstacles.AddRange(LowWalls);
            obstacles.AddRange(Steps);
            foreach (var (buildingName, groundCenter) in Buildings)
            {
                obstacles.Add((buildingName, groundCenter, new Vector3(BuildingWidth, 0f, BuildingDepth) * PieceSize));
            }

            return obstacles;
        }

        private static float HorizontalDistance(Vector3 a, Vector3 b)
        {
            return new Vector2(a.x - b.x, a.z - b.z).magnitude;
        }

        private static float HorizontalDistanceToBox(Vector3 point, Vector3 center, Vector3 size)
        {
            var dx = Mathf.Max(0f, Mathf.Abs(point.x - center.x) - (size.x / 2f));
            var dz = Mathf.Max(0f, Mathf.Abs(point.z - center.z) - (size.z / 2f));
            return new Vector2(dx, dz).magnitude;
        }

        private static Vector3 OnGround(Vector3 footprintCenter, float height)
        {
            return new Vector3(footprintCenter.x, GroundY + (height / 2f), footprintCenter.z);
        }

        private static (string Name, Vector3 Center, Vector3 Size)[] Mirrored(string kind, string side, Vector3 alphaCenter, Vector3 size)
        {
            return new[]
            {
                (kind + " Alpha " + side, alphaCenter, size),
                (kind + " Bravo " + side, MirrorZ(alphaCenter), size),
            };
        }

        private static (string Name, Vector3 Center, Vector3 Size)[] Concat(params (string Name, Vector3 Center, Vector3 Size)[][] groups)
        {
            var all = new List<(string, Vector3, Vector3)>();
            foreach (var group in groups)
            {
                all.AddRange(group);
            }

            return all.ToArray();
        }

        private static void AddBoxes(
            Scene scene, Transform level, string groupName, IReadOnlyList<(string Name, Vector3 Center, Vector3 Size)> boxes)
        {
            var group = new GameObject(groupName);
            group.transform.SetParent(level, false);
            group.isStatic = true;
            foreach (var (boxName, center, size) in boxes)
            {
                SceneBuildHelpers.CreateStaticBox(scene, group.transform, boxName, center, size);
            }
        }

        private static Transform CreateGroup(Transform parent, string groupName)
        {
            var group = new GameObject(groupName).transform;
            group.SetParent(parent, false);
            return group;
        }

        private static void CreatePiece(
            Transform group, int column, int row, Vector3 buildingLocalPosition, ref int index, bool grounded, GameObject visualPrefab)
        {
            // グループは建物の原点に置いたままなので、建物から見た位置をそのまま使える
            DestructiblePiece.Create(
                group,
                group.name + "-" + column + "-" + row,
                buildingLocalPosition,
                Vector3.one * PieceSize,
                index,
                PieceMaxHealth,
                grounded,
                visualPrefab: visualPrefab);
            index++;
        }

        private static void SaveScene(Scene scene, string scenePath)
        {
            if (!EditorSceneManager.SaveScene(scene, scenePath))
            {
                throw new IOException(LogTag + " failed to save " + scenePath);
            }
        }
    }
}
