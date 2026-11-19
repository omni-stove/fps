using System.Linq;
using System;
using Shooter.Core;
using Shooter.Editor;
using Shooter.Runtime;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Shooter.Tests.EditMode
{
    public sealed class BootstrapSceneBuilderTests
    {
        private const string Other = "Assets/Scenes/SampleScene.unity";

        [Test]
        public void UsesDefaultPathWithoutArgument()
        {
            var args = new[] { "Unity", "-batchmode", "-executeMethod", "Shooter.Editor.BootstrapSceneBuilder.Build" };

            Assert.That(BootstrapSceneBuilder.ResolveScenePath(args), Is.EqualTo(BootstrapSceneBuilder.DefaultScenePath));
            Assert.That(BootstrapSceneBuilder.ResolveScenePath(null), Is.EqualTo(BootstrapSceneBuilder.DefaultScenePath));
        }

        [Test]
        public void ReadsPathArgument()
        {
            var args = new[] { "Unity", BootstrapSceneBuilder.ScenePathArgument, @"Assets\game\Scenes\Other.unity", "-quit" };

            Assert.That(BootstrapSceneBuilder.ResolveScenePath(args), Is.EqualTo("Assets/game/Scenes/Other.unity"));
        }

        // string[] を TestCase に直接渡すと params object[] として展開されるので、ソースから渡す。
        private static readonly object[] _malformedArgs =
        {
            new object[] { new[] { "-bootstrapScenePath" } },
            new object[] { new[] { "-bootstrapScenePath", "-quit" } },
            new object[] { new[] { "-bootstrapScenePath", " " } },
            new object[] { new[] { "-bootstrapScenePath", "Assets/A.unity", "-bootstrapScenePath", "Assets/B.unity" } },
            new object[] { new[] { "-bootstrapScenePath", "Bootstrap.unity" } },
        };

        [TestCaseSource(nameof(_malformedArgs))]
        public void RejectsMalformedPathArgument(string[] args)
        {
            Assert.Throws<ArgumentException>(() => BootstrapSceneBuilder.ResolveScenePath(args));
        }

        [TestCase("")]
        [TestCase("Bootstrap.unity")]
        [TestCase("/Assets/Bootstrap.unity")]
        [TestCase("Assets/Bootstrap.prefab")]
        [TestCase("Assets/../Bootstrap.unity")]
        [TestCase("Assets//Bootstrap.unity")]
        public void RejectsScenePathOutsideAssets(string path)
        {
            Assert.Throws<ArgumentException>(() => BootstrapSceneBuilder.ValidateScenePath(path));
        }

        [Test]
        public void PutsSceneFirstAndKeepsTheRest()
        {
            var current = new[]
            {
                new EditorBuildSettingsScene(Other, false),
                new EditorBuildSettingsScene(BootstrapSceneBuilder.DefaultScenePath, false),
            };

            var scenes = BootstrapSceneBuilder.WithSceneFirst(current, BootstrapSceneBuilder.DefaultScenePath);

            Assert.That(scenes, Has.Length.EqualTo(2));
            Assert.That(scenes[0].path, Is.EqualTo(BootstrapSceneBuilder.DefaultScenePath));
            Assert.That(scenes[0].enabled, Is.True);
            Assert.That(scenes[1].path, Is.EqualTo(Other));
            Assert.That(scenes[1].enabled, Is.False, "other scenes keep their enabled flag");
        }

        [Test]
        public void HandlesEmptyBuildSettings()
        {
            var scenes = BootstrapSceneBuilder.WithSceneFirst(null, BootstrapSceneBuilder.DefaultScenePath);

            Assert.That(scenes, Has.Length.EqualTo(1));
            Assert.That(scenes[0].path, Is.EqualTo(BootstrapSceneBuilder.DefaultScenePath));
        }

        [Test]
        public void PopulatedSceneHoldsTheBootstrapAndTheCovers()
        {
            var scene = TestScenes.NewAdditive();
            var rifle = ScriptableObject.CreateInstance<WeaponDefinition>();
            var handgun = ScriptableObject.CreateInstance<WeaponDefinition>();
            var range = ScriptableObject.CreateInstance<GameRulesDefinition>();
            try
            {
                var bootstrap = BootstrapSceneBuilder.PopulateScene(scene, new[] { rifle, handgun }, range);

                var roots = scene.GetRootGameObjects();
                Assert.That(roots, Has.Length.EqualTo(2));
                Assert.That(roots[0], Is.SameAs(bootstrap));
                Assert.That(roots[1].name, Is.EqualTo(BootstrapSceneBuilder.CoversName));
                AssertCovers(roots[1]);
                Assert.That(bootstrap.name, Is.EqualTo(BootstrapSceneBuilder.BootstrapObjectName));
                Assert.That(bootstrap.GetComponent<GameBootstrap>(), Is.Not.Null);
                Assert.That(
                    bootstrap.GetComponent<GameBootstrap>().WeaponDefinitions,
                    Is.EqualTo(new[] { rifle, handgun }),
                    "the player's loadout is wired");
                Assert.That(bootstrap.GetComponent<GameBootstrap>().RulesDefinition, Is.SameAs(range), "the range rules are wired");
                Assert.That(bootstrap.GetComponent<MatchRunner>(), Is.Not.Null, "the bootstrap scene runs a solo match");
                Assert.That(bootstrap.GetComponent<MatchRunner>().TimeLimitSeconds, Is.EqualTo(0f), "the range has no timer");
                Assert.That(bootstrap.GetComponent<TutorialPresenter>(), Is.Not.Null, "the range shows the tutorial");
                Assert.That(bootstrap.transform.childCount, Is.Zero, "everything else is built at runtime");
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
                UnityEngine.Object.DestroyImmediate(rifle);
                UnityEngine.Object.DestroyImmediate(handgun);
                UnityEngine.Object.DestroyImmediate(range);
            }
        }

        [Test]
        public void RefusesToPopulateANonEmptySceneOrWithoutAWeaponOrRules()
        {
            var scene = TestScenes.NewAdditive();
            var rifle = ScriptableObject.CreateInstance<WeaponDefinition>();
            var range = ScriptableObject.CreateInstance<GameRulesDefinition>();
            try
            {
                Assert.Throws<ArgumentNullException>(() => BootstrapSceneBuilder.PopulateScene(scene, null, range));
                Assert.Throws<ArgumentException>(() => BootstrapSceneBuilder.PopulateScene(scene, Array.Empty<WeaponDefinition>(), range));
                Assert.Throws<ArgumentException>(() => BootstrapSceneBuilder.PopulateScene(scene, new[] { rifle, null }, range));
                Assert.Throws<ArgumentNullException>(() => BootstrapSceneBuilder.PopulateScene(scene, new[] { rifle }, null));
                Assert.That(scene.rootCount, Is.Zero);

                var stray = new GameObject("Stray");
                SceneManager.MoveGameObjectToScene(stray, scene);

                Assert.Throws<InvalidOperationException>(() => BootstrapSceneBuilder.PopulateScene(scene, new[] { rifle }, range));
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
                UnityEngine.Object.DestroyImmediate(rifle);
                UnityEngine.Object.DestroyImmediate(range);
            }
        }

        [Test]
        public void CommittedSceneMatchesTheBuilder()
        {
            var path = BootstrapSceneBuilder.DefaultScenePath;
            Assume.That(AssetDatabase.LoadAssetAtPath<SceneAsset>(path), Is.Not.Null, $"run {nameof(BootstrapSceneBuilder)}.Build first");

            var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            try
            {
                var roots = scene.GetRootGameObjects();
                Assert.That(roots, Has.Length.EqualTo(4), $"rerun {nameof(BootstrapSceneBuilder)}.Build to add the covers, the weapon table and the platforms");
                Assert.That(roots[0].GetComponent<GameBootstrap>(), Is.Not.Null);
                Assert.That(roots[1].name, Is.EqualTo(BootstrapSceneBuilder.CoversName));
                AssertCovers(roots[1]);
                Assert.That(roots[2].name, Is.EqualTo(BootstrapSceneBuilder.WeaponTableName));
                Assert.That(roots[3].name, Is.EqualTo(BootstrapSceneBuilder.PlatformsName), $"rerun {nameof(BootstrapSceneBuilder)}.Build to add the platforms");
                var elevatorSettings = AssetDatabase.LoadAssetAtPath<MovingPlatformSettings>(BootstrapSceneBuilder.ElevatorSettingsPath);
                Assert.That(elevatorSettings, Is.Not.Null, $"rerun {nameof(BootstrapSceneBuilder)}.Build");
                AssertPlatforms(roots[3], elevatorSettings);
                Assert.That(
                    roots[2].GetComponentsInChildren<PickupSpawnPoint>().Select(p => p.Contents.Weapon.Id),
                    Is.EqualTo(BootstrapSceneBuilder.FeaturedWeaponIds),
                    $"rerun {nameof(BootstrapSceneBuilder)}.Build");
                Assert.That(roots[0].GetComponent<MatchRunner>(), Is.Not.Null, $"rerun {nameof(BootstrapSceneBuilder)}.Build");
                Assert.That(
                    roots[0].GetComponent<TutorialPresenter>(),
                    Is.Not.Null,
                    $"rerun {nameof(BootstrapSceneBuilder)}.Build to add the tutorial");
                Assert.That(
                    roots[0].GetComponent<GameBootstrap>().WeaponDefinitions,
                    Is.EqualTo(BootstrapSceneBuilder.LoadPlayerWeapons(WeaponAssetBuilder.LoadReferenceWeapons())),
                    $"rerun {nameof(BootstrapSceneBuilder)}.Build");
                var range = AssetDatabase.LoadAssetAtPath<GameRulesDefinition>(GameRulesAssetBuilder.RangePath);
                Assert.That(range, Is.Not.Null, $"run {nameof(GameRulesAssetBuilder)}.Build");
                Assert.That(
                    roots[0].GetComponent<GameBootstrap>().RulesDefinition,
                    Is.SameAs(range),
                    $"rerun {nameof(BootstrapSceneBuilder)}.Build to wire the range rules");
                Assert.That(
                    Array.Exists(EditorBuildSettings.scenes, s => s.path == path && s.enabled),
                    Is.True,
                    $"the range is in the build; rerun {BuildSceneList.MenuPath}");
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        [Test]
        public void PopulatingWithTheElevatorSettingsAddsTheShuttleAndTheElevator()
        {
            var scene = TestScenes.NewAdditive();
            var rifle = ScriptableObject.CreateInstance<WeaponDefinition>();
            var range = ScriptableObject.CreateInstance<GameRulesDefinition>();
            var elevator = ElevatorSettings();
            try
            {
                var bootstrap = BootstrapSceneBuilder.PopulateScene(scene, new[] { rifle }, range, Array.Empty<WeaponDefinition>(), elevator);

                var roots = scene.GetRootGameObjects();
                Assert.That(roots, Has.Length.EqualTo(3));
                Assert.That(roots[0], Is.SameAs(bootstrap));
                Assert.That(roots[1].name, Is.EqualTo(BootstrapSceneBuilder.CoversName));
                Assert.That(roots[2].name, Is.EqualTo(BootstrapSceneBuilder.PlatformsName));
                AssertPlatforms(roots[2], elevator);
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
                UnityEngine.Object.DestroyImmediate(rifle);
                UnityEngine.Object.DestroyImmediate(range);
                UnityEngine.Object.DestroyImmediate(elevator);
            }
        }

        [Test]
        public void ThePlatformsNeedOnCallElevatorSettings()
        {
            var scene = TestScenes.NewAdditive();
            var rifle = ScriptableObject.CreateInstance<WeaponDefinition>();
            var range = ScriptableObject.CreateInstance<GameRulesDefinition>();
            var pingPong = ScriptableObject.CreateInstance<MovingPlatformSettings>();
            var elevator = ElevatorSettings();
            try
            {
                Assert.Throws<ArgumentNullException>(() => BootstrapSceneBuilder.CreatePlatforms(scene, null));
                Assert.Throws<ArgumentException>(() => BootstrapSceneBuilder.CreatePlatforms(scene, pingPong));
                Assert.Throws<ArgumentException>(
                    () => BootstrapSceneBuilder.PopulateScene(scene, new[] { rifle }, range, Array.Empty<WeaponDefinition>(), pingPong));
                Assert.That(scene.rootCount, Is.Zero, "nothing is placed with settings that do not wait for a call");
                Assert.Throws<ArgumentException>(() => BootstrapSceneBuilder.CreatePlatforms(default, elevator));
                Assert.Throws<ArgumentNullException>(() => BootstrapSceneBuilder.ConfigureElevatorSettings(null));
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
                UnityEngine.Object.DestroyImmediate(rifle);
                UnityEngine.Object.DestroyImmediate(range);
                UnityEngine.Object.DestroyImmediate(pingPong);
                UnityEngine.Object.DestroyImmediate(elevator);
            }
        }

        [Test]
        public void TheElevatorSettingsWaitForACallAndLeaveWhenBoarded()
        {
            var settings = ElevatorSettings();
            try
            {
                var spec = settings.ToSpec();
                Assert.That(spec.Kind, Is.EqualTo(PlatformRouteKind.OnCall));
                Assert.That(spec.Speed, Is.EqualTo(BootstrapSceneBuilder.ElevatorSpeed));
                Assert.That(spec.Speed, Is.GreaterThan(0f));
                Assert.That(spec.WaitSeconds, Is.EqualTo(BootstrapSceneBuilder.ElevatorWaitSeconds));
                Assert.That(spec.SpinDegreesPerSecond, Is.EqualTo(0f));
                Assert.That(spec.DepartWhenBoarded, Is.True);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(settings);
            }
        }

        [Test]
        public void ThePlatformsStandAsideFromTheShootingLanesAndCanBeWalkedOnto()
        {
            Assert.That(BootstrapSceneBuilder.PlatformTopHeight, Is.LessThan(0.3f), "below the CharacterController's default step offset");
            Assert.That(BootstrapSceneBuilder.ShuttleOrigin.y + (BootstrapSceneBuilder.PlatformSlabSize.y / 2f), Is.EqualTo(BootstrapSceneBuilder.PlatformTopHeight).Within(1e-5f));
            Assert.That(BootstrapSceneBuilder.ElevatorOrigin.y + (BootstrapSceneBuilder.PlatformSlabSize.y / 2f), Is.EqualTo(BootstrapSceneBuilder.PlatformTopHeight).Within(1e-5f));

            var tower = BootstrapSceneBuilder.ElevatorTower;
            var towerTop = tower.Center.y + (tower.Size.y / 2f);
            var raisedTop = BootstrapSceneBuilder.PlatformTopHeight + BootstrapSceneBuilder.ElevatorStops[BootstrapSceneBuilder.ElevatorTopStop].y;
            Assert.That(towerTop, Is.EqualTo(raisedTop).Within(1e-5f), "the raised elevator is level with the tower top");
            Assert.That(tower.Center.y - (tower.Size.y / 2f), Is.EqualTo(0f).Within(1e-5f), "the tower stands on the floor");
            var slabEdge = BootstrapSceneBuilder.ElevatorOrigin.x + (BootstrapSceneBuilder.PlatformSlabSize.x / 2f);
            Assert.That(tower.Center.x - (tower.Size.x / 2f), Is.EqualTo(slabEdge).Within(1e-5f), "the tower meets the elevator without a gap");

            var elevatorBounds = new Bounds(
                new Vector3(BootstrapSceneBuilder.ElevatorOrigin.x, raisedTop / 2f, BootstrapSceneBuilder.ElevatorOrigin.z),
                new Vector3(BootstrapSceneBuilder.PlatformSlabSize.x, raisedTop + 2f, BootstrapSceneBuilder.PlatformSlabSize.z));
            var bottom = BootstrapSceneBuilder.ElevatorCallBottom;
            var top = BootstrapSceneBuilder.ElevatorCallTop;
            Assert.That(new Bounds(bottom.Center, bottom.Size).Intersects(elevatorBounds), Is.False, "the ground landing is beside the shaft");
            Assert.That(new Bounds(top.Center, top.Size).Intersects(elevatorBounds), Is.False, "the top landing is on the tower");
            Assert.That(bottom.Center.y - (bottom.Size.y / 2f), Is.EqualTo(0f).Within(1e-5f), "the ground landing starts at the floor");
            Assert.That(top.Center.y - (top.Size.y / 2f), Is.EqualTo(towerTop).Within(1e-5f), "the top landing starts at the tower top");

            var feet = GameBootstrap.PlayerSpawn + (Vector3.down * (GameBootstrap.PlayerHeight / 2f));
            var targets = GameBootstrap.TargetDummyPositions(feet, Quaternion.identity);
            var tablePositions = BootstrapSceneBuilder.WeaponTablePositions(16);
            var lanesHalfWidth = 0f;
            foreach (var point in targets.Concat(tablePositions))
            {
                lanesHalfWidth = Mathf.Max(lanesHalfWidth, Mathf.Abs(point.x));
            }

            foreach (var cover in BootstrapSceneBuilder.Covers)
            {
                lanesHalfWidth = Mathf.Max(lanesHalfWidth, Mathf.Abs(cover.Center.x) + (cover.Size.x / 2f));
            }

            var half = BootstrapSceneBuilder.PlatformSlabSize.x / 2f;
            Assert.That(BootstrapSceneBuilder.ShuttleOrigin.x + half, Is.LessThan(-lanesHalfWidth), "the shuttle runs left of the lanes");
            Assert.That(BootstrapSceneBuilder.ElevatorOrigin.x - half, Is.GreaterThan(lanesHalfWidth), "the elevator stands right of the lanes");
            Assert.That(bottom.Center.x - (bottom.Size.x / 2f), Is.GreaterThan(lanesHalfWidth), "the ground landing is right of the lanes");

            var floorHalf = GameBootstrap.FloorScale.x * 5f;
            foreach (var stop in BootstrapSceneBuilder.ShuttleStops)
            {
                Assert.That(Mathf.Abs(BootstrapSceneBuilder.ShuttleOrigin.z + stop.z) + half, Is.LessThan(floorHalf), "the shuttle stays over the floor");
            }

            Assert.That(tower.Center.x + (tower.Size.x / 2f), Is.LessThan(floorHalf), "the tower stands on the floor");
        }

        [Test]
        public void TheCoversAreOneLowAndOneHigh()
        {
            Assert.That(BootstrapSceneBuilder.Covers.Count(), Is.EqualTo(2));
            Assert.That(BootstrapSceneBuilder.LowCover.Size.y, Is.EqualTo(1f));
            Assert.That(BootstrapSceneBuilder.HighCover.Size.y, Is.EqualTo(2f));
            Assert.That(BootstrapSceneBuilder.LowCover.Size.y, Is.LessThan(MotorSpec.DefaultCoverLowWallHeight), "the player crouches behind the low cover");
            Assert.That(BootstrapSceneBuilder.HighCover.Size.y, Is.GreaterThanOrEqualTo(MotorSpec.DefaultCoverLowWallHeight), "the player stands behind the high cover");

            foreach (var cover in BootstrapSceneBuilder.Covers)
            {
                Assert.That(cover.Center.y - (cover.Size.y / 2f), Is.EqualTo(0f).Within(1e-5f), $"{cover.Name} stands on the floor");
            }
        }

        [Test]
        public void TheCoversStandBetweenThePlayerAndTheTargetsAndLeaveALineOfFireOpen()
        {
            var feet = GameBootstrap.PlayerSpawn + (Vector3.down * (GameBootstrap.PlayerHeight / 2f));
            var eye = GameBootstrap.PlayerSpawn + GameBootstrap.EyeOffset;
            var targets = GameBootstrap.TargetDummyPositions(feet, Quaternion.identity);

            var open = 0;
            foreach (var target in targets)
            {
                // 標的の胴の高さ (足元から 1m) を狙う。
                var aim = target + Vector3.up;
                var ray = new Ray(eye, aim - eye);
                var length = Vector3.Distance(eye, aim);
                var blocked = false;
                foreach (var cover in BootstrapSceneBuilder.Covers)
                {
                    var bounds = new Bounds(cover.Center, cover.Size);
                    if (bounds.IntersectRay(ray, out var distance) && distance <= length)
                    {
                        blocked = true;
                    }
                }

                if (!blocked)
                {
                    open++;
                }
            }

            Assert.That(open, Is.GreaterThan(0), "at least one target can be shot from the spawn");

            foreach (var cover in BootstrapSceneBuilder.Covers)
            {
                Assert.That(cover.Center.z, Is.GreaterThan(feet.z + GameBootstrap.PlayerRadius + MotorSpec.DefaultCoverMaxEnterDistance), $"{cover.Name} is ahead of the spawn");
                Assert.That(cover.Center.z, Is.LessThan(targets[0].z), $"{cover.Name} is in front of the targets");
            }

            var center = targets[targets.Length / 2];
            var toCenter = new Ray(eye, center + Vector3.up - eye);
            foreach (var cover in BootstrapSceneBuilder.Covers)
            {
                Assert.That(new Bounds(cover.Center, cover.Size).IntersectRay(toCenter), Is.False, $"{cover.Name} keeps the middle target open");
            }
        }

        [Test]
        public void TheFeaturedWeaponsAreOneReferenceAndOneRealisticSamplePerCategory()
        {
            var featured = LoadFeatured();

            Assert.That(featured.Select(w => w.Id), Is.EqualTo(BootstrapSceneBuilder.FeaturedWeaponIds));
            Assert.That(featured, Has.Length.EqualTo(16));
            var reference = featured.Take(8).ToArray();
            var realistic = featured.Skip(8).ToArray();
            var categories = new[]
            {
                WeaponCategory.AssaultRifle, WeaponCategory.SubmachineGun, WeaponCategory.Shotgun, WeaponCategory.LightMachineGun,
                WeaponCategory.MarksmanRifle, WeaponCategory.SniperRifle, WeaponCategory.Handgun, WeaponCategory.Launcher,
            };
            Assert.That(reference.Select(w => w.ToSpec().Category), Is.EqualTo(categories));
            Assert.That(realistic.Select(w => w.ToSpec().Category), Is.EqualTo(categories));
            Assert.That(reference.Select(w => w.ToSpec().NameKey), Has.All.StartWith("weapon.ref."));
            Assert.That(realistic.Select(w => w.ToSpec().NameKey), Has.All.StartWith("weapon.real."));
            Assert.That(
                reference.Select(w => w.name),
                Is.EqualTo(new[]
                {
                    "Carbine556", "Pdw", "PumpShotgun", "SquadAutomaticWeapon", "BattleRifle", "MagnumSniper", "CompactPistol", "RocketPropelledGrenade",
                }));
            Assert.That(
                realistic.Select(w => w.name),
                Is.EqualTo(new[]
                {
                    "ServiceRifle", "RollerDelayedSmg", "CombatPumpShotgun", "Gpmg762", "MarksmanRifle762x54", "LongRangeBoltRifle", "Pistol45", "ShoulderFiredRocket",
                }));
        }

        [Test]
        public void ThePlayerStartsWithTheReferenceCarbineAndCompactPistol()
        {
            var weapons = BootstrapSceneBuilder.LoadPlayerWeapons(WeaponAssetBuilder.LoadReferenceWeapons());

            Assert.That(weapons.Select(w => w.Id), Is.EqualTo(BootstrapSceneBuilder.PlayerWeaponIds));
            Assert.That(weapons.Select(w => w.name), Is.EqualTo(new[] { "Carbine556", "CompactPistol" }));
            Assert.That(weapons[0].ToSpec().Category, Is.EqualTo(WeaponCategory.AssaultRifle));
            Assert.That(weapons[1].ToSpec().Category, Is.EqualTo(WeaponCategory.Handgun));
            Assert.That(weapons, Is.Not.EqualTo(TestScenes.ExpectedPlayerWeapons()), "the range no longer starts with the kit's rifle and handgun");
        }

        [Test]
        public void TheWeaponTableHasAFrontRowOfReferenceAndABackRowOfRealistic()
        {
            var positions = BootstrapSceneBuilder.WeaponTablePositions(16);
            var feet = GameBootstrap.PlayerSpawn + (Vector3.down * (GameBootstrap.PlayerHeight / 2f));

            Assert.That(positions, Has.Length.EqualTo(16));
            for (var i = 0; i < 8; i++)
            {
                Assert.That(positions[i].z, Is.EqualTo(feet.z + 2.5f).Within(1e-5f));
                Assert.That(positions[i + 8].z, Is.EqualTo(feet.z + 4f).Within(1e-5f));
                Assert.That(positions[i + 8].x, Is.EqualTo(positions[i].x).Within(1e-5f), "the same category lines up front to back");
                Assert.That(positions[i].x, Is.EqualTo(-7f + (2f * i)).Within(1e-5f));
            }
        }

        [Test]
        public void TheWeaponTableLinesUpLookPickupsJustAheadOfTheSpawn()
        {
            var featured = LoadFeatured();
            var scene = TestScenes.NewAdditive();
            try
            {
                var table = BootstrapSceneBuilder.CreateWeaponTable(scene, featured);

                Assert.That(table.name, Is.EqualTo(BootstrapSceneBuilder.WeaponTableName));
                var points = table.GetComponentsInChildren<PickupSpawnPoint>();
                Assert.That(points.Select(p => p.Contents.Weapon), Is.EqualTo(featured));
                var feet = GameBootstrap.PlayerSpawn + (Vector3.down * (GameBootstrap.PlayerHeight / 2f));
                var positions = BootstrapSceneBuilder.WeaponTablePositions(featured.Length);
                for (var i = 0; i < points.Length; i++)
                {
                    var position = points[i].transform.position;
                    Assert.That(position, Is.EqualTo(positions[i]));
                    Assert.That(points[i].CollectMode, Is.EqualTo(PickupCollectMode.Look));
                    Assert.That(points[i].Contents.Amount, Is.EqualTo(featured[i].ToSpec().MagazineSize));
                    Assert.That(Vector3.Distance(position, feet), Is.GreaterThan(points[i].Radius), $"{points[i].name} is not picked up at the spawn");
                    foreach (var cover in BootstrapSceneBuilder.Covers)
                    {
                        Assert.That(position.z, Is.LessThan(cover.Center.z - (cover.Size.z / 2f)), $"{points[i].name} is in front of {cover.Name}");
                    }
                }
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        [Test]
        public void CreateWeaponTableRejectsAnEmptyWeapon()
        {
            var scene = TestScenes.NewAdditive();
            try
            {
                Assert.Throws<ArgumentNullException>(() => BootstrapSceneBuilder.CreateWeaponTable(scene, null));
                Assert.Throws<ArgumentException>(() => BootstrapSceneBuilder.CreateWeaponTable(scene, new WeaponDefinition[] { null }));
                Assert.That(scene.rootCount, Is.Zero, "nothing is placed when a weapon is empty");
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        [Test]
        public void CreateCoversRejectsAnUnloadedScene()
        {
            Assert.Throws<ArgumentException>(() => BootstrapSceneBuilder.CreateCovers(default));
        }

        private static void AssertCovers(GameObject root)
        {
            Assert.That(root.transform.childCount, Is.EqualTo(2), "one low and one high cover");
            var low = root.transform.Find(BootstrapSceneBuilder.LowCoverName);
            var high = root.transform.Find(BootstrapSceneBuilder.HighCoverName);
            Assert.That(low, Is.Not.Null);
            Assert.That(high, Is.Not.Null);

            foreach (var (box, expected) in new[] { (low, BootstrapSceneBuilder.LowCover), (high, BootstrapSceneBuilder.HighCover) })
            {
                Assert.That(Vector3.Distance(box.position, expected.Center), Is.LessThan(1e-4f), expected.Name);
                Assert.That(Vector3.Distance(box.lossyScale, expected.Size), Is.LessThan(1e-4f), expected.Name);
                var collider = box.GetComponent<BoxCollider>();
                Assert.That(collider, Is.Not.Null, $"{expected.Name} blocks the player and the cover probe");
                Assert.That(collider.isTrigger, Is.False, expected.Name);
                Assert.That(box.gameObject.isStatic, Is.True, expected.Name);
            }

            Assert.That(SurfaceMaterial.IdOf(low.GetComponent<Collider>()), Is.EqualTo(SurfaceImpactAssetStep.ConcreteId));
            Assert.That(SurfaceMaterial.IdOf(high.GetComponent<Collider>()), Is.EqualTo(SurfaceImpactAssetStep.MetalId));
        }

        private static MovingPlatformSettings ElevatorSettings()
        {
            var settings = ScriptableObject.CreateInstance<MovingPlatformSettings>();
            settings.name = "TestElevator";
            BootstrapSceneBuilder.ConfigureElevatorSettings(settings);
            return settings;
        }

        private static void AssertPlatforms(GameObject root, MovingPlatformSettings elevatorSettings)
        {
            var platforms = root.GetComponentsInChildren<MovingPlatform>();
            Assert.That(platforms, Has.Length.EqualTo(2), "one shuttle and one elevator");

            var shuttle = root.transform.Find(BootstrapSceneBuilder.ShuttleName).GetComponent<MovingPlatform>();
            Assert.That(shuttle, Is.Not.Null);
            Assert.That(Vector3.Distance(shuttle.transform.position, BootstrapSceneBuilder.ShuttleOrigin), Is.LessThan(1e-4f));
            Assert.That(shuttle.Settings, Is.Null, "the shuttle uses the kit's default motion");
            Assert.That(MovingPlatformSettings.Resolve(shuttle.Settings).Kind, Is.EqualTo(PlatformRouteKind.PingPong), "the shuttle goes back and forth");
            Assert.That(shuttle.Stops, Is.EqualTo(BootstrapSceneBuilder.ShuttleStops));

            var elevator = root.transform.Find(BootstrapSceneBuilder.ElevatorName).GetComponent<MovingPlatform>();
            Assert.That(elevator, Is.Not.Null);
            Assert.That(Vector3.Distance(elevator.transform.position, BootstrapSceneBuilder.ElevatorOrigin), Is.LessThan(1e-4f));
            Assert.That(elevator.Settings, Is.SameAs(elevatorSettings));
            Assert.That(elevator.Settings.ToSpec().Kind, Is.EqualTo(PlatformRouteKind.OnCall), "the elevator waits for a call");
            Assert.That(elevator.Stops, Is.EqualTo(BootstrapSceneBuilder.ElevatorStops));

            foreach (var platform in platforms)
            {
                var slab = platform.transform.Find(MovingPlatform.SlabName);
                Assert.That(slab, Is.Not.Null, $"{platform.name} carries its slab so it builds no default shape at play");
                Assert.That(slab.GetComponent<BoxCollider>(), Is.Not.Null, $"{platform.name} can be stood on");
                Assert.That(slab.GetComponent<BoxCollider>().isTrigger, Is.False);
                Assert.That(Vector3.Distance(slab.lossyScale, BootstrapSceneBuilder.PlatformSlabSize), Is.LessThan(1e-4f));
                Assert.That(slab.gameObject.isStatic, Is.False, $"{platform.name} moves");
                var body = platform.GetComponent<Rigidbody>();
                Assert.That(body, Is.Not.Null);
                Assert.That(body.isKinematic, Is.True);
            }

            var tower = root.transform.Find(BootstrapSceneBuilder.ElevatorTowerName);
            Assert.That(tower, Is.Not.Null);
            Assert.That(Vector3.Distance(tower.position, BootstrapSceneBuilder.ElevatorTower.Center), Is.LessThan(1e-4f));
            Assert.That(Vector3.Distance(tower.lossyScale, BootstrapSceneBuilder.ElevatorTower.Size), Is.LessThan(1e-4f));
            Assert.That(tower.GetComponent<BoxCollider>().isTrigger, Is.False);

            var zones = root.GetComponentsInChildren<PlatformCallZone>();
            Assert.That(zones, Has.Length.EqualTo(2), "one call zone at the ground and one at the top");
            foreach (var (name, expected, stop) in new[]
                     {
                         (BootstrapSceneBuilder.ElevatorCallBottomName, BootstrapSceneBuilder.ElevatorCallBottom, BootstrapSceneBuilder.ElevatorBottomStop),
                         (BootstrapSceneBuilder.ElevatorCallTopName, BootstrapSceneBuilder.ElevatorCallTop, BootstrapSceneBuilder.ElevatorTopStop),
                     })
            {
                var zone = root.transform.Find(name).GetComponent<PlatformCallZone>();
                Assert.That(zone, Is.Not.Null, name);
                Assert.That(zone.Platform, Is.SameAs(elevator), name);
                Assert.That(zone.Stop, Is.EqualTo(stop), name);
                Assert.That(Vector3.Distance(zone.transform.position, expected.Center), Is.LessThan(1e-4f), name);
                var box = zone.GetComponent<BoxCollider>();
                Assert.That(box, Is.Not.Null, name);
                Assert.That(box.isTrigger, Is.True, $"{name} is walked through");
                Assert.That(Vector3.Distance(box.size, expected.Size), Is.LessThan(1e-4f), name);
            }
        }

        private static WeaponDefinition[] LoadFeatured()
        {
            return BootstrapSceneBuilder.LoadFeaturedWeapons(WeaponAssetBuilder.LoadReferenceWeapons(), WeaponAssetBuilder.LoadRealisticWeapons());
        }
    }
}
