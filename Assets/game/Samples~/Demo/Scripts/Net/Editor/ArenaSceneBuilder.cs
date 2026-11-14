using System;
using System.Collections.Generic;
using System.IO;
using Fps.Core;
using Fps.Net;
using Fps.Runtime;
using Unity.AI.Navigation;
using Unity.Netcode;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Fps.Editor
{
    /// <summary>
    /// ネット対戦のマップ NetArena01 を生成する。サーバーは <c>-map NetArena01</c> でこのシーンへ移り、クライアントは Netcode のシーン管理で合わせる。
    /// 60 m 四方の床と外周の壁に、高い壁・低い壁・乗り越えられる段差を置いて NavMesh を焼き、壊せる建物 2 棟
    /// (<see cref="DestructibleStructure"/> と <see cref="NetDestructibleStructure"/>)、チームごとの出現地点 <see cref="SpawnsPerTeam"/> か所、
    /// 拠点制圧の拠点 A/B/C (<see cref="NetControlPoint"/> と旗)、NetTest と同じ配線の <see cref="NetBootstrap"/> を置く (<see cref="NetSceneBuilder.CreateBootstrap"/>)。
    /// 拠点はルールが <see cref="MatchObjective.Domination"/> の試合でだけ使われ、チームデスマッチでは置いてあるだけになる。
    /// 何度実行しても同じ結果になる。batchmode では
    /// <c>-batchmode -quit -projectPath . -executeMethod Fps.Editor.ArenaSceneBuilder.Build</c> で呼ぶ。
    /// </summary>
    /// <remarks>
    /// 配置は z = 0 の線で鏡写しにする。Alpha の陣地は -z 側、Bravo は +z 側で、どの壁・段差・出現地点にも反対側に同じものがある。
    /// 建物と拠点 B は z = 0 の線の上に置き、それ自体が鏡写しになる形にする。
    /// 床の高さは NetTest と同じ <see cref="NetSceneBuilder.GroundY"/> で、出現地点はカプセルの中心 (y = 0) に置く。
    /// </remarks>
    public static class ArenaSceneBuilder
    {
        /// <summary><c>-map</c> に渡す名前。シーンのファイル名と同じ。</summary>
        public const string MapName = "NetArena01";

        public static string ScenePath => BuildSceneList.NetArena01Scene;

        /// <summary>焼いた NavMesh。Unity の慣習どおりシーン名のフォルダに置く。</summary>
        public static string NavMeshDataPath => FpsKitRoot.OutputPath("Scenes/" + MapName + "/NavMesh-Arena.asset");

        public const string MenuPath = "Fps/Net/Build NetArena01 Scene";
        public const string LogTag = "[Net] ArenaSceneBuilder:";

        public const string LevelName = "Level";
        public const string GroundName = "Ground";
        public const string SpawnPointsRootName = "SpawnPoints";

        /// <summary>床の中心から外周までの距離 (m)。床は 60 m 四方。</summary>
        public const float HalfExtent = 30f;

        public const float GroundY = NetSceneBuilder.GroundY;

        public const int SpawnsPerTeam = 8;

        /// <summary>外周の壁の高さと厚さ (m)。乗り越えられず、外へ出られない。</summary>
        public const float BoundaryHeight = 4f;
        public const float BoundaryThickness = 1f;

        /// <summary>高い壁の高さ (m)。乗り越えられる高さ (<see cref="MotorSpec.DefaultVaultMaxHeight"/>) より高く、端から覗くカバーになる。</summary>
        public const float HighWallHeight = 3f;
        public const float HighWallThickness = 0.5f;

        /// <summary>
        /// 低い壁の高さ (m)。<see cref="CoverProbe.LowWallHeight"/> 未満なのでしゃがんで隠れる低いカバーになり、
        /// <see cref="MotorSpec.DefaultVaultMaxHeight"/> 以下なので乗り越えられる。
        /// </summary>
        public const float LowWallHeight = 0.8f;
        public const float LowWallThickness = 0.4f;

        /// <summary>段差の高さと奥行き (m)。<see cref="VaultProbe.MinHeight"/> 以上・<see cref="VaultProbe.MaxDepth"/> 以下なので乗り越えられる。</summary>
        public const float StepHeight = 0.5f;
        public const float StepDepth = 0.8f;

        /// <summary>建物の x 方向の幅・z 方向の奥行き・壁の高さ (部品の数 = m)。</summary>
        public const int BuildingWidth = 6;
        public const int BuildingDepth = 6;
        public const int BuildingWallHeight = 4;

        /// <summary>建物の南北の壁の中央に空ける出入口の幅と高さ (部品の数 = m)。プレイヤー (高さ 2 m) がくぐれる。</summary>
        public const int DoorWidth = 2;
        public const int DoorHeight = 3;

        public const float PieceSize = 1f;
        public const int PieceMaxHealth = DestructiblePiece.DefaultMaxHealth;

        /// <summary>建物 1 棟の部品の数。南北の壁は出入口の分を除き、東西の壁は角を除く。</summary>
        public const int BuildingPieceCount =
            (2 * ((BuildingWidth * BuildingWallHeight) - (DoorWidth * DoorHeight)))
            + (2 * (BuildingDepth - 2) * BuildingWallHeight)
            + (BuildingWidth * BuildingDepth);

        /// <summary>拠点の範囲の水平の半径と高さ (m)。高さは足元から上下に半分ずつなので、床に立つカプセルの中心 (y = 0) が入る。</summary>
        public const float ControlPointRadius = ControlPointZone.DefaultRadius;
        public const float ControlPointHeight = ControlPointZone.DefaultHeight;

        /// <summary>
        /// 拠点の範囲の円と、壁・段差・建物との間に空ける幅 (m)。円の縁に立ったプレイヤーのカプセルが遮蔽物にめり込まず、
        /// 拠点の床で乗り越えや遮蔽の動きが起きないようにする。
        /// </summary>
        public const float ControlPointClearance = NetSceneBuilder.PlayerRadius;

        /// <summary>NavMesh に乗っていることを確かめる点を探す半径 (m)。</summary>
        public const float NavMeshCheckRadius = 1f;

        /// <summary>床 (Plane は 10 m 四方) の拡大率。</summary>
        public static readonly Vector3 GroundScale = new Vector3(HalfExtent / 5f, 1f, HalfExtent / 5f);

        /// <summary>自分のプレイヤーが居ない間の観戦カメラ。両陣地と外周まで見下ろす。</summary>
        public static readonly Vector3 SpectatorPosition = new Vector3(0f, 50f, -42f);
        public static readonly Quaternion SpectatorRotation = Quaternion.Euler(50f, 0f, 0f);

        /// <summary>外周の壁。中心と大きさ (Cube の拡大率)。床の縁のすぐ外に立てる。</summary>
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

        /// <summary>
        /// 高い壁。両陣地の前の東西に 1 枚ずつと、中央の拠点 B を東西から挟む 2 枚 (z = 0 をまたぐ)。
        /// </summary>
        public static readonly IReadOnlyList<(string Name, Vector3 Center, Vector3 Size)> HighWalls = Concat(
            Mirrored("High Wall", "West", OnGround(new Vector3(-10f, 0f, -14f), HighWallHeight), new Vector3(6f, HighWallHeight, HighWallThickness)),
            Mirrored("High Wall", "East", OnGround(new Vector3(10f, 0f, -14f), HighWallHeight), new Vector3(6f, HighWallHeight, HighWallThickness)),
            new[]
            {
                ("High Wall Center West", OnGround(new Vector3(-6f, 0f, 0f), HighWallHeight), new Vector3(HighWallThickness, HighWallHeight, 6f)),
                ("High Wall Center East", OnGround(new Vector3(6f, 0f, 0f), HighWallHeight), new Vector3(HighWallThickness, HighWallHeight, 6f)),
            });

        /// <summary>低い壁。両陣地から中央へ出たところと、東西の通り道に置き、相手の陣地に向ける。</summary>
        public static readonly IReadOnlyList<(string Name, Vector3 Center, Vector3 Size)> LowWalls = Concat(
            Mirrored("Low Wall", "Center", OnGround(new Vector3(0f, 0f, -9f), LowWallHeight), new Vector3(4f, LowWallHeight, LowWallThickness)),
            Mirrored("Low Wall", "West", OnGround(new Vector3(-24f, 0f, -8f), LowWallHeight), new Vector3(4f, LowWallHeight, LowWallThickness)),
            Mirrored("Low Wall", "East", OnGround(new Vector3(24f, 0f, -8f), LowWallHeight), new Vector3(4f, LowWallHeight, LowWallThickness)));

        /// <summary>乗り越えられる段差。両陣地から東西の建物へ向かう通り道に置く。</summary>
        public static readonly IReadOnlyList<(string Name, Vector3 Center, Vector3 Size)> Steps = Concat(
            Mirrored("Step", "West", OnGround(new Vector3(-17f, 0f, -19f), StepHeight), new Vector3(4f, StepHeight, StepDepth)),
            Mirrored("Step", "East", OnGround(new Vector3(17f, 0f, -19f), StepHeight), new Vector3(4f, StepHeight, StepDepth)));

        /// <summary>壊せる建物。床面の中心。z = 0 の線の上に置き、出入口は南北 (両陣地の方) に向ける。</summary>
        public static readonly IReadOnlyList<(string Name, Vector3 GroundCenter)> Buildings = new[]
        {
            ("Building West", new Vector3(-18f, GroundY, 0f)),
            ("Building East", new Vector3(18f, GroundY, 0f)),
        };

        /// <summary>
        /// 出現地点。Alpha は -z の陣地に 2 列 × 4 か所、Bravo はその鏡写し。カプセルの中心が y = 0 に来るので床にちょうど立つ。
        /// </summary>
        public static readonly IReadOnlyList<(string Name, Vector3 Position, NetSpawnPoint.SpawnTeam Team)> SpawnPoints = CreateSpawnLayout();

        /// <summary>
        /// 拠点制圧の拠点 (足元、床の高さ) と番号 (HUD とログで 0 = A)。A は Alpha の陣地寄り、B は中央、C は Bravo の陣地寄り。
        /// A は陣地の前の低い壁と高い壁の間、B は中央の高い壁 2 枚の間に、範囲の円が <see cref="ControlPointClearance"/> を空けて
        /// どの壁・段差・建物にも掛からないように置く。
        /// </summary>
        public static readonly IReadOnlyList<(string Name, Vector3 Position, int Index)> ControlPoints = new[]
        {
            (ControlPointName(0), new Vector3(0f, GroundY, -15f), 0),
            (ControlPointName(1), new Vector3(0f, GroundY, 0f), 1),
            (ControlPointName(2), new Vector3(0f, GroundY, 15f), 2),
        };

        [MenuItem(MenuPath)]
        public static void Build()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                Debug.Log(LogTag + " cancelled");
                return;
            }

            BuildAt(ScenePath, NavMeshDataPath);
        }

        /// <summary>
        /// シーンを生成して NavMesh を焼き、両方を保存して Build Settings に加える (既存の並びは変えない)。
        /// プレイヤーと試合の進行役のプレハブが無ければ先に <see cref="NetSceneBuilder.Build"/> で作る。
        /// 拠点の範囲が壁・段差・建物・出現地点に掛かる、NavMesh が出現地点・拠点に届かない、
        /// または建物と拠点の NetworkObject に ID が付かなければ例外を投げる (batchmode では終了コード 1)。
        /// </summary>
        public static void BuildAt(string scenePath, string navMeshDataPath)
        {
            scenePath = BootstrapSceneBuilder.ValidateScenePath(scenePath);
            navMeshDataPath = SceneBuildHelpers.ValidateAssetPath(navMeshDataPath);

            var overlaps = ControlPointOverlaps();
            if (overlaps.Count > 0)
            {
                throw new InvalidOperationException(LogTag + " control point areas overlap the level: " + string.Join("; ", overlaps));
            }

            // NetSceneBuilder.Build は NetTest を開くので、こちらのシーンを作る前に済ませる
            NetSceneBuilder.EnsurePrefabs();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // NewScene(Single) は参照されていないアセットをアンロードするので、プレハブはシーンを作ってから読む
            var (playerPrefab, matchPrefab) = NetSceneBuilder.LoadPrefabs();

            SceneBuildHelpers.EnsureFolder(Path.GetDirectoryName(scenePath));
            SceneBuildHelpers.EnsureFolder(Path.GetDirectoryName(navMeshDataPath));

            // 建物の部品は NavMeshObstacle で NavMesh を削るので、壊れたところを通れるよう地形だけを置いた状態で焼く
            var surface = CreateLevel(scene);
            SceneBuildHelpers.BakeNavMesh(surface);
            SceneBuildHelpers.SaveNavMeshData(surface, navMeshDataPath);
            RequireNavigable();

            PopulateScene(scene, playerPrefab, matchPrefab);
            SaveScene(scene, scenePath);

            // シーンに置いた NetworkObject の ID (GlobalObjectIdHash) は、保存済みのシーンを読んだときの OnValidate で決まる。
            // 開き直して決めさせ、それを書き戻さないと、サーバーとクライアントで建物を突き合わせられない
            scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            EditorSceneManager.MarkSceneDirty(scene);
            SaveScene(scene, scenePath);
            RequireSceneNetworkObjectIds(scene);

            BuildSceneList.Register(scenePath);
            AssetDatabase.SaveAssets();
            Debug.Log(LogTag + " wrote " + scenePath + " and " + navMeshDataPath + " (" + SpawnPoints.Count + " spawn point(s), "
                + Buildings.Count + " building(s), " + ControlPoints.Count + " control point(s)); start a server with "
                + NetLaunchOptions.MapFlag + " " + MapName);
        }

        /// <summary>
        /// 床・外周の壁・高い壁・低い壁・段差を <see cref="LevelName"/> の下にまとめて空のシーンに置き、NavMeshSurface を返す。
        /// NavMesh は Level の子のコライダーだけから焼く (焼くのは呼び出し側)。
        /// </summary>
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

        /// <summary>
        /// 焼いた NavMesh に出現地点・拠点・建物の出入口の前が乗り、Alpha の最初の出現地点からどれへも歩いて行けることを確かめる。
        /// </summary>
        public static void RequireNavigable()
        {
            var points = NavMeshCheckPoints();
            SceneBuildHelpers.RequireOnNavMesh(points, NavMeshCheckRadius, "arena point");
            SceneBuildHelpers.RequireReachable(Feet(SpawnPoints[0].Position), points, NavMeshCheckRadius, "arena point");
        }

        /// <summary>NavMesh に乗っているべき点 (足元)。出現地点・拠点・建物の南北の出入口の 1.5 m 外。</summary>
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

            var doorOffset = (BuildingDepth * PieceSize / 2f) + 1.5f;
            foreach (var (_, center) in Buildings)
            {
                points.Add(center + new Vector3(0f, 0f, -doorOffset));
                points.Add(center + new Vector3(0f, 0f, doorOffset));
            }

            return points;
        }

        /// <summary>
        /// NetBootstrap・観戦カメラ・ライト・破片の置き場・建物・出現地点・拠点を置き、NetBootstrap を返す。
        /// プレハブの配線は NetTest と同じ (<see cref="NetSceneBuilder.CreateBootstrap"/>)。
        /// </summary>
        public static NetBootstrap PopulateScene(Scene scene, GameObject playerPrefab, GameObject matchPrefab)
        {
            SceneBuildHelpers.RequireLoaded(scene);

            var bootstrap = NetSceneBuilder.CreateBootstrap(scene, playerPrefab, matchPrefab);
            NetSceneBuilder.CreateSpectatorCamera(scene, SpectatorPosition, SpectatorRotation);
            NetSceneBuilder.CreateSun(scene);

            var poolObject = new GameObject(DebrisPool.DefaultObjectName);
            SceneManager.MoveGameObjectToScene(poolObject, scene);
            var pool = poolObject.AddComponent<DebrisPool>();

            foreach (var (buildingName, groundCenter) in Buildings)
            {
                CreateBuilding(scene, buildingName, groundCenter, pool);
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

            EditorSceneManager.MarkSceneDirty(scene);
            return bootstrap;
        }

        /// <summary>
        /// position (足元) に拠点を置いて返す。<see cref="NetControlPoint"/> が占領の状態を同期し、<see cref="ControlPointZone"/> が範囲を持って
        /// Play 中に地面の円盤を出す。中心には遠くから見える旗を立てる (<see cref="DominationSceneBuilder.AddFlag"/>、当たり判定なし)。
        /// </summary>
        public static NetControlPoint CreateControlPoint(Scene scene, string pointName, Vector3 position, int index)
        {
            SceneBuildHelpers.RequireLoaded(scene);

            // シーンに置いた NetworkObject は Netcode がシーンの直下から探して Spawn するので、親を付けない
            var point = new GameObject(pointName);
            SceneManager.MoveGameObjectToScene(point, scene);
            point.transform.SetPositionAndRotation(position, Quaternion.identity);

            try
            {
                point.AddComponent<NetworkObject>();
                var zone = point.AddComponent<ControlPointZone>();
                zone.Index = index;
                zone.Radius = ControlPointRadius;
                zone.Height = ControlPointHeight;
                var networked = point.AddComponent<NetControlPoint>();
                DominationSceneBuilder.AddFlag(point.transform);
                return networked;
            }
            catch
            {
                UnityEngine.Object.DestroyImmediate(point);
                throw;
            }
        }

        /// <summary>シーンと HUD で使う拠点の名前 (番号 0 = "Control Point A")。</summary>
        public static string ControlPointName(int index)
        {
            return "Control Point " + DominationHudFormat.PointName(index);
        }

        /// <summary>
        /// 拠点の範囲の円 (半径に <see cref="ControlPointClearance"/> を足したもの) が、壁 (外周を含む)・段差・建物の床面・
        /// 出現地点・ほかの拠点の範囲に掛かっていれば、その組み合わせを返す。掛かっていなければ空。上から見た平面で調べる。
        /// </summary>
        public static List<string> ControlPointOverlaps()
        {
            var problems = new List<string>();
            var reach = ControlPointRadius + ControlPointClearance;
            var obstacles = new List<(string Name, Vector3 Center, Vector3 Size)>();
            obstacles.AddRange(BoundaryWalls);
            obstacles.AddRange(HighWalls);
            obstacles.AddRange(LowWalls);
            obstacles.AddRange(Steps);
            foreach (var (buildingName, groundCenter) in Buildings)
            {
                obstacles.Add((buildingName, groundCenter, new Vector3(BuildingWidth, 0f, BuildingDepth) * PieceSize));
            }

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

        /// <summary>
        /// groundCenter (床面の中心) に壊せる建物を置いて返す。シーンの直下に置き、壊れた部品は <see cref="NetDestructibleStructure"/> が同期する。
        /// 壁 4 面 (下段は地面に接する) と屋根を 1 m の Cube の部品で組み、南北の壁の中央に出入口を空ける。部品の添字は作った順に 0 から振る。
        /// 弾で壊れるかは試合の開始時に <see cref="NetTeamDeathmatch"/> がルールで決めるので、ここでは壊れない状態にしておく。
        /// </summary>
        public static DestructibleStructure CreateBuilding(Scene scene, string buildingName, Vector3 groundCenter, DebrisPool pool)
        {
            SceneBuildHelpers.RequireLoaded(scene);

            // シーンに置いた NetworkObject は Netcode がシーンの直下から探して Spawn するので、親を付けない
            var root = new GameObject(buildingName);
            SceneManager.MoveGameObjectToScene(root, scene);
            root.transform.SetPositionAndRotation(groundCenter, Quaternion.identity);

            try
            {
                var structure = root.AddComponent<DestructibleStructure>();
                structure.DestructionEnabled = false;
                structure.DebrisPool = pool;
                root.AddComponent<NetDestructibleStructure>();

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
                        CreatePiece(south, x, y, new Vector3(px, height, minZ), ref index, grounded);
                        CreatePiece(north, x, y, new Vector3(px, height, maxZ), ref index, grounded);
                    }

                    for (var z = 1; z < BuildingDepth - 1; z++)
                    {
                        var pz = minZ + (z * PieceSize);
                        CreatePiece(west, z, y, new Vector3(minX, height, pz), ref index, grounded);
                        CreatePiece(east, z, y, new Vector3(maxX, height, pz), ref index, grounded);
                    }
                }

                var roofHeight = (BuildingWallHeight + 0.5f) * PieceSize;
                for (var x = 0; x < BuildingWidth; x++)
                {
                    for (var z = 0; z < BuildingDepth; z++)
                    {
                        CreatePiece(roof, x, z, new Vector3(minX + (x * PieceSize), roofHeight, minZ + (z * PieceSize)), ref index, false);
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

        /// <summary>南北の壁の column 列・row 段目が出入口の穴か。</summary>
        public static bool IsDoor(int column, int row)
        {
            var first = (BuildingWidth - DoorWidth) / 2;
            return row < DoorHeight && column >= first && column < first + DoorWidth;
        }

        /// <summary>出現地点 (カプセルの中心) の足元。</summary>
        public static Vector3 Feet(Vector3 spawnPosition)
        {
            return spawnPosition + new Vector3(0f, GroundY, 0f);
        }

        /// <summary>z を反転した点。Alpha の側の配置から Bravo の側を作る。</summary>
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

        private static float HorizontalDistance(Vector3 a, Vector3 b)
        {
            return new Vector2(a.x - b.x, a.z - b.z).magnitude;
        }

        /// <summary>point から、center・size の箱を上から見た長方形までの距離。中にあれば 0。</summary>
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

        private static void CreatePiece(Transform group, int column, int row, Vector3 buildingLocalPosition, ref int index, bool grounded)
        {
            // グループは建物の原点に置いたままなので、建物から見た位置をそのまま使える
            DestructiblePiece.Create(
                group,
                group.name + "-" + column + "-" + row,
                buildingLocalPosition,
                Vector3.one * PieceSize,
                index,
                PieceMaxHealth,
                grounded);
            index++;
        }

        private static void SaveScene(Scene scene, string scenePath)
        {
            if (!EditorSceneManager.SaveScene(scene, scenePath))
            {
                throw new IOException(LogTag + " failed to save " + scenePath);
            }
        }

        private static void RequireSceneNetworkObjectIds(Scene scene)
        {
            foreach (var root in scene.GetRootGameObjects())
            {
                foreach (var networkObject in root.GetComponentsInChildren<NetworkObject>(true))
                {
                    if (networkObject.PrefabIdHash == 0)
                    {
                        throw new InvalidOperationException(
                            LogTag + " " + networkObject.name + " in " + scene.path + " has no GlobalObjectIdHash after reopening the scene;"
                            + " clients could not match it with the server's");
                    }
                }
            }
        }
    }
}
