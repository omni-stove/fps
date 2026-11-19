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
    // 敵は SideMember を持たない。持たせると敵は陣営の相手を探すが、DominationRunner はプレイヤーに SideMember を付けないので、
    // 誰も狙わなくなる。
    public static class DominationSceneBuilder
    {
        public static string ScenePath => BuildSceneList.DominationScene;

        public static string DefinitionPath => ModeDefinitionAssets.PathOf(GameRulesAssetBuilder.DominationName);

        public static DominationDefinition LoadOrCreateDefinition(string path)
        {
            var definition = ModeDefinitionAssets.LoadOrCreate<DominationDefinition>(path, _ => { });
            definition.Validate();
            return definition;
        }

        public const string MenuPath = SceneBuildSteps.MenuPath;
        public const string LogTag = "[DominationSceneBuilder]";

        public const string FloorName = ModeSceneBuilder.FloorName;
        public const string LevelRootName = ModeSceneBuilder.LevelRootName;
        public const string BootstrapObjectName = ModeSceneBuilder.BootstrapObjectName;
        public const string RunnerObjectName = "DominationRunner";
        public const string SpawnPointsRootName = ModeSceneBuilder.SpawnPointsRootName;
        public const string ControlPointsRootName = "ControlPoints";
        public const string BotsRootName = ModeSceneBuilder.BotsRootName;

        public const string PoleName = "Pole";

        public const string FlagName = "Flag";

        public const float PoleHeight = 3f;
        public const float PoleDiameter = 0.15f;
        public static readonly Vector3 FlagSize = new Vector3(0.9f, 0.6f, 0.05f);

        public const string BotWeaponId = WeaponAssetBuilder.RifleId;

        public static string NavMeshDataPath => ModeSceneBuilder.NavMeshDataPathFor(ScenePath);

        public static readonly Vector3 FloorCenter = ModeSceneBuilder.DefaultFloorCenter;
        public static readonly Vector3 FloorScale = ModeSceneBuilder.DefaultFloorScale;

        public static readonly IReadOnlyList<Vector3> ControlPoints = new[]
        {
            new Vector3(-10f, 0f, 10f),
            new Vector3(0f, 0f, 20f),
            new Vector3(10f, 0f, 30f),
        };

        public const float ControlPointRadius = ControlPointZone.DefaultRadius;
        public const float ControlPointHeight = ControlPointZone.DefaultHeight;

        public static readonly IReadOnlyList<ModeSpawnPlacement> AlphaSpawns = new[]
        {
            new ModeSpawnPlacement(ModeSceneBuilder.PlayerFeet, Vector3.forward),
            new ModeSpawnPlacement(new Vector3(-6f, 0f, ModeSceneBuilder.DefaultNearSpawnZ), Vector3.forward),
            new ModeSpawnPlacement(new Vector3(6f, 0f, ModeSceneBuilder.DefaultNearSpawnZ), Vector3.forward),
        };

        public static readonly IReadOnlyList<ModeSpawnPlacement> BravoSpawns =
            ModeSceneBuilder.SpawnRow(ModeSceneBuilder.DefaultFarSpawnZ, Vector3.back);

        // 手近な C と B から取りに行き、敵がばらけるよう敵ごとに回る順番を変える。
        public static readonly IReadOnlyList<IReadOnlyList<int>> BotObjectives = new IReadOnlyList<int>[]
        {
            new[] { 2, 1, 0 },
            new[] { 1, 0, 2 },
            new[] { 2, 0, 1 },
            new[] { 1, 2, 0 },
        };

        // 居座る間に拠点の外へ出ないよう、拠点の半径に十分収まるずれにする。
        public static readonly IReadOnlyList<Vector3> LoiterOffsets = new[]
        {
            new Vector3(2f, 0f, 2f),
            new Vector3(-2f, 0f, 2f),
            new Vector3(-2f, 0f, -2f),
            new Vector3(2f, 0f, -2f),
        };

        // 1 人で占領し終える DominationRunner.DefaultCaptureSeconds 秒より長く拠点にいられるよう、何周か回る。
        public const int LoiterLaps = 2;

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

        public static string ControlPointName(int index)
        {
            return "ControlPoint-" + DominationHudFormat.PointName(index);
        }

        // DominationRunner は名前の順に Id を振るので、番号 (1 から) で並ぶ名前にする。
        public static string BotName(int number)
        {
            return $"Bravo-{number}";
        }

        public static string BotId(int number)
        {
            return $"bravo-{number}";
        }

        // 倒れて戻った後も、まっすぐ最初の拠点へ向かうよう、巡回路に出現点は含めない。
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
            var tuning = LoadOrCreateDefinition(DefinitionPath);

            SceneBuildHelpers.EnsureFolder(Path.GetDirectoryName(scenePath));
            SceneBuildHelpers.EnsureFolder(Path.GetDirectoryName(navMeshDataPath));

            // 敵のコライダーで NavMesh に穴が空かないよう、地形だけを置いた状態で焼く。
            var surface = CreateLevel(scene);
            EnemyTestSceneBuilder.BakeNavMesh(surface, navMeshDataPath);
            RequireWalkable();

            var runner = PopulateScene(scene, playerWeapons, rules, botWeapon, tuning);

            if (!EditorSceneManager.SaveScene(scene, scenePath))
            {
                throw new IOException($"Failed to save the Domination scene to '{scenePath}'");
            }

            BuildSceneList.Register(scenePath);
            AssetDatabase.SaveAssets();

            Debug.Log($"{LogTag} saved '{scenePath}' and '{navMeshDataPath}' ({ControlPoints.Count} control point(s), {BotObjectives.Count} bot(s))");
            return runner;
        }

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

        public static DominationRunner PopulateScene(
            Scene scene,
            IReadOnlyList<WeaponDefinition> playerWeapons,
            GameRulesDefinition rules,
            WeaponDefinition botWeapon,
            DominationDefinition tuning = null)
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
            if (tuning != null)
            {
                tuning.Validate();
            }

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
                runner.Definition = tuning;

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

        // 範囲の円盤は Play 中に ControlPointZone が作るので、ここでは置かない。
        private static ControlPointZone CreateControlPoint(Transform root, int index, Vector3 center)
        {
            var point = new GameObject(ControlPointName(index));
            point.transform.SetParent(root, false);
            point.transform.SetPositionAndRotation(center, Quaternion.identity);

            var zone = point.AddComponent<ControlPointZone>();
            zone.Index = index;
            zone.Radius = ControlPointRadius;
            zone.Height = ControlPointHeight;
            AddFlag(point.transform);
            return zone;
        }

        // 旗竿と旗は遠くからどこが拠点かを見せるためのものなので、弾と移動を遮らないよう当たり判定を外す。
        // オンラインのマップ (ArenaSceneBuilder) の拠点も同じ見た目にするので public にする。
        public static void AddFlag(Transform point)
        {
            if (point == null)
            {
                throw new ArgumentNullException(nameof(point));
            }

            var pole = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            pole.name = PoleName;
            UnityEngine.Object.DestroyImmediate(pole.GetComponent<Collider>());
            pole.transform.SetParent(point, false);

            // Cylinder は高さ 2・直径 1。
            pole.transform.localPosition = new Vector3(0f, PoleHeight / 2f, 0f);
            pole.transform.localScale = new Vector3(PoleDiameter, PoleHeight / 2f, PoleDiameter);

            var flag = GameObject.CreatePrimitive(PrimitiveType.Cube);
            flag.name = FlagName;
            UnityEngine.Object.DestroyImmediate(flag.GetComponent<Collider>());
            flag.transform.SetParent(point, false);
            flag.transform.localPosition = new Vector3((FlagSize.x + PoleDiameter) / 2f, PoleHeight - (FlagSize.y / 2f), 0f);
            flag.transform.localScale = FlagSize;
        }
    }
}
