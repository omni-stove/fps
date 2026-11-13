using System;
using System.Collections.Generic;
using System.IO;
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
    /// 拠点制圧 (<see cref="DominationRunner"/>) を一人称で遊ぶシーン Domination を生成する。床 (NavMeshSurface 付き)・外周の壁・遮蔽物を置いて
    /// NavMesh を焼き (<see cref="EnemyTestSceneBuilder.BakeNavMesh"/>)、両チームの出現点、<see cref="GameBootstrap"/> (一人称・床は作らない・
    /// ルールは <see cref="GameRulesAssetBuilder.DominationPath"/>)、Bravo の敵 (<see cref="EnemyAgent"/>)、拠点 A/B/C
    /// (<see cref="ControlPointZone"/> と旗竿の目印)、<see cref="DominationRunner"/> を置く。プレイヤー・ライト・HUD は実行時に GameBootstrap が作る。
    /// 保存したシーンは Build Settings に (並びを変えずに) 加える。何度実行しても同じ結果になる。Editor を閉じた状態で次のように呼べる:
    /// <code>unity -batchmode -quit -projectPath . -executeMethod Fps.Editor.DominationSceneBuilder.Build</code>
    /// </summary>
    /// <remarks>
    /// 配置 (上から見て、床は x = ±20、z = -10..50。プレイヤーは原点で +Z を向く):
    /// Alpha は手前 (z = 0)、Bravo は奥 (z = 40) に出る。拠点は A (-10, 10)・B (0, 20)・C (10, 30) と対角に並び、A が Alpha、C が Bravo に近い。
    /// 敵は試合の始めから拠点を順に回り、各拠点では中心の周りを <see cref="LoiterLaps"/> 周歩いて居座る (1 人で占領し終える
    /// <see cref="DominationRunner.DefaultCaptureSeconds"/> 秒より長くいられるように)。プレイヤーを見つけたら巡回をやめて撃ちに来る。
    /// 敵は <see cref="SideMember"/> を持たない。持たせると敵は陣営の相手を探すが、DominationRunner はプレイヤーに SideMember を付けないので、
    /// 誰も狙わなくなる。
    /// </remarks>
    public static class DominationSceneBuilder
    {
        public const string ScenePath = BuildSceneList.DominationScene;
        public const string MenuPath = "Fps/Build Domination Scene";
        public const string LogTag = "[DominationSceneBuilder]";

        public const string FloorName = ModeSceneBuilder.FloorName;
        public const string LevelRootName = ModeSceneBuilder.LevelRootName;
        public const string BootstrapObjectName = ModeSceneBuilder.BootstrapObjectName;
        public const string RunnerObjectName = "DominationRunner";
        public const string SpawnPointsRootName = ModeSceneBuilder.SpawnPointsRootName;
        public const string ControlPointsRootName = "ControlPoints";
        public const string BotsRootName = ModeSceneBuilder.BotsRootName;

        /// <summary>拠点の目印の旗竿と旗の名前。当たり判定は持たせない (弾と移動を遮らないように)。</summary>
        public const string PoleName = "Pole";

        public const string FlagName = "Flag";

        public const float PoleHeight = 3f;
        public const float PoleDiameter = 0.15f;
        public static readonly Vector3 FlagSize = new Vector3(0.9f, 0.6f, 0.05f);

        /// <summary>敵に持たせる武器の Id。武器の一覧 (<see cref="WeaponAssetBuilder.LoadCatalog"/>) から引く。</summary>
        public const string BotWeaponId = WeaponAssetBuilder.RifleId;

        /// <summary>焼いた NavMesh。<see cref="ModeSceneBuilder"/> と同じくシーン名のフォルダに置く。</summary>
        public static readonly string NavMeshDataPath = ModeSceneBuilder.NavMeshDataPathFor(ScenePath);

        public static readonly Vector3 FloorCenter = ModeSceneBuilder.DefaultFloorCenter;
        public static readonly Vector3 FloorScale = ModeSceneBuilder.DefaultFloorScale;

        /// <summary>拠点の中心 (床の高さ)。並びの順に番号 0, 1, 2 (A, B, C) を振る。</summary>
        public static readonly IReadOnlyList<Vector3> ControlPoints = new[]
        {
            new Vector3(-10f, 0f, 10f),
            new Vector3(0f, 0f, 20f),
            new Vector3(10f, 0f, 30f),
        };

        public const float ControlPointRadius = ControlPointZone.DefaultRadius;
        public const float ControlPointHeight = ControlPointZone.DefaultHeight;

        /// <summary>Alpha の出現点。先頭は GameBootstrap がプレイヤーを出す所 (<see cref="ModeSceneBuilder.PlayerFeet"/>、+Z 向き)。</summary>
        public static readonly IReadOnlyList<ModeSpawnPlacement> AlphaSpawns = new[]
        {
            new ModeSpawnPlacement(ModeSceneBuilder.PlayerFeet, Vector3.forward),
            new ModeSpawnPlacement(new Vector3(-6f, 0f, ModeSceneBuilder.DefaultNearSpawnZ), Vector3.forward),
            new ModeSpawnPlacement(new Vector3(6f, 0f, ModeSceneBuilder.DefaultNearSpawnZ), Vector3.forward),
        };

        /// <summary>Bravo の出現点。敵は先頭から 1 体ずつ立つ。</summary>
        public static readonly IReadOnlyList<ModeSpawnPlacement> BravoSpawns =
            ModeSceneBuilder.SpawnRow(ModeSceneBuilder.DefaultFarSpawnZ, Vector3.back);

        /// <summary>
        /// 敵ごとに回る拠点の番号 (<see cref="ControlPoints"/> の添字)。要素の数が敵の数。
        /// 手近な C と B から取りに行き、ばらけるよう順番を変える。
        /// </summary>
        public static readonly IReadOnlyList<IReadOnlyList<int>> BotObjectives = new IReadOnlyList<int>[]
        {
            new[] { 2, 1, 0 },
            new[] { 1, 0, 2 },
            new[] { 2, 0, 1 },
            new[] { 1, 2, 0 },
        };

        /// <summary>拠点に居座るときに回る、中心からのずれ。拠点の半径に十分収まる。</summary>
        public static readonly IReadOnlyList<Vector3> LoiterOffsets = new[]
        {
            new Vector3(2f, 0f, 2f),
            new Vector3(-2f, 0f, 2f),
            new Vector3(-2f, 0f, -2f),
            new Vector3(2f, 0f, -2f),
        };

        /// <summary>1 つの拠点で <see cref="LoiterOffsets"/> を何周するか。</summary>
        public const int LoiterLaps = 2;

        /// <summary>
        /// 遮蔽物。拠点の間の通り道を仕切る高い壁 2 枚、各拠点の前の低い壁 (上端 1.05m、<see cref="EnemyTestSceneBuilder.LowCoverSize"/> と同じ高さ)、
        /// 隅の柱 2 本。どれも出現点と拠点の居座る道から離れている。
        /// </summary>
        public static readonly IReadOnlyList<ModeObstacle> Obstacles = new[]
        {
            new ModeObstacle("Wall-West", new Vector3(-10f, 1.5f, 20f), new Vector3(6f, 3f, 1f)),
            new ModeObstacle("Wall-East", new Vector3(10f, 1.5f, 20f), new Vector3(6f, 3f, 1f)),
            new ModeObstacle("Cover-A", new Vector3(-4f, 0.525f, 10f), new Vector3(0.5f, 1.05f, 4f)),
            new ModeObstacle("Cover-BSouth", new Vector3(0f, 0.525f, 13f), new Vector3(4f, 1.05f, 0.5f)),
            new ModeObstacle("Cover-BNorth", new Vector3(0f, 0.525f, 27f), new Vector3(4f, 1.05f, 0.5f)),
            new ModeObstacle("Cover-C", new Vector3(4f, 0.525f, 30f), new Vector3(0.5f, 1.05f, 4f)),
            new ModeObstacle("Pillar-West", new Vector3(-14f, 1.5f, 32f), new Vector3(2f, 3f, 2f)),
            new ModeObstacle("Pillar-East", new Vector3(14f, 1.5f, 8f), new Vector3(2f, 3f, 2f)),
        };

        /// <summary>拠点の GameObject の名前。番号 0 は <c>ControlPoint-A</c>。</summary>
        public static string ControlPointName(int index)
        {
            return "ControlPoint-" + DominationHudFormat.PointName(index);
        }

        /// <summary>敵の名前。DominationRunner は名前の順に Id を振るので、番号 (1 から) で並ぶようにする。</summary>
        public static string BotName(int number)
        {
            return $"Bravo-{number}";
        }

        /// <summary>敵の <see cref="EnemyAgent.Id"/> (ログと当たりの記録に出る)。</summary>
        public static string BotId(int number)
        {
            return $"bravo-{number}";
        }

        /// <summary>
        /// 敵 (番号は 0 から) の巡回路 (足元)。<see cref="BotObjectives"/> の拠点を順に訪れ、各拠点で <see cref="LoiterOffsets"/> を
        /// <see cref="LoiterLaps"/> 周する。出現点は含めない (倒れて戻った後も、まっすぐ最初の拠点へ向かうように)。
        /// </summary>
        public static IReadOnlyList<Vector3> BotPatrolRoute(int botIndex)
        {
            if (botIndex < 0 || botIndex >= BotObjectives.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(botIndex), botIndex, $"there are {BotObjectives.Count} bot(s)");
            }

            var route = new List<Vector3>();
            foreach (var point in BotObjectives[botIndex])
            {
                var center = ControlPoints[point];
                for (var lap = 0; lap < LoiterLaps; lap++)
                {
                    foreach (var offset in LoiterOffsets)
                    {
                        route.Add(center + offset);
                    }
                }
            }

            return route;
        }

        [MenuItem(MenuPath)]
        public static void Build()
        {
            // メニューから呼ばれたとき、開いているシーンの未保存の変更を黙って捨てない。
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                Debug.Log(LogTag + " cancelled");
                return;
            }

            BuildAt(ScenePath, NavMeshDataPath);
        }

        /// <summary>
        /// シーンを組んで NavMesh を焼き、両方を保存して Build Settings に加える (既存の並びは変えない)。
        /// 武器・ルールのアセットが無いか不正なら何も保存せずに例外を投げる (先に <see cref="WeaponAssetBuilder"/>.Build と
        /// <see cref="GameRulesAssetBuilder"/>.Build を走らせる)。焼いた NavMesh に出現点・巡回地点が乗らないか、プレイヤーの出現点から
        /// 行けない点があれば、シーンを保存せずに例外を投げる (batchmode では終了コード 1 になる)。
        /// </summary>
        public static DominationRunner BuildAt(string scenePath, string navMeshDataPath)
        {
            scenePath = BootstrapSceneBuilder.ValidateScenePath(scenePath);
            navMeshDataPath = SceneBuildHelpers.ValidateAssetPath(navMeshDataPath);

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // NewScene(Single) は参照されていないアセットをアンロードし、先に読んだ定義が null になるので、シーンを作ってから読む。
            var playerWeapons = WeaponAssetBuilder.LoadPlayerLoadout();
            var catalog = WeaponAssetBuilder.LoadCatalog();
            var botWeapon = WeaponAssetBuilder.FindInCatalog(catalog, BotsRootName, BotWeaponId);
            var rules = GameRulesAssetBuilder.LoadRules(GameRulesAssetBuilder.DominationPath);

            SceneBuildHelpers.EnsureFolder(Path.GetDirectoryName(scenePath));
            SceneBuildHelpers.EnsureFolder(Path.GetDirectoryName(navMeshDataPath));

            // 敵のコライダーで NavMesh に穴が空かないよう、地形だけを置いた状態で焼く。
            var surface = CreateLevel(scene);
            EnemyTestSceneBuilder.BakeNavMesh(surface, navMeshDataPath);
            RequireWalkable();

            var runner = PopulateScene(scene, playerWeapons, rules, botWeapon);

            if (!EditorSceneManager.SaveScene(scene, scenePath))
            {
                throw new IOException($"Failed to save the Domination scene to '{scenePath}'");
            }

            BuildSceneList.Register(scenePath);
            AssetDatabase.SaveAssets();

            Debug.Log($"{LogTag} saved '{scenePath}' and '{navMeshDataPath}' ({ControlPoints.Count} control point(s), {BotObjectives.Count} bot(s))");
            return runner;
        }

        /// <summary>
        /// 床 (NavMeshSurface 付き)、外周の壁、遮蔽物 (<see cref="Obstacles"/>) を空のシーンに置き、床の NavMeshSurface を返す。
        /// 焼くのは <see cref="EnemyTestSceneBuilder.BakeNavMesh"/>。
        /// </summary>
        public static NavMeshSurface CreateLevel(Scene scene)
        {
            SceneBuildHelpers.RequireEmpty(scene, "the Domination level");

            var floor = SceneBuildHelpers.CreateStaticPlane(scene, null, FloorName, FloorCenter, FloorScale);
            var surface = SceneBuildHelpers.AddNavMeshSurface(floor, CollectObjects.All);

            var level = new GameObject(LevelRootName);
            SceneManager.MoveGameObjectToScene(level, scene);
            level.isStatic = true;

            // Plane は 10m 四方。外へ歩いて落ちないよう、縁の内側に沿って壁を立てる。
            var halfX = FloorScale.x * 5f;
            var halfZ = FloorScale.z * 5f;
            var inset = ModeSceneBuilder.BoundaryWallThickness / 2f;
            var height = ModeSceneBuilder.BoundaryWallHeight;
            var y = height / 2f;
            var alongX = new Vector3(halfX * 2f, height, ModeSceneBuilder.BoundaryWallThickness);
            var alongZ = new Vector3(ModeSceneBuilder.BoundaryWallThickness, height, halfZ * 2f);
            SceneBuildHelpers.CreateStaticBox(scene, level.transform, "Boundary-North", FloorCenter + new Vector3(0f, y, halfZ - inset), alongX);
            SceneBuildHelpers.CreateStaticBox(scene, level.transform, "Boundary-South", FloorCenter + new Vector3(0f, y, -halfZ + inset), alongX);
            SceneBuildHelpers.CreateStaticBox(scene, level.transform, "Boundary-East", FloorCenter + new Vector3(halfX - inset, y, 0f), alongZ);
            SceneBuildHelpers.CreateStaticBox(scene, level.transform, "Boundary-West", FloorCenter + new Vector3(-halfX + inset, y, 0f), alongZ);

            foreach (var obstacle in Obstacles)
            {
                SceneBuildHelpers.CreateStaticBox(scene, level.transform, obstacle.Name, obstacle.Center, obstacle.Size);
            }

            EditorSceneManager.MarkSceneDirty(scene);
            return surface;
        }

        /// <summary>
        /// 焼いた NavMesh に、両チームの出現点・拠点の中心・敵の巡回地点が乗っていて、プレイヤーの出現点からそのどれへも、
        /// 敵の出現点から自分の巡回地点へも歩けることを確かめる。満たさなければ例外を投げる。
        /// </summary>
        public static void RequireWalkable()
        {
            var radius = ModeSceneBuilder.NavMeshCheckRadius;
            var spawns = new List<Vector3>();
            foreach (var spawn in AlphaSpawns)
            {
                spawns.Add(spawn.Feet);
            }

            foreach (var spawn in BravoSpawns)
            {
                spawns.Add(spawn.Feet);
            }

            SceneBuildHelpers.RequireOnNavMesh(spawns, radius, "spawn point");
            SceneBuildHelpers.RequireReachable(ModeSceneBuilder.PlayerFeet, spawns, radius, "spawn point");
            SceneBuildHelpers.RequireOnNavMesh(ControlPoints, radius, "control point");
            SceneBuildHelpers.RequireReachable(ModeSceneBuilder.PlayerFeet, ControlPoints, radius, "control point");

            for (var i = 0; i < BotObjectives.Count; i++)
            {
                var route = BotPatrolRoute(i);
                SceneBuildHelpers.RequireOnNavMesh(route, radius, $"bot #{i + 1} patrol point");
                SceneBuildHelpers.RequireReachable(BravoSpawns[i].Feet, route, radius, $"bot #{i + 1} patrol point");
            }
        }

        /// <summary>
        /// 地形を置いたシーン (<see cref="CreateLevel"/> の後) に、両チームの出現点、GameBootstrap (一人称・床は作らない、playerWeapons と rules を配線)、
        /// Bravo の敵 (botWeapon で撃つ)、拠点と目印、DominationRunner (出現点を配線) を置き、DominationRunner を返す。
        /// 引数が不正なら何も置かずに例外を投げる。途中で失敗したら置いた物を消してから投げ直す。
        /// </summary>
        public static DominationRunner PopulateScene(
            Scene scene, IReadOnlyList<WeaponDefinition> playerWeapons, GameRulesDefinition rules, WeaponDefinition botWeapon)
        {
            SceneBuildHelpers.RequireLoaded(scene);
            SceneBuildHelpers.RequirePlayerWeapons(playerWeapons, nameof(playerWeapons));
            if (rules == null)
            {
                throw new ArgumentNullException(nameof(rules));
            }

            if (botWeapon == null)
            {
                throw new ArgumentNullException(nameof(botWeapon));
            }

            // 不正なルールを配線しないよう、何か置く前に検査する。
            rules.ToRules();

            var created = new List<GameObject>();
            try
            {
                var spawnRoot = new GameObject(SpawnPointsRootName);
                created.Add(spawnRoot);
                SceneManager.MoveGameObjectToScene(spawnRoot, scene);
                var alphaSpawns = CreateSpawnPoints(spawnRoot.transform, Team.Alpha, AlphaSpawns);
                var bravoSpawns = CreateSpawnPoints(spawnRoot.transform, Team.Bravo, BravoSpawns);

                var bootstrapObject = new GameObject(BootstrapObjectName);
                created.Add(bootstrapObject);
                SceneManager.MoveGameObjectToScene(bootstrapObject, scene);
                var bootstrap = bootstrapObject.AddComponent<GameBootstrap>();
                bootstrap.ViewMode = ViewMode.FirstPerson;
                bootstrap.CreatesFloor = false;
                bootstrap.WeaponDefinitions = playerWeapons;
                bootstrap.RulesDefinition = rules;

                var botsRoot = new GameObject(BotsRootName);
                created.Add(botsRoot);
                SceneManager.MoveGameObjectToScene(botsRoot, scene);
                for (var i = 0; i < BotObjectives.Count; i++)
                {
                    var spawn = BravoSpawns[i];
                    SceneBuildHelpers.CreateEnemy(
                        scene,
                        botsRoot.transform,
                        BotName(i + 1),
                        BotId(i + 1),
                        spawn.Feet,
                        Quaternion.LookRotation(spawn.Facing, Vector3.up),
                        BotPatrolRoute(i),
                        ModeSceneBuilder.DefaultBotBody,
                        botWeapon);
                }

                var pointsRoot = new GameObject(ControlPointsRootName);
                created.Add(pointsRoot);
                SceneManager.MoveGameObjectToScene(pointsRoot, scene);
                for (var i = 0; i < ControlPoints.Count; i++)
                {
                    CreateControlPoint(pointsRoot.transform, i, ControlPoints[i]);
                }

                var runnerObject = new GameObject(RunnerObjectName);
                created.Add(runnerObject);
                SceneManager.MoveGameObjectToScene(runnerObject, scene);
                var runner = runnerObject.AddComponent<DominationRunner>();
                runner.SetSpawnPoints(Team.Alpha, alphaSpawns);
                runner.SetSpawnPoints(Team.Bravo, bravoSpawns);

                EditorSceneManager.MarkSceneDirty(scene);
                return runner;
            }
            catch
            {
                foreach (var gameObject in created)
                {
                    if (gameObject != null)
                    {
                        UnityEngine.Object.DestroyImmediate(gameObject);
                    }
                }

                throw;
            }
        }

        private static List<Transform> CreateSpawnPoints(Transform root, Team team, IReadOnlyList<ModeSpawnPlacement> spawns)
        {
            var points = new List<Transform>(spawns.Count);
            for (var i = 0; i < spawns.Count; i++)
            {
                var spawn = spawns[i];
                var marker = new GameObject($"Spawn-{team}-{i + 1}");
                marker.transform.SetParent(root, false);
                marker.transform.SetPositionAndRotation(spawn.Feet, Quaternion.LookRotation(spawn.Facing, Vector3.up));
                points.Add(marker.transform);
            }

            return points;
        }

        /// <summary>
        /// 拠点 1 つ (<see cref="ControlPointZone"/>) と、その中心に立てる旗竿と旗を置く。範囲の円盤は Play 中に ControlPointZone が作る。
        /// 旗竿は遠くからどこが拠点かを見せるためのもので、当たり判定を外す。
        /// </summary>
        private static ControlPointZone CreateControlPoint(Transform root, int index, Vector3 center)
        {
            var point = new GameObject(ControlPointName(index));
            point.transform.SetParent(root, false);
            point.transform.SetPositionAndRotation(center, Quaternion.identity);

            var zone = point.AddComponent<ControlPointZone>();
            zone.Index = index;
            zone.Radius = ControlPointRadius;
            zone.Height = ControlPointHeight;

            var pole = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            pole.name = PoleName;
            UnityEngine.Object.DestroyImmediate(pole.GetComponent<Collider>());
            pole.transform.SetParent(point.transform, false);

            // Cylinder は高さ 2・直径 1。
            pole.transform.localPosition = new Vector3(0f, PoleHeight / 2f, 0f);
            pole.transform.localScale = new Vector3(PoleDiameter, PoleHeight / 2f, PoleDiameter);

            var flag = GameObject.CreatePrimitive(PrimitiveType.Cube);
            flag.name = FlagName;
            UnityEngine.Object.DestroyImmediate(flag.GetComponent<Collider>());
            flag.transform.SetParent(point.transform, false);
            flag.transform.localPosition = new Vector3((FlagSize.x + PoleDiameter) / 2f, PoleHeight - (FlagSize.y / 2f), 0f);
            flag.transform.localScale = FlagSize;

            return zone;
        }
    }
}
