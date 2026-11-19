using System;
using System.Collections.Generic;
using System.IO;
using Shooter.Core;
using Shooter.Runtime;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Shooter.Editor
{
    public static class TpsRangeSceneBuilder
    {
        public static string ScenePath => BuildSceneList.TpsRangeScene;
        public const string MenuPath = SceneBuildSteps.MenuPath;
        public const string LogTag = "[TpsRangeSceneBuilder]";

        public const string BootstrapObjectName = "GameBootstrap";
        public const string FloorName = GameBootstrap.FloorName;
        public const string CoversName = BootstrapSceneBuilder.CoversName;
        public const string LowCoverName = BootstrapSceneBuilder.LowCoverName;
        public const string HighCoverName = BootstrapSceneBuilder.HighCoverName;
        public const string LowCoverBackName = "Cover_Low_Back";
        public const string HighPillarName = "Cover_High_Pillar";
        public const string VaultWallName = "VaultWall";
        public const string OpenAreaName = "OpenArea";
        public const string HutName = DestructionTestSceneBuilder.HutName;
        public const string DebrisPoolName = DestructionTestSceneBuilder.DebrisPoolName;

        // MotorSpec.DefaultVaultMaxHeight より低く、プレイヤーのジャンプ (PlayerController.DefaultJumpHeight) でも越えられる高さにする。
        // MotorSpec.DefaultCoverLowWallHeight 未満なので低い遮蔽物にもなる。
        public const float VaultWallHeight = 0.8f;

        // 乗り越えの軌道が短く済むよう遮蔽物より薄くする。
        public const float VaultWallThickness = 0.3f;

        public static readonly Vector3 OpenAreaCenter = new Vector3(14f, 0f, -6f);

        // 回避 (MotorSpec.DefaultDodgeDistance) を何度か続けられる広さにする。
        public const float OpenAreaHalfExtent = 6f;

        public static readonly Vector3 HutCenter = new Vector3(14f, 0f, 14f);

        public const string VehicleName = VehicleFixtureBuilder.DefaultName;
        public const string VehicleExitBlockerName = "VehicleExitBlocker";

        public static readonly Vector3 VehicleOrigin = new Vector3(-6f, 0f, -11f);

        // 運転席から降りると反対側の候補に降りることを確かめるため、乗り物の左に降りる位置 (運転席の最初の候補) を塞ぐ。
        public static readonly LevelBox VehicleExitBlocker = new LevelBox(
            VehicleExitBlockerName,
            VehicleOrigin + VehicleFixtureBuilder.LeftExitLocal + new Vector3(0f, 1f, 0f),
            new Vector3(1f, 2f, 1f));

        // GameBootstrap の床 (GameBootstrap.FloorScale) では置く物が収まらないので、シーンに自前の床を置く。Plane は 10m 四方なので 8 倍で 80m 四方にする。
        public static readonly Vector3 FloorScale = new Vector3(8f, 1f, 8f);

        public static readonly float FloorHalfExtent = FloorScale.x * 10f / 2f;

        public static readonly IReadOnlyList<LevelBox> Covers = new[]
        {
            BootstrapSceneBuilder.LowCover,
            BootstrapSceneBuilder.HighCover,
            new LevelBox(
                LowCoverBackName,
                new Vector3(0f, BootstrapSceneBuilder.LowCoverHeight / 2f, -4f),
                new Vector3(2f, BootstrapSceneBuilder.LowCoverHeight, BootstrapSceneBuilder.CoverThickness)),
            new LevelBox(
                HighPillarName,
                new Vector3(7f, BootstrapSceneBuilder.HighCoverHeight / 2f, 2f),
                new Vector3(1f, BootstrapSceneBuilder.HighCoverHeight, 1f)),
        };

        public static readonly LevelBox VaultWall = new LevelBox(
            VaultWallName, new Vector3(-10f, VaultWallHeight / 2f, 2f), new Vector3(4f, VaultWallHeight, VaultWallThickness));

        public static LevelBox LowCover => Covers[0];

        // 着弾の演出を見比べられるよう、遮蔽物のほかは材質を散らす。小屋は部品すべてに効くよう親に付ける。
        public static readonly IReadOnlyDictionary<string, string> SurfaceIds = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [LowCoverName] = BootstrapSceneBuilder.CoverSurfaceIds[BootstrapSceneBuilder.LowCoverName],
            [HighCoverName] = BootstrapSceneBuilder.CoverSurfaceIds[BootstrapSceneBuilder.HighCoverName],
            [LowCoverBackName] = SurfaceImpactAssetStep.ConcreteId,
            [HighPillarName] = SurfaceImpactAssetStep.MetalId,
            [VaultWallName] = SurfaceImpactAssetStep.WoodId,
            [HutName] = SurfaceImpactAssetStep.WoodId,
        };

        public static readonly IReadOnlyList<string> BuiltInNames = new[]
        {
            BootstrapObjectName,
            FloorName,
            SceneBuildHelpers.PerimeterName,
            CoversName,
            VaultWallName,
            OpenAreaName,
            DebrisPoolName,
            HutName,
            VehicleName,
            VehicleExitBlockerName,
        };

        public static IReadOnlyList<ITpsRangeFixture> Fixtures()
        {
            return ExtensionPoints.CreateAll<ITpsRangeFixture>(f => f.Order);
        }

        public static void Build()
        {
            // メニューから呼ばれたとき、開いているシーンの未保存の変更を黙って捨てない。
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                Debug.Log(LogTag + " cancelled");
                return;
            }

            BuildAt(ScenePath);
        }

        public static void BuildAt(string path)
        {
            path = BootstrapSceneBuilder.ValidateScenePath(path);

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // NewScene(Single) は参照されていないアセットをアンロードし、先に読んだ定義が null になるので、シーンを作ってから読む。
            var weapons = LoadPlayerWeapons();
            var rules = GameRulesAssetBuilder.LoadRules(GameRulesAssetBuilder.TpsRangePath);

            var fixtures = Fixtures();

            // 能力の枠は 2 つなので、何もしない能力は入れず、Ability1 にグラップル、Ability2 に展開式の盾を入れる。使えるかはルールの Abilities で決まる (射撃場の既定は Off)。
            var grapple = AbilityAssetBuilder.LoadOrBuildGrappleAbility();
            var shield = AbilityAssetBuilder.LoadOrBuildShieldAbility();

            // 枠が埋まっているのでタレットは枠に入れない。smoke の手順 (TurretSmokeStep) が Play 中に読んで盾の枠と入れ替えるので、アセットだけ用意する。
            AbilityAssetBuilder.LoadOrBuildTurretAbility();

            // 囮もタレットと同じく、smoke の手順 (DecoySmokeStep) が盾の枠と入れ替える。
            AbilityAssetBuilder.LoadOrBuildDecoyAbility();

            // 地雷もタレットと同じく枠には入れず、アセットだけ用意する。試すときは盾の枠と入れ替える。
            AbilityAssetBuilder.LoadOrBuildMineAbility();

            // C4 も地雷と同じく枠には入れず、アセットだけ用意する。試すときは盾の枠と入れ替える。
            AbilityAssetBuilder.LoadOrBuildRemoteChargeAbility();

            // 物を投げる能力も枠に入れず、smoke の手順 (DistractionSmokeStep) がグラップルの枠と入れ替える。
            AbilityAssetBuilder.LoadOrBuildDistractionAbility();

            SceneBuildHelpers.EnsureFolder(Path.GetDirectoryName(path));
            var bootstrap = PopulateScene(scene, weapons, rules, fixtures);
            bootstrap.AbilityDefinitions = new AbilityDefinition[] { grapple, shield };

            if (!EditorSceneManager.SaveScene(scene, path))
            {
                throw new IOException($"Failed to save the TPS range scene to '{path}'");
            }

            BuildSceneList.Register(path);
            AssetDatabase.SaveAssets();

            if (fixtures.Count > 0)
            {
                Debug.Log($"{LogTag} placed fixtures {string.Join(", ", NamesOf(fixtures))}");
            }

            Debug.Log($"{LogTag} saved '{path}' and registered it in Build Settings");
        }

        // 小屋を壊しやすいよう、既定のロードアウトの最後に launcher を足す。
        public static WeaponDefinition[] LoadPlayerWeapons()
        {
            var loadout = WeaponAssetBuilder.LoadPlayerLoadout();
            var launcher = WeaponAssetBuilder.LoadLauncher();
            var weapons = new WeaponDefinition[loadout.Length + 1];
            Array.Copy(loadout, weapons, loadout.Length);
            weapons[loadout.Length] = launcher;
            return weapons;
        }

        public static GameBootstrap PopulateScene(Scene scene, IReadOnlyList<WeaponDefinition> weapons, GameRulesDefinition rules)
        {
            return PopulateScene(scene, weapons, rules, Fixtures());
        }

        public static GameBootstrap PopulateScene(
            Scene scene, IReadOnlyList<WeaponDefinition> weapons, GameRulesDefinition rules, IReadOnlyList<ITpsRangeFixture> fixtures)
        {
            SceneBuildHelpers.RequireEmpty(scene, "the TPS range");
            SceneBuildHelpers.RequirePlayerWeapons(weapons, nameof(weapons));
            if (rules == null)
            {
                throw new ArgumentNullException(nameof(rules));
            }

            RequireValidFixtures(fixtures);

            // 不正なルールを配線しないよう、何か置く前に検査する。
            var gameRules = rules.ToRules();
            var destructionEnabled = gameRules.Destruction == Destruction.On;

            var created = new List<GameObject>();
            try
            {
                // MatchRunner は Start で同じ GameObject の GameBootstrap が Awake で作ったものを拾うので、ここでの配線は要らない。
                var bootstrapObject = new GameObject(BootstrapObjectName, typeof(GameBootstrap), typeof(MatchRunner));
                created.Add(bootstrapObject);
                SceneManager.MoveGameObjectToScene(bootstrapObject, scene);
                var bootstrap = bootstrapObject.GetComponent<GameBootstrap>();
                bootstrap.CreatesFloor = false;

                created.Add(SceneBuildHelpers.CreateStaticPlane(scene, null, FloorName, Vector3.zero, FloorScale));
                created.Add(SceneBuildHelpers.CreatePerimeterWalls(scene, null, Vector3.zero, SceneBuildHelpers.PlaneHalfExtents(FloorScale)));
                bootstrap.WeaponDefinitions = weapons;
                bootstrap.RulesDefinition = rules;

                created.Add(CreateBoxes(scene, CoversName, Covers));
                var vaultWall = SceneBuildHelpers.CreateStaticBox(scene, null, VaultWall.Name, VaultWall.Center, VaultWall.Size);
                created.Add(vaultWall);
                SceneBuildHelpers.SetSurfaceMaterial(vaultWall, SceneBuildHelpers.SurfaceIdOf(SurfaceIds, VaultWall.Name));

                var openArea = new GameObject(OpenAreaName);
                created.Add(openArea);
                SceneManager.MoveGameObjectToScene(openArea, scene);
                openArea.transform.SetPositionAndRotation(OpenAreaCenter, Quaternion.identity);

                var poolObject = new GameObject(DebrisPoolName);
                created.Add(poolObject);
                SceneManager.MoveGameObjectToScene(poolObject, scene);
                var pool = poolObject.AddComponent<DebrisPool>();

                // 小屋の部品は小屋の子として親から見た位置で置かれるので、親を動かせば形を保ったまま移せる。
                var hut = DestructionTestSceneBuilder.CreateHut(scene, destructionEnabled);
                created.Add(hut.gameObject);
                hut.transform.SetPositionAndRotation(HutCenter, Quaternion.identity);
                hut.DebrisPool = pool;
                SceneBuildHelpers.SetSurfaceMaterial(hut.gameObject, SceneBuildHelpers.SurfaceIdOf(SurfaceIds, HutName));

                // 銃座の銃は、プレイヤーと同じ規則で撃てることを見比べられるよう、ロードアウトの最初の武器にする。
                created.Add(VehicleFixtureBuilder.Create(scene, null, VehicleName, VehicleOrigin, Quaternion.identity, weapons[0]).gameObject);
                created.Add(SceneBuildHelpers.CreateStaticBox(
                    scene, null, VehicleExitBlocker.Name, VehicleExitBlocker.Center, VehicleExitBlocker.Size));

                foreach (var fixture in fixtures)
                {
                    created.Add(PlaceFixture(scene, fixture, gameRules));
                }

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

        public static IReadOnlyList<(string Name, Bounds Bounds)> Footprints()
        {
            return Footprints(Fixtures());
        }

        public static IReadOnlyList<(string Name, Bounds Bounds)> Footprints(IReadOnlyList<ITpsRangeFixture> fixtures)
        {
            if (fixtures == null)
            {
                throw new ArgumentNullException(nameof(fixtures));
            }

            var footprints = new List<(string, Bounds)>();
            foreach (var cover in Covers)
            {
                footprints.Add((cover.Name, new Bounds(cover.Center, cover.Size)));
            }

            footprints.Add((VaultWall.Name, new Bounds(VaultWall.Center, VaultWall.Size)));

            var hutSize = new Vector3(
                DestructionTestSceneBuilder.HutWidth * DestructionTestSceneBuilder.PieceSize,
                (DestructionTestSceneBuilder.WallHeight + 1) * DestructionTestSceneBuilder.PieceSize,
                DestructionTestSceneBuilder.HutDepth * DestructionTestSceneBuilder.PieceSize);
            footprints.Add((HutName, new Bounds(HutCenter + new Vector3(0f, hutSize.y / 2f, 0f), hutSize)));
            footprints.Add((VehicleName, VehicleFixtureBuilder.BodyBounds(VehicleOrigin, Quaternion.identity)));
            footprints.Add((VehicleExitBlocker.Name, new Bounds(VehicleExitBlocker.Center, VehicleExitBlocker.Size)));
            foreach (var fixture in fixtures)
            {
                if (fixture != null)
                {
                    footprints.Add((fixture.Name, fixture.Footprint));
                }
            }

            return footprints;
        }

        private static void RequireValidFixtures(IReadOnlyList<ITpsRangeFixture> fixtures)
        {
            if (fixtures == null)
            {
                throw new ArgumentNullException(nameof(fixtures));
            }

            var names = new HashSet<string>(BuiltInNames, StringComparer.Ordinal);
            for (var i = 0; i < fixtures.Count; i++)
            {
                var fixture = fixtures[i];
                if (fixture == null)
                {
                    throw new ArgumentException($"{LogTag} fixture {i} is null", nameof(fixtures));
                }

                var name = fixture.Name;
                if (string.IsNullOrWhiteSpace(name) || name.Contains("/"))
                {
                    throw new ArgumentException($"{LogTag} {fixture.GetType().FullName} has an empty name or one with '/' ('{name}')", nameof(fixtures));
                }

                if (!names.Add(name))
                {
                    throw new ArgumentException($"{LogTag} {fixture.GetType().FullName} uses the name '{name}', which is already taken in the TPS range", nameof(fixtures));
                }
            }
        }

        private static GameObject PlaceFixture(Scene scene, ITpsRangeFixture fixture, GameRules rules)
        {
            var type = fixture.GetType().FullName;
            var placed = fixture.Place(scene, rules);
            if (placed == null)
            {
                throw new InvalidOperationException($"{LogTag} {type} placed nothing");
            }

            if (placed.scene != scene || placed.transform.parent != null)
            {
                Discard(placed);
                throw new InvalidOperationException($"{LogTag} {type} must place '{fixture.Name}' at the root of the TPS range scene");
            }

            if (!string.Equals(placed.name, fixture.Name, StringComparison.Ordinal))
            {
                var actual = placed.name;
                Discard(placed);
                throw new InvalidOperationException($"{LogTag} {type} placed '{actual}' instead of '{fixture.Name}'");
            }

            return placed;
        }

        // 返す前に投げるので PopulateScene の後始末の一覧には入らない。ここで消すが、誤ってプレハブなどのアセットを返されたときは消さない。
        private static void Discard(GameObject placed)
        {
            if (!EditorUtility.IsPersistent(placed))
            {
                UnityEngine.Object.DestroyImmediate(placed);
            }
        }

        private static IEnumerable<string> NamesOf(IReadOnlyList<ITpsRangeFixture> fixtures)
        {
            foreach (var fixture in fixtures)
            {
                yield return fixture.Name;
            }
        }

        private static GameObject CreateBoxes(Scene scene, string rootName, IReadOnlyList<LevelBox> boxes)
        {
            var root = new GameObject(rootName);
            SceneManager.MoveGameObjectToScene(root, scene);
            foreach (var box in boxes)
            {
                var created = SceneBuildHelpers.CreateStaticBox(scene, root.transform, box.Name, box.Center, box.Size);
                SceneBuildHelpers.SetSurfaceMaterial(created, SceneBuildHelpers.SurfaceIdOf(SurfaceIds, box.Name));
            }

            return root;
        }
    }
}
