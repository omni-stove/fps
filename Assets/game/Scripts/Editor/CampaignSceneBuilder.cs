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
using Object = UnityEngine.Object;

namespace Fps.Editor
{
    /// <summary>
    /// キャンペーンのミッション (<see cref="Missions"/>: Campaign01 と <see cref="Campaign02Mission"/>) のシーンとミッション定義を生成する。
    /// ミッションごとの配置は <see cref="CampaignMissionSpec"/> で受け取り、次の順にシーンの直下へ置く:
    /// 地形 (床・壁・遮蔽物、<see cref="LevelObjectName"/>) を置いて <see cref="NavMeshSurface"/> で NavMesh を焼き、
    /// NavMeshData をシーンの隣に保存してから、<see cref="GameBootstrap"/> (床は作らない)、<see cref="MissionRunner"/>、
    /// <see cref="CampaignMissionSpec.Elements"/> (目標の範囲・敵の組・遮蔽点)、セーブと再開を受け持つ <see cref="CampaignRunner"/>、
    /// <see cref="CampaignMissionSpec.Checkpoints"/> の <see cref="CheckpointTrigger"/>。参照はすべてここでコードから設定する。
    /// ルールは CampaignNormal を既定にし、CampaignRunner には <c>-difficulty</c> 用に CampaignEasy / Normal / Hard を配線する
    /// (先に <see cref="GameRulesAssetBuilder.Build"/> を走らせておく)。
    /// 保存したシーンは Build Settings の末尾に加える (先頭の起動シーンは変えない)。何度実行しても同じ結果になる。
    /// Editor を閉じた状態で次のように呼べる:
    /// <code>unity -batchmode -quit -projectPath . -executeMethod Fps.Editor.CampaignSceneBuilder.Build</code>
    /// </summary>
    /// <remarks>
    /// Campaign01 は 3 つの目標 (区域 A に到達 → 敵 4 体を倒す → 出口に到達) と敵 4 体、
    /// 2 つのチェックポイント (開始地点 <c>start</c>・区域 A <c>area_a</c>) を持つ。
    /// 配置 (上から見て、プレイヤーは原点で +Z を向く。床は x = ±15、z = -10〜70):
    /// <list type="bullet">
    /// <item>z &lt; 30 は開始区画。GameBootstrap が実行時に z = 10 へ標的ダミーを並べる。</item>
    /// <item>z = 30〜40 の幅いっぱいが区域 A。z = 40 の仕切りの中央 (x = ±2.5) にだけ出入口がある。</item>
    /// <item>出入口の 3m 先 (z = 43) に幅 16m の衝立があり、区域 A より手前からは仕切りの向こうが見えない
    /// (KillTargetObjective は目標が Active になる前の撃破を数えないので、区域 A に入る前に敵を撃てないようにする)。</item>
    /// <item>衝立の左右の出口を、待ち伏せの敵 2 体 (handgun) が z = 49 から見張る。その奥の z = 55 と z = 63 を、
    /// 巡回の敵 2 体 (rifle) が左右に往復する。</item>
    /// <item>奥の区画の 3 つの遮蔽物の南北の面の左右の端に遮蔽点 (<see cref="CoverPoints"/>) があり、敵 4 体が 1 つの
    /// <see cref="CoverPointRegistry"/> を共有する。撃たれた敵はプレイヤーの方向を遮る点へ移り、端の外へ出て覗いて撃つ。</item>
    /// <item>出口は北の壁の手前 (0, 0, 66)。</item>
    /// <item>チェックポイントは開始地点 (原点、プレイヤーは出現した時点で踏んでいる) と区域 A の中 (0, 0, 34)。</item>
    /// </list>
    /// </remarks>
    public static class CampaignSceneBuilder
    {
        public static string ScenePath => BuildSceneList.CampaignScene;
        public static string DefinitionPath => FpsKitRoot.OutputPath("Missions/" + MissionId + ".asset");

        /// <summary>キャンペーンの焼いた NavMesh のファイル名。</summary>
        public const string NavMeshDataFileName = "NavMesh-Level.asset";

        /// <summary>焼いた NavMesh。Unity の慣習どおりシーン名のフォルダに置く。</summary>
        public static string NavMeshDataPath => FpsKitRoot.OutputPath("Scenes/" + MissionId + "/" + NavMeshDataFileName);

        public const string MenuPath = "Fps/Build Campaign Scenes";
        public const string LogTag = "[CampaignSceneBuilder]";

        /// <summary>Campaign01 のミッション Id。定義のアセット名 (<see cref="DefinitionPath"/>) と同じ。</summary>
        public const string MissionId = "Campaign01";

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

        public const string CoverPointsObjectName = "CoverPoints";

        /// <summary>遮蔽点を置く、遮蔽物の面からの距離 (m)。敵の半径 (0.5m) で削られた NavMesh の縁より少し外。</summary>
        public const float CoverPointStandOff = 0.7f;

        /// <summary>遮蔽点を置く、遮蔽物の端からの内側への距離 (m)。</summary>
        public const float CoverPointEdgeInset = 0.5f;

        /// <summary>
        /// 覗くときに遮蔽物の端の外へ出る距離 (m)。立つ位置から端までの距離に足す。
        /// 斜め前のプレイヤーでも遮蔽物の厚み (1m) の角に遮られないよう、体の幅より大きく出る。
        /// </summary>
        public const float CoverPeekOutset = 1.5f;

        /// <summary>
        /// 遮蔽物 (<see cref="Covers"/>) の横に置く遮蔽点。遮蔽物は高さ 2m で立ったまま隠れる高い壁なので、どれも覗くときは
        /// 近い方の端の外へ出る。南北の両面の左右の端に 1 つずつ置き、南から来るプレイヤーにも、回り込んで北に回ったプレイヤーにも使える。
        /// </summary>
        public static IReadOnlyList<CoverPointPlacement> CoverPoints()
        {
            return HighCoverPoints(Covers);
        }

        /// <summary>
        /// 立ったまま隠れる高い遮蔽物 (直方体) の横に置く遮蔽点。南北の両面の左右の端に 1 つずつ置き、
        /// 覗くときは近い方の端の外へ <see cref="CoverPeekOutset"/> だけ出る。
        /// </summary>
        public static IReadOnlyList<CoverPointPlacement> HighCoverPoints(IEnumerable<LevelBox> covers)
        {
            if (covers == null)
            {
                throw new ArgumentNullException(nameof(covers));
            }

            var points = new List<CoverPointPlacement>();
            foreach (var cover in covers)
            {
                var halfX = cover.Size.x / 2f;
                var halfZ = cover.Size.z / 2f;
                var west = cover.Center.x - halfX + CoverPointEdgeInset;
                var east = cover.Center.x + halfX - CoverPointEdgeInset;
                var peek = CoverPointEdgeInset + CoverPeekOutset;
                var north = cover.Center.z + halfZ + CoverPointStandOff;
                var south = cover.Center.z - halfZ - CoverPointStandOff;

                // 北側の点は南 (-Z) の壁を向き、南から来る弾を遮る。南側はその逆。
                points.Add(CoverPointPlacement.High($"{cover.Name}_NW", new Vector3(west, 0f, north), Vector3.back, new Vector3(-peek, 0f, 0f)));
                points.Add(CoverPointPlacement.High($"{cover.Name}_NE", new Vector3(east, 0f, north), Vector3.back, new Vector3(peek, 0f, 0f)));
                points.Add(CoverPointPlacement.High($"{cover.Name}_SW", new Vector3(west, 0f, south), Vector3.forward, new Vector3(-peek, 0f, 0f)));
                points.Add(CoverPointPlacement.High($"{cover.Name}_SE", new Vector3(east, 0f, south), Vector3.forward, new Vector3(peek, 0f, 0f)));
            }

            return points;
        }

        /// <summary>
        /// しゃがんで隠れる低い遮蔽物 (<see cref="CoverProbe.LowWallHeight"/> 未満の直方体) の横に置く遮蔽点。
        /// 南北の両面の左右の端に 1 つずつ置き、覗くときはその場で立ち上がる。高すぎる遮蔽物があれば <see cref="ArgumentException"/> を投げる。
        /// </summary>
        public static IReadOnlyList<CoverPointPlacement> LowCoverPoints(IEnumerable<LevelBox> covers)
        {
            if (covers == null)
            {
                throw new ArgumentNullException(nameof(covers));
            }

            var points = new List<CoverPointPlacement>();
            foreach (var cover in covers)
            {
                // 立ち上がっても頭が出ない高さだと、覗いても撃てない。
                if (!(cover.Size.y < CoverProbe.LowWallHeight))
                {
                    throw new ArgumentException(
                        $"cover '{cover.Name}' is {cover.Size.y}m tall; a low cover must be lower than {CoverProbe.LowWallHeight}m",
                        nameof(covers));
                }

                var halfX = cover.Size.x / 2f;
                var halfZ = cover.Size.z / 2f;
                var west = cover.Center.x - halfX + CoverPointEdgeInset;
                var east = cover.Center.x + halfX - CoverPointEdgeInset;
                var north = cover.Center.z + halfZ + CoverPointStandOff;
                var south = cover.Center.z - halfZ - CoverPointStandOff;

                points.Add(CoverPointPlacement.Low($"{cover.Name}_NW", new Vector3(west, 0f, north), Vector3.back));
                points.Add(CoverPointPlacement.Low($"{cover.Name}_NE", new Vector3(east, 0f, north), Vector3.back));
                points.Add(CoverPointPlacement.Low($"{cover.Name}_SW", new Vector3(west, 0f, south), Vector3.forward));
                points.Add(CoverPointPlacement.Low($"{cover.Name}_SE", new Vector3(east, 0f, south), Vector3.forward));
            }

            return points;
        }

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

        /// <summary>巡回する敵。最初の地点に置き、2 つの地点を往復する。射程の長い rifle を持つ。</summary>
        public static readonly IReadOnlyList<EnemyPlacement> Patrollers = new[]
        {
            EnemyPlacement.Patrol(
                "Enemy-Patrol-1", "patrol-1", WeaponAssetBuilder.RifleId, new Vector3(-11f, 0f, 55f), new Vector3(11f, 0f, 55f)),
            EnemyPlacement.Patrol(
                "Enemy-Patrol-2", "patrol-2", WeaponAssetBuilder.RifleId, new Vector3(11f, 0f, 63f), new Vector3(-11f, 0f, 63f)),
        };

        /// <summary>
        /// 区域 A の先で待ち伏せる敵。巡回せず、衝立の左右の出口を向いて立つ。
        /// 出口までは 8m ほどで handgun の減衰が始まる距離 (15m) より近いので、handgun を持たせる。
        /// </summary>
        public static readonly IReadOnlyList<EnemyPlacement> Ambushers = new[]
        {
            EnemyPlacement.Ambush(
                "Enemy-Ambush-1", "ambush-1", WeaponAssetBuilder.HandgunId, new Vector3(-12f, 0f, 49f), new Vector3(-11.5f, 0f, 41.5f)),
            EnemyPlacement.Ambush(
                "Enemy-Ambush-2", "ambush-2", WeaponAssetBuilder.HandgunId, new Vector3(12f, 0f, 49f), new Vector3(11.5f, 0f, 41.5f)),
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

        /// <summary>Campaign01 の <see cref="AllEnemies"/> の武器を引く (<see cref="ResolveEnemyWeapons(CampaignMissionSpec, WeaponCatalogAsset)"/>)。</summary>
        public static WeaponDefinition[] ResolveEnemyWeapons(WeaponCatalogAsset catalog)
        {
            return ResolveEnemyWeapons(Campaign01(), catalog);
        }

        /// <summary>
        /// spec の <see cref="CampaignMissionSpec.AllEnemies"/> と同じ順に、各配置の <see cref="EnemyPlacement.WeaponId"/> の武器を catalog から引く。
        /// どれかの Id が catalog に無ければ、配置の Id と武器の Id を添えて <see cref="KeyNotFoundException"/> を投げる。
        /// </summary>
        public static WeaponDefinition[] ResolveEnemyWeapons(CampaignMissionSpec spec, WeaponCatalogAsset catalog)
        {
            if (spec == null)
            {
                throw new ArgumentNullException(nameof(spec));
            }

            if (catalog == null)
            {
                throw new ArgumentNullException(nameof(catalog));
            }

            var placements = spec.AllEnemies();
            var weapons = new WeaponDefinition[placements.Count];
            for (var i = 0; i < placements.Count; i++)
            {
                weapons[i] = WeaponAssetBuilder.FindInCatalog(catalog, placements[i].Id, placements[i].WeaponId);
            }

            return weapons;
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

        /// <summary>Campaign01 の配置。上の定数と一覧から組み立てる。</summary>
        public static CampaignMissionSpec Campaign01()
        {
            return new CampaignMissionSpec(
                MissionId,
                MissionDisplayName,
                ScenePath,
                DefinitionPath,
                NavMeshDataPath,
                FloorCenter,
                FloorScale,
                Walls,
                Covers,
                CoverPoints(),
                CampaignObjectives(),
                new CampaignElement[]
                {
                    CampaignAreaElement.Reach(AreaAObjectName, ReachAreaAId, AreaAPosition, AreaAPadScale),
                    new CampaignEnemyGroupElement(EnemiesObjectName, AllEnemies(), KillEnemiesId),
                    new CampaignCoverPointsElement(CoverPointsObjectName),
                    CampaignAreaElement.Reach(ExitObjectName, ReachExitId, ExitPosition, ExitPadScale),
                },
                new[]
                {
                    new CampaignCheckpoint(StartCheckpointObjectName, StartCheckpointId, StartCheckpointPosition),
                    new CampaignCheckpoint(AreaACheckpointObjectName, AreaACheckpointId, AreaACheckpointPosition),
                });
        }

        /// <summary>生成するミッション。この順に作り、ミッションの一覧 (<see cref="MissionCatalogBuilder"/>) にもこの順に並べる。</summary>
        public static IReadOnlyList<CampaignMissionSpec> Missions()
        {
            return new[] { Campaign01(), Campaign02Mission.Spec() };
        }

        /// <summary>
        /// <see cref="Missions"/> の定義アセットとシーンを既定の場所に生成し、シーンを Build Settings に加え、
        /// ミッションの一覧 (<see cref="MissionCatalogBuilder"/>) も作り直す。
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

            foreach (var spec in Missions())
            {
                BuildAt(spec);
            }

            MissionCatalogBuilder.Build();
        }

        /// <summary>Campaign01 を definitionPath・navMeshDataPath・scenePath に生成・保存する。</summary>
        public static void BuildAt(string scenePath, string definitionPath, string navMeshDataPath)
        {
            BuildAt(Campaign01(), scenePath, definitionPath, navMeshDataPath);
        }

        /// <summary>spec を spec の既定の場所に生成・保存する。</summary>
        public static void BuildAt(CampaignMissionSpec spec)
        {
            if (spec == null)
            {
                throw new ArgumentNullException(nameof(spec));
            }

            BuildAt(spec, spec.ScenePath, spec.DefinitionPath, spec.NavMeshDataPath);
        }

        /// <summary>spec の定義を definitionPath に、焼いた NavMesh を navMeshDataPath に、シーンを scenePath に生成・保存する。</summary>
        public static void BuildAt(CampaignMissionSpec spec, string scenePath, string definitionPath, string navMeshDataPath)
        {
            if (spec == null)
            {
                throw new ArgumentNullException(nameof(spec));
            }

            scenePath = BootstrapSceneBuilder.ValidateScenePath(scenePath);
            definitionPath = SceneBuildHelpers.ValidateAssetPath(definitionPath);
            navMeshDataPath = SceneBuildHelpers.ValidateAssetPath(navMeshDataPath);

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // NewScene(Single) は参照されていないアセットをアンロードし、先に読んだ定義が null になるので、シーンを作ってから読む。
            // 武器のアセットが無い・敵の武器がカタログに無いなら何も保存せずに止める (先に WeaponAssetBuilder.Build を走らせる)。
            // ルールのアセットも同じく、無ければ何も保存せずに止める (先に GameRulesAssetBuilder.Build を走らせる)。
            var playerWeapons = WeaponAssetBuilder.LoadPlayerLoadout();
            var catalog = WeaponAssetBuilder.LoadCatalog();
            ResolveEnemyWeapons(spec, catalog);
            var rules = LoadRules();
            var definition = SceneBuildHelpers.CreateOrUpdateMissionDefinition(definitionPath, d => Configure(d, spec));

            SceneBuildHelpers.EnsureFolder(Path.GetDirectoryName(scenePath));
            SceneBuildHelpers.EnsureFolder(Path.GetDirectoryName(navMeshDataPath));
            var layout = PopulateScene(scene, spec, definition, playerWeapons, catalog, rules);
            SceneBuildHelpers.SaveNavMeshData(layout.Surface, navMeshDataPath);

            if (!EditorSceneManager.SaveScene(scene, scenePath))
            {
                throw new IOException($"Failed to save the campaign scene to '{scenePath}'");
            }

            BuildSceneList.Register(scenePath);
            AssetDatabase.SaveAssets();

            Debug.Log($"{LogTag} saved mission={spec.Id} '{definitionPath}', '{navMeshDataPath}' and '{scenePath}' and registered the scene in Build Settings");
        }

        /// <summary>
        /// キャンペーンの難易度別のルール (<see cref="GameRulesAssetBuilder.CampaignEasyPath"/> /
        /// <see cref="GameRulesAssetBuilder.CampaignNormalPath"/> / <see cref="GameRulesAssetBuilder.CampaignHardPath"/>) を読む。
        /// どれかが無いか不正なら例外を投げる。
        /// </summary>
        public static CampaignRules LoadRules()
        {
            return new CampaignRules(
                GameRulesAssetBuilder.LoadRules(GameRulesAssetBuilder.CampaignEasyPath),
                GameRulesAssetBuilder.LoadRules(GameRulesAssetBuilder.CampaignNormalPath),
                GameRulesAssetBuilder.LoadRules(GameRulesAssetBuilder.CampaignHardPath));
        }

        /// <summary>definition の中身を Campaign01 にし、Mission に変換できることを確かめる。</summary>
        public static void Configure(MissionDefinition definition)
        {
            Configure(definition, Campaign01());
        }

        /// <summary>definition の中身を spec の表示名と目標にし、Mission に変換できることを確かめる。</summary>
        public static void Configure(MissionDefinition definition, CampaignMissionSpec spec)
        {
            if (definition == null)
            {
                throw new ArgumentNullException(nameof(definition));
            }

            if (spec == null)
            {
                throw new ArgumentNullException(nameof(spec));
            }

            definition.DisplayName = spec.DisplayName;
            definition.SetObjectives(spec.Objectives);

            // 不正ならここで MissionDefinitionException になり、壊れたアセットを保存しない。
            definition.CreateMission();
        }

        /// <summary>Campaign01 を置く (<see cref="PopulateScene(Scene, CampaignMissionSpec, MissionDefinition, IReadOnlyList{WeaponDefinition}, WeaponCatalogAsset, CampaignRules)"/>)。</summary>
        public static CampaignLayout PopulateScene(
            Scene scene,
            MissionDefinition definition,
            IReadOnlyList<WeaponDefinition> playerWeapons,
            WeaponCatalogAsset enemyWeapons,
            CampaignRules rules)
        {
            return PopulateScene(scene, Campaign01(), definition, playerWeapons, enemyWeapons, rules);
        }

        /// <summary>
        /// 空のシーンに spec の地形を置いて NavMesh を焼き (アセットには保存しない)、GameBootstrap・MissionRunner・
        /// spec の目標の範囲・敵の組・遮蔽点・CampaignRunner・チェックポイントを置いて
        /// 参照をすべて設定する。焼いた NavMesh に敵の位置・巡回地点・区域・チェックポイントが乗っていて、開始地点から
        /// 歩いて行けることを確かめ、そうでなければ例外を投げる。失敗したときは置いた物をすべて片付ける。
        /// Edit Mode では Awake/Start が走らないので、ミッションと敵は Play したときに動き出す。
        /// プレイヤーは playerWeapons (通常は <see cref="WeaponAssetBuilder.LoadPlayerLoadout"/> の rifle, handgun) を持ち、敵は配置ごとに
        /// <see cref="EnemyPlacement.WeaponId"/> の武器を enemyWeapons から引いて撃つ。Id が enemyWeapons に無ければ
        /// 何も置かずに <see cref="KeyNotFoundException"/> を投げる。
        /// ルールは GameBootstrap と CampaignRunner の既定に rules の Normal を、CampaignRunner の <c>-difficulty</c> 用に 3 つとも配線する。
        /// </summary>
        public static CampaignLayout PopulateScene(
            Scene scene,
            CampaignMissionSpec spec,
            MissionDefinition definition,
            IReadOnlyList<WeaponDefinition> playerWeapons,
            WeaponCatalogAsset enemyWeapons,
            CampaignRules rules)
        {
            SceneBuildHelpers.RequireEmpty(scene, "the campaign scene");
            if (spec == null)
            {
                throw new ArgumentNullException(nameof(spec));
            }

            if (definition == null)
            {
                throw new ArgumentNullException(nameof(definition));
            }

            if (rules == null)
            {
                throw new ArgumentNullException(nameof(rules));
            }

            SceneBuildHelpers.RequirePlayerWeapons(playerWeapons, nameof(playerWeapons));

            var weapons = ResolveEnemyWeapons(spec, enemyWeapons);

            NavMeshSurface surface = null;
            try
            {
                // 敵のコライダーや区域のトリガーで NavMesh に穴が空かないよう、地形だけを置いた状態で焼く。
                surface = CreateLevel(scene, spec);
                SceneBuildHelpers.BakeNavMesh(surface);
                RequireNavigable(spec);

                var bootstrapObject = new GameObject(BootstrapObjectName);
                SceneManager.MoveGameObjectToScene(bootstrapObject, scene);
                var bootstrap = bootstrapObject.AddComponent<GameBootstrap>();
                bootstrap.CreatesFloor = false;
                bootstrap.WeaponDefinitions = playerWeapons;
                bootstrap.RulesDefinition = rules.Normal;

                var runnerObject = new GameObject(RunnerObjectName);
                SceneManager.MoveGameObjectToScene(runnerObject, scene);
                var runner = runnerObject.AddComponent<MissionRunner>();
                runner.Definition = definition;

                var body = new EnemyBody(EnemyHeight, EnemyRadius, EnemySpeed, EnemyMaxHealth);
                var areas = new List<ObjectiveAreaBase>();
                var kills = new List<KillTargetObjective>();
                var reveals = new List<ObjectiveReveal>();
                var enemies = new List<EnemyAgent>();
                CoverPointRegistry coverRegistry = null;
                foreach (var element in spec.Elements)
                {
                    switch (element)
                    {
                        case CampaignAreaElement area:
                            areas.Add(CreateArea(scene, area, runner));
                            break;

                        case CampaignEnemyGroupElement group:
                            CreateEnemyGroup(scene, group, runner, body, weapons, enemies, kills, reveals);
                            break;

                        case CampaignCoverPointsElement coverPoints:
                            coverRegistry = SceneBuildHelpers.CreateCoverPoints(scene, null, coverPoints.ObjectName, spec.CoverPoints);
                            break;

                        default:
                            throw new NotSupportedException($"unknown campaign element '{element.GetType().Name}' ({element.ObjectName})");
                    }
                }

                // 敵どうしで同じ点を取り合わないよう、全員に同じ registry を渡す。
                if (coverRegistry != null)
                {
                    foreach (var enemy in enemies)
                    {
                        enemy.CoverRegistry = coverRegistry;
                    }
                }

                // セーブの置き場所は CampaignRunner が実行時に FileSaveStorage.CreateDefault (スロットはミッション Id ごと) で決める。
                // プレイヤーは GameBootstrap が実行時に作るので、CampaignRunner は GameBootstrap から受け取る。
                var campaignObject = new GameObject(CampaignRunnerObjectName);
                SceneManager.MoveGameObjectToScene(campaignObject, scene);
                var campaign = campaignObject.AddComponent<CampaignRunner>();
                campaign.Runner = runner;
                campaign.Bootstrap = bootstrap;
                campaign.RulesDefinition = rules.Normal;
                campaign.EasyRules = rules.Easy;
                campaign.NormalRules = rules.Normal;
                campaign.HardRules = rules.Hard;

                var checkpoints = new List<CheckpointTrigger>();
                foreach (var checkpoint in spec.Checkpoints)
                {
                    checkpoints.Add(CreateCheckpoint(scene, checkpoint.ObjectName, checkpoint.Feet, checkpoint.Id, campaign));
                }

                EditorSceneManager.MarkSceneDirty(scene);
                return new CampaignLayout(
                    bootstrap, runner, surface, areas, kills, reveals, enemies, campaign, checkpoints, coverRegistry);
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

        /// <summary>Campaign01 の地形を置く (<see cref="CreateLevel(Scene, CampaignMissionSpec)"/>)。</summary>
        public static NavMeshSurface CreateLevel(Scene scene)
        {
            return CreateLevel(scene, Campaign01());
        }

        /// <summary>
        /// spec の地形 (床・壁・遮蔽物) を <see cref="LevelObjectName"/> の下に置き、その子だけから焼く NavMeshSurface を返す。
        /// 子だけを集めるので、同時に開いている他のシーンの物は NavMesh に入らない。
        /// </summary>
        public static NavMeshSurface CreateLevel(Scene scene, CampaignMissionSpec spec)
        {
            if (spec == null)
            {
                throw new ArgumentNullException(nameof(spec));
            }

            SceneBuildHelpers.RequireEmpty(scene, "the campaign level");

            var level = new GameObject(LevelObjectName);
            level.isStatic = true;
            SceneManager.MoveGameObjectToScene(level, scene);
            var surface = SceneBuildHelpers.AddNavMeshSurface(level, CollectObjects.Children);

            SceneBuildHelpers.CreateStaticPlane(scene, level.transform, FloorName, spec.FloorCenter, spec.FloorScale);
            foreach (var wall in spec.Walls)
            {
                SceneBuildHelpers.CreateStaticBox(scene, level.transform, wall.Name, wall.Center, wall.Size);
            }

            foreach (var cover in spec.Covers)
            {
                SceneBuildHelpers.CreateStaticBox(scene, level.transform, cover.Name, cover.Center, cover.Size);
            }

            EditorSceneManager.MarkSceneDirty(scene);
            return surface;
        }

        /// <summary>Campaign01 の、NavMesh の上で開始地点から行かなければならない点 (<see cref="CampaignMissionSpec.RequiredPoints"/>)。</summary>
        public static IReadOnlyList<Vector3> RequiredPoints()
        {
            return Campaign01().RequiredPoints();
        }

        private static void RequireNavigable(CampaignMissionSpec spec)
        {
            var start = CampaignMissionSpec.PlayerStart;
            var points = spec.RequiredPoints();
            SceneBuildHelpers.RequireOnNavMesh(new[] { start }, NavMeshCheckRadius, $"{spec.Id} player start");
            SceneBuildHelpers.RequireOnNavMesh(points, NavMeshCheckRadius, $"{spec.Id} campaign point");
            SceneBuildHelpers.RequireReachable(start, points, NavMeshCheckRadius, $"{spec.Id} campaign point");
        }

        private static ObjectiveAreaBase CreateArea(Scene scene, CampaignAreaElement area, MissionRunner runner)
        {
            var pad = SceneBuildHelpers.CreateAreaPad(area.ObjectName, area.Position, area.PadScale, AreaTriggerHeight);
            SceneManager.MoveGameObjectToScene(pad, scene);
            ObjectiveAreaBase trigger;
            switch (area.Kind)
            {
                case CampaignAreaKind.Reach:
                    trigger = pad.AddComponent<ReachAreaTrigger>();
                    break;

                case CampaignAreaKind.Interact:
                    trigger = pad.AddComponent<InteractObjective>();
                    break;

                default:
                    throw new NotSupportedException($"unknown area kind {area.Kind} ({area.ObjectName})");
            }

            trigger.ObjectiveId = area.ObjectiveId;
            trigger.Runner = runner;
            return trigger;
        }

        /// <summary>
        /// group の敵を group の GameObject の子に置き、enemies に足す。weapons は <see cref="CampaignMissionSpec.AllEnemies"/> の順の武器で、
        /// 先頭から enemies に入っている数だけ飛ばした所から使う。
        /// </summary>
        private static void CreateEnemyGroup(
            Scene scene,
            CampaignEnemyGroupElement group,
            MissionRunner runner,
            EnemyBody body,
            IReadOnlyList<WeaponDefinition> weapons,
            List<EnemyAgent> enemies,
            List<KillTargetObjective> kills,
            List<ObjectiveReveal> reveals)
        {
            var root = new GameObject(group.ObjectName);
            SceneManager.MoveGameObjectToScene(root, scene);

            var targets = new Damageable[group.Enemies.Count];
            var members = new GameObject[group.Enemies.Count];
            for (var i = 0; i < group.Enemies.Count; i++)
            {
                var placement = group.Enemies[i];
                var enemy = SceneBuildHelpers.CreateEnemy(
                    scene,
                    root.transform,
                    placement.Name,
                    placement.Id,
                    placement.Position,
                    placement.Rotation,
                    placement.PatrolRoute,
                    body,
                    weapons[enemies.Count]);
                enemies.Add(enemy);
                targets[i] = enemy.GetComponent<Damageable>();
                members[i] = enemy.gameObject;
            }

            if (group.KillObjectiveId != null)
            {
                var kill = root.AddComponent<KillTargetObjective>();
                kill.ObjectiveId = group.KillObjectiveId;
                kill.Runner = runner;
                kill.SetTargets(targets);
                kills.Add(kill);
            }

            if (group.RevealObjectiveId != null)
            {
                // 目標が Active になるまでは撃てない・撃ってこないように、敵ごと消しておく (KillTargetObjective は Active 前の撃破を数えない)。
                foreach (var member in members)
                {
                    member.SetActive(false);
                }

                var reveal = root.AddComponent<ObjectiveReveal>();
                reveal.ObjectiveId = group.RevealObjectiveId;
                reveal.Runner = runner;
                reveal.SetTargets(members);
                reveals.Add(reveal);
            }
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

    /// <summary>キャンペーンの難易度別のルール。<see cref="CampaignSceneBuilder.LoadRules"/> で読む。</summary>
    public sealed class CampaignRules
    {
        public GameRulesDefinition Easy { get; }
        public GameRulesDefinition Normal { get; }
        public GameRulesDefinition Hard { get; }

        /// <exception cref="ArgumentNullException">どれかが null。</exception>
        public CampaignRules(GameRulesDefinition easy, GameRulesDefinition normal, GameRulesDefinition hard)
        {
            Easy = easy != null ? easy : throw new ArgumentNullException(nameof(easy));
            Normal = normal != null ? normal : throw new ArgumentNullException(nameof(normal));
            Hard = hard != null ? hard : throw new ArgumentNullException(nameof(hard));
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

        /// <summary>持たせる武器の Id (<see cref="WeaponDefinition.Id"/> の UUID)。シーンを生成するときに武器の一覧から引く。</summary>
        public string WeaponId { get; }

        /// <summary>置く位置 (足元)。</summary>
        public Vector3 Position { get; }

        public Quaternion Rotation { get; }

        /// <summary>巡回地点 (足元)。空なら巡回せずその場で待つ。</summary>
        public IReadOnlyList<Vector3> PatrolRoute { get; }

        public bool Patrols => PatrolRoute.Count > 0;

        private EnemyPlacement(
            string name,
            string id,
            string weaponId,
            Vector3 position,
            Quaternion rotation,
            Vector3[] patrolRoute)
        {
            if (string.IsNullOrWhiteSpace(weaponId))
            {
                throw new ArgumentException($"enemy placement '{id}' needs a weapon id", nameof(weaponId));
            }

            Name = name;
            Id = id;
            WeaponId = weaponId;
            Position = position;
            Rotation = rotation;
            PatrolRoute = patrolRoute;
        }

        /// <summary>from に置き、from と to を往復する敵。weaponId の武器で撃つ。</summary>
        public static EnemyPlacement Patrol(string name, string id, string weaponId, Vector3 from, Vector3 to)
        {
            return new EnemyPlacement(name, id, weaponId, from, SceneBuildHelpers.FacingTowards(from, to), new[] { from, to });
        }

        /// <summary>position に立ち、watch の方を向いて待つ敵。weaponId の武器で撃つ。</summary>
        public static EnemyPlacement Ambush(string name, string id, string weaponId, Vector3 position, Vector3 watch)
        {
            return new EnemyPlacement(
                name, id, weaponId, position, SceneBuildHelpers.FacingTowards(position, watch), Array.Empty<Vector3>());
        }
    }

    /// <summary>CampaignSceneBuilder.PopulateScene が置いたもの。</summary>
    public sealed class CampaignLayout
    {
        public GameBootstrap Bootstrap { get; }
        public MissionRunner Runner { get; }
        public NavMeshSurface Surface { get; }

        /// <summary>目標の範囲 (<see cref="ReachAreaTrigger"/> と <see cref="InteractObjective"/>)。置いた順。</summary>
        public IReadOnlyList<ObjectiveAreaBase> Areas { get; }

        /// <summary>敵の組の撃破で進める目標。置いた順。</summary>
        public IReadOnlyList<KillTargetObjective> KillObjectives { get; }

        /// <summary>目標が Active になるまで敵を隠しておくもの。置いた順。</summary>
        public IReadOnlyList<ObjectiveReveal> Reveals { get; }

        /// <summary>全員。<see cref="CampaignMissionSpec.AllEnemies"/> と同じ順。</summary>
        public IReadOnlyList<EnemyAgent> Enemies { get; }

        /// <summary>セーブと再開を受け持つ。<see cref="Runner"/> と <see cref="Bootstrap"/> につないである。</summary>
        public CampaignRunner Campaign { get; }

        /// <summary>チェックポイント。<see cref="CampaignMissionSpec.Checkpoints"/> と同じ順。</summary>
        public IReadOnlyList<CheckpointTrigger> Checkpoints { get; }

        /// <summary>遮蔽点をまとめたもの。<see cref="Enemies"/> の全員に渡してある。遮蔽点が無いミッションでは null。</summary>
        public CoverPointRegistry CoverRegistry { get; }

        public CampaignLayout(
            GameBootstrap bootstrap,
            MissionRunner runner,
            NavMeshSurface surface,
            IReadOnlyList<ObjectiveAreaBase> areas,
            IReadOnlyList<KillTargetObjective> killObjectives,
            IReadOnlyList<ObjectiveReveal> reveals,
            IReadOnlyList<EnemyAgent> enemies,
            CampaignRunner campaign,
            IReadOnlyList<CheckpointTrigger> checkpoints,
            CoverPointRegistry coverRegistry)
        {
            Bootstrap = bootstrap;
            Runner = runner;
            Surface = surface;
            Areas = areas ?? throw new ArgumentNullException(nameof(areas));
            KillObjectives = killObjectives ?? throw new ArgumentNullException(nameof(killObjectives));
            Reveals = reveals ?? throw new ArgumentNullException(nameof(reveals));
            Enemies = enemies ?? throw new ArgumentNullException(nameof(enemies));
            Campaign = campaign;
            Checkpoints = checkpoints ?? throw new ArgumentNullException(nameof(checkpoints));
            CoverRegistry = coverRegistry;
        }

        /// <summary>objectiveId を進める範囲。無ければ <see cref="KeyNotFoundException"/>。</summary>
        public ObjectiveAreaBase Area(string objectiveId)
        {
            foreach (var area in Areas)
            {
                if (string.Equals(area.ObjectiveId, objectiveId, StringComparison.Ordinal))
                {
                    return area;
                }
            }

            throw new KeyNotFoundException($"no area progresses objective '{objectiveId}'");
        }

        /// <summary>objectiveId を進める撃破の目標。無ければ <see cref="KeyNotFoundException"/>。</summary>
        public KillTargetObjective KillObjective(string objectiveId)
        {
            foreach (var kill in KillObjectives)
            {
                if (string.Equals(kill.ObjectiveId, objectiveId, StringComparison.Ordinal))
                {
                    return kill;
                }
            }

            throw new KeyNotFoundException($"no kill objective progresses objective '{objectiveId}'");
        }

        /// <summary>objectiveId が Active になったら敵を出すもの。無ければ <see cref="KeyNotFoundException"/>。</summary>
        public ObjectiveReveal Reveal(string objectiveId)
        {
            foreach (var reveal in Reveals)
            {
                if (string.Equals(reveal.ObjectiveId, objectiveId, StringComparison.Ordinal))
                {
                    return reveal;
                }
            }

            throw new KeyNotFoundException($"nothing is revealed by objective '{objectiveId}'");
        }

        /// <summary>Id が checkpointId のチェックポイント。無ければ <see cref="KeyNotFoundException"/>。</summary>
        public CheckpointTrigger Checkpoint(string checkpointId)
        {
            foreach (var checkpoint in Checkpoints)
            {
                if (string.Equals(checkpoint.CheckpointId, checkpointId, StringComparison.Ordinal))
                {
                    return checkpoint;
                }
            }

            throw new KeyNotFoundException($"no checkpoint '{checkpointId}'");
        }
    }
}
