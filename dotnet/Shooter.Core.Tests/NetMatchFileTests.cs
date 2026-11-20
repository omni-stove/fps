using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using NUnit.Framework;

namespace Shooter.Core.Tests
{
    public sealed class NetMatchFileTests
    {
        private static readonly string[] _expectedBeacon = { "beacon" };
        private static readonly string[] _repeatedBeacon = { "beacon", "beacon" };

        private const string _rifleId = "6c1f3b2a-8d4e-4f7a-9b0c-2e5d7a1f4c83";
        private const string _handgunId = "b4e2a9d7-3c16-4a58-8f2e-7d0b9c6a1e45";

        private const string _handgunJson = "{\"id\": \"" + _handgunId + "\", \"category\": \"Handgun\", \"fireMode\": \"Semi\", \"magazineSize\": 12,"
            + " \"fireInterval\": 0.2, \"reloadSeconds\": 1.5, \"damage\": 34, \"range\": 60}";

        private static WeaponSpec Rifle() => new WeaponSpec(
            _rifleId, "weapon.rifle", WeaponCategory.AssaultRifle, FireMode.Auto, 30, 90, 0.1f, 2f, 25, 100f,
            new DamageFalloff(20f, 60f, 0.5f), adsFieldOfView: 45f, recoilPitchDegrees: 0.5f, shotNoiseRadius: 12f);

        private static WeaponSpec Handgun() => new WeaponSpec(
            _handgunId, "weapon.handgun", WeaponCategory.Handgun, FireMode.Semi, 12, 48, 0.2f, 1.5f, 34, 60f, DamageFalloff.None);

        private static NetMatchFile File(GameRules rules = null, WeaponCatalog catalog = null)
        {
            return new NetMatchFile(
                "Domination",
                rules ?? GameRules.Default.WithObjective(MatchObjective.Domination),
                new NetMatchSettings(scoreLimit: 12, timeLimitSeconds: 240f, captureSeconds: 6.5f, maxCapturers: 2),
                "DefaultLoadout",
                new LoadoutRule(new[] { new[] { WeaponCategory.AssaultRifle }, new[] { WeaponCategory.Handgun } }),
                new[] { Rifle(), Handgun() },
                0.4f,
                catalog == null ? null : "WeaponCatalog",
                catalog);
        }

        [Test]
        public void WrittenFileReadsBackTheSameValues()
        {
            var original = File(catalog: new WeaponCatalog(new[] { Rifle(), Handgun() }));

            var text = NetMatchFile.Write(original);
            var read = NetMatchFile.Parse(text, "Domination.json");

            Assert.That(read.Name, Is.EqualTo("Domination"));
            Assert.That(read.Rules.Objective, Is.EqualTo(MatchObjective.Domination));
            Assert.That(GameRulesJson.Write(read.Rules), Is.EqualTo(GameRulesJson.Write(original.Rules)));
            Assert.That(read.Settings.ScoreLimit, Is.EqualTo(12));
            Assert.That(read.Settings.TimeLimitSeconds, Is.EqualTo(240f));
            Assert.That(read.Settings.CaptureSeconds, Is.EqualTo(6.5f));
            Assert.That(read.Settings.MaxCapturers, Is.EqualTo(2));
            Assert.That(read.Settings.RestartDelaySeconds, Is.EqualTo(NetMatchSettings.DefaultRestartDelaySeconds));
            Assert.That(read.LoadoutName, Is.EqualTo("DefaultLoadout"));
            Assert.That(read.SwitchSeconds, Is.EqualTo(0.4f));
            Assert.That(read.LoadoutRule.SlotCount, Is.EqualTo(2));
            Assert.That(read.LoadoutWeapons[0].Id, Is.EqualTo(_rifleId));
            Assert.That(read.LoadoutWeapons[0].Falloff.StartDistance, Is.EqualTo(20f));
            Assert.That(read.LoadoutWeapons[0].AdsFieldOfView, Is.EqualTo(45f));
            Assert.That(read.LoadoutWeapons[0].RecoilPitchDegrees, Is.EqualTo(0.5f));
            Assert.That(read.LoadoutWeapons[0].ShotNoiseRadius, Is.EqualTo(12f));
            Assert.That(read.LoadoutWeapons[1].Falloff.StartDistance, Is.EqualTo(DamageFalloff.None.StartDistance));
            Assert.That(read.CatalogName, Is.EqualTo("WeaponCatalog"));
            Assert.That(read.Catalog.Count, Is.EqualTo(2));
            Assert.That(NetMatchFile.Write(read), Is.EqualTo(text));
        }

        [Test]
        public void MotorValuesReadBackAndDefaultWhenLeftOut()
        {
            var motor = new NetMotorSettings(
                new MotorSpec(
                    6f, 9f, 1.5f, 18f, 0.2f, 3f, dodgeDistance: 4f,
                    walkDirection: new MoveDirectionScale(1f, 0.7f, 0.85f), sprintDirection: new MoveDirectionScale(1f, 0.5f, 0.6f)),
                new StanceGeometry(1.8f, 0.9f, 1.6f, 1.1f, 0.4f),
                new CapsuleMoverSpec(50f, 0.35f, 0.05f, CapsuleMoverSpec.DefaultLayerMask));
            var original = new NetMatchFile(
                "TeamDeathmatch", GameRules.Default, NetMatchSettings.Default, "DefaultLoadout",
                new LoadoutRule(new[] { new[] { WeaponCategory.AssaultRifle }, new[] { WeaponCategory.Handgun } }),
                new[] { Rifle(), Handgun() }, 0.4f, null, null, motor);

            var read = NetMatchFile.Parse(NetMatchFile.Write(original), "motor.json").Motor;

            Assert.That(read.Motor.WalkSpeed, Is.EqualTo(6f));
            Assert.That(read.Motor.SprintSpeed, Is.EqualTo(9f));
            Assert.That(read.Motor.DodgeDistance, Is.EqualTo(4f));
            Assert.That(read.Motor.WalkDirection.Forward, Is.EqualTo(1f));
            Assert.That(read.Motor.WalkDirection.Backward, Is.EqualTo(0.7f));
            Assert.That(read.Motor.WalkDirection.Side, Is.EqualTo(0.85f));
            Assert.That(read.Motor.SprintDirection.Backward, Is.EqualTo(0.5f));
            Assert.That(read.Motor.SprintDirection.Side, Is.EqualTo(0.6f));
            Assert.That(read.Stance.Standing.Height, Is.EqualTo(1.8f));
            Assert.That(read.Stance.Standing.CenterY, Is.EqualTo(0.9f));
            Assert.That(read.Stance.Standing.EyeY, Is.EqualTo(1.6f));
            Assert.That(read.Stance.Crouched.Height, Is.EqualTo(1.1f));
            Assert.That(read.Stance.Radius, Is.EqualTo(0.4f));
            Assert.That(read.Mover.SlopeLimitDegrees, Is.EqualTo(50f));
            Assert.That(read.Mover.StepOffset, Is.EqualTo(0.35f));
            Assert.That(read.Mover.SkinWidth, Is.EqualTo(0.05f));

            var bare = NetMatchFile.Parse(
                "{\"loadout\": {\"slots\": [[\"Handgun\"]], \"weapons\": [\"" + _handgunId + "\"]}, \"weapons\": [" + _handgunJson + "]}", "bare.json");
            Assert.That(bare.Motor, Is.SameAs(NetMotorSettings.Default));
        }

        [Test]
        public void ProneValuesReadBackAndDefaultWhenLeftOut()
        {
            var bareText = "{\"loadout\": {\"slots\": [[\"Handgun\"]], \"weapons\": [\"" + _handgunId + "\"]}, \"weapons\": [" + _handgunJson + "]";
            var read = NetMatchFile.Parse(
                bareText + ", \"motor\": {\"proneHeight\": 0.9, \"proneSpeed\": 0.8, \"diveDistance\": 3, \"diveSeconds\": 0.6}}", "prone.json").Motor;

            Assert.That(read.Stance.Prone.Height, Is.EqualTo(1f), "fitted to the capsule's lowest height (twice the radius)");
            Assert.That(read.Prone.Speed, Is.EqualTo(0.8f));
            Assert.That(read.Prone.DiveDistance, Is.EqualTo(3f));
            Assert.That(read.Prone.DiveSeconds, Is.EqualTo(0.6f));

            var defaults = NetMatchFile.Parse(bareText + ", \"motor\": {\"walkSpeed\": 5}}", "defaults.json").Motor;
            Assert.That(defaults.Stance.Prone.Height, Is.EqualTo(ProneSpec.DefaultHeight));
            Assert.That(defaults.Prone.Speed, Is.EqualTo(ProneSpec.DefaultSpeed));
            Assert.That(NetMotorSettings.Default.Stance.Prone.Height, Is.EqualTo(ProneSpec.DefaultHeight));
        }

        [Test]
        public void LeanAirJumpSwimAndWallRunReadBack()
        {
            var motor = new NetMotorSettings(
                new MotorSpec(6f, 9f, 1.5f, 18f, 0.2f, 3f, leanSideOffset: 0.5f, leanAngleDegrees: 22f, leanSpeed: 5f, airJumps: 1, airJumpSpeed: 6f, jumpGraceSeconds: 0.15f),
                new StanceGeometry(1.8f, 0.9f, 1.6f, 1.1f, 0.4f),
                CapsuleMoverSpec.Default,
                ProneSpec.Default,
                new SwimTuning(2.5f, 10f, 25f),
                new WallRunTuning(true, 1.8f, 4.5f, 0.3f, 7.5f));
            var original = new NetMatchFile(
                "TeamDeathmatch", GameRules.Default, NetMatchSettings.Default, "DefaultLoadout",
                new LoadoutRule(new[] { new[] { WeaponCategory.AssaultRifle }, new[] { WeaponCategory.Handgun } }),
                new[] { Rifle(), Handgun() }, 0.4f, null, null, motor);

            var text = NetMatchFile.Write(original);
            var read = NetMatchFile.Parse(text, "traversal.json").Motor;

            Assert.That(read.Motor.LeanSideOffset, Is.EqualTo(0.5f));
            Assert.That(read.Motor.LeanAngleDegrees, Is.EqualTo(22f));
            Assert.That(read.Motor.LeanSpeed, Is.EqualTo(5f));
            Assert.That(read.Motor.AirJumps, Is.EqualTo(1));
            Assert.That(read.Motor.AirJumpSpeed, Is.EqualTo(6f));
            Assert.That(read.Motor.JumpGraceSeconds, Is.EqualTo(0.15f));
            Assert.That(read.Swim.Speed, Is.EqualTo(2.5f));
            Assert.That(read.Swim.Buoyancy, Is.EqualTo(10f));
            Assert.That(read.Swim.BreathSeconds, Is.EqualTo(25f));
            Assert.That(read.WallRun.Enabled, Is.True);
            Assert.That(read.WallRun.MaxSeconds, Is.EqualTo(1.8f));
            Assert.That(read.WallRun.MinSpeed, Is.EqualTo(4.5f));
            Assert.That(read.WallRun.GravityScale, Is.EqualTo(0.3f));
            Assert.That(read.WallRun.JumpOffSpeed, Is.EqualTo(7.5f));
            Assert.That(text, Does.Contain("\"airJumps\": 1,"), "the jump count is written as a whole number");
            Assert.That(text, Does.Contain("\"wallRunEnabled\": true,"));
        }

        [Test]
        public void LeanAirJumpSwimAndWallRunDefaultWhenLeftOut()
        {
            var bareText = "{\"loadout\": {\"slots\": [[\"Handgun\"]], \"weapons\": [\"" + _handgunId + "\"]}, \"weapons\": [" + _handgunJson + "]";
            var read = NetMatchFile.Parse(bareText + ", \"motor\": {\"airJumps\": 2, \"swimSpeed\": 4, \"wallRunEnabled\": true}}", "partial.json").Motor;

            Assert.That(read.Motor.AirJumps, Is.EqualTo(2));
            Assert.That(read.Motor.AirJumpSpeed, Is.EqualTo(MotorSpec.DefaultAirJumpSpeed));
            Assert.That(read.Motor.LeanSideOffset, Is.EqualTo(MotorSpec.DefaultLeanSideOffset));
            Assert.That(read.Motor.LeanAngleDegrees, Is.EqualTo(MotorSpec.DefaultLeanAngleDegrees));
            Assert.That(read.Motor.LeanSpeed, Is.EqualTo(MotorSpec.DefaultLeanSpeed));
            Assert.That(read.Swim.Speed, Is.EqualTo(4f));
            Assert.That(read.Swim.Buoyancy, Is.EqualTo(SwimTuning.DefaultBuoyancy));
            Assert.That(read.Swim.BreathSeconds, Is.EqualTo(SwimTuning.DefaultBreathSeconds));
            Assert.That(read.WallRun.Enabled, Is.True);
            Assert.That(read.WallRun.MaxSeconds, Is.EqualTo(WallRunTuning.DefaultMaxSeconds));
            Assert.That(read.WallRun.MinSpeed, Is.EqualTo(WallRunTuning.DefaultMinSpeed));
            Assert.That(read.WallRun.GravityScale, Is.EqualTo(WallRunTuning.DefaultGravityScale));
            Assert.That(read.WallRun.JumpOffSpeed, Is.EqualTo(WallRunTuning.DefaultJumpOffSpeed));

            var defaults = NetMatchFile.Parse(bareText + ", \"motor\": {\"walkSpeed\": 5}}", "defaults.json").Motor;
            Assert.That(defaults.Motor.AirJumps, Is.EqualTo(MotorSpec.DefaultAirJumps));
            Assert.That(defaults.Motor.JumpGraceSeconds, Is.EqualTo(MotorSpec.DefaultJumpGraceSeconds));
            Assert.That(defaults.Swim.Speed, Is.EqualTo(SwimTuning.DefaultSpeed));
            Assert.That(defaults.WallRun.Enabled, Is.EqualTo(WallRunTuning.DefaultEnabled));
            Assert.That(NetMotorSettings.Default.Swim, Is.SameAs(SwimTuning.Default));
            Assert.That(NetMotorSettings.Default.WallRun, Is.SameAs(WallRunTuning.Default));
        }

        [Test]
        public void TheParachuteTuningReadsBackAndDefaultsWhenLeftOut()
        {
            var defaults = NetMotorSettings.Default;
            var motor = new NetMotorSettings(
                defaults.Motor, defaults.Stance, defaults.Mover, defaults.Prone, defaults.Swim, defaults.WallRun, new ParachuteTuning(12f, 3f, 7f, 9f));
            var original = new NetMatchFile(
                "TeamDeathmatch", GameRules.Default, NetMatchSettings.Default, "DefaultLoadout",
                new LoadoutRule(new[] { new[] { WeaponCategory.AssaultRifle }, new[] { WeaponCategory.Handgun } }),
                new[] { Rifle(), Handgun() }, 0.4f, null, null, motor);

            var text = NetMatchFile.Write(original);
            var read = NetMatchFile.Parse(text, "parachute.json").Motor.Parachute;

            Assert.That(read.MinOpenHeight, Is.EqualTo(12f));
            Assert.That(read.DescentSpeed, Is.EqualTo(3f));
            Assert.That(read.HorizontalSpeed, Is.EqualTo(7f));
            Assert.That(read.SteerAcceleration, Is.EqualTo(9f));
            Assert.That(text, Does.Contain("\"parachuteMinOpenHeight\": 12"));

            var bareText = "{\"loadout\": {\"slots\": [[\"Handgun\"]], \"weapons\": [\"" + _handgunId + "\"]}, \"weapons\": [" + _handgunJson + "]";
            var partial = NetMatchFile.Parse(bareText + ", \"motor\": {\"parachuteDescentSpeed\": 2.5}}", "partial.json").Motor.Parachute;

            Assert.That(partial.DescentSpeed, Is.EqualTo(2.5f));
            Assert.That(partial.MinOpenHeight, Is.EqualTo(ParachuteMotion.DefaultMinOpenHeight));
            Assert.That(partial.HorizontalSpeed, Is.EqualTo(ParachuteMotion.DefaultHorizontalSpeed));
            Assert.That(partial.SteerAcceleration, Is.EqualTo(ParachuteMotion.DefaultSteerAcceleration));
            Assert.That(NetMotorSettings.Default.Parachute, Is.SameAs(ParachuteTuning.Default));
        }

        [Test]
        public void CarryLoadReadsBackAndDefaultsWhenLeftOut()
        {
            var carry = new CarryLoadSpec(CarrySource.Carried, CarrySource.None, 5f, 0.04f, 0.6f, CarryGaits.Walk | CarryGaits.Sprint);
            var motor = new NetMotorSettings(
                new MotorSpec(6f, 9f, 1.5f, 18f, 0.2f, 3f, carryLoad: carry),
                new StanceGeometry(1.8f, 0.9f, 1.6f, 1.1f, 0.4f),
                new CapsuleMoverSpec(50f, 0.35f, 0.05f, CapsuleMoverSpec.DefaultLayerMask));
            var original = new NetMatchFile(
                "TeamDeathmatch", GameRules.Default, NetMatchSettings.Default, "DefaultLoadout",
                new LoadoutRule(new[] { new[] { WeaponCategory.AssaultRifle }, new[] { WeaponCategory.Handgun } }),
                new[] { Rifle(), Handgun() }, 0.4f, null, null, motor);
            var read = NetMatchFile.Parse(NetMatchFile.Write(original), "carry.json").Motor.Motor.CarryLoad;

            Assert.That(read.WeightSource, Is.EqualTo(CarrySource.Carried));
            Assert.That(read.MultiplierSource, Is.EqualTo(CarrySource.None));
            Assert.That(read.FreeWeight, Is.EqualTo(5f));
            Assert.That(read.SpeedLossPerKg, Is.EqualTo(0.04f));
            Assert.That(read.MinWeightMultiplier, Is.EqualTo(0.6f));
            Assert.That(read.Gaits, Is.EqualTo(CarryGaits.Walk | CarryGaits.Sprint));

            var bareText = "{\"loadout\": {\"slots\": [[\"Handgun\"]], \"weapons\": [\"" + _handgunId + "\"]}, \"weapons\": [" + _handgunJson + "]";
            var partial = NetMatchFile.Parse(bareText + ", \"motor\": {\"carrySpeedLossPerKg\": 0.1, \"carryCrouch\": false}}", "partial.json")
                .Motor.Motor.CarryLoad;

            Assert.That(partial.SpeedLossPerKg, Is.EqualTo(0.1f));
            Assert.That(partial.WeightSource, Is.EqualTo(CarryLoadSpec.DefaultWeightSource));
            Assert.That(partial.MultiplierSource, Is.EqualTo(CarryLoadSpec.DefaultMultiplierSource));
            Assert.That(partial.FreeWeight, Is.EqualTo(CarryLoadSpec.DefaultFreeWeight));
            Assert.That(partial.MinWeightMultiplier, Is.EqualTo(CarryLoadSpec.DefaultMinWeightMultiplier));
            Assert.That(partial.Gaits, Is.EqualTo(CarryGaits.Walk | CarryGaits.Sprint | CarryGaits.Prone));
        }

        [Test]
        public void DirectionScalesDefaultToUniformWhenLeftOut()
        {
            var bareText = "{\"loadout\": {\"slots\": [[\"Handgun\"]], \"weapons\": [\"" + _handgunId + "\"]}, \"weapons\": [" + _handgunJson + "]";
            var read = NetMatchFile.Parse(bareText + ", \"motor\": {\"sprintBackwardScale\": 0.5}}", "direction.json").Motor.Motor;

            Assert.That(read.SprintDirection.Backward, Is.EqualTo(0.5f));
            Assert.That(read.SprintDirection.Forward, Is.EqualTo(1f));
            Assert.That(read.SprintDirection.Side, Is.EqualTo(1f));
            Assert.That(read.WalkDirection.Forward, Is.EqualTo(1f));
            Assert.That(read.WalkDirection.Backward, Is.EqualTo(1f));
            Assert.That(read.WalkDirection.Side, Is.EqualTo(1f));
        }

        [TestCase("{\"motor\": {\"carryWeightSource\": \"Pockets\"}}", "motor.carryWeightSource")]
        [TestCase("{\"motor\": {\"carryFreeWeight\": -1}}", "motor.carryFreeWeight")]
        [TestCase("{\"motor\": {\"carrySpeedLossPerKg\": -0.1}}", "motor.carrySpeedLossPerKg")]
        [TestCase("{\"motor\": {\"carryMinWeightMultiplier\": 1.5}}", "motor.carryMinWeightMultiplier")]
        [TestCase("{\"motor\": {\"carrySprint\": 1}}", "motor.carrySprint")]
        [TestCase("{\"motor\": {\"walkBackwardScale\": -1}}", "motor.walkBackwardScale")]
        [TestCase("{\"motor\": {\"sprintSideScale\": -0.5}}", "motor.sprintSideScale")]
        [TestCase("{\"motor\": {\"sprintForwardScale\": -1}}", "motor.sprintForwardScale")]
        [TestCase("{\"motor\": {\"proneSpeed\": -1}}", "motor.proneSpeed")]
        [TestCase("{\"motor\": {\"diveSeconds\": 0}}", "motor.diveSeconds")]
        [TestCase("{\"motor\": {\"radius\": -1}}", "motor.radius")]
        [TestCase("{\"motor\": {\"walkSpeed\": -1}}", "motor.walkSpeed")]
        [TestCase("{\"motor\": {\"crouchHeight\": 9}}", "motor.crouchHeight")]
        [TestCase("{\"motor\": {\"slopeLimitDegrees\": 120}}", "motor.slopeLimitDegrees")]
        [TestCase("{\"motor\": {\"leanSideOffset\": -1}}", "motor.leanSideOffset")]
        [TestCase("{\"motor\": {\"leanAngleDegrees\": 100}}", "motor.leanAngleDegrees")]
        [TestCase("{\"motor\": {\"leanSpeed\": 0}}", "motor.leanSpeed")]
        [TestCase("{\"motor\": {\"airJumps\": -1}}", "motor.airJumps")]
        [TestCase("{\"motor\": {\"airJumps\": 1.5}}", "motor.airJumps")]
        [TestCase("{\"motor\": {\"airJumpSpeed\": -2}}", "motor.airJumpSpeed")]
        [TestCase("{\"motor\": {\"jumpGraceSeconds\": -0.1}}", "motor.jumpGraceSeconds")]
        [TestCase("{\"motor\": {\"swimSpeed\": -1}}", "motor.swimSpeed")]
        [TestCase("{\"motor\": {\"swimBuoyancy\": -1}}", "motor.swimBuoyancy")]
        [TestCase("{\"motor\": {\"swimBreathSeconds\": 0}}", "motor.swimBreathSeconds")]
        [TestCase("{\"motor\": {\"wallRunEnabled\": \"yes\"}}", "motor.wallRunEnabled")]
        [TestCase("{\"motor\": {\"wallRunMaxSeconds\": 0}}", "motor.wallRunMaxSeconds")]
        [TestCase("{\"motor\": {\"wallRunMinSpeed\": -1}}", "motor.wallRunMinSpeed")]
        [TestCase("{\"motor\": {\"wallRunGravityScale\": -1}}", "motor.wallRunGravityScale")]
        [TestCase("{\"motor\": {\"wallRunJumpOffSpeed\": -1}}", "motor.wallRunJumpOffSpeed")]
        [TestCase("{\"motor\": {\"parachuteMinOpenHeight\": 0}}", "motor.parachuteMinOpenHeight")]
        [TestCase("{\"motor\": {\"parachuteDescentSpeed\": -1}}", "motor.parachuteDescentSpeed")]
        [TestCase("{\"motor\": {\"parachuteHorizontalSpeed\": -1}}", "motor.parachuteHorizontalSpeed")]
        [TestCase("{\"motor\": {\"parachuteSteerAcceleration\": 0}}", "motor.parachuteSteerAcceleration")]
        public void BadMotorValuesNameTheItem(string text, string item)
        {
            var e = Assert.Throws<ServerDataJsonException>(() => NetMatchFile.Parse(text, "bad.json"));

            Assert.That(e.Item, Is.EqualTo(item));
        }

        [Test]
        public void WithoutACatalogPlayersCannotChoose()
        {
            var read = NetMatchFile.Parse(NetMatchFile.Write(File()), "TeamDeathmatch.json");

            Assert.That(read.Catalog, Is.Null);
            Assert.That(read.CatalogName, Is.Null);
            Assert.That(read.CreateLoadout().TryChoose(1, new[] { _handgunId, _rifleId }, out _), Is.False);
        }

        [Test]
        public void WithoutAbilitiesTheFileHasNone()
        {
            var text = NetMatchFile.Write(File());

            Assert.That(text, Does.Not.Contain("\"" + NetMatchFile.AbilitiesItem + "\": ["));
            Assert.That(NetMatchFile.Parse(text, "TeamDeathmatch.json").LoadoutAbilities, Is.Null);
        }

        [Test]
        public void LoadoutAbilitiesReadBackInSlotOrder()
        {
            var plain = File();
            var original = new NetMatchFile(
                plain.Name, plain.Rules, plain.Settings, plain.LoadoutName, plain.LoadoutRule, plain.LoadoutWeapons, plain.SwitchSeconds,
                null, null, null, new[] { "beacon", string.Empty });

            var text = NetMatchFile.Write(original);
            var read = NetMatchFile.Parse(text, "Domination.json");

            Assert.That(NetMatchFile.AbilitiesItem, Is.EqualTo("abilities"));
            Assert.That(text, Does.Contain("\"abilities\": [\"beacon\", \"\"]"));
            Assert.That(read.LoadoutAbilities, Is.EqualTo(new[] { "beacon", string.Empty }));
            Assert.That(NetMatchFile.Write(read), Is.EqualTo(text));
        }

        [Test]
        public void AnEmptyAbilityListIsKeptApartFromNoList()
        {
            var text = NetMatchFile.Write(File()).Replace("\"weapons\": [\"" + _rifleId, "\"abilities\": [], \"weapons\": [\"" + _rifleId);

            var read = NetMatchFile.Parse(text, "empty.json");

            Assert.That(read.LoadoutAbilities, Is.Not.Null);
            Assert.That(read.LoadoutAbilities, Is.Empty);
        }

        [Test]
        public void TheFileKeepsItsOwnCopyOfTheAbilities()
        {
            var plain = File();
            var abilities = new[] { "beacon" };
            var file = new NetMatchFile(
                plain.Name, plain.Rules, plain.Settings, plain.LoadoutName, plain.LoadoutRule, plain.LoadoutWeapons, plain.SwitchSeconds,
                null, null, null, abilities);

            abilities[0] = "mine";

            Assert.That(file.LoadoutAbilities, Is.EqualTo(_expectedBeacon));
        }

        [Test]
        public void AbilitiesThatRepeatOrAreNullAreRefused()
        {
            var plain = File();

            Assert.Throws<ArgumentException>(() => new NetMatchFile(
                plain.Name, plain.Rules, plain.Settings, plain.LoadoutName, plain.LoadoutRule, plain.LoadoutWeapons, plain.SwitchSeconds,
                null, null, null, _repeatedBeacon));
            Assert.Throws<ArgumentException>(() => new NetMatchFile(
                plain.Name, plain.Rules, plain.Settings, plain.LoadoutName, plain.LoadoutRule, plain.LoadoutWeapons, plain.SwitchSeconds,
                null, null, null, new[] { "beacon", null }));
            Assert.DoesNotThrow(() => new NetMatchFile(
                plain.Name, plain.Rules, plain.Settings, plain.LoadoutName, plain.LoadoutRule, plain.LoadoutWeapons, plain.SwitchSeconds,
                null, null, null, new[] { string.Empty, string.Empty }));
        }

        [TestCase("\"abilities\": \"beacon\", ", "loadout.abilities")]
        [TestCase("\"abilities\": [1], ", "loadout.abilities[0]")]
        [TestCase("\"abilities\": [\"beacon\", \"beacon\"], ", "loadout.abilities[1]")]
        [TestCase("\"abilities\": null, ", "loadout.abilities")]
        public void BadAbilitiesNameTheItem(string abilities, string item)
        {
            var text = NetMatchFile.Write(File()).Replace("\"weapons\": [\"" + _rifleId, abilities + "\"weapons\": [\"" + _rifleId);

            var e = Assert.Throws<ServerDataJsonException>(() => NetMatchFile.Parse(text, "bad.json"));

            Assert.That(e.Item, Is.EqualTo(item));
        }

        [Test]
        public void EachLoadoutIsFresh()
        {
            var file = File(catalog: new WeaponCatalog(new[] { Rifle(), Handgun() }));
            var first = file.CreateLoadout();
            Assert.That(first.TryChoose(1, new[] { _rifleId, _handgunId }, out _), Is.True);

            Assert.That(file.CreateLoadout().HasChoice(1), Is.False);
        }

        [Test]
        public void SampleWeaponJsonCanBePastedIntoTheWeapons()
        {
            var text = "{\"rules\": {\"objective\": \"None\"}, \"loadout\": {\"slots\": [[\"Handgun\"]], \"weapons\": [\"" + _handgunId + "\"]},"
                + " \"weapons\": [{\"id\": \"" + _handgunId + "\", \"category\": \"Handgun\", \"fireMode\": \"Semi\", \"magazineSize\": 12,"
                + " \"fireInterval\": 0.2, \"reloadSeconds\": 1.5, \"damage\": 34, \"range\": 60}]}";

            var read = NetMatchFile.Parse(text, "min.json");

            Assert.That(read.Settings.ScoreLimit, Is.EqualTo(NetMatchSettings.DefaultScoreLimit));
            Assert.That(read.LoadoutWeapons[0].Damage, Is.EqualTo(34));
            Assert.That(read.LoadoutWeapons[0].ShotNoiseRadius, Is.EqualTo(WeaponSpec.DefaultShotNoiseRadius));
            Assert.That(read.SwitchSeconds, Is.Zero);
        }

        [TestCase("{\"loadout\": {\"slots\": [[\"Handgun\"]], \"weapons\": [\"x\"]}}", "loadout.weapons[0]")]
        [TestCase("{\"match\": {\"scoreLimit\": 0}, \"loadout\": {}}", "match.scoreLimit")]
        [TestCase("{\"match\": {\"scoreLimits\": 1}}", "match.scoreLimits")]
        [TestCase("{\"rules\": {\"actions\": {\"grenadeCount\": -1}}}", "rules.actions.grenadeCount")]
        [TestCase("{\"name\": \"x\"}", "loadout")]
        public void ProblemsNameTheItem(string text, string item)
        {
            var e = Assert.Catch(() => NetMatchFile.Parse(text, "bad.json"));

            var named = e is ServerDataJsonException data ? data.Item : ((GameRulesJsonException)e).Item;
            Assert.That(named, Is.EqualTo(item));
            Assert.That(e.Message, Does.Contain("bad.json"));
        }

        [Test]
        public void WeaponValuesAreCheckedWithTheirItemName()
        {
            var text = "{\"loadout\": {\"slots\": [[\"Handgun\"]], \"weapons\": [\"" + _handgunId + "\"]},"
                + " \"weapons\": [{\"id\": \"" + _handgunId + "\", \"category\": \"Handgun\", \"fireMode\": \"Semi\", \"magazineSize\": 0,"
                + " \"fireInterval\": 0.2, \"reloadSeconds\": 1.5, \"damage\": 34, \"range\": 60}]}";

            var e = Assert.Throws<ServerDataJsonException>(() => NetMatchFile.Parse(text, "bad.json"));

            Assert.That(e.Item, Is.EqualTo("weapons[0].magazineSize"));
        }

        [Test]
        public void LoadoutThatBreaksItsRuleIsRefused()
        {
            var text = NetMatchFile.Write(File()).Replace("\"AssaultRifle\"]", "\"Shotgun\"]");

            var e = Assert.Throws<ServerDataJsonException>(() => NetMatchFile.Parse(text, "bad.json"));

            Assert.That(e.Item, Is.EqualTo("loadout"));
        }

        [Test]
        public void MapPointsReadBackInTheSameOrder()
        {
            var points = new NetMapPoints(
                new List<SpawnPoint>
                {
                    new SpawnPoint(new Vector3(1f, 0.5f, -2.25f), Team.Alpha),
                    new SpawnPoint(new Vector3(-1f, 0.5f, 2f), Team.Bravo),
                    new SpawnPoint(new Vector3(0f, 1f, 0f)),
                },
                new List<ControlPointArea> { new ControlPointArea(1, new Vector3(0f, 0f, 15f), 4f, 3f), new ControlPointArea(0, Vector3.Zero, 4f, 3f) });

            var text = NetMapPoints.Write(points);
            var read = NetMapPoints.Parse(text, "NetArena01.points.json");

            Assert.That(read.SpawnPoints, Has.Count.EqualTo(3));
            Assert.That(read.SpawnPoints[0].Position, Is.EqualTo(new Vector3(1f, 0.5f, -2.25f)));
            Assert.That(read.SpawnPoints[0].Team, Is.EqualTo(Team.Alpha));
            Assert.That(read.SpawnPoints[2].Team, Is.Null);
            Assert.That(read.ControlPoints[0].Index, Is.EqualTo(1));
            Assert.That(read.ControlPoints[0].Center, Is.EqualTo(new Vector3(0f, 0f, 15f)));
            Assert.That(read.ControlPoints[1].Radius, Is.EqualTo(4f));
            Assert.That(NetMapPoints.Write(read), Is.EqualTo(text));
            Assert.That(read.FlagBases, Is.Empty);
            Assert.That(text, Does.Not.Contain(NetMapPoints.FlagBasesItem), "a map without flag bases is written as before");
            Assert.That(read.Payload, Is.Null);
            Assert.That(text, Does.Not.Contain("\"" + NetMapPoints.PayloadItem + "\""), "a map without a cart is written as before");
        }

        private static readonly int[] _secondAndThirdPoints = { 1, 2 };
        private static readonly int[] _lastPoint = { 1 };

        [Test]
        public void MapPointsCarryThePayloadRouteAndItsCheckpoints()
        {
            var points = new NetMapPoints(
                new List<SpawnPoint> { new SpawnPoint(new Vector3(1f, 0.5f, -2.25f), Team.Alpha) },
                new List<ControlPointArea>(),
                null,
                new NetMapPayload(
                    Team.Bravo,
                    new[] { new Vector3(0f, -1f, -18f), new Vector3(0f, -1f, 0f), new Vector3(4f, -1f, 3f), new Vector3(4f, -1f, 18f) },
                    _secondAndThirdPoints,
                    4.5f,
                    6f));

            var text = NetMapPoints.Write(points);
            var read = NetMapPoints.Parse(text, "NetArena01.points.json");

            Assert.That(text, Does.Contain(
                "  \"payload\": {\n"
                + "    \"attackers\": \"Bravo\",\n"
                + "    \"radius\": 4.5,\n"
                + "    \"height\": 6,\n"
                + "    \"route\": [\n"
                + "      {\n"
                + "        \"position\": [0, -1, -18]\n"
                + "      },\n"
                + "      {\n"
                + "        \"position\": [0, -1, 0],\n"
                + "        \"checkpoint\": true\n"
                + "      },\n"
                + "      {\n"
                + "        \"position\": [4, -1, 3],\n"
                + "        \"checkpoint\": true\n"
                + "      },\n"
                + "      {\n"
                + "        \"position\": [4, -1, 18]\n"
                + "      }\n"
                + "    ]\n"
                + "  }\n"));
            Assert.That(read.Payload, Is.Not.Null);
            Assert.That(read.Payload.Attackers, Is.EqualTo(Team.Bravo));
            Assert.That(read.Payload.Radius, Is.EqualTo(4.5f));
            Assert.That(read.Payload.Height, Is.EqualTo(6f));
            Assert.That(read.Payload.Route.PointCount, Is.EqualTo(4));
            Assert.That(read.Payload.Route.Point(2), Is.EqualTo(new Vector3(4f, -1f, 3f)));
            Assert.That(read.Payload.CheckpointPoints, Is.EqualTo(_secondAndThirdPoints));
            Assert.That(read.Payload.Route.Length, Is.EqualTo(18d + 5d + 15d).Within(1e-4));
            Assert.That(NetMapPoints.Write(read), Is.EqualTo(text));
        }

        [Test]
        public void APayloadRouteMayLeaveOutTheCheckpointsAndWriteThemFalse()
        {
            const string text = "{\"payload\": {\"attackers\": \"Alpha\", \"radius\": 5, \"height\": 6, \"route\": ["
                + "{\"position\": [0, 0, 0], \"checkpoint\": false}, {\"position\": [0, 0, 10]}]}}";

            var read = NetMapPoints.Parse(text, "hand.points.json");

            Assert.That(read.Payload.Attackers, Is.EqualTo(Team.Alpha));
            Assert.That(read.Payload.CheckpointPoints, Is.Empty);
            Assert.That(read.Payload.Route.Length, Is.EqualTo(10d));
            Assert.That(read.SpawnPoints, Is.Empty);
        }

        [Test]
        public void ThePayloadAreaFollowsTheCartAlongTheRoute()
        {
            var payload = new NetMapPayload(Team.Alpha, new[] { Vector3.Zero, new Vector3(0f, 0f, 10f) }, null, 2f, 4f);

            var area = payload.AreaAt(5d);

            Assert.That(area.Center, Is.EqualTo(new Vector3(0f, 0f, 5f)));
            Assert.That(area.Radius, Is.EqualTo(2f));
            Assert.That(area.Contains(new Vector3(1.5f, 1.9f, 5f)), Is.True);
            Assert.That(area.Contains(new Vector3(0f, 0f, 7.5f)), Is.False, "2.5 m along the route is outside a 2 m radius");
            Assert.That(area.Contains(new Vector3(0f, 2.5f, 5f)), Is.False, "above half the height");
            Assert.That(payload.AreaAt(99d).Center, Is.EqualTo(new Vector3(0f, 0f, 10f)), "past the end stays at the end");
        }

        [Test]
        public void APayloadNeedsATeamARouteAndAPositiveArea()
        {
            var route = new[] { Vector3.Zero, new Vector3(0f, 0f, 10f) };
            Assert.That(
                Assert.Throws<ArgumentOutOfRangeException>(() => new NetMapPayload((Team)7, route, null, 5f, 6f)).ParamName,
                Is.EqualTo("attackers"));
            Assert.That(Assert.Throws<ArgumentOutOfRangeException>(() => new NetMapPayload(Team.Alpha, route, null, 0f, 6f)).ParamName, Is.EqualTo("radius"));
            Assert.That(
                Assert.Throws<ArgumentOutOfRangeException>(() => new NetMapPayload(Team.Alpha, route, null, 5f, float.PositiveInfinity)).ParamName,
                Is.EqualTo("height"));
            Assert.Throws<ArgumentNullException>(() => new NetMapPayload(Team.Alpha, null, null, 5f, 6f));
            Assert.Throws<ArgumentException>(() => new NetMapPayload(Team.Alpha, new[] { Vector3.Zero }, null, 5f, 6f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new NetMapPayload(Team.Alpha, route, _lastPoint, 5f, 6f), "the end is not a checkpoint");
        }

        [Test]
        public void MapPointsCarryFlagBases()
        {
            var points = new NetMapPoints(
                new List<SpawnPoint> { new SpawnPoint(new Vector3(1f, 0.5f, -2.25f), Team.Alpha) },
                new List<ControlPointArea>(),
                new List<FlagBasePoint>
                {
                    new FlagBasePoint(Team.Bravo, new Vector3(20f, 0.5f, 1.5f)),
                    new FlagBasePoint(Team.Alpha, new Vector3(-20f, 0.5f, -1.5f)),
                });

            var text = NetMapPoints.Write(points);
            var read = NetMapPoints.Parse(text, "NetCtf01.points.json");

            Assert.That(text, Does.Contain("\"flagBases\": ["));
            Assert.That(read.FlagBases, Has.Count.EqualTo(2));
            Assert.That(read.FlagBases[0].Team, Is.EqualTo(Team.Bravo));
            Assert.That(read.FlagBases[0].Position, Is.EqualTo(new Vector3(20f, 0.5f, 1.5f)));
            Assert.That(read.FlagBases[1].Team, Is.EqualTo(Team.Alpha));
            Assert.That(read.FlagBases[1].Position, Is.EqualTo(new Vector3(-20f, 0.5f, -1.5f)));
            Assert.That(NetMapPoints.Write(read), Is.EqualTo(text));
        }

        [Test]
        public void AFlagBaseNeedsATeamWithAFlagAndAFinitePosition()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new FlagBasePoint((Team)7, Vector3.Zero));
            Assert.Throws<ArgumentOutOfRangeException>(() => new FlagBasePoint(Team.Alpha, new Vector3(float.NaN, 0f, 0f)));
            Assert.Throws<ArgumentOutOfRangeException>(() => new FlagBasePoint(Team.Bravo, new Vector3(0f, float.PositiveInfinity, 0f)));
        }

        // 書き出したファイルを手で直したり書式を変えたりしたまま、サーバーが起動できなくなるのを防ぐ
        [Test]
        public void CommittedServerDataReadsAndIsWrittenTheSameWay()
        {
            var root = ServerDataRoot();
            var rules = Directory.GetFiles(Path.Combine(root, "rules"), "*" + NetMatchFile.Extension);
            var points = Directory.GetFiles(Path.Combine(root, "maps"), "*" + NetMapPoints.Extension);
            Assert.That(rules, Is.Not.Empty);
            Assert.That(points, Is.Not.Empty);

            foreach (var path in rules)
            {
                var text = System.IO.File.ReadAllText(path);
                Assert.That(NetMatchFile.Write(NetMatchFile.Parse(text, path)), Is.EqualTo(text), path);
            }

            foreach (var path in points)
            {
                var text = System.IO.File.ReadAllText(path);
                Assert.That(NetMapPoints.Write(NetMapPoints.Parse(text, path)), Is.EqualTo(text), path);
            }

            Assert.That(NetMatchFile.Parse(System.IO.File.ReadAllText(Path.Combine(root, "rules", "Domination.json")), "Domination").Rules.Objective,
                Is.EqualTo(MatchObjective.Domination));
        }

        [Test]
        public void CommittedRulesListTheBetweenMatchesItemsAtTheirDefaults()
        {
            var items = new[]
            {
                NetMatchFile.MapPoolItem, NetMatchFile.MapVoteSecondsItem, NetMatchFile.MaxTeamSizeDifferenceItem, NetMatchFile.PrivateMatchItem,
                NetMatchFile.JoinCodeItem, NetMatchFile.ChatIntervalSecondsItem, NetMatchFile.MaxChatLengthItem,
            };

            foreach (var path in Directory.GetFiles(Path.Combine(ServerDataRoot(), "rules"), "*" + NetMatchFile.Extension))
            {
                var text = System.IO.File.ReadAllText(path);
                foreach (var item in items)
                {
                    Assert.That(text, Does.Contain("\"" + item + "\": "), path + " " + item);
                }

                var settings = NetMatchFile.Parse(text, path).Settings;
                Assert.That(settings.MapPool, Is.Empty, path);
                Assert.That(settings.MapVoteSeconds, Is.EqualTo(0f), path);
                Assert.That(settings.MaxTeamSizeDifference, Is.EqualTo(0), path);
                Assert.That(settings.PrivateMatch, Is.False, path);
                Assert.That(settings.JoinCode, Is.Null, path);
                Assert.That(settings.ChatIntervalSeconds, Is.EqualTo(1f), path);
                Assert.That(settings.MaxChatLength, Is.EqualTo(120), path);
            }
        }

        private static readonly string[] _expectedMapPool = { "NetTest", "NetArena01" };

        [Test]
        public void CommittedRulesEditedAsTheGuideSaysStillRead()
        {
            var text = System.IO.File.ReadAllText(Path.Combine(ServerDataRoot(), "rules", "TeamDeathmatch.json"));
            var edits = new[]
            {
                ("\"mapPool\": [],", "\"mapPool\": [\"NetTest\", \"NetArena01\"],"),
                ("\"mapVoteSeconds\": 0,", "\"mapVoteSeconds\": 15,"),
                ("\"maxTeamSizeDifference\": 0,", "\"maxTeamSizeDifference\": 1,"),
                ("\"privateMatch\": false,", "\"privateMatch\": true,"),
                ("\"joinCode\": \"\",", "\"joinCode\": \"test-1\","),
                ("\"maxChatLength\": 120", "\"maxChatLength\": 170"),
            };
            foreach (var (from, to) in edits)
            {
                Assert.That(text, Does.Contain(from));
                text = text.Replace(from, to);
            }

            var settings = NetMatchFile.Parse(text, "TeamDeathmatch.json").Settings;

            Assert.That(settings.MapPool, Is.EqualTo(_expectedMapPool));
            Assert.That(settings.MapVoteSeconds, Is.EqualTo(15f));
            Assert.That(settings.MaxTeamSizeDifference, Is.EqualTo(1));
            Assert.That(settings.PrivateMatch, Is.True);
            Assert.That(settings.JoinCode, Is.EqualTo("test-1"));
            Assert.That(settings.MaxChatLength, Is.EqualTo(170));
        }

        [TestCase("{\"spawnPoints\": [{\"position\": [0, 1]}]}", "spawnPoints[0].position")]
        [TestCase("{\"spawnPoints\": [{\"position\": [0, 1, 2], \"team\": \"Charlie\"}]}", "spawnPoints[0].team")]
        [TestCase("{\"controlPoints\": [{\"index\": 0, \"center\": [0, 0, 0], \"radius\": 0, \"height\": 3}]}", "controlPoints[0].radius")]
        [TestCase("{\"controlPoints\": [{\"index\": 0, \"center\": [0, 0, 0], \"radius\": 1}]}", "controlPoints[0].height")]
        [TestCase("{\"flagBases\": [{\"position\": [0, 1, 2]}]}", "flagBases[0].team")]
        [TestCase("{\"flagBases\": [{\"team\": \"Charlie\", \"position\": [0, 1, 2]}]}", "flagBases[0].team")]
        [TestCase("{\"flagBases\": [{\"team\": \"Alpha\"}]}", "flagBases[0].position")]
        [TestCase("{\"flagBases\": [{\"team\": \"Alpha\", \"position\": [0, 1]}]}", "flagBases[0].position")]
        [TestCase("{\"payload\": {\"radius\": 5, \"height\": 6, \"route\": [{\"position\": [0, 0, 0]}, {\"position\": [0, 0, 9]}]}}", "payload.attackers")]
        [TestCase("{\"payload\": {\"attackers\": \"Charlie\", \"radius\": 5, \"height\": 6, \"route\": []}}", "payload.attackers")]
        [TestCase("{\"payload\": {\"attackers\": \"Alpha\", \"height\": 6, \"route\": []}}", "payload.radius")]
        [TestCase("{\"payload\": {\"attackers\": \"Alpha\", \"radius\": 0, \"height\": 6, \"route\": [{\"position\": [0, 0, 0]}, {\"position\": [0, 0, 9]}]}}", "payload.radius")]
        [TestCase("{\"payload\": {\"attackers\": \"Alpha\", \"radius\": 5, \"height\": -1, \"route\": [{\"position\": [0, 0, 0]}, {\"position\": [0, 0, 9]}]}}", "payload.height")]
        [TestCase("{\"payload\": {\"attackers\": \"Alpha\", \"radius\": 5, \"height\": 6}}", "payload.route")]
        [TestCase("{\"payload\": {\"attackers\": \"Alpha\", \"radius\": 5, \"height\": 6, \"route\": [{\"position\": [0, 0, 0]}]}}", "payload.route")]
        [TestCase("{\"payload\": {\"attackers\": \"Alpha\", \"radius\": 5, \"height\": 6, \"route\": [{\"position\": [0, 0, 0]}, {\"position\": [0, 0, 0]}]}}", "payload.route")]
        [TestCase("{\"payload\": {\"attackers\": \"Alpha\", \"radius\": 5, \"height\": 6, \"route\": [{\"position\": [0, 0, 0], \"checkpoint\": true}, {\"position\": [0, 0, 9]}]}}", "payload.route")]
        [TestCase("{\"payload\": {\"attackers\": \"Alpha\", \"radius\": 5, \"height\": 6, \"route\": [{\"position\": [0, 0]}, {\"position\": [0, 0, 9]}]}}", "payload.route[0].position")]
        [TestCase("{\"payload\": {\"attackers\": \"Alpha\", \"radius\": 5, \"height\": 6, \"route\": [{\"position\": [0, 0, 0], \"checkpoint\": 1}, {\"position\": [0, 0, 9]}]}}", "payload.route[0].checkpoint")]
        [TestCase("{\"payload\": {\"attackers\": \"Alpha\", \"radius\": 5, \"height\": 6, \"route\": [{\"position\": [0, 0, 0], \"speed\": 1}, {\"position\": [0, 0, 9]}]}}", "payload.route[0].speed")]
        [TestCase("{\"payload\": {\"attackers\": \"Alpha\", \"radius\": 5, \"height\": 6, \"route\": [], \"checkpoints\": [1]}}", "payload.checkpoints")]
        public void BadMapPointsNameTheItem(string text, string item)
        {
            var e = Assert.Throws<ServerDataJsonException>(() => NetMapPoints.Parse(text, "bad.points.json"));

            Assert.That(e.Item, Is.EqualTo(item));
        }

        internal static string ServerDataRoot()
        {
            for (var directory = new DirectoryInfo(TestContext.CurrentContext.TestDirectory); directory != null; directory = directory.Parent)
            {
                var candidate = Path.Combine(directory.FullName, "server-data");
                if (Directory.Exists(candidate))
                {
                    return candidate;
                }
            }

            Assert.Fail("server-data is not above " + TestContext.CurrentContext.TestDirectory);
            return null;
        }
    }
}
