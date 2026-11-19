using System;
using System.Collections.Generic;
using System.IO;
using Shooter.Runtime;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using LadderComponent = Shooter.Runtime.Traversal.Ladder;

namespace Shooter.Editor
{
    public static class EnemyTestSceneBuilder
    {
        public static string ScenePath => BuildSceneList.EnemyAiTestScene;

        // Unity の慣習どおり、焼いた NavMesh はシーン名のフォルダに置く。
        public static string NavMeshDataPath => ModeSceneBuilder.NavMeshDataPathFor(ScenePath);

        public const string MenuPath = SceneBuildSteps.MenuPath;
        public const string LogTag = "[EnemyTestSceneBuilder]";

        public const string FloorName = "Floor";
        public const string WallName = "Wall";
        public const string EnemyName = "Enemy-1";
        public const string EnemyId = "enemy-1";

        public const string EnemyWeaponId = WeaponAssetBuilder.RifleId;
        public const string BootstrapObjectName = "GameBootstrap";

        // Plane は 10m 四方なので、4 倍で 40m 四方にする。
        public static readonly Vector3 FloorScale = new Vector3(4f, 1f, 4f);

        public static readonly Vector3 WallCenter = new Vector3(0f, 1.5f, 8f);

        public static readonly Vector3 WallSize = new Vector3(18f, 3f, 0.5f);

        public const string LowCoverName = "LowCover";

        // 上端 (1.05m) を Shooter.Core.MotorSpec.DefaultCoverLowWallHeight 未満にして、しゃがんだ敵 (EnemyAgent.DefaultCrouchHeight) が隠れられるようにする。
        // プレイヤーの目 (1.6m) から巡回路の最後の地点は、この壁の上を越えて見える。
        public static readonly Vector3 LowCoverCenter = new Vector3(10f, 0.525f, 2f);

        public static readonly Vector3 LowCoverSize = new Vector3(0.5f, 1.05f, 3f);

        public const string CoverPointsObjectName = "CoverPoints";

        public const string LadderTowerName = "LadderTower";

        public static readonly Vector3 LadderTowerBaseCenter = new Vector3(-14f, 0f, -10f);

        public static readonly Vector3 LadderTowerSize = new Vector3(4f, 3f, 4f);

        public static readonly Vector3 LadderTowerSide = Vector3.right;

        public const float LadderTowerLadderWidth = 0.8f;

        public static Vector3 LadderTowerTop => LadderTowerBaseCenter + (Vector3.up * LadderTowerSize.y);

        // 壁の端の点で覗く位置 (x = ±10.8) は、原点のプレイヤーとの間を壁の厚みの角まで含めて遮られない所にする。
        public static readonly IReadOnlyList<CoverPointPlacement> CoverPoints = new[]
        {
            CoverPointPlacement.High("Cover-WallEast", new Vector3(8.3f, 0f, 8.95f), Vector3.back, new Vector3(2.5f, 0f, 0f)),
            CoverPointPlacement.High("Cover-WallWest", new Vector3(-8.3f, 0f, 8.95f), Vector3.back, new Vector3(-2.5f, 0f, 0f)),
            CoverPointPlacement.Low("Cover-LowEast", new Vector3(10.95f, 0f, 2f), Vector3.left),
            CoverPointPlacement.Low("Cover-LowWest", new Vector3(9.05f, 0f, 2f), Vector3.right),
        };

        public static readonly Vector3[] PatrolRoute =
        {
            new Vector3(-14f, 0f, 14f),
            new Vector3(14f, 0f, 14f),
            new Vector3(14f, 0f, 2f),
        };

        public const float EnemyHeight = 2f;
        public const float EnemyRadius = 0.5f;
        public const float EnemySpeed = 3.5f;
        public const int EnemyMaxHealth = 100;

        public const float NavMeshCheckRadius = 1f;

        public const string PickupsObjectName = "Pickups";
        public const string WeaponPickupName = "PickupSpawn-Weapon";
        public const string AmmoPickupName = "PickupSpawn-Ammo";
        public const string HealthPickupName = "PickupSpawn-Health";
        public const string InteractHealthPickupName = "PickupSpawn-Health-Interact";

        public const int WeaponPickupAmmoIfOwned = 2;

        public const int AmmoPickupAmount = 60;
        public const int HealthPickupAmount = 40;
        public const float PickupRespawnSeconds = 15f;

        public const int EnemyDropAmmoAmount = 30;

        public const int EnemyDropHealthAmount = 25;

        // 拾える物は、どれも出現位置から拾える範囲 (Pickup.DefaultRadius) の外に置く。
        public static readonly Vector3 WeaponPickupPosition = new Vector3(-6f, 0f, 3f);

        public static readonly Vector3 AmmoPickupPosition = new Vector3(-3f, 0f, 3f);
        public static readonly Vector3 HealthPickupPosition = new Vector3(3f, 0f, 3f);
        public static readonly Vector3 InteractHealthPickupPosition = new Vector3(-9f, 0f, 3f);

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

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // NewScene(Single) は参照されていないアセットをアンロードし、先に読んだ定義が null になるので、シーンを作ってから読む。
            // 武器のアセットが無い・敵の武器がカタログに無いなら何も保存せずに止める (先に WeaponAssetBuilder.Build を走らせる)。
            var playerWeapons = WeaponAssetBuilder.LoadPlayerLoadout();
            var catalog = WeaponAssetBuilder.LoadCatalog();
            WeaponAssetBuilder.FindInCatalog(catalog, EnemyId, EnemyWeaponId);
            var pickupWeapon = WeaponAssetBuilder.LoadLauncher();

            SceneBuildHelpers.EnsureFolder(Path.GetDirectoryName(scenePath));
            SceneBuildHelpers.EnsureFolder(Path.GetDirectoryName(navMeshDataPath));

            // 敵のコライダーで NavMesh に穴が空かないよう、地形だけを置いた状態で焼く。
            var surface = CreateLevel(scene);
            var tower = CreateLadderTower(scene);
            BakeNavMesh(surface, navMeshDataPath);
            SceneBuildHelpers.RequireOnNavMesh(PatrolRoute, NavMeshCheckRadius, "patrol point");
            RequireLadderReachable(tower);
            var coverStands = SceneBuildHelpers.CoverStandPoints(CoverPoints);
            SceneBuildHelpers.RequireOnNavMesh(coverStands, NavMeshCheckRadius, "cover point");
            SceneBuildHelpers.RequireReachable(PatrolRoute[0], coverStands, NavMeshCheckRadius, "cover point");
            CreateActors(scene, playerWeapons, catalog);
            CreatePickups(scene, pickupWeapon);

            if (!EditorSceneManager.SaveScene(scene, scenePath))
            {
                throw new IOException($"Failed to save the enemy AI test scene to '{scenePath}'");
            }

            BuildSceneList.Register(scenePath);
            AssetDatabase.SaveAssets();

            Debug.Log($"{LogTag} saved '{scenePath}' and '{navMeshDataPath}'");
        }

        public static NavMeshSurface CreateLevel(Scene scene)
        {
            SceneBuildHelpers.RequireEmpty(scene, "the level");

            var floor = SceneBuildHelpers.CreateStaticPlane(scene, null, FloorName, Vector3.zero, FloorScale);
            var surface = SceneBuildHelpers.AddNavMeshSurface(floor, CollectObjects.All);
            SceneBuildHelpers.CreateStaticBox(scene, null, WallName, WallCenter, WallSize);
            SceneBuildHelpers.CreateStaticBox(scene, null, LowCoverName, LowCoverCenter, LowCoverSize);
            SceneBuildHelpers.CreatePerimeterWalls(scene, null, Vector3.zero, SceneBuildHelpers.PlaneHalfExtents(FloorScale));

            EditorSceneManager.MarkSceneDirty(scene);
            return surface;
        }

        // 床の NavMeshSurface はシーンのすべてから焼くので、CreateLevel の後・焼く前に呼べば台の上にも NavMesh ができる。
        public static GameObject CreateLadderTower(Scene scene)
        {
            var tower = LadderTowerBuilder.Create(
                scene, LadderTowerName, LadderTowerBaseCenter, LadderTowerSize, LadderTowerSide, LadderTowerLadderWidth);
            EditorSceneManager.MarkSceneDirty(scene);
            return tower;
        }

        // はしごのリンクは実行時にはしごが張るので、確かめる間だけ張って外す。
        public static void RequireLadderReachable(GameObject tower)
        {
            if (tower == null)
            {
                throw new ArgumentNullException(nameof(tower));
            }

            var ladder = tower.GetComponentInChildren<LadderComponent>();
            if (ladder == null)
            {
                throw new InvalidOperationException($"'{tower.name}' has no ladder");
            }

            ladder.Link();
            try
            {
                SceneBuildHelpers.RequireReachable(PatrolRoute[0], new[] { LadderTowerTop }, NavMeshCheckRadius, "ladder tower top");
            }
            finally
            {
                ladder.Unlink();
            }
        }

        public static NavMeshData BakeNavMesh(NavMeshSurface surface, string assetPath)
        {
            if (surface == null)
            {
                throw new ArgumentNullException(nameof(surface));
            }

            assetPath = SceneBuildHelpers.ValidateAssetPath(assetPath);
            SceneBuildHelpers.BakeNavMesh(surface);
            return SceneBuildHelpers.SaveNavMeshData(surface, assetPath);
        }

        public static EnemyAgent CreateActors(Scene scene, IReadOnlyList<WeaponDefinition> playerWeapons, WeaponCatalogAsset enemyWeapons)
        {
            SceneBuildHelpers.RequireLoaded(scene);
            SceneBuildHelpers.RequirePlayerWeapons(playerWeapons, nameof(playerWeapons));

            if (enemyWeapons == null)
            {
                throw new ArgumentNullException(nameof(enemyWeapons));
            }

            var enemyWeapon = WeaponAssetBuilder.FindInCatalog(enemyWeapons, EnemyId, EnemyWeaponId);

            var bootstrapObject = new GameObject(BootstrapObjectName);
            SceneManager.MoveGameObjectToScene(bootstrapObject, scene);
            var bootstrap = bootstrapObject.AddComponent<GameBootstrap>();
            bootstrap.CreatesFloor = false;
            bootstrap.WeaponDefinitions = playerWeapons;

            var enemy = SceneBuildHelpers.CreateEnemy(
                scene,
                null,
                EnemyName,
                EnemyId,
                PatrolRoute[0],
                Quaternion.LookRotation(PatrolRoute[1] - PatrolRoute[0], Vector3.up),
                PatrolRoute,
                new EnemyBody(EnemyHeight, EnemyRadius, EnemySpeed, EnemyMaxHealth),
                enemyWeapon);
            enemy.CoverRegistry = SceneBuildHelpers.CreateCoverPoints(scene, null, CoverPointsObjectName, CoverPoints);

            var drop = enemy.gameObject.AddComponent<DropOnDeath>();
            drop.Drops = EnemyDrops();
            EditorSceneManager.MarkSceneDirty(scene);
            return enemy;
        }

        public static PickupContents[] EnemyDrops()
        {
            return new[]
            {
                PickupContents.ForReserveAmmo(EnemyDropAmmoAmount),
                PickupContents.ForHealth(EnemyDropHealthAmount),
            };
        }

        public static GameObject CreatePickups(Scene scene, WeaponDefinition weapon)
        {
            SceneBuildHelpers.RequireLoaded(scene);
            if (weapon == null)
            {
                throw new ArgumentNullException(nameof(weapon));
            }

            // 中身を先に作って検査し、不正なら何も置かない。
            var placements = new (string Name, Vector3 Position, PickupContents Contents, PickupCollectMode Mode)[]
            {
                (WeaponPickupName, WeaponPickupPosition, PickupContents.ForWeapon(weapon, WeaponPickupAmmoIfOwned), PickupCollectMode.Touch),
                (AmmoPickupName, AmmoPickupPosition, PickupContents.ForReserveAmmo(AmmoPickupAmount), PickupCollectMode.Touch),
                (HealthPickupName, HealthPickupPosition, PickupContents.ForHealth(HealthPickupAmount), PickupCollectMode.Touch),
                (InteractHealthPickupName, InteractHealthPickupPosition, PickupContents.ForHealth(HealthPickupAmount), PickupCollectMode.Interact),
            };

            var root = new GameObject(PickupsObjectName);
            SceneManager.MoveGameObjectToScene(root, scene);
            foreach (var placement in placements)
            {
                var pointObject = new GameObject(placement.Name);
                pointObject.transform.SetParent(root.transform, false);
                pointObject.transform.position = placement.Position;
                var point = pointObject.AddComponent<PickupSpawnPoint>();
                point.Contents = placement.Contents;
                point.CollectMode = placement.Mode;
                point.RespawnSeconds = PickupRespawnSeconds;
            }

            EditorSceneManager.MarkSceneDirty(scene);
            return root;
        }

        public static EditorBuildSettingsScene[] WithSceneAppended(IReadOnlyList<EditorBuildSettingsScene> current, string path)
        {
            return BuildSceneList.WithScene(current, path);
        }
    }
}
