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
using Object = UnityEngine.Object;

namespace Shooter.Editor
{
    public static class CampaignSceneBuilder
    {
        public static string ScenePath => BuildSceneList.CampaignScene;
        public static string DefinitionPath => ShooterKitRoot.OutputPath("Missions/" + MissionId + ".asset");

        public const string NavMeshDataFileName = "NavMesh-Level.asset";

        // Unity の慣習どおり、焼いた NavMesh はシーン名のフォルダに置く。
        public static string NavMeshDataPath => ShooterKitRoot.OutputPath("Scenes/" + MissionId + "/" + NavMeshDataFileName);

        public const string MenuPath = "Shooter/Build Campaign Scenes";
        public const string LogTag = "[CampaignSceneBuilder]";

        public const string MissionId = "Campaign01";

        public static string MissionNameKey => MissionDefinition.NameKeyOf(MissionId);

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

        public static readonly Vector3 FloorCenter = new Vector3(0f, 0f, 30f);

        public static readonly Vector3 FloorScale = new Vector3(3f, 1f, 8f);

        public const float WallHeight = 3f;
        public const float WallThickness = 0.5f;

        public const float DividerZ = 40f;

        public const float DoorwayHalfWidth = 2.5f;

        // KillTargetObjective は目標が Active になる前の撃破を数えないので、区域 A に入る前に敵を撃てないよう、
        // 出入口の先に衝立を置いて区域 A より手前からは仕切りの向こうを見えなくする。
        public const float BaffleZ = 43f;

        public const float BaffleHalfWidth = 8f;

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

        // 高さ 2m にして、敵の目 (1.6m) からもプレイヤーの目からも体を隠せるようにする。
        public static readonly IReadOnlyList<LevelBox> Covers = new[]
        {
            new LevelBox("Cover_1", new Vector3(-5f, 1f, 50f), new Vector3(3f, 2f, 1f)),
            new LevelBox("Cover_2", new Vector3(5f, 1f, 50f), new Vector3(3f, 2f, 1f)),
            new LevelBox("Cover_3", new Vector3(0f, 1f, 59f), new Vector3(4f, 2f, 1f)),
        };

        public const string CoverPointsObjectName = "CoverPoints";

        public const string PickupsObjectName = "Pickups";

        public const int WeaponPickupAmmoIfOwned = 2;

        public const int AmmoPickupAmount = 60;

        public const float AmmoPickupRespawnSeconds = 60f;

        public static IReadOnlyList<CampaignPickupPlacement> Pickups()
        {
            return new[]
            {
                CampaignPickupPlacement.Weapon("Pickup-Launcher", new Vector3(-6f, 0f, 12f), WeaponAssetBuilder.LauncherId, WeaponPickupAmmoIfOwned, 0f),
                CampaignPickupPlacement.Ammo("Pickup-Ammo-Start", new Vector3(6f, 0f, 12f), AmmoPickupAmount, AmmoPickupRespawnSeconds),
                CampaignPickupPlacement.AmmoCrate("AmmoCrate-AreaA", new Vector3(13.5f, 0f, 36f)),
                CampaignPickupPlacement.Ammo("Pickup-Ammo-North", new Vector3(-12f, 0f, 60f), AmmoPickupAmount, AmmoPickupRespawnSeconds),
            };
        }

        // 敵の半径 (0.5m) で削られた NavMesh の縁より少し外に置く。
        public const float CoverPointStandOff = 0.7f;

        public const float CoverPointEdgeInset = 0.5f;

        // 斜め前のプレイヤーでも遮蔽物の厚み (1m) の角に遮られないよう、体の幅より大きく出る。
        public const float CoverPeekOutset = 1.5f;

        public static IReadOnlyList<CoverPointPlacement> CoverPoints()
        {
            return HighCoverPoints(Covers);
        }

        // 南北の両面の左右の端に置くので、南から来るプレイヤーにも、回り込んで北に回ったプレイヤーにも使える。
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
                if (!(cover.Size.y < MotorSpec.DefaultCoverLowWallHeight))
                {
                    throw new ArgumentException(
                        $"cover '{cover.Name}' is {cover.Size.y}m tall; a low cover must be lower than {MotorSpec.DefaultCoverLowWallHeight}m",
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

        public static readonly Vector3 AreaAPosition = new Vector3(0f, 0f, 35f);

        public static readonly Vector3 AreaAPadScale = new Vector3(29f, 0.1f, 10f);

        public static readonly Vector3 ExitPosition = new Vector3(0f, 0f, 66f);

        public static readonly Vector3 ExitPadScale = new Vector3(6f, 0.1f, 4f);

        // プレイヤー (2m) より高くする。
        public const float AreaTriggerHeight = 2.5f;

        public static readonly Vector3 StartCheckpointPosition = new Vector3(0f, 0f, 0f);

        // 区域 A の中で、仕切りの向こうから見えない位置に置く。
        public static readonly Vector3 AreaACheckpointPosition = new Vector3(0f, 0f, 34f);

        // チェックポイントの Id はセーブデータに残るので変えない。
        public const string StartCheckpointId = "start";

        public const string AreaACheckpointId = "area_a";

        // 区域 A の印 (厚さ 0.1m) より厚くして、重なる所で面がちらつかないようにする。
        public static readonly Vector3 CheckpointPadScale = new Vector3(4f, 0.15f, 4f);

        public const float EnemyHeight = 2f;
        public const float EnemyRadius = 0.5f;
        public const float EnemySpeed = 3.5f;
        public const int EnemyMaxHealth = 100;

        public static readonly IReadOnlyList<EnemyPlacement> Patrollers = new[]
        {
            EnemyPlacement.Patrol(
                "Enemy-Patrol-1", "patrol-1", WeaponAssetBuilder.RifleId, new Vector3(-11f, 0f, 55f), new Vector3(11f, 0f, 55f)),
            EnemyPlacement.Patrol(
                "Enemy-Patrol-2", "patrol-2", WeaponAssetBuilder.RifleId, new Vector3(11f, 0f, 63f), new Vector3(-11f, 0f, 63f)),
        };

        // 見張る出口までは 8m ほどで handgun の減衰が始まる距離 (15m) より近いので、handgun を持たせる。
        public static readonly IReadOnlyList<EnemyPlacement> Ambushers = new[]
        {
            EnemyPlacement.Ambush(
                "Enemy-Ambush-1", "ambush-1", WeaponAssetBuilder.HandgunId, new Vector3(-12f, 0f, 49f), new Vector3(-11.5f, 0f, 41.5f)),
            EnemyPlacement.Ambush(
                "Enemy-Ambush-2", "ambush-2", WeaponAssetBuilder.HandgunId, new Vector3(12f, 0f, 49f), new Vector3(11.5f, 0f, 41.5f)),
        };

        public const float NavMeshCheckRadius = 1f;

        public static IReadOnlyList<EnemyPlacement> AllEnemies()
        {
            var all = new List<EnemyPlacement>(Patrollers);
            all.AddRange(Ambushers);
            return all;
        }

        public static WeaponDefinition[] ResolveEnemyWeapons(WeaponCatalogAsset catalog)
        {
            return ResolveEnemyWeapons(Campaign01(), catalog);
        }

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

        public static IReadOnlyList<ObjectiveDefinition> CampaignObjectives()
        {
            return new[]
            {
                new ObjectiveDefinition(ReachAreaAId, MissionDefinition.ObjectiveKeyOf(MissionId, ReachAreaAId)),
                new ObjectiveDefinition(KillEnemiesId, MissionDefinition.ObjectiveKeyOf(MissionId, KillEnemiesId), EnemyCount, false, ReachAreaAId),
                new ObjectiveDefinition(ReachExitId, MissionDefinition.ObjectiveKeyOf(MissionId, ReachExitId), 1, false, KillEnemiesId),
            };
        }

        public static CampaignMissionSpec Campaign01()
        {
            return new CampaignMissionSpec(
                MissionId,
                MissionNameKey,
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
                    new CampaignPickupsElement(PickupsObjectName, Pickups()),
                },
                new[]
                {
                    new CampaignCheckpoint(StartCheckpointObjectName, StartCheckpointId, StartCheckpointPosition),
                    new CampaignCheckpoint(AreaACheckpointObjectName, AreaACheckpointId, AreaACheckpointPosition),
                });
        }

        // ミッションの一覧 (MissionCatalogBuilder) にもこの順に並ぶ。
        public static IReadOnlyList<CampaignMissionSpec> Missions()
        {
            return new[] { Campaign01(), Campaign02Mission.Spec() };
        }

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

        public static void BuildAt(string scenePath, string definitionPath, string navMeshDataPath)
        {
            BuildAt(Campaign01(), scenePath, definitionPath, navMeshDataPath);
        }

        public static void BuildAt(CampaignMissionSpec spec)
        {
            if (spec == null)
            {
                throw new ArgumentNullException(nameof(spec));
            }

            BuildAt(spec, spec.ScenePath, spec.DefinitionPath, spec.NavMeshDataPath);
        }

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
            var pickupWeapons = LoadPickupWeapons(catalog);
            ResolvePickupWeapons(spec, pickupWeapons);
            var rules = LoadRules();
            var definition = SceneBuildHelpers.CreateOrUpdateMissionDefinition(definitionPath, d => Configure(d, spec));

            SceneBuildHelpers.EnsureFolder(Path.GetDirectoryName(scenePath));
            SceneBuildHelpers.EnsureFolder(Path.GetDirectoryName(navMeshDataPath));
            var layout = PopulateScene(scene, spec, definition, playerWeapons, catalog, rules, pickupWeapons);

            // 一覧はシーンを作った後に作る (一覧がシーンの有無を確かめる) ので、初めて作るときはまだ無い。
            // そのときは配線せず、CampaignRunner は入口 (GameContent) の一覧か既定の秒数を使う。
            var missions = AssetDatabase.LoadAssetAtPath<MissionCatalog>(MissionCatalogBuilder.CatalogPath);
            if (missions != null)
            {
                layout.Campaign.Missions = missions;
            }

            SceneBuildHelpers.SaveNavMeshData(layout.Surface, navMeshDataPath);

            if (!EditorSceneManager.SaveScene(scene, scenePath))
            {
                throw new IOException($"Failed to save the campaign scene to '{scenePath}'");
            }

            BuildSceneList.Register(scenePath);
            AssetDatabase.SaveAssets();

            Debug.Log($"{LogTag} saved mission={spec.Id} '{definitionPath}', '{navMeshDataPath}' and '{scenePath}' and registered the scene in Build Settings");
        }

        public static CampaignRules LoadRules()
        {
            return new CampaignRules(
                GameRulesAssetBuilder.LoadRules(GameRulesAssetBuilder.CampaignEasyPath),
                GameRulesAssetBuilder.LoadRules(GameRulesAssetBuilder.CampaignNormalPath),
                GameRulesAssetBuilder.LoadRules(GameRulesAssetBuilder.CampaignHardPath));
        }

        public static void Configure(MissionDefinition definition)
        {
            Configure(definition, Campaign01());
        }

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

            definition.Id = spec.Id;
            definition.DisplayNameKey = spec.NameKey;
            definition.SetObjectives(spec.Objectives);

            // 不正ならここで MissionDefinitionException になり、壊れたアセットを保存しない。
            definition.CreateMission();
        }

        public static CampaignLayout PopulateScene(
            Scene scene,
            MissionDefinition definition,
            IReadOnlyList<WeaponDefinition> playerWeapons,
            WeaponCatalogAsset enemyWeapons,
            CampaignRules rules)
        {
            return PopulateScene(scene, Campaign01(), definition, playerWeapons, enemyWeapons, rules);
        }

        public static CampaignLayout PopulateScene(
            Scene scene,
            CampaignMissionSpec spec,
            MissionDefinition definition,
            IReadOnlyList<WeaponDefinition> playerWeapons,
            WeaponCatalogAsset enemyWeapons,
            CampaignRules rules,
            IReadOnlyList<WeaponDefinition> pickupWeapons = null)
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
            pickupWeapons ??= LoadPickupWeapons(enemyWeapons);
            ResolvePickupWeapons(spec, pickupWeapons);

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

                        case CampaignPickupsElement pickups:
                            CreatePickups(scene, pickups, pickupWeapons);
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

                // セーブの置き場所は CampaignRunner が実行時に CloudSaveBootstrap.Storage (スロットはミッション Id ごと) で決める。
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

        // ランチャーはプレイヤーにも敵にも持たせないのでカタログに入れていないため、拾える物の武器として足す。
        public static IReadOnlyList<WeaponDefinition> LoadPickupWeapons(WeaponCatalogAsset catalog)
        {
            if (catalog == null)
            {
                throw new ArgumentNullException(nameof(catalog));
            }

            var weapons = new List<WeaponDefinition>(catalog.Weapons);
            var launcher = WeaponAssetBuilder.LoadLauncher();
            if (FindWeapon(weapons, launcher.Id) == null)
            {
                weapons.Add(launcher);
            }

            return weapons;
        }

        public static void ResolvePickupWeapons(CampaignMissionSpec spec, IReadOnlyList<WeaponDefinition> weapons)
        {
            if (spec == null)
            {
                throw new ArgumentNullException(nameof(spec));
            }

            foreach (var element in spec.Elements)
            {
                if (element is CampaignPickupsElement pickups)
                {
                    foreach (var pickup in pickups.Pickups)
                    {
                        if (pickup.WeaponId != null)
                        {
                            RequireWeapon(weapons, pickup);
                        }
                    }
                }
            }
        }

        public static GameObject CreatePickups(Scene scene, CampaignPickupsElement element, IReadOnlyList<WeaponDefinition> weapons)
        {
            if (element == null)
            {
                throw new ArgumentNullException(nameof(element));
            }

            // 中身を先にすべて作り、引けない武器があれば何も置かない。
            var contents = new PickupContents[element.Pickups.Count];
            for (var i = 0; i < contents.Length; i++)
            {
                var pickup = element.Pickups[i];
                switch (pickup.Kind)
                {
                    case CampaignPickupKind.Weapon:
                        contents[i] = PickupContents.ForWeapon(RequireWeapon(weapons, pickup), pickup.Amount);
                        break;
                    case CampaignPickupKind.Ammo:
                        contents[i] = PickupContents.ForReserveAmmo(pickup.Amount);
                        break;
                }
            }

            var root = new GameObject(element.ObjectName);
            SceneManager.MoveGameObjectToScene(root, scene);
            for (var i = 0; i < contents.Length; i++)
            {
                var pickup = element.Pickups[i];
                var pointObject = new GameObject(pickup.ObjectName);
                pointObject.transform.SetParent(root.transform, false);
                pointObject.transform.position = pickup.Feet;
                if (pickup.Kind == CampaignPickupKind.AmmoCrate)
                {
                    pointObject.AddComponent<AmmoCrate>().Build();
                    continue;
                }

                var point = pointObject.AddComponent<PickupSpawnPoint>();
                point.Contents = contents[i];
                point.CollectMode = PickupCollectMode.Touch;
                point.RespawnSeconds = pickup.RespawnSeconds;
            }

            return root;
        }

        private static WeaponDefinition RequireWeapon(IReadOnlyList<WeaponDefinition> weapons, CampaignPickupPlacement pickup)
        {
            var found = FindWeapon(weapons, pickup.WeaponId);
            if (found == null)
            {
                throw new KeyNotFoundException($"pickup '{pickup.ObjectName}' uses weapon id '{pickup.WeaponId}', which is not among the pickup weapons");
            }

            return found;
        }

        private static WeaponDefinition FindWeapon(IEnumerable<WeaponDefinition> weapons, string weaponId)
        {
            if (weapons == null)
            {
                throw new ArgumentNullException(nameof(weapons));
            }

            foreach (var weapon in weapons)
            {
                if (weapon != null && string.Equals(weapon.Id, weaponId, StringComparison.Ordinal))
                {
                    return weapon;
                }
            }

            return null;
        }

        public static NavMeshSurface CreateLevel(Scene scene)
        {
            return CreateLevel(scene, Campaign01());
        }

        // LevelObjectName の子だけから焼くので、同時に開いている他のシーンの物は NavMesh に入らない。
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

        // 印の中心は床から厚さの半分だけ上がるので、再開位置のずれはその分を引き、プレイヤーの足が床に着く高さにする。
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

    public sealed class CampaignRules
    {
        public GameRulesDefinition Easy { get; }
        public GameRulesDefinition Normal { get; }
        public GameRulesDefinition Hard { get; }

        public CampaignRules(GameRulesDefinition easy, GameRulesDefinition normal, GameRulesDefinition hard)
        {
            Easy = easy != null ? easy : throw new ArgumentNullException(nameof(easy));
            Normal = normal != null ? normal : throw new ArgumentNullException(nameof(normal));
            Hard = hard != null ? hard : throw new ArgumentNullException(nameof(hard));
        }
    }

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

    public sealed class EnemyPlacement
    {
        public string Name { get; }
        public string Id { get; }

        public string WeaponId { get; }

        public Vector3 Position { get; }

        public Quaternion Rotation { get; }

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

        public static EnemyPlacement Patrol(string name, string id, string weaponId, Vector3 from, Vector3 to)
        {
            return new EnemyPlacement(name, id, weaponId, from, SceneBuildHelpers.FacingTowards(from, to), new[] { from, to });
        }

        public static EnemyPlacement Ambush(string name, string id, string weaponId, Vector3 position, Vector3 watch)
        {
            return new EnemyPlacement(
                name, id, weaponId, position, SceneBuildHelpers.FacingTowards(position, watch), Array.Empty<Vector3>());
        }
    }

    public sealed class CampaignLayout
    {
        public GameBootstrap Bootstrap { get; }
        public MissionRunner Runner { get; }
        public NavMeshSurface Surface { get; }

        public IReadOnlyList<ObjectiveAreaBase> Areas { get; }

        public IReadOnlyList<KillTargetObjective> KillObjectives { get; }

        public IReadOnlyList<ObjectiveReveal> Reveals { get; }

        public IReadOnlyList<EnemyAgent> Enemies { get; }

        public CampaignRunner Campaign { get; }

        public IReadOnlyList<CheckpointTrigger> Checkpoints { get; }

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
