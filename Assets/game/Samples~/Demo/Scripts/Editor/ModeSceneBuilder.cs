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
    // bot のコライダーで NavMesh に穴が空かないよう、地形だけの状態で焼いてから bot を置く。
    // GameBootstrap はプレイヤーを原点に +Z を向けて出すので、プレイヤーの陣営の最初の出現点はそこに置く (違えば何も保存せずに例外を投げる)。
    public abstract class ModeSceneBuilder
    {
        public const string FloorName = "Floor";
        public const string LevelRootName = "Level";
        public const string SpawnPointsRootName = "SpawnPoints";
        public const string BotsRootName = "Bots";
        public const string BootstrapObjectName = "GameBootstrap";
        public const string RunnerObjectName = "ModeRunner";

        public const string NavMeshDataFileName = "NavMesh-Floor.asset";

        public static readonly Vector3 DefaultFloorCenter = new Vector3(0f, 0f, 20f);

        public static readonly Vector3 DefaultFloorScale = new Vector3(4f, 1f, 6f);

        public const float BoundaryWallHeight = 3f;
        public const float BoundaryWallThickness = 0.5f;

        public static readonly EnemyBody DefaultBotBody = new EnemyBody(
            EnemyTestSceneBuilder.EnemyHeight, EnemyTestSceneBuilder.EnemyRadius, EnemyTestSceneBuilder.EnemySpeed, EnemyTestSceneBuilder.EnemyMaxHealth);

        public const float NavMeshCheckRadius = EnemyTestSceneBuilder.NavMeshCheckRadius;

        public static readonly Vector3 PlayerFeet = GameBootstrap.PlayerSpawn + (Vector3.down * (GameBootstrap.PlayerHeight / 2f));

        public static readonly IReadOnlyList<ModeObstacle> DefaultObstacles = new[]
        {
            new ModeObstacle("Cover-Center", new Vector3(0f, 1.5f, 20f), new Vector3(8f, 3f, 1f)),
            new ModeObstacle("Cover-WestNear", new Vector3(-10f, 0.525f, 13f), new Vector3(4f, 1.05f, 0.5f)),
            new ModeObstacle("Cover-EastNear", new Vector3(10f, 0.525f, 13f), new Vector3(4f, 1.05f, 0.5f)),
            new ModeObstacle("Cover-WestFar", new Vector3(-10f, 0.525f, 27f), new Vector3(4f, 1.05f, 0.5f)),
            new ModeObstacle("Cover-EastFar", new Vector3(10f, 0.525f, 27f), new Vector3(4f, 1.05f, 0.5f)),
        };

        public static readonly IReadOnlyList<float> DefaultSpawnColumns = new[] { 0f, -6f, 6f, -12f, 12f };

        public const float DefaultNearSpawnZ = 0f;

        public const float DefaultFarSpawnZ = 40f;

        protected ModeSceneBuilder(string scenePath, string rulesPath)
        {
            ScenePath = BootstrapSceneBuilder.ValidateScenePath(scenePath);
            RulesPath = SceneBuildHelpers.ValidateAssetPath(rulesPath);
        }

        public string ScenePath { get; }

        public string RulesPath { get; }

        public string NavMeshDataPath => NavMeshDataPathFor(ScenePath);

        public virtual string LogTag => "[" + GetType().Name + "]";

        protected virtual Side PlayerSide => new Side(0);

        protected virtual IReadOnlyList<ModeSideLayout> Sides => TwoSides(2, 3);

        protected virtual Vector3 FloorCenter => DefaultFloorCenter;

        protected virtual Vector3 FloorScale => DefaultFloorScale;

        protected virtual IReadOnlyList<ModeObstacle> Obstacles => DefaultObstacles;

        protected virtual string BotWeaponId => WeaponAssetBuilder.RifleId;

        protected virtual EnemyBody BotBody => DefaultBotBody;

        // ここで Runner に設定したプレイヤーの陣営は、戻した後に PlayerSide で上書きされる。context.Runner はまだ null。
        protected abstract ModeRunnerBase AddRunner(GameObject host, ModeSceneContext context);

        // NavMesh を焼く前に呼ぶので、ここで置いたコライダーは NavMesh に効く。
        protected virtual void AddLevel(Scene scene, Transform levelRoot)
        {
        }

        // NavMesh を焼いた後に呼ぶので、ここで置いた物は NavMesh に効かない。
        protected virtual void AddModeObjects(ModeSceneContext context)
        {
        }

        // 既定は相手の陣地へ攻め込むよう、出現点と、次の陣営 (最後なら先頭) の同じ番号の出現点を往復する。
        protected virtual IReadOnlyList<Vector3> BotPatrolRoute(IReadOnlyList<ModeSideLayout> sides, int sideIndex, int spawnIndex)
        {
            var own = sides[sideIndex].Spawns[spawnIndex];
            var next = sides[(sideIndex + 1) % sides.Count];
            var target = next.Spawns[spawnIndex % next.Spawns.Count];
            return new[] { own.Feet, target.Feet };
        }

        public static IReadOnlyList<ModeSideLayout> TwoSides(int sideZeroBots, int sideOneBots)
        {
            return new[]
            {
                new ModeSideLayout(new Side(0), SpawnRow(DefaultNearSpawnZ, Vector3.forward), sideZeroBots),
                new ModeSideLayout(new Side(1), SpawnRow(DefaultFarSpawnZ, Vector3.back), sideOneBots),
            };
        }

        public static IReadOnlyList<ModeSpawnPlacement> SpawnRow(float z, Vector3 facing)
        {
            var spawns = new ModeSpawnPlacement[DefaultSpawnColumns.Count];
            for (var i = 0; i < spawns.Length; i++)
            {
                spawns[i] = new ModeSpawnPlacement(new Vector3(DefaultSpawnColumns[i], 0f, z), facing);
            }

            return spawns;
        }

        public static string NavMeshDataPathFor(string scenePath)
        {
            scenePath = BootstrapSceneBuilder.ValidateScenePath(scenePath);
            var folder = Path.GetDirectoryName(scenePath)?.Replace('\\', '/');
            var name = Path.GetFileNameWithoutExtension(scenePath);
            return SceneBuildHelpers.ValidateAssetPath(folder + "/" + name + "/" + NavMeshDataFileName);
        }

        // ModeRunnerBase は名前の順に ID を振るので、陣営と番号で名前を決めて Play ごとに ID が変わらないようにする。
        public static string BotName(Side side, int number)
        {
            return $"Bot-{side.Id}-{number}";
        }

        public static string BotId(Side side, int number)
        {
            return $"bot-{side.Id}-{number}";
        }

        public void BuildSceneFromMenu()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                Debug.Log(LogTag + " cancelled");
                return;
            }

            BuildScene();
        }

        public ModeSceneContext BuildScene()
        {
            return BuildSceneAt(ScenePath, NavMeshDataPath);
        }

        public ModeSceneContext BuildSceneAt(string scenePath, string navMeshDataPath)
        {
            scenePath = BootstrapSceneBuilder.ValidateScenePath(scenePath);
            navMeshDataPath = SceneBuildHelpers.ValidateAssetPath(navMeshDataPath);

            var sides = ValidateLayout(Sides, PlayerSide);
            var obstacles = Obstacles ?? throw new InvalidOperationException($"{GetType().Name}.Obstacles must not be null");
            var routes = PatrolRoutes(sides);

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // NewScene(Single) は参照されていないアセットをアンロードし、先に読んだ定義が null になるので、シーンを作ってから読む。
            var playerWeapons = WeaponAssetBuilder.LoadPlayerLoadout();
            var catalog = WeaponAssetBuilder.LoadCatalog();
            var botWeapon = WeaponAssetBuilder.FindInCatalog(catalog, BotsRootName, BotWeaponId);
            var rules = GameRulesAssetBuilder.LoadRules(RulesPath);

            SceneBuildHelpers.EnsureFolder(Path.GetDirectoryName(scenePath));
            SceneBuildHelpers.EnsureFolder(Path.GetDirectoryName(navMeshDataPath));

            var surface = CreateLevel(scene, obstacles);
            EnemyTestSceneBuilder.BakeNavMesh(surface, navMeshDataPath);
            RequireWalkable(sides, routes);

            var context = new ModeSceneContext(scene, surface, catalog, rules);
            context.SpawnPoints = CreateSpawnPoints(scene, sides);
            context.Bootstrap = CreateBootstrap(scene, playerWeapons, rules);
            context.Bots = CreateBots(scene, sides, routes, botWeapon);
            context.Runner = CreateRunner(scene, context);
            AddModeObjects(context);
            EditorSceneManager.MarkSceneDirty(scene);

            if (!EditorSceneManager.SaveScene(scene, scenePath))
            {
                throw new IOException($"Failed to save the mode scene to '{scenePath}'");
            }

            BuildSceneList.Register(scenePath);
            AssetDatabase.SaveAssets();

            Debug.Log($"{LogTag} saved '{scenePath}' and '{navMeshDataPath}' (rules '{RulesPath}', {context.Bots.Count} bot(s))");
            return context;
        }

        public static IReadOnlyList<ModeSideLayout> ValidateLayout(IReadOnlyList<ModeSideLayout> sides, Side playerSide)
        {
            if (sides == null || sides.Count == 0)
            {
                throw new InvalidOperationException("a mode scene needs at least one side");
            }

            var seen = new HashSet<Side>();
            ModeSideLayout? player = null;
            foreach (var side in sides)
            {
                if (side.Spawns == null || side.Spawns.Count == 0)
                {
                    throw new InvalidOperationException($"side {side.Side.Id} has no spawn point");
                }

                if (!seen.Add(side.Side))
                {
                    throw new InvalidOperationException($"side {side.Side.Id} is laid out twice");
                }

                var usable = side.Side == playerSide ? side.Spawns.Count - 1 : side.Spawns.Count;
                if (side.BotCount > usable)
                {
                    throw new InvalidOperationException(
                        $"side {side.Side.Id} has {side.BotCount} bot(s) but only {usable} spawn point(s) for them");
                }

                if (side.Side == playerSide)
                {
                    player = side;
                }
            }

            if (!player.HasValue)
            {
                throw new InvalidOperationException($"the player's side {playerSide.Id} is not laid out");
            }

            var first = player.Value.Spawns[0];
            if ((first.Feet - PlayerFeet).sqrMagnitude > 1e-4f || Vector3.Dot(first.Facing, Vector3.forward) < 0.999f)
            {
                throw new InvalidOperationException(
                    $"the first spawn point of the player's side {playerSide.Id} must be at {PlayerFeet} facing +Z, where GameBootstrap puts the player, "
                    + $"but is at {first.Feet} facing {first.Facing}");
            }

            return sides;
        }

        private List<IReadOnlyList<Vector3>[]> PatrolRoutes(IReadOnlyList<ModeSideLayout> sides)
        {
            var routes = new List<IReadOnlyList<Vector3>[]>(sides.Count);
            for (var s = 0; s < sides.Count; s++)
            {
                var perSpawn = new IReadOnlyList<Vector3>[sides[s].Spawns.Count];
                for (var i = 0; i < perSpawn.Length; i++)
                {
                    var route = BotPatrolRoute(sides, s, i);
                    if (route == null || route.Count == 0)
                    {
                        throw new InvalidOperationException($"{GetType().Name}.BotPatrolRoute returned no point for side {sides[s].Side.Id} spawn #{i}");
                    }

                    perSpawn[i] = route;
                }

                routes.Add(perSpawn);
            }

            return routes;
        }

        private NavMeshSurface CreateLevel(Scene scene, IReadOnlyList<ModeObstacle> obstacles)
        {
            SceneBuildHelpers.RequireEmpty(scene, "the mode level");

            var floor = SceneBuildHelpers.CreateStaticPlane(scene, null, FloorName, FloorCenter, FloorScale);
            var surface = SceneBuildHelpers.AddNavMeshSurface(floor, CollectObjects.All);

            var level = new GameObject(LevelRootName);
            SceneManager.MoveGameObjectToScene(level, scene);
            level.isStatic = true;

            // Plane は 10m 四方。外へ歩いて落ちないよう、縁の内側に沿って壁を立てる。
            var halfX = FloorScale.x * 5f;
            var halfZ = FloorScale.z * 5f;
            var inset = BoundaryWallThickness / 2f;
            var y = BoundaryWallHeight / 2f;
            var alongX = new Vector3(halfX * 2f, BoundaryWallHeight, BoundaryWallThickness);
            var alongZ = new Vector3(BoundaryWallThickness, BoundaryWallHeight, halfZ * 2f);
            SceneBuildHelpers.CreateStaticBox(scene, level.transform, "Boundary-North", FloorCenter + new Vector3(0f, y, halfZ - inset), alongX);
            SceneBuildHelpers.CreateStaticBox(scene, level.transform, "Boundary-South", FloorCenter + new Vector3(0f, y, -halfZ + inset), alongX);
            SceneBuildHelpers.CreateStaticBox(scene, level.transform, "Boundary-East", FloorCenter + new Vector3(halfX - inset, y, 0f), alongZ);
            SceneBuildHelpers.CreateStaticBox(scene, level.transform, "Boundary-West", FloorCenter + new Vector3(-halfX + inset, y, 0f), alongZ);

            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var obstacle in obstacles)
            {
                if (!names.Add(obstacle.Name))
                {
                    throw new InvalidOperationException($"obstacle name '{obstacle.Name}' is used twice");
                }

                SceneBuildHelpers.CreateStaticBox(scene, level.transform, obstacle.Name, obstacle.Center, obstacle.Size);
            }

            AddLevel(scene, level.transform);
            return surface;
        }

        private void RequireWalkable(IReadOnlyList<ModeSideLayout> sides, List<IReadOnlyList<Vector3>[]> routes)
        {
            var spawns = new List<Vector3>();
            foreach (var side in sides)
            {
                foreach (var spawn in side.Spawns)
                {
                    spawns.Add(spawn.Feet);
                }
            }

            SceneBuildHelpers.RequireOnNavMesh(spawns, NavMeshCheckRadius, "spawn point");
            SceneBuildHelpers.RequireReachable(PlayerFeet, spawns, NavMeshCheckRadius, "spawn point");

            for (var s = 0; s < sides.Count; s++)
            {
                for (var i = 0; i < routes[s].Length; i++)
                {
                    SceneBuildHelpers.RequireOnNavMesh(routes[s][i], NavMeshCheckRadius, $"side {sides[s].Side.Id} patrol point");
                    SceneBuildHelpers.RequireReachable(sides[s].Spawns[i].Feet, routes[s][i], NavMeshCheckRadius, $"side {sides[s].Side.Id} patrol point");
                }
            }
        }

        private static List<SideSpawnPoint> CreateSpawnPoints(Scene scene, IReadOnlyList<ModeSideLayout> sides)
        {
            var root = new GameObject(SpawnPointsRootName);
            SceneManager.MoveGameObjectToScene(root, scene);

            var points = new List<SideSpawnPoint>();
            foreach (var side in sides)
            {
                for (var i = 0; i < side.Spawns.Count; i++)
                {
                    var spawn = side.Spawns[i];
                    var marker = new GameObject($"Spawn-{side.Side.Id}-{i + 1}");
                    marker.transform.SetParent(root.transform, false);
                    marker.transform.SetPositionAndRotation(spawn.Feet, Quaternion.LookRotation(spawn.Facing, Vector3.up));

                    var point = marker.AddComponent<SideSpawnPoint>();
                    point.Side = side.Side;
                    points.Add(point);
                }
            }

            return points;
        }

        private static GameBootstrap CreateBootstrap(Scene scene, IReadOnlyList<WeaponDefinition> playerWeapons, GameRulesDefinition rules)
        {
            SceneBuildHelpers.RequirePlayerWeapons(playerWeapons, nameof(playerWeapons));

            var host = new GameObject(BootstrapObjectName);
            SceneManager.MoveGameObjectToScene(host, scene);
            var bootstrap = host.AddComponent<GameBootstrap>();
            bootstrap.CreatesFloor = false;
            bootstrap.WeaponDefinitions = playerWeapons;
            bootstrap.RulesDefinition = rules;
            return bootstrap;
        }

        private List<EnemyAgent> CreateBots(
            Scene scene, IReadOnlyList<ModeSideLayout> sides, List<IReadOnlyList<Vector3>[]> routes, WeaponDefinition weapon)
        {
            var root = new GameObject(BotsRootName);
            SceneManager.MoveGameObjectToScene(root, scene);

            var bots = new List<EnemyAgent>();
            for (var s = 0; s < sides.Count; s++)
            {
                var side = sides[s];

                // プレイヤーの陣営の最初の出現点はプレイヤーが使うので、bot はその次から立たせる。
                var first = side.Side == PlayerSide ? 1 : 0;
                for (var n = 0; n < side.BotCount; n++)
                {
                    var spawnIndex = first + n;
                    var spawn = side.Spawns[spawnIndex];
                    var bot = SceneBuildHelpers.CreateEnemy(
                        scene,
                        root.transform,
                        BotName(side.Side, n + 1),
                        BotId(side.Side, n + 1),
                        spawn.Feet,
                        Quaternion.LookRotation(spawn.Facing, Vector3.up),
                        routes[s][spawnIndex],
                        BotBody,
                        weapon);
                    bot.gameObject.AddComponent<SideMember>().Side = side.Side;
                    bots.Add(bot);
                }
            }

            return bots;
        }

        private ModeRunnerBase CreateRunner(Scene scene, ModeSceneContext context)
        {
            var host = new GameObject(RunnerObjectName);
            SceneManager.MoveGameObjectToScene(host, scene);

            var runner = AddRunner(host, context);
            if (runner == null || runner.gameObject != host)
            {
                throw new InvalidOperationException($"{GetType().Name}.AddRunner must add a ModeRunnerBase to the given host and return it");
            }

            runner.PlayerSide = PlayerSide;
            return runner;
        }
    }

    public readonly struct ModeSideLayout
    {
        public Side Side { get; }

        public IReadOnlyList<ModeSpawnPlacement> Spawns { get; }

        public int BotCount { get; }

        public ModeSideLayout(Side side, IReadOnlyList<ModeSpawnPlacement> spawns, int botCount)
        {
            if (botCount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(botCount), botCount, "botCount must not be negative");
            }

            Side = side;
            Spawns = spawns ?? throw new ArgumentNullException(nameof(spawns));
            BotCount = botCount;
        }
    }

    public readonly struct ModeSpawnPlacement
    {
        public Vector3 Feet { get; }

        public Vector3 Facing { get; }

        public ModeSpawnPlacement(Vector3 feet, Vector3 facing)
        {
            var flat = new Vector3(facing.x, 0f, facing.z);
            if (flat.sqrMagnitude < 1e-6f)
            {
                throw new ArgumentOutOfRangeException(nameof(facing), facing, "a spawn point must face horizontally");
            }

            Feet = feet;
            Facing = flat.normalized;
        }
    }

    public readonly struct ModeObstacle
    {
        public string Name { get; }
        public Vector3 Center { get; }
        public Vector3 Size { get; }

        public ModeObstacle(string name, Vector3 center, Vector3 size)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("an obstacle needs a name", nameof(name));
            }

            if (!(size.x > 0f) || !(size.y > 0f) || !(size.z > 0f))
            {
                throw new ArgumentOutOfRangeException(nameof(size), size, $"obstacle '{name}' must have a positive size");
            }

            Name = name;
            Center = center;
            Size = size;
        }
    }

    public sealed class ModeSceneContext
    {
        public ModeSceneContext(Scene scene, NavMeshSurface surface, WeaponCatalogAsset catalog, GameRulesDefinition rules)
        {
            Scene = scene;
            Surface = surface;
            Catalog = catalog;
            Rules = rules;
        }

        public Scene Scene { get; }

        public NavMeshSurface Surface { get; }

        public WeaponCatalogAsset Catalog { get; }

        public GameRulesDefinition Rules { get; }

        public IReadOnlyList<SideSpawnPoint> SpawnPoints { get; internal set; } = Array.Empty<SideSpawnPoint>();

        public GameBootstrap Bootstrap { get; internal set; }

        public IReadOnlyList<EnemyAgent> Bots { get; internal set; } = Array.Empty<EnemyAgent>();

        public ModeRunnerBase Runner { get; internal set; }
    }
}
