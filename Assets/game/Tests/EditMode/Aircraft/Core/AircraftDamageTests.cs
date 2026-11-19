using System;
using NUnit.Framework;

namespace Shooter.Core.Tests
{
    // Unity の EditMode テストと dotnet/Shooter.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class AircraftDamageTests
    {
        private const float _epsilon = 1e-4f;

        [Test]
        public void AnUntouchedAircraftIsIntact()
        {
            var damage = new AircraftDamage(AircraftDamageSpec.Default);

            Assert.That(damage.Condition, Is.EqualTo(AircraftCondition.Intact));
            Assert.That(damage.Remaining(AircraftDamageSpec.DefaultLeftWingId), Is.EqualTo(AircraftDamageSpec.DefaultWingDurability));
            Assert.That(damage.Remaining(AircraftDamageSpec.DefaultEngineId), Is.EqualTo(AircraftDamageSpec.DefaultEngineDurability));
            Assert.That(damage.IsBroken(AircraftDamageSpec.DefaultTailId), Is.False);
        }

        [Test]
        public void ALeftWingAtZeroDropsOnlyTheLeftLiftToTheFloor()
        {
            var damage = new AircraftDamage(AircraftDamageSpec.Default);

            Assert.That(damage.TryApply(AircraftDamageSpec.DefaultLeftWingId, 1000, out var result), Is.True);

            Assert.That(result.Broke, Is.True);
            Assert.That(result.Effect, Is.EqualTo(AircraftDamageEffect.LeftLift));
            Assert.That(result.Dealt, Is.EqualTo(AircraftDamageSpec.DefaultWingDurability), "only what was left counts");
            Assert.That(result.Remaining, Is.Zero);
            Assert.That(damage.Condition.LeftLift, Is.EqualTo(AircraftDamageSpec.DefaultMinLift).Within(_epsilon));
            Assert.That(damage.Condition.RightLift, Is.EqualTo(1f));
            Assert.That(damage.Condition.Control, Is.EqualTo(1f));
            Assert.That(damage.Condition.Thrust, Is.EqualTo(1f));
        }

        [Test]
        public void AnEngineAtZeroGivesNoThrust()
        {
            var damage = new AircraftDamage(AircraftDamageSpec.Default);

            damage.TryApply(AircraftDamageSpec.DefaultEngineId, AircraftDamageSpec.DefaultEngineDurability, out var result);

            Assert.That(result.Broke, Is.True);
            Assert.That(damage.Condition.Thrust, Is.Zero);
            Assert.That(damage.Condition.Lift, Is.EqualTo(1f));
        }

        [Test]
        public void TheMultiplierFallsInAStraightLineFromOneToTheFloor()
        {
            var spec = new AircraftDamageSpec(
                new[] { new AircraftDamagePartSpec("tail", AircraftDamageEffect.Control, 100) },
                minControl: 0.4f);
            var damage = new AircraftDamage(spec);

            damage.TryApply("tail", 50, out var result);

            Assert.That(result.Broke, Is.False);
            Assert.That(result.Remaining, Is.EqualTo(50));
            Assert.That(damage.Condition.Control, Is.EqualTo(0.7f).Within(_epsilon), "0.4 + 0.6 * 0.5");
        }

        [Test]
        public void TwoEnginesShareTheThrust()
        {
            var spec = new AircraftDamageSpec(new[]
            {
                new AircraftDamagePartSpec("engine_left", AircraftDamageEffect.Thrust, 100),
                new AircraftDamagePartSpec("engine_right", AircraftDamageEffect.Thrust, 200),
            });
            var damage = new AircraftDamage(spec);

            damage.TryApply("engine_left", 100, out _);

            Assert.That(damage.Condition.Thrust, Is.EqualTo(0.5f).Within(_epsilon));
        }

        [Test]
        public void ABrokenPartBreaksOnlyOnce()
        {
            var damage = new AircraftDamage(AircraftDamageSpec.Default);
            damage.TryApply(AircraftDamageSpec.DefaultTailId, AircraftDamageSpec.DefaultTailDurability, out _);

            damage.TryApply(AircraftDamageSpec.DefaultTailId, 50, out var again);

            Assert.That(again.Broke, Is.False);
            Assert.That(again.Dealt, Is.Zero);
            Assert.That(damage.IsBroken(AircraftDamageSpec.DefaultTailId), Is.True);
            Assert.That(damage.Condition.Control, Is.EqualTo(AircraftDamageSpec.DefaultMinControl).Within(_epsilon));
        }

        [Test]
        public void AnUnknownPartIsIgnored()
        {
            var damage = new AircraftDamage(AircraftDamageSpec.Default);

            Assert.That(damage.TryApply("fuel", 1000, out var result), Is.False);
            Assert.That(damage.TryApply(null, 1000, out _), Is.False);
            Assert.That(result.PartId, Is.Empty);
            Assert.That(damage.Contains("fuel"), Is.False);
            Assert.That(damage.Condition, Is.EqualTo(AircraftCondition.Intact));
            Assert.Throws<ArgumentException>(() => damage.Remaining("fuel"));
        }

        [Test]
        public void RepairPutsEveryPartBack()
        {
            var damage = new AircraftDamage(AircraftDamageSpec.Default);
            damage.TryApply(AircraftDamageSpec.DefaultLeftWingId, 1000, out _);
            damage.TryApply(AircraftDamageSpec.DefaultEngineId, 10, out _);

            damage.Repair();

            Assert.That(damage.Condition, Is.EqualTo(AircraftCondition.Intact));
            Assert.That(damage.Remaining(AircraftDamageSpec.DefaultLeftWingId), Is.EqualTo(AircraftDamageSpec.DefaultWingDurability));
            Assert.That(damage.Remaining(AircraftDamageSpec.DefaultEngineId), Is.EqualTo(AircraftDamageSpec.DefaultEngineDurability));
        }

        [Test]
        public void SetRemainingWritesThePartAndRejectsOutOfRange()
        {
            var damage = new AircraftDamage(AircraftDamageSpec.Default);

            damage.SetRemaining(AircraftDamageSpec.DefaultRightWingId, 0);

            Assert.That(damage.Condition.RightLift, Is.EqualTo(AircraftDamageSpec.DefaultMinLift).Within(_epsilon));
            Assert.Throws<ArgumentOutOfRangeException>(() => damage.SetRemaining(AircraftDamageSpec.DefaultRightWingId, -1));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => damage.SetRemaining(AircraftDamageSpec.DefaultRightWingId, AircraftDamageSpec.DefaultWingDurability + 1));
            Assert.Throws<ArgumentException>(() => damage.SetRemaining("fuel", 0));
        }

        [Test]
        public void ApplyRejectsNegativeDamage()
        {
            var damage = new AircraftDamage(AircraftDamageSpec.Default);

            Assert.Throws<ArgumentOutOfRangeException>(() => damage.TryApply(AircraftDamageSpec.DefaultEngineId, -1, out _));
            Assert.Throws<ArgumentNullException>(() => new AircraftDamage(null));
        }

        [Test]
        public void TheSpecRejectsBadValues()
        {
            Assert.Throws<ArgumentException>(() => new AircraftDamagePartSpec(" ", AircraftDamageEffect.Thrust, 10));
            Assert.Throws<ArgumentOutOfRangeException>(() => new AircraftDamagePartSpec("engine", AircraftDamageEffect.Thrust, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new AircraftDamagePartSpec("engine", (AircraftDamageEffect)99, 10));
            Assert.Throws<ArgumentOutOfRangeException>(() => new AircraftDamageSpec(minLift: 1.5f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new AircraftDamageSpec(minThrust: float.NaN));
            Assert.Throws<ArgumentException>(() => new AircraftDamageSpec(new[]
            {
                new AircraftDamagePartSpec("engine", AircraftDamageEffect.Thrust, 10),
                new AircraftDamagePartSpec("engine", AircraftDamageEffect.Control, 10),
            }));
            Assert.Throws<ArgumentException>(() => new AircraftDamageSpec(new AircraftDamagePartSpec[] { null }));
        }

        [Test]
        public void AnEmptyPartListLeavesTheAircraftIntact()
        {
            var damage = new AircraftDamage(new AircraftDamageSpec(Array.Empty<AircraftDamagePartSpec>()));

            Assert.That(damage.TryApply(AircraftDamageSpec.DefaultEngineId, 1000, out _), Is.False);
            Assert.That(damage.Condition, Is.EqualTo(AircraftCondition.Intact));
        }

        [Test]
        public void TheBrokenLineNamesThePartAndTheMultipliers()
        {
            var line = AircraftLog.PartBroken("Plane", "wing_left", AircraftDamageEffect.LeftLift, new AircraftCondition(leftLift: 0.2f, thrust: 0.5f));

            Assert.That(line, Is.EqualTo("[Vehicle] 'Plane' part broken part=wing_left effect=LeftLift lift=0.20/1.00 control=1.00 thrust=0.50"));
        }

        [Test]
        public void TheNetConditionLineNamesTheMultipliers()
        {
            var line = AircraftLog.NetCondition("Plane", new AircraftCondition(leftLift: 0.2f, control: 0.4f));

            Assert.That(line, Is.EqualTo("[Vehicle] 'Plane' net condition lift=0.20/1.00 control=0.40 thrust=1.00"));
        }

        [Test]
        public void TheSettingsAndEffectsLinesKeepTheVehicleTag()
        {
            Assert.That(
                AircraftLog.InvalidAircraftDamage("Plane", "bad part"),
                Is.EqualTo("[Vehicle] 'Plane' aircraft damage settings are invalid; parts take no flight damage: bad part"));
            Assert.That(
                AircraftLog.EffectsMissingMotor("Plane"),
                Is.EqualTo("[Vehicle] 'Plane' has no SimpleAircraftMotor; the aircraft effects show nothing"));
            Assert.That(AircraftLog.Tag, Is.EqualTo(VehicleLog.Tag));
        }
    }
}
