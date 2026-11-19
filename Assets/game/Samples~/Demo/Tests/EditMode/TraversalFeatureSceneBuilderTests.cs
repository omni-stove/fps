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
        public void TheFourMovesAreLinedUpInThePlayground()
        {
            Assert.That(
                TraversalFeatureSceneBuilder.Features,
                Is.EqualTo(new[] { TraversalFeature.Lean, TraversalFeature.Swim, TraversalFeature.WallRun, TraversalFeature.DoubleJump }));

            var names = FeatureSceneBuilder.PlaygroundStations().Select(s => s.Fixture.Name).ToArray();
            foreach (var feature in TraversalFeatureSceneBuilder.Features)
            {
                Assert.That(names, Has.Member(TraversalFeatureSceneBuilder.Fixture(feature).Name), feature.ToString());
            }
        }

        [Test]
        public void TheTuningIsNamedAfterThePlayground()
        {
            Assert.That(TraversalFeatureSceneBuilder.MotorSettingsPath, Is.EqualTo(DemoSample.Root + "/Movement/Playground.asset"));
            Assert.That(TraversalFeatureSceneBuilder.TraversalTuningPath, Is.EqualTo(DemoSample.Root + "/Tuning/Playground.json"));
            Assert.That(TraversalFeatureSceneBuilder.LegacyAssetPaths, Is.EqualTo(new[]
            {
                DemoSample.Root + "/Movement/Feature_DoubleJump.asset",
                DemoSample.Root + "/Tuning/Feature_WallRun.json",
            }));
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
        public void PlaygroundRulesLetThePlayerJumpAndClimbOutOfThePool()
        {
            var rules = GameRulesAssetBuilder.PlaygroundRules();

            Assert.That(rules.Actions.Jump, Is.EqualTo(Jump.On));
            Assert.That(rules.Actions.Mantle, Is.EqualTo(Mantle.On), "climbing out of water is a mantle");
        }

        [Test]
        public void TheFixturesAreNotPickedUpByTheTpsRange()
        {
            var names = TpsRangeSceneBuilder.Fixtures().Select(f => f.GetType().Name).ToArray();

            Assert.That(names, Has.No.Member("TraversalFeatureFixture"));
        }

        [Test]
        public void LeanSceneHasACornerToPeekAroundAndALowWallToCrouchBehind()
        {
            Populate();

            var corner = Find(TraversalFeatureSceneBuilder.LeanCornerName).transform;
            Assert.That(corner.Find(TraversalFeatureSceneBuilder.CornerFrontWallName), Is.Not.Null);
            Assert.That(corner.Find(TraversalFeatureSceneBuilder.CornerSideWallName), Is.Not.Null);
            var low = corner.Find(TraversalFeatureSceneBuilder.LowWallName);
            Assert.That(low.localScale.y, Is.EqualTo(TraversalFeatureSceneBuilder.LowWallHeight));
            Assert.That(TraversalFeatureSceneBuilder.CornerWallHeight, Is.GreaterThan(GameBootstrap.PlayerHeight), "the corner hides a standing player");
            Assert.That(TraversalFeatureSceneBuilder.LowWallHeight, Is.LessThan(MotorSpec.DefaultCoverLowWallHeight));
            Assert.That(TraversalFeatureSceneBuilder.LowWallHeight, Is.GreaterThan(PlayerController.DefaultCrouchHeight / 2f), "hides the crouched body");
        }

        [Test]
        public void SwimSceneHasShallowAndDeepWaterAndAPoolWithAHighRim()
        {
            var station = StationOf(TraversalFeatureSceneBuilder.SwimPoolsName);
            Populate();

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
        public void PlaygroundTraversalJsonTurnsOnlyTheWallRunOnBesideACorridor()
        {
            var traversal = NewText(TraversalFeatureSceneBuilder.TraversalTuningJson());

            Populate();

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
        public void PlaygroundMotorSettingsHaveOneAirJumpAndStepsTooHighForOneJump()
        {
            var settings = NewMotorSettings();
            TraversalFeatureSceneBuilder.ConfigureDoubleJump(settings, null);

            Populate();

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
        public void UndefinedFeaturesAreRejected()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => TraversalFeatureSceneBuilder.Fixture((TraversalFeature)99));
            Assert.Throws<ArgumentOutOfRangeException>(() => TraversalFeatureSceneBuilder.Boxes((TraversalFeature)99));
            Assert.Throws<ArgumentOutOfRangeException>(() => TraversalFeatureSceneBuilder.Waters((TraversalFeature)99));
        }

        private static FeatureStation StationOf(string name)
        {
            return FeatureSceneBuilder.PlaygroundStations().Single(s => s.Fixture.Name == name);
        }

        private void Populate()
        {
            var feature = FeatureSceneBuilder.Playground();
            var bootstrap = FeatureSceneBuilder.PopulateScene(_scene, NewWeapons(), NewRules(), feature);
            Assert.That(bootstrap.CreatesFloor, Is.False);
        }

        private WeaponDefinition[] NewWeapons()
        {
            var weapon = ScriptableObject.CreateInstance<WeaponDefinition>();
            weapon.name = "Rifle";
            _assets.Add(weapon);
            WeaponAssetBuilder.Configure(weapon, WeaponAssetBuilder.RifleSpec());
            return new[] { weapon };
        }

        private GameRulesDefinition NewRules()
        {
            var definition = ScriptableObject.CreateInstance<GameRulesDefinition>();
            definition.name = "Rules";
            _assets.Add(definition);
            GameRulesAssetBuilder.Configure(definition, GameRulesAssetBuilder.PlaygroundRules());
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
