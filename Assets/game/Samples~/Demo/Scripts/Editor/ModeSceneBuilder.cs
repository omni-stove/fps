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
    /// オフラインのモード (<see cref="ModeRunnerBase"/>) のシーンを組む土台。派生クラスは Runner (<see cref="AddRunner"/>) と、
    /// そのモードに固有の地形 (<see cref="AddLevel"/>)・物 (<see cref="AddModeObjects"/>) だけを書けばよい。
    /// </summary>
    /// <remarks>
    /// <see cref="BuildSceneAt"/> は次の順に組み、シーンと焼いた NavMesh を保存して Build Settings に加える (既存の並びは変えない)。
    /// <list type="number">
    /// <item>空のシーンに床 (<see cref="NavMeshSurface"/> 付き)・外周の壁・遮蔽物 (<see cref="Obstacles"/>)・派生の地形を置き、NavMesh を焼く。
    /// 焼き方と保存は <see cref="EnemyTestSceneBuilder.BakeNavMesh"/> と同じ。bot のコライダーで穴が空かないよう、地形だけの状態で焼く。</item>
    /// <item>出現点と巡回路が NavMesh に乗っていて、プレイヤーの出現点から全陣営の出現点へ歩けることを確かめる。</item>
    /// <item>陣営ごとの <see cref="SideSpawnPoint"/>、<see cref="GameBootstrap"/> (床は作らない・武器とルール <see cref="RulesPath"/> を配線)、
    /// 陣営ごとの bot (<see cref="SceneBuildHelpers.CreateEnemy"/> の <see cref="EnemyAgent"/> に <see cref="SideMember"/> を付けたもの) を置く。</item>
    /// <item><see cref="AddRunner"/> の Runner を置き、プレイヤーの陣営 (<see cref="PlayerSide"/>) を設定してから <see cref="AddModeObjects"/> を呼ぶ。</item>
    /// </list>
    /// プレイヤーとライトと HUD (<see cref="ModeHudView"/> を含む) は実行時に GameBootstrap が作る。プレイヤーは原点に +Z を向いて出るので、
    /// プレイヤーの陣営の最初の出現点はそこに置くこと (違えば何も保存せずに例外を投げる)。倒れた後は Runner が自陣営の出現点に戻す。
    /// 何度実行しても同じ結果になる。武器のアセット・ルールのアセットが無ければ何も保存せずに止まる
    /// (先に <see cref="WeaponAssetBuilder"/>.Build と <see cref="GameRulesAssetBuilder"/>.Build を走らせる)。
    /// </remarks>
    public abstract class ModeSceneBuilder
    {
        public const string FloorName = "Floor";
        public const string LevelRootName = "Level";
        public const string SpawnPointsRootName = "SpawnPoints";
        public const string BotsRootName = "Bots";
        public const string BootstrapObjectName = "GameBootstrap";
        public const string RunnerObjectName = "ModeRunner";

        /// <summary>シーン名のフォルダに置く、焼いた NavMesh のファイル名 (<see cref="EnemyTestSceneBuilder.NavMeshDataPath"/> と同じ慣習)。</summary>
        public const string NavMeshDataFileName = "NavMesh-Floor.asset";

        /// <summary>Plane は 10m 四方なので、x は ±20m、z は -10m から 50m。</summary>
        public static readonly Vector3 DefaultFloorCenter = new Vector3(0f, 0f, 20f);

        public static readonly Vector3 DefaultFloorScale = new Vector3(4f, 1f, 6f);

        public const float BoundaryWallHeight = 3f;
        public const float BoundaryWallThickness = 0.5f;

        /// <summary>bot の体。<see cref="EnemyTestSceneBuilder"/> の敵と同じ大きさ・速さ・体力。</summary>
        public static readonly EnemyBody DefaultBotBody = new EnemyBody(
            EnemyTestSceneBuilder.EnemyHeight, EnemyTestSceneBuilder.EnemyRadius, EnemyTestSceneBuilder.EnemySpeed, EnemyTestSceneBuilder.EnemyMaxHealth);

        /// <summary>出現点・巡回地点が NavMesh 上にあるかを確かめるときに探す半径 (メートル)。</summary>
        public const float NavMeshCheckRadius = EnemyTestSceneBuilder.NavMeshCheckRadius;

        /// <summary>GameBootstrap がプレイヤーを出す足元。プレイヤーの陣営の最初の出現点はここに置く。</summary>
        public static readonly Vector3 PlayerFeet = GameBootstrap.PlayerSpawn + (Vector3.down * (GameBootstrap.PlayerHeight / 2f));

        /// <summary>
        /// 既定の遮蔽物。中央に高い壁、その左右の手前と奥に低い壁 (上端 1.05m、<see cref="EnemyTestSceneBuilder.LowCoverSize"/> と同じ高さ) を置く。
        /// どれも出現点 (z = 0 と z = 40) から離れている。
        /// </summary>
        public static readonly IReadOnlyList<ModeObstacle> DefaultObstacles = new[]
        {
            new ModeObstacle("Cover-Center", new Vector3(0f, 1.5f, 20f), new Vector3(8f, 3f, 1f)),
            new ModeObstacle("Cover-WestNear", new Vector3(-10f, 0.525f, 13f), new Vector3(4f, 1.05f, 0.5f)),
            new ModeObstacle("Cover-EastNear", new Vector3(10f, 0.525f, 13f), new Vector3(4f, 1.05f, 0.5f)),
            new ModeObstacle("Cover-WestFar", new Vector3(-10f, 0.525f, 27f), new Vector3(4f, 1.05f, 0.5f)),
            new ModeObstacle("Cover-EastFar", new Vector3(10f, 0.525f, 27f), new Vector3(4f, 1.05f, 0.5f)),
        };

        /// <summary>既定の陣営ごとの出現点を並べる x。先頭 (0) がプレイヤーの陣営ではプレイヤーの出現点になる。</summary>
        public static readonly IReadOnlyList<float> DefaultSpawnColumns = new[] { 0f, -6f, 6f, -12f, 12f };

        /// <summary>既定の 2 陣営の出現点の z。陣営 0 (プレイヤー) は手前で +Z、陣営 1 は奥で -Z を向く。</summary>
        public const float DefaultNearSpawnZ = 0f;

        public const float DefaultFarSpawnZ = 40f;

        /// <param name="scenePath">保存するシーン。Assets/ 以下の .unity。</param>
        /// <param name="rulesPath">GameBootstrap に配線するルール (<see cref="GameRulesDefinition"/>)。Assets/ 以下の .asset。</param>
        protected ModeSceneBuilder(string scenePath, string rulesPath)
        {
            ScenePath = BootstrapSceneBuilder.ValidateScenePath(scenePath);
            RulesPath = SceneBuildHelpers.ValidateAssetPath(rulesPath);
        }

        public string ScenePath { get; }

        public string RulesPath { get; }

        /// <summary>焼いた NavMesh を置く既定の場所 (<see cref="NavMeshDataPathFor"/>)。</summary>
        public string NavMeshDataPath => NavMeshDataPathFor(ScenePath);

        /// <summary>ログの頭に付ける印。既定は <c>[派生クラス名]</c>。</summary>
        public virtual string LogTag => "[" + GetType().Name + "]";

        /// <summary>GameBootstrap のプレイヤーの陣営。<see cref="Sides"/> に含まれ、最初の出現点が <see cref="PlayerFeet"/> にあること。</summary>
        protected virtual Side PlayerSide => new Side(0);

        /// <summary>陣営ごとの出現点と bot の数。既定は <see cref="TwoSides"/> (プレイヤーの陣営に bot 2 体、相手に 3 体)。</summary>
        protected virtual IReadOnlyList<ModeSideLayout> Sides => TwoSides(2, 3);

        /// <summary>床の中心と大きさ (Plane の拡大率)。外周の壁は床の縁の内側に沿って置く。</summary>
        protected virtual Vector3 FloorCenter => DefaultFloorCenter;

        protected virtual Vector3 FloorScale => DefaultFloorScale;

        /// <summary>遮蔽物。既定は <see cref="DefaultObstacles"/>。</summary>
        protected virtual IReadOnlyList<ModeObstacle> Obstacles => DefaultObstacles;

        /// <summary>bot に持たせる武器の Id。武器の一覧 (<see cref="WeaponAssetBuilder.LoadCatalog"/>) から引く。</summary>
        protected virtual string BotWeaponId => WeaponAssetBuilder.RifleId;

        protected virtual EnemyBody BotBody => DefaultBotBody;

        /// <summary>
        /// host にこのモードの Runner を付けて設定し、返す。陣営の数など Runner の設定はここで決める
        /// (プレイヤーの陣営は戻した後に <see cref="PlayerSide"/> で上書きする)。context の <see cref="ModeSceneContext.Runner"/> はまだ null。
        /// </summary>
        protected abstract ModeRunnerBase AddRunner(GameObject host, ModeSceneContext context);

        /// <summary>
        /// NavMesh を焼く前に、モードに固有の地形 (歩けない所・段差など) を levelRoot の下に置く。既定では何もしない。
        /// ここで置いたコライダーは NavMesh に効く。
        /// </summary>
        protected virtual void AddLevel(Scene scene, Transform levelRoot)
        {
        }

        /// <summary>
        /// Runner を置いた後に、モードに固有の物 (拠点・爆弾・拾える物など) を置いて Runner に配線する。既定では何もしない。
        /// ここで置いた物は NavMesh に効かない (焼いた後なので)。
        /// </summary>
        protected virtual void AddModeObjects(ModeSceneContext context)
        {
        }

        /// <summary>
        /// 陣営ごとに bot が巡回する地点 (足元)。最初の地点がその bot の出現点。
        /// 既定は、出現点と、次の陣営 (<see cref="Sides"/> の並びで次、最後なら先頭) の同じ番号の出現点を往復する (相手の陣地へ攻め込む)。
        /// </summary>
        protected virtual IReadOnlyList<Vector3> BotPatrolRoute(IReadOnlyList<ModeSideLayout> sides, int sideIndex, int spawnIndex)
        {
            var own = sides[sideIndex].Spawns[spawnIndex];
            var next = sides[(sideIndex + 1) % sides.Count];
            var target = next.Spawns[spawnIndex % next.Spawns.Count];
            return new[] { own.Feet, target.Feet };
        }

        /// <summary>
        /// 手前 (z = <see cref="DefaultNearSpawnZ"/>) の陣営 0 と奥 (z = <see cref="DefaultFarSpawnZ"/>) の陣営 1 が向かい合う配置。
        /// どちらも <see cref="DefaultSpawnColumns"/> の x に出現点を並べる。bot の数は陣営 0 が sideZeroBots、陣営 1 が sideOneBots。
        /// </summary>
        public static IReadOnlyList<ModeSideLayout> TwoSides(int sideZeroBots, int sideOneBots)
        {
            return new[]
            {
                new ModeSideLayout(new Side(0), SpawnRow(DefaultNearSpawnZ, Vector3.forward), sideZeroBots),
                new ModeSideLayout(new Side(1), SpawnRow(DefaultFarSpawnZ, Vector3.back), sideOneBots),
            };
        }

        /// <summary>z の列に <see cref="DefaultSpawnColumns"/> の x で出現点を並べ、どれも facing を向かせる。</summary>
        public static IReadOnlyList<ModeSpawnPlacement> SpawnRow(float z, Vector3 facing)
        {
            var spawns = new ModeSpawnPlacement[DefaultSpawnColumns.Count];
            for (var i = 0; i < spawns.Length; i++)
            {
                spawns[i] = new ModeSpawnPlacement(new Vector3(DefaultSpawnColumns[i], 0f, z), facing);
            }

            return spawns;
        }

        /// <summary>シーン <c>Assets/.../&lt;名前&gt;.unity</c> の NavMesh を置く <c>Assets/.../&lt;名前&gt;/NavMesh-Floor.asset</c>。</summary>
        public static string NavMeshDataPathFor(string scenePath)
        {
            scenePath = BootstrapSceneBuilder.ValidateScenePath(scenePath);
            var folder = Path.GetDirectoryName(scenePath)?.Replace('\\', '/');
            var name = Path.GetFileNameWithoutExtension(scenePath);
            return SceneBuildHelpers.ValidateAssetPath(folder + "/" + name + "/" + NavMeshDataFileName);
        }

        /// <summary>
        /// bot の名前。陣営と、陣営の中の番号 (1 から) で決める。<see cref="ModeRunnerBase"/> は名前の順に ID を振るので、Play ごとに変わらない。
        /// </summary>
        public static string BotName(Side side, int number)
        {
            return $"Bot-{side.Id}-{number}";
        }

        /// <summary>bot の <see cref="EnemyAgent.Id"/> (ログと当たりの記録に出る)。</summary>
        public static string BotId(Side side, int number)
        {
            return $"bot-{side.Id}-{number}";
        }

        /// <summary>
        /// メニューから呼ぶとき用。変更中のシーンを保存するか尋ね、断られたら何もしない。batchmode では尋ねずに <see cref="BuildScene"/> する。
        /// </summary>
        public void BuildSceneFromMenu()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                Debug.Log(LogTag + " cancelled");
                return;
            }

            BuildScene();
        }

        /// <summary><see cref="ScenePath"/> と <see cref="NavMeshDataPath"/> に組む (<see cref="BuildSceneAt"/>)。</summary>
        public ModeSceneContext BuildScene()
        {
            return BuildSceneAt(ScenePath, NavMeshDataPath);
        }

        /// <summary>
        /// シーンを組んで NavMesh を焼き、両方を保存して Build Settings に加え、置いた物を返す。
        /// 配置が不正なら、シーンを作る前に <see cref="InvalidOperationException"/> を投げる。
        /// 焼いた NavMesh に出現点・巡回地点が乗らないか、行けない出現点があれば、シーンを保存せずに例外を投げる (batchmode では終了コード 1 になる)。
        /// </summary>
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

        /// <summary>
        /// sides が、陣営が重ならず、どの陣営にも出現点があり、playerSide を含み、その最初の出現点が <see cref="PlayerFeet"/> で +Z を向き、
        /// bot の数が出現点に収まる (プレイヤーの陣営は最初の点を除いて) ことを確かめて返す。満たさなければ <see cref="InvalidOperationException"/>。
        /// </summary>
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

    /// <summary>1 つの陣営の出現点と bot の数 (<see cref="ModeSceneBuilder"/>)。</summary>
    public readonly struct ModeSideLayout
    {
        public Side Side { get; }

        /// <summary>出現点。プレイヤーの陣営では先頭がプレイヤーの出現点。bot は残りの点に先頭から立つ。</summary>
        public IReadOnlyList<ModeSpawnPlacement> Spawns { get; }

        public int BotCount { get; }

        /// <exception cref="ArgumentNullException">spawns が null。</exception>
        /// <exception cref="ArgumentOutOfRangeException">botCount が負。</exception>
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

    /// <summary>出現点 1 つ。足元の位置と、水平に向く向き。</summary>
    public readonly struct ModeSpawnPlacement
    {
        public Vector3 Feet { get; }

        /// <summary>出現したときに向く方向 (水平、長さ 1)。</summary>
        public Vector3 Facing { get; }

        /// <exception cref="ArgumentOutOfRangeException">facing に水平の成分が無いとき。</exception>
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

    /// <summary>動かない直方体の遮蔽物 1 つ。center は中心、size は大きさ (ワールド)。</summary>
    public readonly struct ModeObstacle
    {
        public string Name { get; }
        public Vector3 Center { get; }
        public Vector3 Size { get; }

        /// <exception cref="ArgumentException">name が空。</exception>
        /// <exception cref="ArgumentOutOfRangeException">size のどれかが 0 以下。</exception>
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

    /// <summary><see cref="ModeSceneBuilder"/> が組んだシーンと、置いた物。派生のフックに渡す。</summary>
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

        /// <summary>NavMesh を焼いた床の NavMeshSurface。</summary>
        public NavMeshSurface Surface { get; }

        /// <summary>武器の一覧。モード固有の物 (拾える武器など) の武器はここから引く。</summary>
        public WeaponCatalogAsset Catalog { get; }

        /// <summary>GameBootstrap に配線したルール。</summary>
        public GameRulesDefinition Rules { get; }

        public IReadOnlyList<SideSpawnPoint> SpawnPoints { get; internal set; } = Array.Empty<SideSpawnPoint>();

        public GameBootstrap Bootstrap { get; internal set; }

        /// <summary>置いた bot。陣営の並び、陣営の中では番号の順。</summary>
        public IReadOnlyList<EnemyAgent> Bots { get; internal set; } = Array.Empty<EnemyAgent>();

        /// <summary>置いた Runner。<see cref="ModeSceneBuilder.AddRunner"/> の中ではまだ null。</summary>
        public ModeRunnerBase Runner { get; internal set; }
    }
}
