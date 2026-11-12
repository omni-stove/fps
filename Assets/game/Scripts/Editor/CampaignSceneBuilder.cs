using System;
using System.Collections.Generic;
using System.IO;
using Fps.Runtime;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Fps.Editor
{
    /// <summary>
    /// キャンペーンの最初のミッション Campaign01 のシーンとミッション定義を生成する。
    /// 地形 (床・外周の壁・仕切り・遮蔽物) を置いて <see cref="NavMeshSurface"/> で NavMesh を焼き、
    /// NavMeshData をシーンの隣に保存してから、<see cref="GameBootstrap"/> (床は作らない)、<see cref="MissionRunner"/>、
    /// 3 つの目標 (区域 A に到達 → 敵 4 体を倒す → 出口に到達) と敵 4 体、セーブと再開を受け持つ <see cref="CampaignRunner"/> と
    /// 2 つの <see cref="CheckpointTrigger"/> (開始地点 <c>start</c>・区域 A <c>area_a</c>) を置く。参照はすべてここでコードから設定する。
    /// 保存したシーンは Build Settings の末尾に加える (先頭の起動シーンは変えない)。何度実行しても同じ結果になる。
    /// Editor を閉じた状態で次のように呼べる:
    /// <code>unity -batchmode -quit -projectPath . -executeMethod Fps.Editor.CampaignSceneBuilder.Build</code>
    /// </summary>
    /// <remarks>
    /// 配置 (上から見て、プレイヤーは原点で +Z を向く。床は x = ±15、z = -10〜70):
    /// <list type="bullet">
    /// <item>z &lt; 30 は開始区画。GameBootstrap が実行時に z = 10 へ標的ダミーを並べる。</item>
    /// <item>z = 30〜40 の幅いっぱいが区域 A。z = 40 の仕切りの中央 (x = ±2.5) にだけ出入口がある。</item>
    /// <item>出入口の 3m 先 (z = 43) に幅 16m の衝立があり、区域 A より手前からは仕切りの向こうが見えない
    /// (KillTargetObjective は目標が Active になる前の撃破を数えないので、区域 A に入る前に敵を撃てないようにする)。</item>
    /// <item>衝立の左右の出口を、待ち伏せの敵 2 体が z = 49 から見張る。その奥の z = 55 と z = 63 を、巡回の敵 2 体が左右に往復する。</item>
    /// <item>出口は北の壁の手前 (0, 0, 66)。</item>
    /// <item>チェックポイントは開始地点 (原点、プレイヤーは出現した時点で踏んでいる) と区域 A の中 (0, 0, 34)。</item>
    /// </list>
    /// </remarks>
    public static class CampaignSceneBuilder
    {
        public const string ScenePath = "Assets/game/Scenes/Campaign01.unity";
        public const string DefinitionPath = "Assets/game/Missions/Campaign01.asset";

        /// <summary>焼いた NavMesh。Unity の慣習どおりシーン名のフォルダに置く。</summary>
        public const string NavMeshDataPath = "Assets/game/Scenes/Campaign01/NavMesh-Level.asset";

        public const string MenuPath = "Fps/Build Campaign01 Scene";
        public const string LogTag = "[CampaignSceneBuilder]";

        public const string MissionDisplayName = "Campaign 01";

        public const string ReachAreaAId = "reach_area_a";
        public const string KillEnemiesId = "kill_enemies";
        public const string ReachExitId = "reach_exit";
        public const int EnemyCount = 4;

        public const string BootstrapObjectName = "GameBootstrap";
        public const string RunnerObjectName = "MissionRunner";
        public const string LevelObjectName = "Level";
        public const string FloorName = "Floor";
        public const string AreaAObjectName = "AreaA";
        public const string EnemiesObjectName = "Enemies";
        public const string ExitObjectName = "Exit";
        public const string CampaignRunnerObjectName = "CampaignRunner";
        public const string StartCheckpointObjectName = "Checkpoint_Start";
        public const string AreaACheckpointObjectName = "Checkpoint_AreaA";

        // ---- 地形 ----

        /// <summary>床の中心。Plane は 10m 四方なので、<see cref="FloorScale"/> で x = ±15、z = -10〜70 になる。</summary>
        public static readonly Vector3 FloorCenter = new Vector3(0f, 0f, 30f);

        public static readonly Vector3 FloorScale = new Vector3(3f, 1f, 8f);

        public const float WallHeight = 3f;
        public const float WallThickness = 0.5f;

        /// <summary>区域 A と奥の区画を分ける仕切りの z。</summary>
        public const float DividerZ = 40f;

        /// <summary>仕切りの出入口の半幅 (x = ±2.5)。</summary>
        public const float DoorwayHalfWidth = 2.5f;

        /// <summary>出入口の先で視線を遮る衝立の z と半幅。</summary>
        public const float BaffleZ = 43f;

        public const float BaffleHalfWidth = 8f;

        /// <summary>壁 (外周・仕切り・衝立)。高さはすべて <see cref="WallHeight"/>。</summary>
        public static readonly IReadOnlyList<LevelBox> Walls = new[]
        {
            new LevelBox("Wall_South", new Vector3(0f, WallHeight / 2f, -10f), new Vector3(30f, WallHeight, WallThickness)),
            new LevelBox("Wall_North", new Vector3(0f, WallHeight / 2f, 70f), new Vector3(30f, WallHeight, WallThickness)),
            new LevelBox("Wall_West", new Vector3(-15f, WallHeight / 2f, 30f), new Vector3(WallThickness, WallHeight, 80f)),
            new LevelBox("Wall_East", new Vector3(15f, WallHeight / 2f, 30f), new Vector3(WallThickness, WallHeight, 80f)),
            new LevelBox(
                "Wall_Divider_West",
                new Vector3(-(15f + DoorwayHalfWidth) / 2f, WallHeight / 2f, DividerZ),
                new Vector3(15f - DoorwayHalfWidth, WallHeight, WallThickness)),
            new LevelBox(
                "Wall_Divider_East",
                new Vector3((15f + DoorwayHalfWidth) / 2f, WallHeight / 2f, DividerZ),
                new Vector3(15f - DoorwayHalfWidth, WallHeight, WallThickness)),
            new LevelBox(
                "Wall_Baffle",
                new Vector3(0f, WallHeight / 2f, BaffleZ),
                new Vector3(BaffleHalfWidth * 2f, WallHeight, WallThickness)),
        };

        /// <summary>奥の区画の遮蔽物。高さ 2m で、敵の目 (1.6m) からもプレイヤーの目からも体を隠せる。</summary>
        public static readonly IReadOnlyList<LevelBox> Covers = new[]
        {
            new LevelBox("Cover_1", new Vector3(-5f, 1f, 50f), new Vector3(3f, 2f, 1f)),
            new LevelBox("Cover_2", new Vector3(5f, 1f, 50f), new Vector3(3f, 2f, 1f)),
            new LevelBox("Cover_3", new Vector3(0f, 1f, 59f), new Vector3(4f, 2f, 1f)),
        };

        // ---- 目標 ----

        /// <summary>区域 A の床の中心。仕切りの手前、幅いっぱい (z = 30〜40)。</summary>
        public static readonly Vector3 AreaAPosition = new Vector3(0f, 0f, 35f);

        public static readonly Vector3 AreaAPadScale = new Vector3(29f, 0.1f, 10f);

        /// <summary>出口の床の中心。</summary>
        public static readonly Vector3 ExitPosition = new Vector3(0f, 0f, 66f);

        public static readonly Vector3 ExitPadScale = new Vector3(6f, 0.1f, 4f);

        /// <summary>区域のトリガーの高さ。プレイヤー (2m) より高い。</summary>
        public const float AreaTriggerHeight = 2.5f;

        // ---- チェックポイント (#74) ----

        /// <summary>開始地点のチェックポイント (足元)。GameBootstrap がプレイヤーを置く位置。</summary>
        public static readonly Vector3 StartCheckpointPosition = new Vector3(0f, 0f, 0f);

        /// <summary>区域 A のチェックポイント (足元)。区域 A の中で、仕切りの向こうから見えない位置。</summary>
        public static readonly Vector3 AreaACheckpointPosition = new Vector3(0f, 0f, 34f);

        /// <summary>開始地点のチェックポイントの Id。セーブデータに残るので変えない。</summary>
        public const string StartCheckpointId = "start";

        /// <summary>区域 A のチェックポイントの Id。セーブデータに残るので変えない。</summary>
        public const string AreaACheckpointId = "area_a";

        /// <summary>
        /// チェックポイントの床の印の大きさ。区域 A の印 (厚さ 0.1m) より厚くして、重なる所で面がちらつかないようにする。
        /// 区域 A のチェックポイントは区域 A の中 (z = 32〜36) に収まる。
        /// </summary>
        public static readonly Vector3 CheckpointPadScale = new Vector3(4f, 0.15f, 4f);

        // ---- 敵 ----

        public const float EnemyHeight = 2f;
        public const float EnemyRadius = 0.5f;
        public const float EnemySpeed = 3.5f;
        public const int EnemyMaxHealth = 100;

        /// <summary>巡回する敵。最初の地点に置き、2 つの地点を往復する。</summary>
        public static readonly IReadOnlyList<EnemyPlacement> Patrollers = new[]
        {
            EnemyPlacement.Patrol("Enemy-Patrol-1", "patrol-1", new Vector3(-11f, 0f, 55f), new Vector3(11f, 0f, 55f)),
            EnemyPlacement.Patrol("Enemy-Patrol-2", "patrol-2", new Vector3(11f, 0f, 63f), new Vector3(-11f, 0f, 63f)),
        };

        /// <summary>区域 A の先で待ち伏せる敵。巡回せず、衝立の左右の出口を向いて立つ。</summary>
        public static readonly IReadOnlyList<EnemyPlacement> Ambushers = new[]
        {
            EnemyPlacement.Ambush("Enemy-Ambush-1", "ambush-1", new Vector3(-12f, 0f, 49f), new Vector3(-11.5f, 0f, 41.5f)),
            EnemyPlacement.Ambush("Enemy-Ambush-2", "ambush-2", new Vector3(12f, 0f, 49f), new Vector3(11.5f, 0f, 41.5f)),
        };

        /// <summary>点が NavMesh 上にあるかを確かめるときに探す半径 (メートル)。</summary>
        public const float NavMeshCheckRadius = 1f;

        /// <summary>巡回の敵、待ち伏せの敵の順に並べた全員。</summary>
        public static IReadOnlyList<EnemyPlacement> AllEnemies()
        {
            var all = new List<EnemyPlacement>(Patrollers);
            all.AddRange(Ambushers);
            return all;
        }

        /// <summary>Campaign01 の目標。定義順に、前の目標を前提にする。</summary>
        public static IReadOnlyList<ObjectiveDefinition> CampaignObjectives()
        {
            return new[]
            {
                new ObjectiveDefinition(ReachAreaAId, "区域 A に到達する"),
                new ObjectiveDefinition(KillEnemiesId, $"敵を {EnemyCount} 体倒す", EnemyCount, false, ReachAreaAId),
                new ObjectiveDefinition(ReachExitId, "出口に到達する", 1, false, KillEnemiesId),
            };
        }

        /// <summary>
        /// 定義アセットとシーンを既定の場所に生成し、シーンを Build Settings に加える。
        /// 失敗したら例外を投げる (batchmode では Unity が終了コード 1 で終わる)。
        /// </summary>
        [MenuItem(MenuPath)]
        public static void Build()
        {
            // メニューから呼ばれたとき、開いているシーンの未保存の変更を黙って捨てない。
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                Debug.Log($"{LogTag} cancelled");
                return;
            }

            BuildAt(ScenePath, DefinitionPath, NavMeshDataPath);
        }

        /// <summary>definitionPath に定義を、navMeshDataPath に焼いた NavMesh を、scenePath にシーンを生成・保存する。</summary>
        public static void BuildAt(string scenePath, string definitionPath, string navMeshDataPath)
        {
            scenePath = BootstrapSceneBuilder.ValidateScenePath(scenePath);
            definitionPath = SceneBuildHelpers.ValidateAssetPath(definitionPath);
            navMeshDataPath = SceneBuildHelpers.ValidateAssetPath(navMeshDataPath);

            var definition = SceneBuildHelpers.CreateOrUpdateMissionDefinition(definitionPath, Configure);

            SceneBuildHelpers.EnsureFolder(Path.GetDirectoryName(scenePath));
            SceneBuildHelpers.EnsureFolder(Path.GetDirectoryName(navMeshDataPath));

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var layout = PopulateScene(scene, definition);
            SceneBuildHelpers.SaveNavMeshData(layout.Surface, navMeshDataPath);

            if (!EditorSceneManager.SaveScene(scene, scenePath))
            {
                throw new IOException($"Failed to save the campaign scene to '{scenePath}'");
            }

            EditorBuildSettings.scenes = SceneBuildHelpers.WithSceneAppended(EditorBuildSettings.scenes, scenePath);
            AssetDatabase.SaveAssets();

            Debug.Log($"{LogTag} saved '{definitionPath}', '{navMeshDataPath}' and '{scenePath}' and registered the scene in Build Settings");
        }

        /// <summary>definition の中身を Campaign01 にし、Mission に変換できることを確かめる。</summary>
        public static void Configure(MissionDefinition definition)
        {
            if (definition == null)
            {
                throw new ArgumentNullException(nameof(definition));
            }

            definition.DisplayName = MissionDisplayName;
            definition.SetObjectives(CampaignObjectives());

            // 不正ならここで MissionDefinitionException になり、壊れたアセットを保存しない。
            definition.CreateMission();
        }

        /// <summary>
        /// 空のシーンに地形を置いて NavMesh を焼き (アセットには保存しない)、敵・目標・GameBootstrap・MissionRunner・
        /// CampaignRunner・チェックポイントを置いて
        /// 参照をすべて設定する。焼いた NavMesh に敵の位置・巡回地点・区域・チェックポイントが乗っていて、開始地点から
        /// 歩いて行けることを確かめ、そうでなければ例外を投げる。失敗したときは置いた物をすべて片付ける。
        /// Edit Mode では Awake/Start が走らないので、ミッションと敵は Play したときに動き出す。
        /// </summary>
        public static CampaignLayout PopulateScene(Scene scene, MissionDefinition definition)
        {
            SceneBuildHelpers.RequireEmpty(scene, "the campaign scene");
            if (definition == null)
            {
                throw new ArgumentNullException(nameof(definition));
            }

            NavMeshSurface surface = null;
            try
            {
                // 敵のコライダーや区域のトリガーで NavMesh に穴が空かないよう、地形だけを置いた状態で焼く。
                surface = CreateLevel(scene);
                SceneBuildHelpers.BakeNavMesh(surface);
                RequireNavigable();

                var bootstrapObject = new GameObject(BootstrapObjectName);
                SceneManager.MoveGameObjectToScene(bootstrapObject, scene);
                var bootstrap = bootstrapObject.AddComponent<GameBootstrap>();
                bootstrap.CreatesFloor = false;

                var runnerObject = new GameObject(RunnerObjectName);
                SceneManager.MoveGameObjectToScene(runnerObject, scene);
                var runner = runnerObject.AddComponent<MissionRunner>();
                runner.Definition = definition;

                var areaA = CreateArea(scene, AreaAObjectName, AreaAPosition, AreaAPadScale, ReachAreaAId, runner);

                var enemiesRoot = new GameObject(EnemiesObjectName);
                SceneManager.MoveGameObjectToScene(enemiesRoot, scene);
                var body = new EnemyBody(EnemyHeight, EnemyRadius, EnemySpeed, EnemyMaxHealth);
                var placements = AllEnemies();
                var enemies = new EnemyAgent[placements.Count];
                var targets = new Damageable[placements.Count];
                for (var i = 0; i < placements.Count; i++)
                {
                    var placement = placements[i];
                    enemies[i] = SceneBuildHelpers.CreateEnemy(
                        scene,
                        enemiesRoot.transform,
                        placement.Name,
                        placement.Id,
                        placement.Position,
                        placement.Rotation,
                        placement.PatrolRoute,
                        body);
                    targets[i] = enemies[i].GetComponent<Damageable>();
                }

                var kill = enemiesRoot.AddComponent<KillTargetObjective>();
                kill.ObjectiveId = KillEnemiesId;
                kill.Runner = runner;
                kill.SetTargets(targets);

                var exit = CreateArea(scene, ExitObjectName, ExitPosition, ExitPadScale, ReachExitId, runner);

                // セーブの置き場所は CampaignRunner が実行時に FileSaveStorage.CreateDefault (スロット "campaign") で決める。
                // プレイヤーは GameBootstrap が実行時に作るので、CampaignRunner は GameBootstrap から受け取る。
                var campaignObject = new GameObject(CampaignRunnerObjectName);
                SceneManager.MoveGameObjectToScene(campaignObject, scene);
                var campaign = campaignObject.AddComponent<CampaignRunner>();
                campaign.Runner = runner;
                campaign.Bootstrap = bootstrap;

                var startCheckpoint = CreateCheckpoint(
                    scene, StartCheckpointObjectName, StartCheckpointPosition, StartCheckpointId, campaign);
                var areaACheckpoint = CreateCheckpoint(
                    scene, AreaACheckpointObjectName, AreaACheckpointPosition, AreaACheckpointId, campaign);

                EditorSceneManager.MarkSceneDirty(scene);
                return new CampaignLayout(
                    bootstrap, runner, surface, areaA, kill, enemies, exit, campaign, startCheckpoint, areaACheckpoint);
            }
            catch
            {
                var data = surface != null ? surface.navMeshData : null;
                foreach (var root in scene.GetRootGameObjects())
                {
                    Object.DestroyImmediate(root);
                }

                // まだアセットにしていない焼きたての NavMesh だけを捨てる。
                if (data != null && !AssetDatabase.Contains(data))
                {
                    Object.DestroyImmediate(data);
                }

                throw;
            }
        }

        /// <summary>
        /// 地形 (床・壁・遮蔽物) を <see cref="LevelObjectName"/> の下に置き、その子だけから焼く NavMeshSurface を返す。
        /// 子だけを集めるので、同時に開いている他のシーンの物は NavMesh に入らない。
        /// </summary>
        public static NavMeshSurface CreateLevel(Scene scene)
        {
            SceneBuildHelpers.RequireEmpty(scene, "the campaign level");

            var level = new GameObject(LevelObjectName);
            level.isStatic = true;
            SceneManager.MoveGameObjectToScene(level, scene);
            var surface = SceneBuildHelpers.AddNavMeshSurface(level, CollectObjects.Children);

            SceneBuildHelpers.CreateStaticPlane(scene, level.transform, FloorName, FloorCenter, FloorScale);
            foreach (var wall in Walls)
            {
                SceneBuildHelpers.CreateStaticBox(scene, level.transform, wall.Name, wall.Center, wall.Size);
            }

            foreach (var cover in Covers)
            {
                SceneBuildHelpers.CreateStaticBox(scene, level.transform, cover.Name, cover.Center, cover.Size);
            }

            EditorSceneManager.MarkSceneDirty(scene);
            return surface;
        }

        /// <summary>NavMesh の上で開始地点から行かなければならない点 (足元)。</summary>
        public static IReadOnlyList<Vector3> RequiredPoints()
        {
            var points = new List<Vector3> { AreaACheckpointPosition, AreaAPosition, ExitPosition };
            foreach (var enemy in AllEnemies())
            {
                points.Add(enemy.Position);
                points.AddRange(enemy.PatrolRoute);
            }

            return points;
        }

        private static void RequireNavigable()
        {
            var points = RequiredPoints();
            SceneBuildHelpers.RequireOnNavMesh(new[] { StartCheckpointPosition }, NavMeshCheckRadius, "start checkpoint");
            SceneBuildHelpers.RequireOnNavMesh(points, NavMeshCheckRadius, "campaign point");
            SceneBuildHelpers.RequireReachable(StartCheckpointPosition, points, NavMeshCheckRadius, "campaign point");
        }

        private static ReachAreaTrigger CreateArea(
            Scene scene,
            string name,
            Vector3 position,
            Vector3 padScale,
            string objectiveId,
            MissionRunner runner)
        {
            var pad = SceneBuildHelpers.CreateAreaPad(name, position, padScale, AreaTriggerHeight);
            SceneManager.MoveGameObjectToScene(pad, scene);
            var trigger = pad.AddComponent<ReachAreaTrigger>();
            trigger.ObjectiveId = objectiveId;
            trigger.Runner = runner;
            return trigger;
        }

        /// <summary>
        /// feet (足元) に床の印とトリガーを置いた <see cref="CheckpointTrigger"/> を作る。
        /// 印の中心は床から厚さの半分だけ上がるので、再開位置のずれはその分を引き、プレイヤーの足が床に着く高さにする。
        /// </summary>
        private static CheckpointTrigger CreateCheckpoint(
            Scene scene,
            string name,
            Vector3 feet,
            string checkpointId,
            CampaignRunner campaign)
        {
            var pad = SceneBuildHelpers.CreateAreaPad(name, feet, CheckpointPadScale, AreaTriggerHeight);
            SceneManager.MoveGameObjectToScene(pad, scene);
            var checkpoint = pad.AddComponent<CheckpointTrigger>();
            checkpoint.CheckpointId = checkpointId;
            checkpoint.Runner = campaign;
            checkpoint.PlayerTag = GameBootstrap.PlayerTag;
            checkpoint.RespawnOffset = new Vector3(0f, (GameBootstrap.PlayerHeight / 2f) - (CheckpointPadScale.y / 2f), 0f);
            return checkpoint;
        }
    }

    /// <summary>地形の直方体 1 つ。</summary>
    public readonly struct LevelBox
    {
        public string Name { get; }
        public Vector3 Center { get; }
        public Vector3 Size { get; }

        public LevelBox(string name, Vector3 center, Vector3 size)
        {
            Name = name;
            Center = center;
            Size = size;
        }
    }

    /// <summary>敵 1 体の置き方。</summary>
    public sealed class EnemyPlacement
    {
        public string Name { get; }
        public string Id { get; }

        /// <summary>置く位置 (足元)。</summary>
        public Vector3 Position { get; }

        public Quaternion Rotation { get; }

        /// <summary>巡回地点 (足元)。空なら巡回せずその場で待つ。</summary>
        public IReadOnlyList<Vector3> PatrolRoute { get; }

        public bool Patrols => PatrolRoute.Count > 0;

        private EnemyPlacement(string name, string id, Vector3 position, Quaternion rotation, Vector3[] patrolRoute)
        {
            Name = name;
            Id = id;
            Position = position;
            Rotation = rotation;
            PatrolRoute = patrolRoute;
        }

        /// <summary>from に置き、from と to を往復する敵。</summary>
        public static EnemyPlacement Patrol(string name, string id, Vector3 from, Vector3 to)
        {
            return new EnemyPlacement(name, id, from, SceneBuildHelpers.FacingTowards(from, to), new[] { from, to });
        }

        /// <summary>position に立ち、watch の方を向いて待つ敵。</summary>
        public static EnemyPlacement Ambush(string name, string id, Vector3 position, Vector3 watch)
        {
            return new EnemyPlacement(name, id, position, SceneBuildHelpers.FacingTowards(position, watch), Array.Empty<Vector3>());
        }
    }

    /// <summary><see cref="CampaignSceneBuilder.PopulateScene"/> が置いたもの。</summary>
    public sealed class CampaignLayout
    {
        public GameBootstrap Bootstrap { get; }
        public MissionRunner Runner { get; }
        public NavMeshSurface Surface { get; }
        public ReachAreaTrigger AreaA { get; }
        public KillTargetObjective KillEnemies { get; }

        /// <summary>巡回の敵、待ち伏せの敵の順。</summary>
        public IReadOnlyList<EnemyAgent> Enemies { get; }

        public ReachAreaTrigger Exit { get; }

        /// <summary>セーブと再開を受け持つ。<see cref="Runner"/> と <see cref="Bootstrap"/> につないである。</summary>
        public CampaignRunner Campaign { get; }

        /// <summary>開始地点のチェックポイント (Id は <see cref="CampaignSceneBuilder.StartCheckpointId"/>)。</summary>
        public CheckpointTrigger StartCheckpoint { get; }

        /// <summary>区域 A のチェックポイント (Id は <see cref="CampaignSceneBuilder.AreaACheckpointId"/>)。</summary>
        public CheckpointTrigger AreaACheckpoint { get; }

        public CampaignLayout(
            GameBootstrap bootstrap,
            MissionRunner runner,
            NavMeshSurface surface,
            ReachAreaTrigger areaA,
            KillTargetObjective killEnemies,
            IReadOnlyList<EnemyAgent> enemies,
            ReachAreaTrigger exit,
            CampaignRunner campaign,
            CheckpointTrigger startCheckpoint,
            CheckpointTrigger areaACheckpoint)
        {
            Bootstrap = bootstrap;
            Runner = runner;
            Surface = surface;
            AreaA = areaA;
            KillEnemies = killEnemies;
            Enemies = enemies;
            Exit = exit;
            Campaign = campaign;
            StartCheckpoint = startCheckpoint;
            AreaACheckpoint = areaACheckpoint;
        }
    }
}
