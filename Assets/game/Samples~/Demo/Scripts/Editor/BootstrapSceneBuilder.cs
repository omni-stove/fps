using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Shooter.Core;
using Shooter.Runtime;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Shooter.Editor
{
    public static class BootstrapSceneBuilder
    {
        public static string DefaultScenePath => BuildSceneList.BootstrapScene;
        public const string ScenePathArgument = "-bootstrapScenePath";
        public const string BootstrapObjectName = "GameBootstrap";
        public const string MenuPath = SceneBuildSteps.MenuPath;

        public const string CoversName = "Covers";

        public const string LowCoverName = "Cover_Low";
        public const string HighCoverName = "Cover_High";

        // MotorSpec.DefaultCoverLowWallHeight 未満にして、しゃがんで隠れ、立って覗き、上からブラインドファイアできるようにする。
        public const float LowCoverHeight = 1f;

        // MotorSpec.DefaultCoverLowWallHeight 以上で目 (1.6m) より高くして、立ったまま隠れ、端から覗けるようにする。
        public const float HighCoverHeight = 2f;

        public const float CoverThickness = 0.4f;

        public const float CoverDistance = GameBootstrap.TargetDummyDistance / 2f;

        // 真ん中の標的への射線を空けて左右に 1 つずつ置き、出現位置からの射線にかかるのは、左の低い遮蔽物が左端の標的への射線の低い所だけにする。
        public static readonly IReadOnlyList<LevelBox> Covers = new[]
        {
            new LevelBox(LowCoverName, new Vector3(-3f, LowCoverHeight / 2f, CoverDistance), new Vector3(3f, LowCoverHeight, CoverThickness)),
            new LevelBox(HighCoverName, new Vector3(3f, HighCoverHeight / 2f, CoverDistance), new Vector3(1.5f, HighCoverHeight, CoverThickness)),
        };

        public static LevelBox LowCover => Covers[0];

        public static LevelBox HighCover => Covers[1];

        // 着弾の演出を材質ごとに見比べられるよう、低い壁と高い壁で面の材質を変える。
        public static readonly IReadOnlyDictionary<string, string> CoverSurfaceIds = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [LowCoverName] = SurfaceImpactAssetStep.ConcreteId,
            [HighCoverName] = SurfaceImpactAssetStep.MetalId,
        };

        public const string WeaponTableName = "WeaponTable";

        // サンプルを全部 (Reference と Realistic で 64) 並べると出現位置の前が埋まるので、それぞれ武器種ごとに 1 つだけにし、同じ武器種の順にして前後の列で撃ち比べられるようにする。
        public static readonly IReadOnlyList<string> FeaturedReferenceWeaponIds = new[]
        {
            "25974cbc-c194-4f98-b22d-c9d34c3ba6e4",
            "6b1ce288-952f-4ead-b23b-337df82d3a02",
            "7f8258fd-a401-403b-93d2-60a22a69cb0b",
            "e250274d-82be-401c-a098-2ee06295b582",
            "07895118-2fb7-4b50-aa16-8f8afef0fc79",
            "7875b62b-86b5-4af7-bc20-27f643d3c4fe",
            "343b6024-75f2-4d68-8ac6-d31fad86aa47",
            "b9daae5b-9015-46cf-b104-6f9a9ba9c623",
        };

        public static readonly IReadOnlyList<string> FeaturedRealisticWeaponIds = new[]
        {
            "43c9c224-dfea-463d-a411-fd0f6c37824d",
            "2f3825b8-dac2-4744-8025-d2909cbb57d4",
            "05e08bbf-1757-464a-a142-8dcdd0c86ba9",
            "aa7e7cf5-6840-4b20-b0ec-ae5d1dfb9c5c",
            "c1f6ce08-79a8-4d20-a16a-6eb6cee0256d",
            "a21e791d-cf45-4f05-981d-17480c5a791b",
            "c5171e80-7b9c-44f0-bdd5-3e4fdd67f585",
            "148acbfc-9d35-4c65-96fc-36bea468eaa4",
        };

        public static IReadOnlyList<string> FeaturedWeaponIds => FeaturedReferenceWeaponIds.Concat(FeaturedRealisticWeaponIds).ToArray();

        public static readonly IReadOnlyList<string> PlayerWeaponIds = new[]
        {
            "25974cbc-c194-4f98-b22d-c9d34c3ba6e4",
            "343b6024-75f2-4d68-8ac6-d31fad86aa47",
        };

        // 出現位置で拾える範囲 (RangeWeaponRackFixture.PickupRadius) に入らず、遮蔽物より手前に置く。
        public const float WeaponTableDistance = 2.5f;

        public const float WeaponTableSpacing = 2f;

        public const int WeaponTableRowLength = 8;

        // 後ろの列も遮蔽物 (CoverDistance) より手前に収めるため、横の間隔より詰める。
        public const float WeaponTableRowSpacing = 1.5f;

        public const string PlatformsName = "Platforms";

        public const string ShuttleName = "Platform_Shuttle";

        public const string ElevatorName = "Platform_Elevator";

        public const string ElevatorTowerName = "Elevator_Tower";

        public const string ElevatorCallBottomName = "Elevator_Call_Bottom";

        public const string ElevatorCallTopName = "Elevator_Call_Top";

        public const int ElevatorBottomStop = 0;

        public const int ElevatorTopStop = 1;

        public static string PlatformFolder => ShooterKitRoot.OutputPath("Platforms");

        public static string ElevatorSettingsPath => PlatformFolder + "/RangeElevator.asset";

        public const float ElevatorSpeed = 2f;

        public const float ElevatorWaitSeconds = 1f;

        public static readonly Vector3 PlatformSlabSize = MovingPlatform.DefaultSlabSize;

        // CharacterController の既定の段差 (0.3m) より低くして、床から歩いて乗れるようにする。
        public const float PlatformTopHeight = 0.25f;

        public const float ElevatorRise = 4f;

        // 遮蔽物・武器の台・標的 (x が ±7m の内) の外の左右に置き、射撃の邪魔をしない。
        public static readonly Vector3 ShuttleOrigin = new Vector3(-12f, PlatformTopHeight - (PlatformSlabSize.y / 2f), 0f);

        public static readonly IReadOnlyList<Vector3> ShuttleStops = new[] { Vector3.zero, new Vector3(0f, 0f, 8f) };

        public static readonly Vector3 ElevatorOrigin = new Vector3(12f, PlatformTopHeight - (PlatformSlabSize.y / 2f), 0f);

        public static readonly IReadOnlyList<Vector3> ElevatorStops = new[] { Vector3.zero, new Vector3(0f, ElevatorRise, 0f) };

        // 上の点に着いた板の上面と塔の上面を揃え、段差なしで降りられるようにする。
        public static LevelBox ElevatorTower
        {
            get
            {
                var height = PlatformTopHeight + ElevatorRise;
                var size = new Vector3(4f, height, 4f);
                var x = ElevatorOrigin.x + (PlatformSlabSize.x / 2f) + (size.x / 2f);
                return new LevelBox(ElevatorTowerName, new Vector3(x, height / 2f, ElevatorOrigin.z), size);
            }
        }

        // 板の真上でなく手前に置き、乗ったときでなく乗り場に来たときに呼ぶ。
        public static LevelBox ElevatorCallBottom =>
            new LevelBox(
                ElevatorCallBottomName,
                new Vector3(ElevatorOrigin.x - (PlatformSlabSize.x / 2f) - 1.5f, 1f, ElevatorOrigin.z),
                new Vector3(2f, 2f, PlatformSlabSize.z));

        public static LevelBox ElevatorCallTop
        {
            get
            {
                var tower = ElevatorTower;
                var top = tower.Center.y + (tower.Size.y / 2f);
                return new LevelBox(ElevatorCallTopName, new Vector3(tower.Center.x, top + 1f, tower.Center.z), new Vector3(2f, 2f, PlatformSlabSize.z));
            }
        }

        public const string LockOnPenName = "LockOnPen";

        public const string LockOnTurretName = "LockOnTurret_Ground";

        public const string LockOnPenNorthName = "LockOnPen_North";

        public const string LockOnPenSouthName = "LockOnPen_South";

        public const string LockOnPenWestName = "LockOnPen_West";

        public const string LockOnPenEastNorthName = "LockOnPen_East_North";

        public const string LockOnPenEastSouthName = "LockOnPen_East_South";

        // 奥の左の角に置き、遮蔽物・武器の台・標的・往復する床・エレベーターのどれとも重ならないようにする。
        public static readonly Vector3 LockOnPenCenter = new Vector3(-19f, 0f, 19f);

        public const float LockOnPenHalfExtent = 3f;

        // 目 (1.6m) より高くして、囲いの外からは入り口を通してしか見通せないようにする。
        public const float LockOnPenWallHeight = 3f;

        public const float LockOnPenWallThickness = 0.4f;

        // 狭くして、入り口から見通せる範囲を射撃場の奥だけに絞る。広げると標的の列や出現位置の右手まで見える。
        public const float LockOnPenDoorWidth = 1.5f;

        // 0 にして、地上を歩くプレイヤーも捉える。キットの既定 (5m) は空にいる相手だけを狙う。
        public const float LockOnTurretMinTargetHeight = 0f;

        // 頭は入り口 (東の壁) へ向けておき、入り口から覗いた相手をすぐ捉えられるようにする。
        public static Quaternion LockOnTurretHeadRotation => Quaternion.LookRotation(Vector3.right, Vector3.up);

        // 標的の人形を捉えて頭をそちらへ向け続けないよう、プレイヤーのタグの体だけを狙わせる。
        public static LockOnTurretSpec GroundTurretSpec =>
            new LockOnTurretSpec(LockOnTurretSpec.DefaultTurnDegreesPerSecond, LockOnTurretMinTargetHeight, GameBootstrap.PlayerTag);

        // 入り口は東 (射撃場の奥、標的の後ろ) へ開ける。出現位置や射撃の列から見えると、射撃場にいる間ずっと警告が出るため。
        public static IReadOnlyList<LevelBox> LockOnPenWalls
        {
            get
            {
                var c = LockOnPenCenter;
                var h = LockOnPenHalfExtent;
                var t = LockOnPenWallThickness;
                var y = LockOnPenWallHeight / 2f;
                var side = h - t - (LockOnPenDoorWidth / 2f);
                var sideOffset = (LockOnPenDoorWidth / 2f) + (side / 2f);
                return new[]
                {
                    new LevelBox(LockOnPenNorthName, new Vector3(c.x, y, c.z + h - (t / 2f)), new Vector3(2f * h, LockOnPenWallHeight, t)),
                    new LevelBox(LockOnPenSouthName, new Vector3(c.x, y, c.z - h + (t / 2f)), new Vector3(2f * h, LockOnPenWallHeight, t)),
                    new LevelBox(LockOnPenWestName, new Vector3(c.x - h + (t / 2f), y, c.z), new Vector3(t, LockOnPenWallHeight, (2f * h) - (2f * t))),
                    new LevelBox(LockOnPenEastNorthName, new Vector3(c.x + h - (t / 2f), y, c.z + sideOffset), new Vector3(t, LockOnPenWallHeight, side)),
                    new LevelBox(LockOnPenEastSouthName, new Vector3(c.x + h - (t / 2f), y, c.z - sideOffset), new Vector3(t, LockOnPenWallHeight, side)),
                };
            }
        }

        public static void Build()
        {
            // メニューから呼ばれたとき、開いているシーンの未保存の変更を黙って捨てない。
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                Debug.Log("[BootstrapSceneBuilder] cancelled");
                return;
            }

            var path = ResolveScenePath(Environment.GetCommandLineArgs());
            BuildAt(path);
        }

        public static void BuildAt(string path)
        {
            path = ValidateScenePath(path);

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // NewScene(Single) は参照されていないアセットをアンロードし、先に読んだ定義が null になるので、シーンを作ってから読む。
            // 武器のアセットが無ければ何も保存せずに止める (先に WeaponAssetBuilder.Build を走らせる)。
            // 射撃場のルールのアセットも同じく、無ければ止める (先に GameRulesAssetBuilder.Build を走らせる)。
            var reference = WeaponAssetBuilder.LoadReferenceWeapons();
            var realistic = WeaponAssetBuilder.LoadRealisticWeapons();
            var weapons = LoadPlayerWeapons(reference);
            var rules = GameRulesAssetBuilder.LoadRules(GameRulesAssetBuilder.RangePath);
            var featured = LoadFeaturedWeapons(reference, realistic);
            // 名前でなく弾の値で選ぶのは、ゲームが武器を差し替えても、捕捉できる武器ならタレットに付けられるようにするため。
            var lockOnWeapon = VehicleRangeSceneBuilder.PickLockOnWeapon(WeaponAssetBuilder.LoadRangeWeapons().Weapons)
                ?? throw new InvalidOperationException(
                    $"[BootstrapSceneBuilder] no range weapon has a homing round for the lock-on turret; add one to '{WeaponAssetBuilder.RangeWeaponsPath}'");
            var elevator = BuildElevatorSettings();

            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            PopulateScene(scene, weapons, rules, featured, elevator, lockOnWeapon);

            if (!EditorSceneManager.SaveScene(scene, path))
            {
                throw new IOException($"Failed to save the bootstrap scene to '{path}'");
            }

            // 起動時のシーンはメインメニュー (MenuSceneBuilder が先頭に置く) なので、射撃場は並びを変えずに加える
            BuildSceneList.Register(path);
            AssetDatabase.SaveAssets();

            Debug.Log($"[BootstrapSceneBuilder] saved '{path}' and registered it in Build Settings");
        }

        public static GameObject PopulateScene(Scene scene, IReadOnlyList<WeaponDefinition> weapons, GameRulesDefinition rules)
        {
            return PopulateScene(scene, weapons, rules, Array.Empty<WeaponDefinition>());
        }

        public static GameObject PopulateScene(
            Scene scene, IReadOnlyList<WeaponDefinition> weapons, GameRulesDefinition rules, IReadOnlyList<WeaponDefinition> featured)
        {
            if (featured == null)
            {
                throw new ArgumentNullException(nameof(featured));
            }

            if (!scene.IsValid() || !scene.isLoaded)
            {
                throw new ArgumentException("scene must be a valid, loaded scene", nameof(scene));
            }

            SceneBuildHelpers.RequirePlayerWeapons(weapons, nameof(weapons));
            if (rules == null)
            {
                throw new ArgumentNullException(nameof(rules));
            }

            if (scene.rootCount != 0)
            {
                throw new InvalidOperationException(
                    $"scene '{scene.name}' already has {scene.rootCount} root object(s); the bootstrap scene must start empty");
            }

            // MatchRunner と TutorialPresenter は Start で同じ GameObject の GameBootstrap が Awake で作ったものを拾うので、ここでの配線は要らない。
            var bootstrap = new GameObject(BootstrapObjectName, typeof(GameBootstrap), typeof(MatchRunner), typeof(TutorialPresenter));
            var component = bootstrap.GetComponent<GameBootstrap>();
            component.WeaponDefinitions = weapons;
            component.RulesDefinition = rules;
            SceneManager.MoveGameObjectToScene(bootstrap, scene);
            CreateCovers(scene);
            if (featured.Count > 0)
            {
                CreateWeaponTable(scene, featured);
            }

            CreatePerimeter(scene);
            EditorSceneManager.MarkSceneDirty(scene);
            return bootstrap;
        }

        // 動く床とエレベーターを足す形を分けるのは、エレベーターの設定がアセットで、それを作らずに場を組むテストや他の場の組み立てを変えないため。
        public static GameObject PopulateScene(
            Scene scene,
            IReadOnlyList<WeaponDefinition> weapons,
            GameRulesDefinition rules,
            IReadOnlyList<WeaponDefinition> featured,
            MovingPlatformSettings elevatorSettings)
        {
            RequireElevatorSettings(elevatorSettings);
            var bootstrap = PopulateScene(scene, weapons, rules, featured);
            CreatePlatforms(scene, elevatorSettings);
            EditorSceneManager.MarkSceneDirty(scene);
            return bootstrap;
        }

        // 見張りのタレットを足す形を分けるのは、捕捉の武器が射撃場の武器のアセット (RangeWeapons) から来て、それを作らずに場を組むテストや他の場の組み立てを変えないため。
        public static GameObject PopulateScene(
            Scene scene,
            IReadOnlyList<WeaponDefinition> weapons,
            GameRulesDefinition rules,
            IReadOnlyList<WeaponDefinition> featured,
            MovingPlatformSettings elevatorSettings,
            WeaponDefinition lockOnWeapon)
        {
            RequireLockOnWeapon(lockOnWeapon);
            var bootstrap = PopulateScene(scene, weapons, rules, featured, elevatorSettings);
            CreateLockOnPen(scene, lockOnWeapon);
            EditorSceneManager.MarkSceneDirty(scene);
            return bootstrap;
        }

        // タレットは警告を確かめるための見張りで、撃たない。捕捉の進み方は武器の Homing の値を使う。
        public static GameObject CreateLockOnPen(Scene scene, WeaponDefinition lockOnWeapon)
        {
            SceneBuildHelpers.RequireLoaded(scene);
            RequireLockOnWeapon(lockOnWeapon);

            var root = new GameObject(LockOnPenName);
            SceneManager.MoveGameObjectToScene(root, scene);
            try
            {
                foreach (var wall in LockOnPenWalls)
                {
                    var box = SceneBuildHelpers.CreateStaticBox(scene, root.transform, wall.Name, wall.Center, wall.Size);
                    SceneBuildHelpers.SetSurfaceMaterial(box, SurfaceImpactAssetStep.ConcreteId);
                }

                VehicleRangeSceneBuilder.CreateLockOnTurret(
                    scene, root.transform, LockOnTurretName, LockOnPenCenter, LockOnTurretHeadRotation, lockOnWeapon, GroundTurretSpec);
            }
            catch
            {
                UnityEngine.Object.DestroyImmediate(root);
                throw;
            }

            return root;
        }

        private static void RequireLockOnWeapon(WeaponDefinition weapon)
        {
            if (weapon == null)
            {
                throw new ArgumentNullException(nameof(weapon));
            }

            // 値が不正なら ToSpec が ArgumentException を投げる。
            if (weapon.ToSpec().Round.Homing == null)
            {
                throw new ArgumentException($"weapon '{weapon.Id}' has no homing round, so the turret could never lock on", nameof(weapon));
            }
        }

        public static GameObject CreatePlatforms(Scene scene, MovingPlatformSettings elevatorSettings)
        {
            SceneBuildHelpers.RequireLoaded(scene);
            RequireElevatorSettings(elevatorSettings);

            var root = new GameObject(PlatformsName);
            SceneManager.MoveGameObjectToScene(root, scene);

            // 往復する床は設定を持たせず、キットの既定の動き (往復) を使う。
            CreatePlatform(root.transform, ShuttleName, ShuttleOrigin, null, ShuttleStops);
            var elevator = CreatePlatform(root.transform, ElevatorName, ElevatorOrigin, elevatorSettings, ElevatorStops);

            var tower = ElevatorTower;
            var towerBox = SceneBuildHelpers.CreateStaticBox(scene, root.transform, tower.Name, tower.Center, tower.Size);
            SceneBuildHelpers.SetSurfaceMaterial(towerBox, SurfaceImpactAssetStep.ConcreteId);

            CreateCallZone(root.transform, ElevatorCallBottom, elevator, ElevatorBottomStop);
            CreateCallZone(root.transform, ElevatorCallTop, elevator, ElevatorTopStop);
            return root;
        }

        // 既存のアセットは GUID を保って中身だけ更新するので、場が参照したまま値を直せる。
        public static MovingPlatformSettings BuildElevatorSettings()
        {
            return SceneBuildHelpers.CreateOrUpdateAsset<MovingPlatformSettings>(ElevatorSettingsPath, ConfigureElevatorSettings);
        }

        public static void ConfigureElevatorSettings(MovingPlatformSettings settings)
        {
            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            settings.Kind = PlatformRouteKind.OnCall;
            settings.Speed = ElevatorSpeed;
            settings.WaitSeconds = ElevatorWaitSeconds;
            settings.SpinDegreesPerSecond = 0f;
            settings.DepartWhenBoarded = true;
        }

        private static void RequireElevatorSettings(MovingPlatformSettings settings)
        {
            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            // 値が不正なら ToSpec が ArgumentOutOfRangeException を投げる。
            if (settings.ToSpec().Kind != PlatformRouteKind.OnCall)
            {
                throw new ArgumentException($"the elevator settings '{settings.name}' must be {PlatformRouteKind.OnCall}", nameof(settings));
            }
        }

        private static MovingPlatform CreatePlatform(
            Transform parent, string name, Vector3 origin, MovingPlatformSettings settings, IReadOnlyList<Vector3> stops)
        {
            var body = new GameObject(name);
            body.transform.SetParent(parent, false);
            body.transform.position = origin;

            // 板をここで作るのは、MovingPlatform が実行時に作ると Play のたびに既定の板を作った警告が出るため。
            var slab = GameObject.CreatePrimitive(PrimitiveType.Cube);
            slab.name = MovingPlatform.SlabName;
            slab.transform.SetParent(body.transform, false);
            slab.transform.localScale = PlatformSlabSize;
            SceneBuildHelpers.SetSurfaceMaterial(slab, SurfaceImpactAssetStep.MetalId);

            var platform = body.AddComponent<MovingPlatform>();
            platform.SlabSize = PlatformSlabSize;
            platform.Configure(settings, stops);
            return platform;
        }

        private static PlatformCallZone CreateCallZone(Transform parent, LevelBox box, MovingPlatform platform, int stop)
        {
            var zoneObject = new GameObject(box.Name);
            zoneObject.transform.SetParent(parent, false);
            zoneObject.transform.position = box.Center;
            var zone = zoneObject.AddComponent<PlatformCallZone>();
            zone.Platform = platform;
            zone.Stop = stop;
            zone.Size = box.Size;
            zone.EnsureTrigger();
            return zone;
        }

        // 床は GameBootstrap が実行時に作るので、床の物でなく、実行時の床 (原点に置く GameBootstrap.FloorScale の Plane) の縁から囲む範囲を決める。
        public static Vector2 PerimeterHalfExtents => SceneBuildHelpers.PlaneHalfExtents(GameBootstrap.FloorScale);

        public static GameObject CreatePerimeter(Scene scene)
        {
            SceneBuildHelpers.RequireLoaded(scene);
            return SceneBuildHelpers.CreatePerimeterWalls(scene, null, Vector3.zero, PerimeterHalfExtents);
        }

        // 床は GameBootstrap が実行時に作るので、遮蔽物は床の上 (y = 0 から) に立つ大きさと位置にしてある。
        public static GameObject CreateCovers(Scene scene)
        {
            SceneBuildHelpers.RequireLoaded(scene);

            var root = new GameObject(CoversName);
            SceneManager.MoveGameObjectToScene(root, scene);
            foreach (var cover in Covers)
            {
                var box = SceneBuildHelpers.CreateStaticBox(scene, root.transform, cover.Name, cover.Center, cover.Size);
                SceneBuildHelpers.SetSurfaceMaterial(box, SceneBuildHelpers.SurfaceIdOf(CoverSurfaceIds, cover.Name));
            }

            return root;
        }

        public static WeaponDefinition[] LoadPlayerWeapons(WeaponCatalogAsset reference)
        {
            return FindAll(reference, PlayerWeaponIds);
        }

        public static WeaponDefinition[] LoadFeaturedWeapons(WeaponCatalogAsset reference, WeaponCatalogAsset realistic)
        {
            return FindAll(reference, FeaturedReferenceWeaponIds).Concat(FindAll(realistic, FeaturedRealisticWeaponIds)).ToArray();
        }

        private static WeaponDefinition[] FindAll(WeaponCatalogAsset catalog, IReadOnlyList<string> ids)
        {
            var weapons = new WeaponDefinition[ids.Count];
            for (var i = 0; i < weapons.Length; i++)
            {
                weapons[i] = WeaponAssetBuilder.FindInCatalog(catalog, WeaponTableName, ids[i]);
            }

            return weapons;
        }

        public static Vector3[] WeaponTablePositions(int count)
        {
            var feet = GameBootstrap.PlayerSpawn + (Vector3.down * (GameBootstrap.PlayerHeight / 2f));
            var positions = new Vector3[count];
            for (var start = 0; start < count; start += WeaponTableRowLength)
            {
                var row = start / WeaponTableRowLength;
                var offsets = LineLayout.CenteredOffsets(Math.Min(WeaponTableRowLength, count - start), WeaponTableSpacing);
                for (var i = 0; i < offsets.Length; i++)
                {
                    positions[start + i] = feet + new Vector3(offsets[i], 0f, WeaponTableDistance + (row * WeaponTableRowSpacing));
                }
            }

            return positions;
        }

        public static GameObject CreateWeaponTable(Scene scene, IReadOnlyList<WeaponDefinition> weapons)
        {
            SceneBuildHelpers.RequireLoaded(scene);
            if (weapons == null)
            {
                throw new ArgumentNullException(nameof(weapons));
            }

            // 中身を先に作って検査し、不正なら何も置かない。
            var contents = new PickupContents[weapons.Count];
            for (var i = 0; i < weapons.Count; i++)
            {
                if (weapons[i] == null)
                {
                    throw new ArgumentException($"featured weapon #{i} is empty", nameof(weapons));
                }

                contents[i] = PickupContents.ForWeapon(weapons[i], weapons[i].ToSpec().MagazineSize);
            }

            var root = new GameObject(WeaponTableName);
            SceneManager.MoveGameObjectToScene(root, scene);
            var positions = WeaponTablePositions(weapons.Count);
            for (var i = 0; i < weapons.Count; i++)
            {
                RangeWeaponRackFixture.CreatePickup(root.transform, weapons[i].name, contents[i], positions[i]);
            }

            return root;
        }

        public static EditorBuildSettingsScene[] WithSceneFirst(IReadOnlyList<EditorBuildSettingsScene> current, string path)
        {
            return BuildSceneList.WithStartScene(current, path);
        }

        public static string ResolveScenePath(IReadOnlyList<string> args)
        {
            if (args == null)
            {
                return DefaultScenePath;
            }

            string found = null;
            for (var i = 0; i < args.Count; i++)
            {
                if (!string.Equals(args[i], ScenePathArgument, StringComparison.Ordinal))
                {
                    continue;
                }

                if (i + 1 >= args.Count || string.IsNullOrWhiteSpace(args[i + 1]) || args[i + 1].StartsWith("-", StringComparison.Ordinal))
                {
                    throw new ArgumentException($"{ScenePathArgument} needs a path such as {DefaultScenePath}", nameof(args));
                }

                if (found != null)
                {
                    throw new ArgumentException($"{ScenePathArgument} is given more than once", nameof(args));
                }

                found = args[i + 1];
                i++;
            }

            return found == null ? DefaultScenePath : ValidateScenePath(found);
        }

        public static string ValidateScenePath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                throw new ArgumentException("scene path must not be empty", nameof(path));
            }

            var normalized = path.Trim().Replace('\\', '/');
            if (!normalized.StartsWith("Assets/", StringComparison.Ordinal))
            {
                throw new ArgumentException($"scene path '{path}' must be under Assets/", nameof(path));
            }

            if (!normalized.EndsWith(".unity", StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException($"scene path '{path}' must end with .unity", nameof(path));
            }

            foreach (var segment in normalized.Split('/'))
            {
                if (segment.Length == 0 || segment == "." || segment == "..")
                {
                    throw new ArgumentException($"scene path '{path}' must not contain empty, '.' or '..' segments", nameof(path));
                }
            }

            return normalized;
        }
    }
}
