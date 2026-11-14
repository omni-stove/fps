using System;
using System.Collections.Generic;
using Fps.Core;
using Fps.Editor;
using Fps.Runtime;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Fps.Tests.EditMode
{
    public sealed class TpsRangeSceneBuilderTests
    {
        private const float Epsilon = 1e-4f;

        private Scene _scene;
        private readonly List<UnityEngine.Object> _assets = new List<UnityEngine.Object>();

        [SetUp]
        public void SetUp()
        {
            _scene = TestScenes.NewAdditive();
        }

        [TearDown]
        public void TearDown()
        {
            if (_scene.IsValid())
            {
                EditorSceneManager.CloseScene(_scene, true);
            }

            foreach (var asset in _assets)
            {
                if (asset != null)
                {
                    UnityEngine.Object.DestroyImmediate(asset);
                }
            }

            _assets.Clear();
        }

        [Test]
        public void SavesToTheTpsRangeSceneOfTheBuildList()
        {
            Assert.That(TpsRangeSceneBuilder.ScenePath, Is.EqualTo(BuildSceneList.TpsRangeScene));
            Assert.That(TpsRangeSceneBuilder.MenuPath, Is.EqualTo("Fps/Build TPS Range Scene"));
        }

        [Test]
        public void BootstrapStartsInThirdPersonWithTheRulesAndAMatchRunner()
        {
            var rifle = NewWeapon("Rifle", WeaponAssetBuilder.RifleSpec());
            var launcher = NewWeapon("Launcher", WeaponAssetBuilder.LauncherSpec());
            var rules = NewRules(GameRulesAssetBuilder.TpsRangeRules());

            var bootstrap = TpsRangeSceneBuilder.PopulateScene(_scene, new[] { rifle, launcher }, rules);

            Assert.That(bootstrap.name, Is.EqualTo(TpsRangeSceneBuilder.BootstrapObjectName));
            Assert.That(bootstrap.gameObject.scene, Is.EqualTo(_scene));
            Assert.That(bootstrap.ViewMode, Is.EqualTo(ViewMode.ThirdPerson));
            Assert.That(bootstrap.CreatesFloor, Is.True, "the range has no floor of its own");
            Assert.That(bootstrap.WeaponDefinitions, Is.EqualTo(new[] { rifle, launcher }));
            Assert.That(bootstrap.RulesDefinition, Is.SameAs(rules));
            Assert.That(bootstrap.GetComponent<MatchRunner>(), Is.Not.Null);
        }

        [Test]
        public void PlacesTheCoversTheVaultWallTheOpenAreaAndTheHut()
        {
            var rules = NewRules(GameRulesAssetBuilder.TpsRangeRules());
            TpsRangeSceneBuilder.PopulateScene(_scene, new[] { NewWeapon("Rifle", WeaponAssetBuilder.RifleSpec()) }, rules);
            Physics.SyncTransforms();

            var covers = Find(TpsRangeSceneBuilder.CoversName).transform;
            Assert.That(covers.childCount, Is.EqualTo(TpsRangeSceneBuilder.Covers.Count));
            foreach (var box in TpsRangeSceneBuilder.Covers)
            {
                var cover = covers.Find(box.Name);
                Assert.That(cover, Is.Not.Null, box.Name);
                Assert.That(cover.GetComponent<BoxCollider>(), Is.Not.Null, box.Name);
                Assert.That(cover.gameObject.isStatic, Is.True, box.Name);
                Assert.That(cover.GetComponent<Collider>().bounds.min.y, Is.EqualTo(0f).Within(Epsilon), box.Name + " stands on the floor");
            }

            var vault = Find(TpsRangeSceneBuilder.VaultWallName);
            Assert.That(vault.GetComponent<BoxCollider>(), Is.Not.Null);
            Assert.That(vault.GetComponent<Collider>().bounds.max.y, Is.EqualTo(TpsRangeSceneBuilder.VaultWallHeight).Within(Epsilon));

            Assert.That(Find(TpsRangeSceneBuilder.OpenAreaName).transform.position, Is.EqualTo(TpsRangeSceneBuilder.OpenAreaCenter));

            var hut = Find(TpsRangeSceneBuilder.HutName).GetComponent<DestructibleStructure>();
            Assert.That(hut, Is.Not.Null);
            Assert.That(hut.transform.position, Is.EqualTo(TpsRangeSceneBuilder.HutCenter));
            Assert.That(hut.Pieces, Has.Count.EqualTo(DestructionTestSceneBuilder.PieceCount));
            Assert.That(hut.DestructionEnabled, Is.True, "the TPS range rules turn destruction on");
            Assert.That(hut.DebrisPool, Is.SameAs(Find(TpsRangeSceneBuilder.DebrisPoolName).GetComponent<DebrisPool>()));
            foreach (var piece in hut.Pieces)
            {
                if (piece.Grounded)
                {
                    Assert.That(piece.Collider.bounds.min.y, Is.EqualTo(0f).Within(Epsilon), piece.name + " rests on the floor");
                }
            }
        }

        [Test]
        public void PlacesAParkedVehicleWhoseDriverExitIsBlocked()
        {
            var rules = NewRules(GameRulesAssetBuilder.TpsRangeRules());
            TpsRangeSceneBuilder.PopulateScene(_scene, new[] { NewWeapon("Rifle", WeaponAssetBuilder.RifleSpec()) }, rules);
            Physics.SyncTransforms();

            var vehicle = Find(TpsRangeSceneBuilder.VehicleName).GetComponent<Vehicle>();
            Assert.That(vehicle, Is.Not.Null);
            Assert.That(vehicle.transform.position, Is.EqualTo(TpsRangeSceneBuilder.VehicleOrigin));
            Assert.That(vehicle.Seats, Has.Count.EqualTo(3));
            Assert.That(vehicle.Seats[0].Role, Is.EqualTo(VehicleSeatRole.Driver));
            Assert.That(vehicle.Seats[1].Role, Is.EqualTo(VehicleSeatRole.Passenger));
            Assert.That(vehicle.Seats[1].ViewPoint, Is.Not.Null);
            Assert.That(vehicle.Seats[2].Role, Is.EqualTo(VehicleSeatRole.Gunner));
            Assert.That(vehicle.Seats[2].Pose, Is.EqualTo(VehicleSeatPose.Standing));
            Assert.That(Find(TpsRangeSceneBuilder.VehicleExitBlockerName).GetComponent<BoxCollider>(), Is.Not.Null);

            var rider = new GameObject("Rider").AddComponent<VehicleTestRider>();
            SceneManager.MoveGameObjectToScene(rider.gameObject, _scene);
            Assert.That(vehicle.TryEnter(rider, 0, requireInRange: false), Is.True);
            Assert.That(vehicle.TryFindExit(vehicle.Seats[0], rider, out var feet, out _, out var candidate), Is.True);
            Assert.That(candidate, Is.EqualTo(1), "the left exit is blocked, so the driver gets out on the right");
            Assert.That(
                Vector3.Distance(feet, TpsRangeSceneBuilder.VehicleOrigin + VehicleFixtureBuilder.RightExitLocal), Is.LessThan(Epsilon));
        }

        [Test]
        public void WallsAndBoxesCarryTheirSurfaceMaterials()
        {
            var rules = NewRules(GameRulesAssetBuilder.TpsRangeRules());
            TpsRangeSceneBuilder.PopulateScene(_scene, new[] { NewWeapon("Rifle", WeaponAssetBuilder.RifleSpec()) }, rules);

            var covers = Find(TpsRangeSceneBuilder.CoversName).transform;
            foreach (var box in TpsRangeSceneBuilder.Covers)
            {
                var collider = covers.Find(box.Name).GetComponent<Collider>();
                Assert.That(SurfaceMaterial.IdOf(collider), Is.EqualTo(TpsRangeSceneBuilder.SurfaceIds[box.Name]), box.Name);
            }

            Assert.That(
                SurfaceMaterial.IdOf(Find(TpsRangeSceneBuilder.VaultWallName).GetComponent<Collider>()),
                Is.EqualTo(SurfaceImpactAssetStep.WoodId));

            var hut = Find(TpsRangeSceneBuilder.HutName).GetComponent<DestructibleStructure>();
            foreach (var piece in hut.Pieces)
            {
                Assert.That(SurfaceMaterial.IdOf(piece.Collider), Is.EqualTo(SurfaceImpactAssetStep.WoodId), piece.name);
            }

            var catalogIds = new HashSet<string>();
            foreach (var spec in SurfaceImpactAssetStep.Specs())
            {
                catalogIds.Add(spec.MaterialId);
            }

            foreach (var id in TpsRangeSceneBuilder.SurfaceIds.Values)
            {
                Assert.That(catalogIds, Does.Contain(id), $"'{id}' has its own impact assets");
            }
        }

        [Test]
        public void TheCoversAreOneLowAndOneHighAtLeast()
        {
            var low = 0;
            var high = 0;
            foreach (var cover in TpsRangeSceneBuilder.Covers)
            {
                if (cover.Size.y < CoverProbe.LowWallHeight)
                {
                    low++;
                }
                else
                {
                    high++;
                }
            }

            Assert.That(low, Is.GreaterThanOrEqualTo(2));
            Assert.That(high, Is.GreaterThanOrEqualTo(2));
            Assert.That(TpsRangeSceneBuilder.LowCover.Name, Is.EqualTo(TpsRangeSceneBuilder.LowCoverName));
            Assert.That(TpsRangeSceneBuilder.LowCover.Size.y, Is.LessThan(CoverProbe.LowWallHeight));
        }

        [Test]
        public void TheVaultWallCanBeVaultedOrJumped()
        {
            Assert.That(TpsRangeSceneBuilder.VaultWall.Size.y, Is.EqualTo(TpsRangeSceneBuilder.VaultWallHeight));
            Assert.That(TpsRangeSceneBuilder.VaultWallHeight, Is.LessThan(MotorSpec.DefaultVaultMaxHeight));
            Assert.That(TpsRangeSceneBuilder.VaultWallHeight, Is.LessThan(PlayerController.DefaultJumpHeight));
            Assert.That(TpsRangeSceneBuilder.VaultWallHeight, Is.LessThan(CoverProbe.LowWallHeight), "the vault wall also works as a low cover");
        }

        [Test]
        public void EverythingFitsOnTheFloorWithoutOverlapping()
        {
            var footprints = TpsRangeSceneBuilder.Footprints();
            var half = TpsRangeSceneBuilder.FloorHalfExtent;
            var openArea = new Bounds(
                TpsRangeSceneBuilder.OpenAreaCenter + (Vector3.up * 5f),
                new Vector3(TpsRangeSceneBuilder.OpenAreaHalfExtent * 2f, 10f, TpsRangeSceneBuilder.OpenAreaHalfExtent * 2f));
            Assert.That(Mathf.Abs(openArea.min.x) <= half && Mathf.Abs(openArea.max.x) <= half, Is.True, "the open area is on the floor");
            Assert.That(Mathf.Abs(openArea.min.z) <= half && Mathf.Abs(openArea.max.z) <= half, Is.True, "the open area is on the floor");

            // 出現位置 (原点) に立ったプレイヤーと標的ダミーの列に重ならないことも確かめる。
            var player = new Bounds(
                GameBootstrap.PlayerSpawn,
                new Vector3(GameBootstrap.PlayerRadius * 2f, GameBootstrap.PlayerHeight, GameBootstrap.PlayerRadius * 2f));
            var dummies = GameBootstrap.TargetDummyPositions(Vector3.zero, Quaternion.identity);

            for (var i = 0; i < footprints.Count; i++)
            {
                var (name, bounds) = footprints[i];
                Assert.That(bounds.min.x, Is.GreaterThanOrEqualTo(-half), name);
                Assert.That(bounds.max.x, Is.LessThanOrEqualTo(half), name);
                Assert.That(bounds.min.z, Is.GreaterThanOrEqualTo(-half), name);
                Assert.That(bounds.max.z, Is.LessThanOrEqualTo(half), name);
                Assert.That(bounds.Intersects(openArea), Is.False, name + " is in the open area");
                Assert.That(bounds.Intersects(player), Is.False, name + " is on the player's spawn");
                foreach (var dummy in dummies)
                {
                    var flat = new Vector2(dummy.x, dummy.z);
                    var near = new Vector2(Mathf.Clamp(flat.x, bounds.min.x, bounds.max.x), Mathf.Clamp(flat.y, bounds.min.z, bounds.max.z));
                    Assert.That(Vector2.Distance(flat, near), Is.GreaterThan(GameBootstrap.PlayerRadius), $"{name} is on the target dummy at {dummy}");
                }

                for (var j = i + 1; j < footprints.Count; j++)
                {
                    Assert.That(bounds.Intersects(footprints[j].Bounds), Is.False, $"{name} overlaps {footprints[j].Name}");
                }
            }
        }

        [Test]
        public void RefusesMissingOrInvalidArgumentsAndPlacesNothing()
        {
            var rifle = NewWeapon("Rifle", WeaponAssetBuilder.RifleSpec());
            var rules = NewRules(GameRulesAssetBuilder.TpsRangeRules());
            var broken = NewRules(GameRulesAssetBuilder.TpsRangeRules());
            broken.ReserveAmmoMultiplier = -1f;

            Assert.Throws<ArgumentNullException>(() => TpsRangeSceneBuilder.PopulateScene(_scene, null, rules));
            Assert.Throws<ArgumentException>(() => TpsRangeSceneBuilder.PopulateScene(_scene, Array.Empty<WeaponDefinition>(), rules));
            Assert.Throws<ArgumentException>(() => TpsRangeSceneBuilder.PopulateScene(_scene, new WeaponDefinition[] { null }, rules));
            Assert.Throws<ArgumentNullException>(() => TpsRangeSceneBuilder.PopulateScene(_scene, new[] { rifle }, null));
            Assert.Throws<GameRulesDefinitionException>(() => TpsRangeSceneBuilder.PopulateScene(_scene, new[] { rifle }, broken));
            Assert.That(_scene.rootCount, Is.Zero, "nothing is placed");
        }

        [Test]
        public void PlacesTheGivenFixturesAfterTheBuiltInsWithTheRules()
        {
            var rules = NewRules(GameRulesAssetBuilder.TpsRangeRules());
            var first = new FakeFixture("FixtureA");
            var second = new FakeFixture("FixtureB");

            TpsRangeSceneBuilder.PopulateScene(
                _scene, new[] { NewWeapon("Rifle", WeaponAssetBuilder.RifleSpec()) }, rules, new ITpsRangeFixture[] { first, second });

            var roots = _scene.GetRootGameObjects();
            Assert.That(roots[roots.Length - 2].name, Is.EqualTo("FixtureA"));
            Assert.That(roots[roots.Length - 1].name, Is.EqualTo("FixtureB"));
            Assert.That(first.ReceivedRules, Is.Not.Null);
            Assert.That(first.ReceivedRules.Actions.Cover, Is.EqualTo(rules.ToRules().Actions.Cover));
        }

        [Test]
        public void RefusesFixturesWithEmptyOrTakenNamesAndPlacesNothing()
        {
            var rifle = NewWeapon("Rifle", WeaponAssetBuilder.RifleSpec());
            var rules = NewRules(GameRulesAssetBuilder.TpsRangeRules());

            Assert.Throws<ArgumentNullException>(() => TpsRangeSceneBuilder.PopulateScene(_scene, new[] { rifle }, rules, null));
            Assert.Throws<ArgumentException>(
                () => TpsRangeSceneBuilder.PopulateScene(_scene, new[] { rifle }, rules, new ITpsRangeFixture[] { null }));
            Assert.Throws<ArgumentException>(
                () => TpsRangeSceneBuilder.PopulateScene(_scene, new[] { rifle }, rules, new ITpsRangeFixture[] { new FakeFixture(" ") }));
            Assert.Throws<ArgumentException>(
                () => TpsRangeSceneBuilder.PopulateScene(_scene, new[] { rifle }, rules, new ITpsRangeFixture[] { new FakeFixture("A/B") }));
            Assert.Throws<ArgumentException>(
                () => TpsRangeSceneBuilder.PopulateScene(
                    _scene, new[] { rifle }, rules, new ITpsRangeFixture[] { new FakeFixture(TpsRangeSceneBuilder.VaultWallName) }));
            Assert.Throws<ArgumentException>(
                () => TpsRangeSceneBuilder.PopulateScene(
                    _scene, new[] { rifle }, rules, new ITpsRangeFixture[] { new FakeFixture("Same"), new FakeFixture("Same") }));
            Assert.That(_scene.rootCount, Is.Zero, "nothing is placed");
        }

        [Test]
        public void AFixtureThatBreaksTheRulesUndoesEverything()
        {
            var rifle = NewWeapon("Rifle", WeaponAssetBuilder.RifleSpec());
            var rules = NewRules(GameRulesAssetBuilder.TpsRangeRules());

            Assert.Throws<InvalidOperationException>(
                () => TpsRangeSceneBuilder.PopulateScene(
                    _scene, new[] { rifle }, rules, new ITpsRangeFixture[] { new FakeFixture("Fine"), new FakeFixture("Named", "Other") }));
            Assert.That(_scene.rootCount, Is.Zero, "a wrong name removes what was placed");

            Assert.Throws<InvalidOperationException>(
                () => TpsRangeSceneBuilder.PopulateScene(
                    _scene, new[] { rifle }, rules, new ITpsRangeFixture[] { new FakeFixture("Nothing", placesNothing: true) }));
            Assert.That(_scene.rootCount, Is.Zero, "placing nothing removes what was placed");

            Assert.Throws<InvalidOperationException>(
                () => TpsRangeSceneBuilder.PopulateScene(
                    _scene, new[] { rifle }, rules, new ITpsRangeFixture[] { new FakeFixture("Child", asChild: true) }));
            Assert.That(_scene.rootCount, Is.Zero, "placing under another object removes what was placed");
        }

        [Test]
        public void FootprintsIncludeTheGivenFixtures()
        {
            var fixture = new FakeFixture("Fixture");

            var footprints = TpsRangeSceneBuilder.Footprints(new ITpsRangeFixture[] { fixture });

            Assert.That(footprints[footprints.Count - 1].Name, Is.EqualTo("Fixture"));
            Assert.That(footprints[footprints.Count - 1].Bounds, Is.EqualTo(fixture.Footprint));
            Assert.That(footprints.Count, Is.EqualTo(TpsRangeSceneBuilder.Footprints(Array.Empty<ITpsRangeFixture>()).Count + 1));
            Assert.Throws<ArgumentNullException>(() => TpsRangeSceneBuilder.Footprints(null));
        }

        [Test]
        public void OnlyPublicFixturesAreFound()
        {
            Assert.That(ExtensionPoints.IsCandidate(typeof(FakeFixture)), Is.False);
            foreach (var fixture in TpsRangeSceneBuilder.Fixtures())
            {
                Assert.That(fixture, Is.Not.InstanceOf<FakeFixture>());
                Assert.That(TpsRangeSceneBuilder.BuiltInNames, Does.Not.Contain(fixture.Name), fixture.GetType().FullName);
            }
        }

        [Test]
        public void RefusesANonEmptyScene()
        {
            var stray = new GameObject("Stray");
            SceneManager.MoveGameObjectToScene(stray, _scene);
            var rules = NewRules(GameRulesAssetBuilder.TpsRangeRules());

            Assert.Throws<InvalidOperationException>(
                () => TpsRangeSceneBuilder.PopulateScene(_scene, new[] { NewWeapon("Rifle", WeaponAssetBuilder.RifleSpec()) }, rules));
            Assert.That(_scene.rootCount, Is.EqualTo(1));
        }

        [Test]
        public void CommittedSceneStartsInThirdPersonWithTheTpsRangeRules()
        {
            var path = TpsRangeSceneBuilder.ScenePath;
            Assume.That(AssetDatabase.LoadAssetAtPath<SceneAsset>(path), Is.Not.Null, $"run {nameof(TpsRangeSceneBuilder)}.Build first");

            // テストの前に開いていた追加シーンは使わず、保存済みのシーンを開いて確かめる。
            EditorSceneManager.CloseScene(_scene, true);
            _scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);

            GameBootstrap bootstrap = null;
            DestructibleStructure hut = null;
            foreach (var root in _scene.GetRootGameObjects())
            {
                bootstrap = bootstrap != null ? bootstrap : root.GetComponent<GameBootstrap>();
                hut = hut != null ? hut : root.GetComponent<DestructibleStructure>();
            }

            Assert.That(bootstrap, Is.Not.Null);
            Assert.That(bootstrap.ViewMode, Is.EqualTo(ViewMode.ThirdPerson), $"rerun {nameof(TpsRangeSceneBuilder)}.Build");
            Assert.That(bootstrap.GetComponent<MatchRunner>(), Is.Not.Null);
            Assert.That(bootstrap.WeaponDefinitions, Is.Not.Empty);
            Assert.That(
                bootstrap.RulesDefinition,
                Is.SameAs(AssetDatabase.LoadAssetAtPath<GameRulesDefinition>(GameRulesAssetBuilder.TpsRangePath)));
            Assert.That(hut, Is.Not.Null);
            Assert.That(hut.DebrisPool, Is.Not.Null);
        }

        private WeaponDefinition NewWeapon(string name, WeaponSpec spec)
        {
            var weapon = ScriptableObject.CreateInstance<WeaponDefinition>();
            weapon.name = name;
            _assets.Add(weapon);
            WeaponAssetBuilder.Configure(weapon, spec);
            return weapon;
        }

        private GameRulesDefinition NewRules(GameRules rules)
        {
            var definition = ScriptableObject.CreateInstance<GameRulesDefinition>();
            definition.name = "Rules";
            _assets.Add(definition);
            GameRulesAssetBuilder.Configure(definition, rules);
            return definition;
        }

        private GameObject Find(string name)
        {
            foreach (var root in _scene.GetRootGameObjects())
            {
                if (root.name == name)
                {
                    return root;
                }
            }

            Assert.Fail($"'{name}' is not in the scene");
            return null;
        }

        /// <summary>決まりどおりに置くか、名前・置き場所・戻り値のどれかを外す置く物。private なので射撃場の生成には拾われない。</summary>
        private sealed class FakeFixture : ITpsRangeFixture
        {
            private readonly string _placedName;
            private readonly bool _placesNothing;
            private readonly bool _asChild;

            public FakeFixture(string name, string placedName = null, bool placesNothing = false, bool asChild = false)
            {
                Name = name;
                _placedName = placedName ?? name;
                _placesNothing = placesNothing;
                _asChild = asChild;
            }

            public int Order => 0;

            public string Name { get; }

            public Bounds Footprint => new Bounds(new Vector3(-20f, 0.5f, -20f), Vector3.one);

            public GameRules ReceivedRules { get; private set; }

            public GameObject Place(Scene scene, GameRules rules)
            {
                ReceivedRules = rules;
                if (_placesNothing)
                {
                    return null;
                }

                var placed = new GameObject(_placedName);
                SceneManager.MoveGameObjectToScene(placed, scene);
                if (_asChild)
                {
                    // 射撃場の組み込みの物の下に入れ、失敗したときに組み込みの物と一緒に消えることまで確かめる。
                    placed.transform.SetParent(scene.GetRootGameObjects()[0].transform, false);
                }

                return placed;
            }
        }
    }
}
