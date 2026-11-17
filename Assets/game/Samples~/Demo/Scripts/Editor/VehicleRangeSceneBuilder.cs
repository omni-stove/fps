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
    // 乗り物を試す試走場 VehicleRange。プレイヤーは原点で +Z を向いて出て、GameBootstrap が z = 10 (x = -4..4) に標的ダミーを並べる。
    // 銃座付きの車は出現位置のすぐ右後ろで +X を向き、その先の平地の直線の奥に坂 (上り・平ら・下り) を置く。
    // 銃座で撃つ的は直線の右 (z = -14)、障害物は出現位置の奥の別の道 (z = 18 の前後) に置く。
    // 固定銃座と、それで壊す仮の車は出現位置の左後ろに向かい合わせ、敵は奥の端に置く。
    // 敵は smoke の撃つ所から銃声の届く半径 (どの武器も 50m 以下) より遠くに置き、smoke の間に寄って来ないようにする。
    public static class VehicleRangeSceneBuilder
    {
        public static string ScenePath => BuildSceneList.VehicleRangeScene;

        public static string NavMeshDataPath => ModeSceneBuilder.NavMeshDataPathFor(ScenePath);

        public const string MenuPath = "Fps/Build Vehicle Range Scene";
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
        public const string EnemiesName = "Enemies";
        public const string EnemyName = "Enemy";
        public const string EnemyId = "VehicleRangeEnemy";

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
            var playerWeapons = TpsRangeSceneBuilder.LoadPlayerWeapons();
            var gunWeapon = MountedGunRangeFixture.PickGunWeapon(WeaponAssetBuilder.LoadRangeWeapons().Weapons);
            var rules = GameRulesAssetBuilder.LoadRules(GameRulesAssetBuilder.TpsRangePath);

            SceneBuildHelpers.EnsureFolder(Path.GetDirectoryName(scenePath));
            SceneBuildHelpers.EnsureFolder(Path.GetDirectoryName(navMeshDataPath));

            // 乗り物と的は動くか壊れるので、NavMesh を削らないよう床とコースだけの状態で焼く。
            var surface = CreateLevel(scene);
            SceneBuildHelpers.BakeNavMesh(surface);
            SceneBuildHelpers.SaveNavMeshData(surface, navMeshDataPath);
            SceneBuildHelpers.RequireOnNavMesh(new[] { EnemyGround }, NavMeshCheckRadius, "enemy");
            CreateActors(scene, playerWeapons, gunWeapon, rules);

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

        public static GameBootstrap CreateActors(
            Scene scene, IReadOnlyList<WeaponDefinition> playerWeapons, WeaponDefinition gunWeapon, GameRulesDefinition rules)
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

            // 不正なルールを配線しないよう、何か置く前に検査する。
            rules.ToRules();

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

        // CreateStaticBox は回さずに置くので、置いてから傾ける。
        private static GameObject CreateTiltedBox(Scene scene, Transform parent, string name, Vector3 center, Quaternion rotation, Vector3 size)
        {
            var box = SceneBuildHelpers.CreateStaticBox(scene, parent, name, center, size);
            box.transform.rotation = rotation;
            return box;
        }
    }
}
