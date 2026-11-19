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
using WaterVolume = Shooter.Runtime.Traversal.Water;

namespace Shooter.Editor
{
    // 敵は smoke の撃つ所から銃声の届く半径 (どの武器も 50m 以下) より遠くに置き、smoke の間に寄って来ないようにする。
    public static class VehicleRangeSceneBuilder
    {
        public static string ScenePath => BuildSceneList.VehicleRangeScene;

        public static string NavMeshDataPath => ModeSceneBuilder.NavMeshDataPathFor(ScenePath);

        public const string MenuPath = "Shooter/Build Vehicle Range Scene";
        public const string LogTag = "[VehicleRangeSceneBuilder]";

        public const string BootstrapObjectName = "GameBootstrap";
        public const string FloorName = GameBootstrap.FloorName;
        public const string CourseName = "Course";
        public const string SlopeUpName = "Slope_Up";
        public const string PlateauName = "Plateau";
        public const string SlopeDownName = "Slope_Down";
        public const string ObstaclesName = "Obstacles";
        public const string PillarNamePrefix = "Pillar_";
        public const string TargetsName = "TurretTargets";
        public const string TargetIdPrefix = "VehicleTarget";
        public const string GunCarName = "GunCar";
        public const string CarName = CarFixtureBuilder.DefaultName;
        public const string MountedGunName = MountedGunFixtureBuilder.DefaultName;
        public const string AircraftName = AircraftFixtureBuilder.DefaultName;
        public const string EnemiesName = "Enemies";
        public const string EnemyName = "Enemy";
        public const string EnemyId = "VehicleRangeEnemy";
        public const string OuterGroundName = "OuterGround";
        public const string PoolName = "Pool";
        public const string WaterName = "Water";
        public const string WaterSurfaceName = "WaterSurface";
        public const string HelicopterName = VehicleFixtureBuilder.HelicopterName;
        public const string BoatName = VehicleFixtureBuilder.BoatName;
        public const string BotCarName = "BotCar";
        public const string BotCarVehicleId = "bot-car";
        public const string BotCarRouteName = "BotCarRoute";
        public const string BotAircraftName = "BotAircraft";
        public const string BotAircraftVehicleId = "bot-aircraft";
        public const string BotAircraftWaypointsName = "BotAircraftWaypoints";
        public const string LockOnTurretName = "LockOnTurret";
        public const string LockOnTurretHeadName = "Head";
        public const string LockOnWarningName = "LockOnWarning";

        // 飛行機は数秒で床の外へ出るので、空中で脱出してパラシュートで降りる地面と、Bot の飛行機が走り出す地面を床の外まで広げる。
        // 床と重ねても描画がちらつかず、車輪が段を感じない程度だけ下げる。
        public static readonly Vector3 OuterGroundScale = new Vector3(400f, 1f, 400f);
        public const float OuterGroundDrop = 0.02f;

        public static float OuterGroundHalfExtent => OuterGroundScale.x * 10f / 2f;

        // 水場は銃座・車・的・コースから離れた南東に置く。底を床に置き、水面までの深さは船の底が床に着かない分だけ取る。
        public static readonly Vector3 PoolGround = new Vector3(45f, 0f, -45f);
        public static readonly Vector3 PoolSize = new Vector3(24f, 2f, 40f);
        public const float PoolSurfaceHeight = 1.2f;

        // 水場の南の端から北へ進ませ、smoke が 10m ほど進めても水の中に収まる所。
        public static readonly Vector3 BoatGround = new Vector3(45f, 0f, -58f);
        public static readonly Quaternion BoatRotation = Quaternion.LookRotation(Vector3.forward, Vector3.up);

        // 滑走路の西で、Bot の車の道から離れた北西の角。西へ進ませれば smoke の 10m で床の外へ出ない。
        public static readonly Vector3 HelicopterOrigin = new Vector3(-50f, 0f, 58f);
        public static readonly Quaternion HelicopterRotation = Quaternion.LookRotation(Vector3.left, Vector3.up);

        // 滑走路 (x=-30 で翼の端まで ±4.5m) と銃座・車の間を空けた西の帯を、縦長に回る道。
        public static readonly Vector3[] BotCarRoutePoints =
        {
            new Vector3(-60f, 0f, -30f),
            new Vector3(-60f, 0f, 30f),
            new Vector3(-45f, 0f, 30f),
            new Vector3(-45f, 0f, -30f),
        };

        // 道の最後の辺の途中から、最初の点へ向いて走り出す。
        public static readonly Vector3 BotCarOrigin = new Vector3(-52f, 0f, -30f);
        public static readonly Quaternion BotCarRotation = Quaternion.LookRotation(Vector3.left, Vector3.up);

        // 外周の地面で滑走して飛び上がる。最初の点のほうを向けて置き、地上で曲がらずに済むようにする。
        public static readonly Vector3 BotAircraftOrigin = new Vector3(-110f, 0f, -60f);
        public static readonly Quaternion BotAircraftRotation = Quaternion.LookRotation(Vector3.forward, Vector3.up);

        // タレットの射程 (捕捉に使う武器の射程、サンプルでは 150m) より高く回し、smoke で捉えさせるプレイヤーの飛行機と取り違えないようにする。
        // 点どうしは傾け切って回る輪の直径 (巡航の速さで 600m ほど) より離す (BotAircraftPilot の点の注意書き)。
        public const float BotAircraftAltitude = 160f;

        public static readonly Vector3[] BotAircraftWaypoints =
        {
            new Vector3(-150f, BotAircraftAltitude, 500f),
            new Vector3(450f, BotAircraftAltitude, 500f),
            new Vector3(450f, BotAircraftAltitude, -500f),
            new Vector3(-150f, BotAircraftAltitude, -500f),
        };

        // 滑走路の北の端の東。離陸して上り始めた飛行機が射程に入り、捕捉の円錐に収まる程度に離す。
        public static readonly Vector3 LockOnTurretOrigin = new Vector3(-5f, 0f, 50f);
        public static readonly Vector3 LockOnTurretBaseSize = new Vector3(1.5f, 1.2f, 1.5f);
        public static readonly Vector3 LockOnTurretHeadLocal = new Vector3(0f, 1.6f, 0f);
        public static readonly Vector3 LockOnTurretBarrelSize = new Vector3(0.35f, 0.35f, 1.6f);

        public static readonly Vector3 FloorScale = new Vector3(14f, 1f, 14f);
        public static readonly float FloorHalfExtent = FloorScale.x * 10f / 2f;

        // 運転席の乗る位置が出現位置のそばに来る所。車体はプレイヤーのカプセル (半径 0.5m) に触れない。
        public static readonly Vector3 GunCarOrigin = new Vector3(0f, 0f, -1.8f);
        public static readonly Quaternion GunCarRotation = Quaternion.LookRotation(Vector3.right, Vector3.up);

        public const float CourseWidth = 6f;

        // 車は 10m ほど走ってからブレーキで 20m より手前に止まるので、坂はそれより十分先から始める。
        public const float SlopeStartX = 30f;
        public const float SlopeAngleDegrees = 10f;
        public const float SlopeLength = 12f;
        public const float SlopeThickness = 0.5f;
        public const float PlateauLength = 8f;

        public static float SlopeRise => SlopeLength * Mathf.Sin(SlopeAngleDegrees * Mathf.Deg2Rad);

        public static float SlopeRun => SlopeLength * Mathf.Cos(SlopeAngleDegrees * Mathf.Deg2Rad);

        public static float PlateauStartX => SlopeStartX + SlopeRun;

        public static float SlopeDownStartX => PlateauStartX + PlateauLength;

        public static float CourseEndX => SlopeDownStartX + SlopeRun;

        public const float ObstacleLaneZ = 18f;
        public const float PillarOffsetZ = 2f;
        public static readonly Vector3 PillarSize = new Vector3(1f, 1.5f, 1f);
        public static readonly float[] PillarXs = { 10f, 16f, 22f, 28f, 34f };

        // 車は 10m 先から 20m 先の間に止まるので、どこで止まっても近い的を下へ 10° (銃座の下の端) より浅く狙える所に並べる。
        public static readonly Vector3[] TargetGrounds =
        {
            new Vector3(10f, 0f, -14f),
            new Vector3(16f, 0f, -14f),
            new Vector3(22f, 0f, -14f),
        };

        // 車が爆発しても届かない (VehicleDurabilitySpec の既定の半径 8m) 距離だけ離して向かい合わせる。
        public static readonly Vector3 MountedGunOrigin = new Vector3(-14f, 0f, -6f);
        public static readonly Quaternion MountedGunRotation = Quaternion.LookRotation(Vector3.back, Vector3.up);
        public static readonly Vector3 CarOrigin = new Vector3(-14f, 0f, -24f);
        public static readonly Quaternion CarRotation = Quaternion.LookRotation(Vector3.right, Vector3.up);

        // 銃座・車・柱から離れた西の端に、床の北の端まで 120m ほどの滑走路を取る。全開で離陸の速さに届くのは 80m ほど先。
        public static readonly Vector3 AircraftOrigin = new Vector3(-30f, 0f, -55f);
        public static readonly Quaternion AircraftRotation = Quaternion.LookRotation(Vector3.forward, Vector3.up);

        public static float RunwayLength => FloorHalfExtent - AircraftOrigin.z;

        public static readonly Vector3 EnemyGround = new Vector3(40f, 0f, 55f);
        public static readonly EnemyBody EnemyBodySize = new EnemyBody(2f, 0.5f, 3.5f, Damageable.DefaultMaxHealth);
        public const float EnemySightRange = 20f;
        public const float EnemyAttackRange = 20f;
        public const float EnemyLoseSightSeconds = 2f;
        public const float EnemySearchSeconds = 6f;
        public const float EnemyReactionSeconds = 0.5f;

        public const float NavMeshCheckRadius = 1f;

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

        public static void BuildAt(string scenePath, string navMeshDataPath)
        {
            scenePath = BootstrapSceneBuilder.ValidateScenePath(scenePath);
            navMeshDataPath = SceneBuildHelpers.ValidateAssetPath(navMeshDataPath);

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // NewScene(Single) は参照されていないアセットをアンロードし、先に読んだ定義が null になるので、シーンを作ってから読む。
            var aircraft = AircraftFixtureBuilder.LoadOrBuildDefinition(AircraftFixtureBuilder.DefinitionPath);
            var aircraftEngine = AircraftFixtureBuilder.BuildEngineSound(AircraftFixtureBuilder.EngineSoundPath);
            var helicopter = VehicleFixtureBuilder.LoadOrBuildHelicopterDefinition(VehicleFixtureBuilder.HelicopterDefinitionPath);
            var boat = VehicleFixtureBuilder.LoadOrBuildBoatDefinition(VehicleFixtureBuilder.BoatDefinitionPath);
            var playerWeapons = TpsRangeSceneBuilder.LoadPlayerWeapons();
            var rangeWeapons = WeaponAssetBuilder.LoadRangeWeapons().Weapons;
            var gunWeapon = MountedGunRangeFixture.PickGunWeapon(rangeWeapons);
            var lockOnWeapon = PickLockOnWeapon(rangeWeapons)
                ?? throw new InvalidOperationException(
                    $"{LogTag} no range weapon has a homing round for the lock-on turret; add one to '{WeaponAssetBuilder.RangeWeaponsPath}'");
            var rules = GameRulesAssetBuilder.LoadRules(GameRulesAssetBuilder.TpsRangePath);

            SceneBuildHelpers.EnsureFolder(Path.GetDirectoryName(scenePath));
            SceneBuildHelpers.EnsureFolder(Path.GetDirectoryName(navMeshDataPath));

            // 乗り物と的は動くか壊れるので、NavMesh を削らないよう床とコースだけの状態で焼く。
            // 床の外の地面も焼かないのは、敵が床の外へ歩き出さず、1km 四方を焼く時間も掛けないようにするため。
            var surface = CreateLevel(scene);
            SceneBuildHelpers.BakeNavMesh(surface);
            SceneBuildHelpers.SaveNavMeshData(surface, navMeshDataPath);
            SceneBuildHelpers.RequireOnNavMesh(new[] { EnemyGround }, NavMeshCheckRadius, "enemy");
            CreateSurroundings(scene);
            CreateActors(scene, playerWeapons, gunWeapon, rules, aircraft, helicopter, boat, lockOnWeapon, aircraftEngine);

            if (!EditorSceneManager.SaveScene(scene, scenePath))
            {
                throw new IOException($"Failed to save the vehicle range scene to '{scenePath}'");
            }

            BuildSceneList.Register(scenePath);
            AssetDatabase.SaveAssets();

            Debug.Log($"{LogTag} saved '{scenePath}' and '{navMeshDataPath}'");
        }

        public static NavMeshSurface CreateLevel(Scene scene)
        {
            SceneBuildHelpers.RequireEmpty(scene, "the vehicle range");

            var floor = SceneBuildHelpers.CreateStaticPlane(scene, null, FloorName, Vector3.zero, FloorScale);
            var surface = SceneBuildHelpers.AddNavMeshSurface(floor, CollectObjects.All);

            var course = new GameObject(CourseName);
            SceneManager.MoveGameObjectToScene(course, scene);
            var angle = SlopeAngleDegrees * Mathf.Deg2Rad;
            var up = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f);
            var upNormal = new Vector3(-Mathf.Sin(angle), Mathf.Cos(angle), 0f);
            var down = new Vector3(Mathf.Cos(angle), -Mathf.Sin(angle), 0f);
            var downNormal = new Vector3(Mathf.Sin(angle), Mathf.Cos(angle), 0f);
            var lane = new Vector3(0f, 0f, GunCarOrigin.z);
            var slopeSize = new Vector3(SlopeLength, SlopeThickness, CourseWidth);

            // 板の上の面が地面と平らな面に段なくつながるよう、上の面の端を基準に置き、厚みの分だけ面の裏へ下げる。
            CreateTiltedBox(
                scene,
                course.transform,
                SlopeUpName,
                lane + new Vector3(SlopeStartX, 0f, 0f) + (up * (SlopeLength / 2f)) - (upNormal * (SlopeThickness / 2f)),
                Quaternion.Euler(0f, 0f, SlopeAngleDegrees),
                slopeSize);
            SceneBuildHelpers.CreateStaticBox(
                scene,
                course.transform,
                PlateauName,
                lane + new Vector3(PlateauStartX + (PlateauLength / 2f), SlopeRise / 2f, 0f),
                new Vector3(PlateauLength, SlopeRise, CourseWidth));
            CreateTiltedBox(
                scene,
                course.transform,
                SlopeDownName,
                lane + new Vector3(SlopeDownStartX, SlopeRise, 0f) + (down * (SlopeLength / 2f)) - (downNormal * (SlopeThickness / 2f)),
                Quaternion.Euler(0f, 0f, -SlopeAngleDegrees),
                slopeSize);

            var obstacles = new GameObject(ObstaclesName);
            SceneManager.MoveGameObjectToScene(obstacles, scene);
            for (var i = 0; i < PillarXs.Length; i++)
            {
                SceneBuildHelpers.CreateStaticBox(scene, obstacles.transform, PillarNamePrefix + i, PillarCenter(i), PillarSize);
            }

            EditorSceneManager.MarkSceneDirty(scene);
            return surface;
        }

        // 交互に左右へずらし、間を縫って走る道にする。
        public static Vector3 PillarCenter(int index)
        {
            var side = index % 2 == 0 ? 1f : -1f;
            return new Vector3(PillarXs[index], PillarSize.y / 2f, ObstacleLaneZ + (side * PillarOffsetZ));
        }

        // 床の外の地面と水場は、NavMesh を焼いた後に置く (BuildAt)。どちらも敵が歩く所ではないため。
        public static void CreateSurroundings(Scene scene)
        {
            SceneBuildHelpers.RequireLoaded(scene);
            var created = new List<GameObject>();
            try
            {
                created.Add(SceneBuildHelpers.CreateStaticPlane(scene, null, OuterGroundName, new Vector3(0f, -OuterGroundDrop, 0f), OuterGroundScale));

                var pool = new GameObject(PoolName);
                created.Add(pool);
                SceneManager.MoveGameObjectToScene(pool, scene);
                pool.transform.position = PoolGround;

                var water = new GameObject(WaterName).AddComponent<WaterVolume>();
                water.transform.SetParent(pool.transform, false);
                water.Size = PoolSize;
                water.SurfaceHeight = PoolSurfaceHeight;

                // 水面の板は見た目だけの目印なので当たりを外す。残すと、人が泳げず船も浮かず、板の上に乗ってしまうため。
                var surface = GameObject.CreatePrimitive(PrimitiveType.Cube);
                surface.name = WaterSurfaceName;
                UnityEngine.Object.DestroyImmediate(surface.GetComponent<Collider>());
                surface.transform.SetParent(pool.transform, false);
                surface.transform.localPosition = new Vector3(0f, PoolSurfaceHeight - 0.05f, 0f);
                surface.transform.localScale = new Vector3(PoolSize.x, 0.1f, PoolSize.z);

                EditorSceneManager.MarkSceneDirty(scene);
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

        // 名前でなく弾の値で選ぶのは、ゲームが武器を差し替えても、捕捉できる武器ならタレットに付けられるようにするため。
        public static WeaponDefinition PickLockOnWeapon(IReadOnlyList<WeaponDefinition> weapons)
        {
            if (weapons == null)
            {
                throw new ArgumentNullException(nameof(weapons));
            }

            foreach (var weapon in weapons)
            {
                if (weapon != null && weapon.ToSpec().Round.Homing != null)
                {
                    return weapon;
                }
            }

            return null;
        }

        public static Vector3 BoatOrigin(BoatSpec spec)
        {
            return new Vector3(BoatGround.x, PoolGround.y + PoolSurfaceHeight + VehicleFixtureBuilder.RestHeightAboveSurface(spec), BoatGround.z);
        }

        public static GameBootstrap CreateActors(
            Scene scene,
            IReadOnlyList<WeaponDefinition> playerWeapons,
            WeaponDefinition gunWeapon,
            GameRulesDefinition rules,
            AircraftDefinition aircraft,
            HelicopterDefinition helicopter,
            BoatDefinition boat,
            WeaponDefinition lockOnWeapon,
            AudioClip aircraftEngine = null)
        {
            SceneBuildHelpers.RequireLoaded(scene);
            SceneBuildHelpers.RequirePlayerWeapons(playerWeapons, nameof(playerWeapons));
            if (gunWeapon == null)
            {
                throw new ArgumentNullException(nameof(gunWeapon));
            }

            if (rules == null)
            {
                throw new ArgumentNullException(nameof(rules));
            }

            if (aircraft == null)
            {
                throw new ArgumentNullException(nameof(aircraft));
            }

            if (helicopter == null)
            {
                throw new ArgumentNullException(nameof(helicopter));
            }

            if (boat == null)
            {
                throw new ArgumentNullException(nameof(boat));
            }

            if (lockOnWeapon == null)
            {
                throw new ArgumentNullException(nameof(lockOnWeapon));
            }

            // 不正なルールや機体の値を配線しないよう、何か置く前に検査する。
            rules.ToRules();
            AircraftFixtureBuilder.Validate(aircraft);
            helicopter.ToSpec();
            var hull = boat.ToSpec();
            if (lockOnWeapon.ToSpec().Round.Homing == null)
            {
                throw new ArgumentException($"weapon '{lockOnWeapon.Id}' has no homing round, so the turret could never lock on", nameof(lockOnWeapon));
            }

            var created = new List<GameObject>();
            try
            {
                var bootstrapObject = new GameObject(BootstrapObjectName);
                created.Add(bootstrapObject);
                SceneManager.MoveGameObjectToScene(bootstrapObject, scene);
                var bootstrap = bootstrapObject.AddComponent<GameBootstrap>();
                bootstrap.CreatesFloor = false;
                bootstrap.WeaponDefinitions = playerWeapons;
                bootstrap.RulesDefinition = rules;

                // 銃座の銃は、プレイヤーと同じ規則で撃てることを見比べられるよう、ロードアウトの最初の武器にする。
                created.Add(CarFixtureBuilder.Create(scene, null, GunCarName, GunCarOrigin, GunCarRotation, playerWeapons[0]).gameObject);
                created.Add(CarFixtureBuilder.Create(scene, null, CarName, CarOrigin, CarRotation).gameObject);
                created.Add(MountedGunFixtureBuilder.Create(scene, null, MountedGunName, MountedGunOrigin, MountedGunRotation, gunWeapon).gameObject);
                var plane = AircraftFixtureBuilder.Create(scene, null, AircraftName, AircraftOrigin, AircraftRotation, aircraft, aircraftEngine);
                created.Add(plane.gameObject);
                VehicleFixtureBuilder.EquipAircraft(plane);

                created.Add(VehicleFixtureBuilder.CreateHelicopter(
                    scene, null, HelicopterName, HelicopterOrigin, HelicopterRotation, helicopter).gameObject);
                created.Add(VehicleFixtureBuilder.CreateBoat(scene, null, BoatName, BoatOrigin(hull), BoatRotation, boat).gameObject);
                CreateBotCar(scene, created);
                CreateBotAircraft(scene, aircraft, aircraftEngine, created);
                CreateLockOnTurret(scene, lockOnWeapon, created);

                // 警告は、どの画面のプレイヤーが狙われたかを GameSignals に出すだけで、表示は HUD が受け持つ。
                var warning = new GameObject(LockOnWarningName);
                created.Add(warning);
                SceneManager.MoveGameObjectToScene(warning, scene);
                warning.AddComponent<LockOnWarningPublisher>();

                var targets = new GameObject(TargetsName);
                created.Add(targets);
                SceneManager.MoveGameObjectToScene(targets, scene);
                for (var i = 0; i < TargetGrounds.Length; i++)
                {
                    var ground = TargetGrounds[i];
                    TargetDummy.Create(
                        TargetIdPrefix + i,
                        ground,
                        SceneBuildHelpers.FacingTowards(ground, new Vector3(ground.x, 0f, GunCarOrigin.z)),
                        targets.transform);
                }

                var enemies = new GameObject(EnemiesName);
                created.Add(enemies);
                SceneManager.MoveGameObjectToScene(enemies, scene);
                var enemy = SceneBuildHelpers.CreateEnemy(
                    scene,
                    enemies.transform,
                    EnemyName,
                    EnemyId,
                    EnemyGround,
                    SceneBuildHelpers.FacingTowards(EnemyGround, new Vector3(EnemyGround.x, 0f, GunCarOrigin.z)),
                    new List<Vector3>(),
                    EnemyBodySize,
                    playerWeapons[0]);
                enemy.ApplySpec(EnemyAiSpec());

                EditorSceneManager.MarkSceneDirty(scene);
                return bootstrap;
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

        public static EnemySpec EnemyAiSpec()
        {
            return new EnemySpec(EnemySightRange, EnemyAttackRange, EnemyLoseSightSeconds, EnemySearchSeconds, EnemyReactionSeconds);
        }

        // 運転席は空のまま置き、BotVehicleDriver の代わりの運転手に走らせる。Id を変えるのは、銃座で壊す車 (同じ組み方) と smoke が見分けるため。
        private static void CreateBotCar(Scene scene, List<GameObject> created)
        {
            var route = new GameObject(BotCarRouteName).AddComponent<VehicleRoute>();
            created.Add(route.gameObject);
            SceneManager.MoveGameObjectToScene(route.gameObject, scene);
            var points = new Transform[BotCarRoutePoints.Length];
            for (var i = 0; i < points.Length; i++)
            {
                points[i] = new GameObject("Point_" + i).transform;
                points[i].SetParent(route.transform, false);
                points[i].position = BotCarRoutePoints[i];
            }

            route.SetPoints(points);
            route.Loop = true;

            var car = CarFixtureBuilder.Create(scene, null, BotCarName, BotCarOrigin, BotCarRotation);
            created.Add(car.gameObject);
            car.Configure(VehicleFixtureBuilder.Respec(car.Spec, BotCarVehicleId));
            var driver = car.gameObject.AddComponent<BotVehicleDriver>();
            driver.DrivesUnmanned = true;
            driver.SetRoute(route);
        }

        // 点の高さをなぞらせるのは、地面に置いた機体の置いた高さ (0m) を保とうとして、離陸しても上がらなくなるのを避けるため。
        private static void CreateBotAircraft(Scene scene, AircraftDefinition aircraft, AudioClip engine, List<GameObject> created)
        {
            var waypoints = new GameObject(BotAircraftWaypointsName);
            created.Add(waypoints);
            SceneManager.MoveGameObjectToScene(waypoints, scene);
            var points = new Transform[BotAircraftWaypoints.Length];
            for (var i = 0; i < points.Length; i++)
            {
                points[i] = new GameObject("Waypoint_" + i).transform;
                points[i].SetParent(waypoints.transform, false);
                points[i].position = BotAircraftWaypoints[i];
            }

            var plane = AircraftFixtureBuilder.Create(scene, null, BotAircraftName, BotAircraftOrigin, BotAircraftRotation, aircraft, engine);
            created.Add(plane.gameObject);
            plane.Configure(VehicleFixtureBuilder.Respec(plane.Spec, BotAircraftVehicleId));
            var pilot = plane.gameObject.AddComponent<BotAircraftPilot>();
            pilot.SetWaypoints(points);
            pilot.FollowWaypointHeight = true;
        }

        // 台と頭の箱は試すための目印なので、差し替える参照は持たない。頭の当たりを外すのは、捉えた相手との間の見通しを自分の頭で遮らないようにするため。
        private static void CreateLockOnTurret(Scene scene, WeaponDefinition weapon, List<GameObject> created)
        {
            var root = new GameObject(LockOnTurretName);
            created.Add(root);
            SceneManager.MoveGameObjectToScene(root, scene);
            root.transform.position = LockOnTurretOrigin;

            var pedestal = GameObject.CreatePrimitive(PrimitiveType.Cube);
            pedestal.name = "Base";
            pedestal.transform.SetParent(root.transform, false);
            pedestal.transform.localPosition = new Vector3(0f, LockOnTurretBaseSize.y / 2f, 0f);
            pedestal.transform.localScale = LockOnTurretBaseSize;

            var head = new GameObject(LockOnTurretHeadName).transform;
            head.SetParent(root.transform, false);
            head.localPosition = LockOnTurretHeadLocal;
            head.localRotation = Quaternion.LookRotation(new Vector3(0f, 1f, -1f), Vector3.up);

            var barrel = GameObject.CreatePrimitive(PrimitiveType.Cube);
            barrel.name = "Barrel";
            UnityEngine.Object.DestroyImmediate(barrel.GetComponent<Collider>());
            barrel.transform.SetParent(head, false);
            barrel.transform.localPosition = new Vector3(0f, 0f, LockOnTurretBarrelSize.z / 2f);
            barrel.transform.localScale = LockOnTurretBarrelSize;

            var turret = root.AddComponent<LockOnTurret>();
            turret.Weapon = weapon;
            turret.Head = head;
            turret.Configure(LockOnTurretSpec.Default);
        }

        // CreateStaticBox は回さずに置くので、置いてから傾ける。
        private static GameObject CreateTiltedBox(Scene scene, Transform parent, string name, Vector3 center, Quaternion rotation, Vector3 size)
        {
            var box = SceneBuildHelpers.CreateStaticBox(scene, parent, name, center, size);
            box.transform.rotation = rotation;
            return box;
        }
    }
}
