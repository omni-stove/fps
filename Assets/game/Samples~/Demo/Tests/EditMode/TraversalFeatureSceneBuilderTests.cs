using System;
using System.Collections.Generic;
using System.Linq;
using Shooter.Core;
using Shooter.Editor;
using Shooter.Runtime;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using WaterComponent = Shooter.Runtime.Traversal.Water;

namespace Shooter.Tests.EditMode
{
    public sealed class TraversalFeatureSceneBuilderTests
    {
        private const float Epsilon = 1e-4f;

        // 出現位置の周り (プレイヤーの直径の倍ほど) には何も置かない (FeatureSceneBuilderTests と同じ)。
        private const float SpawnClearance = 1.5f;

        // 立ち 2 m のプレイヤーの胸は足元から 1.3 m (SwimStep.ChestY: 中心 1 m と目 1.6 m の間)。
        private const float ChestAboveFeet = 1.3f;

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
        public void ThereIsOneSceneForEachNewMove()
        {
            Assert.That(
                TraversalFeatureSceneBuilder.Features,
                Is.EqualTo(new[] { TraversalFeature.Lean, TraversalFeature.Swim, TraversalFeature.WallRun, TraversalFeature.DoubleJump }));
            Assert.That(TraversalFeatureSceneBuilder.MenuPath, Is.EqualTo("Shooter/Build Traversal Feature Scenes"));
        }

        [Test]
        public void OnlyTheWallRunAndTheDoubleJumpScenesCarryTheirOwnTuning()
        {
            Assert.That(TraversalFeatureSceneBuilder.Features.Where(TraversalFeatureSceneBuilder.UsesTraversalTuning), Is.EqualTo(new[] { TraversalFeature.WallRun }));
            Assert.That(TraversalFeatureSceneBuilder.Features.Where(TraversalFeatureSceneBuilder.UsesMotorSettings), Is.EqualTo(new[] { TraversalFeature.DoubleJump }));
        }

        [Test]
        public void TheKitDefaultsKeepTheWallRunAndTheDoubleJumpOffForOtherScenes()
        {
            Assert.That(WallRunTuning.Default.Enabled, Is.False);
            Assert.That(MotorSpec.Default.AirJumps, Is.EqualTo(0));
            Assert.That(TraversalTuning.Current.WallRun.Enabled, Is.False, "the game-wide traversal.json leaves the wall run off");
            Assert.That(MotorSettings.Resolve(null).AirJumps, Is.EqualTo(0), "the game-wide motor settings leave the double jump off");
        }

        [Test]
        public void OnlyTheSwimSceneTurnsTheMantleOnToClimbOutOfThePool()
        {
            foreach (var feature in TraversalFeatureSceneBuilder.Features)
            {
                var rules = TraversalFeatureSceneBuilder.Rules(feature);
                var expected = feature == TraversalFeature.Swim ? Mantle.On : Mantle.Off;
                Assert.That(rules.Actions.Mantle, Is.EqualTo(expected), feature.ToString());
                Assert.That(rules.Actions.Jump, Is.EqualTo(Jump.On), feature.ToString());
                Assert.That(rules.Actions.Vault, Is.EqualTo(Vault.Off), feature + " climbs only with its own move");
            }
        }

        [Test]
        public void RulesPathsDoNotCollideWithTheOnOffRuleScenes()
        {
            var rulePaths = GameRulesDebugDraft.Fields.Select(GameRulesAssetBuilder.FeatureRulesPath).ToArray();

            foreach (var feature in TraversalFeatureSceneBuilder.Features)
            {
                Assert.That(rulePaths, Has.No.Member(TraversalFeatureSceneBuilder.RulesPath(feature)), feature.ToString());
            }
        }

        [Test]
        public void EachSceneHasOneStationBesideTheSpawn()
        {
            var names = new HashSet<string>();
            foreach (var feature in TraversalFeatureSceneBuilder.Features)
            {
                var station = TraversalFeatureSceneBuilder.Stations(feature).Single();
                Assert.That(station.Placement, Is.EqualTo(FeaturePlacement.BesideSpawn), feature.ToString());
                Assert.That(station.Footprint.min.x, Is.EqualTo(FeatureSceneBuilder.BesideSpawnMinX).Within(Epsilon), feature.ToString());
                Assert.That(station.Footprint.min.z, Is.EqualTo(FeatureSceneBuilder.BesideSpawnMinZ).Within(Epsilon), feature.ToString());
                Assert.That(station.Footprint.min.y, Is.EqualTo(0f).Within(Epsilon), feature + " stands on the floor");
                Assert.That(names.Add(station.Fixture.Name), Is.True, feature + " has its own name");
            }
        }

        [Test]
        public void StationsKeepClearOfTheSpawnAndTheTargetDummies()
        {
            var spawn = new Bounds(Vector3.zero, new Vector3(SpawnClearance * 2f, 1f, SpawnClearance * 2f));
            var dummies = GameBootstrap.TargetDummyPositions(Vector3.zero, Quaternion.identity);

            foreach (var feature in TraversalFeatureSceneBuilder.Features)
            {
                var footprint = Flat(TraversalFeatureSceneBuilder.Stations(feature).Single().Footprint);
                Assert.That(footprint.Intersects(spawn), Is.False, feature + " is on the spawn");
                foreach (var dummy in dummies)
                {
                    Assert.That(footprint.Intersects(new Bounds(dummy, Vector3.one)), Is.False, $"{feature} is on the dummy at {dummy}");
                }
            }
        }

        [Test]
        public void TheFloorCoversEachStationWithAMargin()
        {
            foreach (var feature in TraversalFeatureSceneBuilder.Features)
            {
                var stations = TraversalFeatureSceneBuilder.Stations(feature);
                var half = FeatureSceneBuilder.FloorHalfExtent(stations);
                var footprint = stations.Single().Footprint;
                Assert.That(Mathf.Max(Mathf.Abs(footprint.min.x), Mathf.Abs(footprint.max.x)) + FeatureSceneBuilder.FloorMargin, Is.LessThanOrEqualTo(half), feature.ToString());
                Assert.That(Mathf.Max(Mathf.Abs(footprint.min.z), Mathf.Abs(footprint.max.z)) + FeatureSceneBuilder.FloorMargin, Is.LessThanOrEqualTo(half), feature.ToString());
            }
        }

        [Test]
        public void TheFixtureIsNotPickedUpByTheTpsRange()
        {
            var names = TpsRangeSceneBuilder.Fixtures().Select(f => f.GetType().Name).ToArray();

            Assert.That(names, Has.No.Member("TraversalFeatureFixture"));
        }

        [Test]
        public void LeanSceneHasACornerToPeekAroundAndALowWallToCrouchBehind()
        {
            var bootstrap = Populate(TraversalFeature.Lean);

            var corner = Find(TraversalFeatureSceneBuilder.LeanCornerName).transform;
            Assert.That(corner.Find(TraversalFeatureSceneBuilder.CornerFrontWallName), Is.Not.Null);
            Assert.That(corner.Find(TraversalFeatureSceneBuilder.CornerSideWallName), Is.Not.Null);
            var low = corner.Find(TraversalFeatureSceneBuilder.LowWallName);
            Assert.That(low.localScale.y, Is.EqualTo(TraversalFeatureSceneBuilder.LowWallHeight));
            Assert.That(TraversalFeatureSceneBuilder.CornerWallHeight, Is.GreaterThan(GameBootstrap.PlayerHeight), "the corner hides a standing player");
            Assert.That(TraversalFeatureSceneBuilder.LowWallHeight, Is.LessThan(MotorSpec.DefaultCoverLowWallHeight));
            Assert.That(TraversalFeatureSceneBuilder.LowWallHeight, Is.GreaterThan(PlayerController.DefaultCrouchHeight / 2f), "hides the crouched body");
            Assert.That(bootstrap.MotorSettings, Is.Null);
            Assert.That(bootstrap.TraversalTuningJson, Is.Null);
        }

        [Test]
        public void SwimSceneHasShallowAndDeepWaterAndAPoolWithAHighRim()
        {
            var station = TraversalFeatureSceneBuilder.Stations(TraversalFeature.Swim).Single();
            Populate(TraversalFeature.Swim);

            var pools = Find(TraversalFeatureSceneBuilder.SwimPoolsName);
            var waters = pools.GetComponentsInChildren<WaterComponent>().ToDictionary(w => w.name);
            Assert.That(waters.Keys, Is.EquivalentTo(new[]
            {
                TraversalFeatureSceneBuilder.ShallowWaterName,
                TraversalFeatureSceneBuilder.DeepWaterName,
                TraversalFeatureSceneBuilder.HighRimWaterName,
            }));

            var shallow = waters[TraversalFeatureSceneBuilder.ShallowWaterName];
            var deep = waters[TraversalFeatureSceneBuilder.DeepWaterName];
            var highRim = waters[TraversalFeatureSceneBuilder.HighRimWaterName];
            Assert.That(shallow.SurfaceHeight, Is.LessThan(ChestAboveFeet), "wading, not swimming");
            Assert.That(deep.SurfaceHeight, Is.GreaterThan(GameBootstrap.PlayerHeight), "over the head");
            Assert.That(highRim.SurfaceHeight, Is.EqualTo(deep.SurfaceHeight));
            foreach (var water in waters.Values)
            {
                Assert.That(water.SurfaceHeight, Is.LessThanOrEqualTo(water.Size.y), water.name);
                Assert.That(water.TryGetBox(out _), Is.True, water.name);
            }

            var deepBottom = TraversalFeatureSceneBuilder.Waters(TraversalFeature.Swim).Single(w => w.Name == TraversalFeatureSceneBuilder.DeepWaterName).Bottom;
            Assert.That(Vector3.Distance(deep.transform.position, deepBottom + station.Offset), Is.LessThan(Epsilon), "moved with the station");

            // 浮いている足元は水面から胸の高さだけ下。縁はジャンプでは届かず、崖のぼりで届く
            var rimAboveFloatingFeet = TraversalFeatureSceneBuilder.HighRimHeight - (TraversalFeatureSceneBuilder.DeepSurfaceHeight - ChestAboveFeet);
            Assert.That(rimAboveFloatingFeet, Is.GreaterThan(MotorSpec.DefaultJumpHeight));
            Assert.That(rimAboveFloatingFeet, Is.LessThanOrEqualTo(MotorSpec.DefaultMantleMaxHeight));
            Assert.That(TraversalFeatureSceneBuilder.HighRimHeight, Is.GreaterThan(MotorSpec.DefaultMantleMaxHeight), "no climbing in from the floor");
            Assert.That(TraversalFeatureSceneBuilder.DividerThickness, Is.GreaterThanOrEqualTo(GameBootstrap.PlayerRadius * 2f), "room to stand on the divider");
            Assert.That(pools.transform.Find(TraversalFeatureSceneBuilder.DividerName).localScale.y, Is.EqualTo(TraversalFeatureSceneBuilder.HighRimHeight));
        }

        [Test]
        public void WallRunSceneWiresATraversalJsonThatTurnsOnlyTheWallRunOn()
        {
            var traversal = NewText(TraversalFeatureSceneBuilder.TraversalTuningJson());

            var bootstrap = Populate(TraversalFeature.WallRun, traversal: traversal);

            Assert.That(bootstrap.TraversalTuningJson, Is.SameAs(traversal));
            Assert.That(bootstrap.MotorSettings, Is.Null);
            var spec = TraversalJson.Parse(traversal.text, "test");
            Assert.That(spec.WallRun.Enabled, Is.True);
            Assert.That(spec.WallRun.MaxSeconds, Is.EqualTo(WallRunTuning.DefaultMaxSeconds));
            Assert.That(spec.WallRun.MinSpeed, Is.EqualTo(WallRunTuning.DefaultMinSpeed));
            Assert.That(spec.Swim.Speed, Is.EqualTo(SwimTuning.DefaultSpeed));

            var corridor = Find(TraversalFeatureSceneBuilder.WallRunCorridorName).transform;
            var left = corridor.Find(TraversalFeatureSceneBuilder.LeftRunWallName);
            var right = corridor.Find(TraversalFeatureSceneBuilder.RightRunWallName);
            var gap = (right.position.x - (right.localScale.x / 2f)) - (left.position.x + (left.localScale.x / 2f));
            Assert.That(gap, Is.EqualTo(TraversalFeatureSceneBuilder.CorridorWidth).Within(Epsilon));
            Assert.That(gap, Is.GreaterThan(GameBootstrap.PlayerRadius * 2f));
            Assert.That(left.localScale.z, Is.GreaterThanOrEqualTo(MotorSpec.DefaultSprintSpeed * WallRunTuning.DefaultMaxSeconds), "a whole run fits along the wall");
            Assert.That(left.localScale.y, Is.GreaterThan(GameBootstrap.PlayerHeight + MotorSpec.DefaultJumpHeight));
        }

        [Test]
        public void DoubleJumpSceneWiresMotorSettingsWithOneAirJumpAndStepsTooHighForOneJump()
        {
            var settings = NewMotorSettings();
            TraversalFeatureSceneBuilder.ConfigureDoubleJump(settings, null);

            var bootstrap = Populate(TraversalFeature.DoubleJump, settings);

            Assert.That(bootstrap.MotorSettings, Is.SameAs(settings));
            Assert.That(bootstrap.TraversalTuningJson, Is.Null);
            Assert.That(settings.CreateSpec().AirJumps, Is.EqualTo(TraversalFeatureSceneBuilder.DoubleJumpAirJumps));

            var steps = Find(TraversalFeatureSceneBuilder.DoubleJumpStepsName).transform;
            Assert.That(steps.childCount, Is.EqualTo(TraversalFeatureSceneBuilder.StepCount));
            for (var i = 0; i < TraversalFeatureSceneBuilder.StepCount; i++)
            {
                var step = steps.Find(TraversalFeatureSceneBuilder.StepNamePrefix + (i + 1));
                Assert.That(step.localScale.y, Is.EqualTo(TraversalFeatureSceneBuilder.StepRise * (i + 1)).Within(Epsilon), step.name);
            }

            var airJumpHeight = MotorSpec.DefaultAirJumpSpeed * MotorSpec.DefaultAirJumpSpeed / (2f * MotorSpec.DefaultGravity);
            Assert.That(TraversalFeatureSceneBuilder.StepRise, Is.GreaterThan(MotorSpec.DefaultJumpHeight), "one jump falls short");
            Assert.That(TraversalFeatureSceneBuilder.StepRise, Is.LessThan(MotorSpec.DefaultJumpHeight + airJumpHeight), "a double jump makes it");
        }

        [Test]
        public void ConfigureDoubleJumpKeepsTheGameWideValuesAndTheName()
        {
            var source = NewMotorSettings();
            source.WalkSpeed = 3f;
            source.JumpHeight = 1.5f;
            var settings = NewMotorSettings();
            settings.name = "Feature_DoubleJump";

            TraversalFeatureSceneBuilder.ConfigureDoubleJump(settings, source);

            Assert.That(settings.name, Is.EqualTo("Feature_DoubleJump"));
            Assert.That(settings.WalkSpeed, Is.EqualTo(3f));
            Assert.That(settings.JumpHeight, Is.EqualTo(1.5f));
            Assert.That(settings.AirJumps, Is.EqualTo(TraversalFeatureSceneBuilder.DoubleJumpAirJumps));
            Assert.That(source.AirJumps, Is.EqualTo(MotorSpec.DefaultAirJumps), "the game-wide settings stay as they are");
        }

        [Test]
        public void ConfigureDoubleJumpRejectsBrokenSourceValues()
        {
            var source = NewMotorSettings();
            source.Gravity = -1f;

            Assert.Throws<ArgumentOutOfRangeException>(() => TraversalFeatureSceneBuilder.ConfigureDoubleJump(NewMotorSettings(), source));
            Assert.Throws<ArgumentNullException>(() => TraversalFeatureSceneBuilder.ConfigureDoubleJump(null, source));
        }

        [Test]
        public void TheScenesThatNeedTuningRejectMissingTuningAndLeaveTheSceneEmpty()
        {
            Assert.Throws<ArgumentNullException>(() => TraversalFeatureSceneBuilder.PopulateScene(
                _scene, NewWeapons(), NewRules(TraversalFeature.DoubleJump), TraversalFeature.DoubleJump, null, null));
            Assert.Throws<ArgumentNullException>(() => TraversalFeatureSceneBuilder.PopulateScene(
                _scene, NewWeapons(), NewRules(TraversalFeature.WallRun), TraversalFeature.WallRun, null, null));
            Assert.That(_scene.rootCount, Is.EqualTo(0));
        }

        [Test]
        public void UndefinedFeaturesAreRejected()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => TraversalFeatureSceneBuilder.Stations((TraversalFeature)99));
            Assert.Throws<ArgumentOutOfRangeException>(() => TraversalFeatureSceneBuilder.Rules((TraversalFeature)99));
            Assert.Throws<ArgumentOutOfRangeException>(() => TraversalFeatureSceneBuilder.Boxes((TraversalFeature)99));
        }

        private GameBootstrap Populate(TraversalFeature feature, MotorSettings motorSettings = null, TextAsset traversal = null)
        {
            var bootstrap = TraversalFeatureSceneBuilder.PopulateScene(
                _scene, NewWeapons(), NewRules(feature), feature, motorSettings, traversal);

            var names = _scene.GetRootGameObjects().Select(o => o.name).ToArray();
            Assert.That(names, Is.EquivalentTo(new[]
            {
                FeatureSceneBuilder.BootstrapObjectName,
                FeatureSceneBuilder.FloorName,
                TraversalFeatureSceneBuilder.Stations(feature).Single().Fixture.Name,
            }));
            Assert.That(bootstrap.CreatesFloor, Is.False);
            return bootstrap;
        }

        private static Bounds Flat(Bounds bounds)
        {
            return new Bounds(new Vector3(bounds.center.x, 0f, bounds.center.z), new Vector3(bounds.size.x, 1f, bounds.size.z));
        }

        private WeaponDefinition[] NewWeapons()
        {
            var weapon = ScriptableObject.CreateInstance<WeaponDefinition>();
            weapon.name = "Rifle";
            _assets.Add(weapon);
            WeaponAssetBuilder.Configure(weapon, WeaponAssetBuilder.RifleSpec());
            return new[] { weapon };
        }

        private GameRulesDefinition NewRules(TraversalFeature feature)
        {
            var definition = ScriptableObject.CreateInstance<GameRulesDefinition>();
            definition.name = "Rules";
            _assets.Add(definition);
            GameRulesAssetBuilder.Configure(definition, TraversalFeatureSceneBuilder.Rules(feature));
            return definition;
        }

        private MotorSettings NewMotorSettings()
        {
            var settings = ScriptableObject.CreateInstance<MotorSettings>();
            _assets.Add(settings);
            return settings;
        }

        private TextAsset NewText(string text)
        {
            var asset = new TextAsset(text) { name = "Traversal" };
            _assets.Add(asset);
            return asset;
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
    }
}
