using System;
using NUnit.Framework;

namespace Shooter.Core.Tests
{
    // Unity の EditMode テストと dotnet/Shooter.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class VehicleDurabilityTests
    {
        [Test]
        public void TheDefaultsSmokeAtHalfBurnAtAQuarterAndExplodeWithTheDefaultBlast()
        {
            var spec = VehicleDurabilitySpec.Default;

            Assert.That(spec.SmokeHealthFraction, Is.EqualTo(0.5f));
            Assert.That(spec.BurnHealthFraction, Is.EqualTo(0.25f));
            Assert.That(spec.BurnDamagePerSecond, Is.EqualTo(25f));
            Assert.That(spec.Explosion.Damage.MaxDamage, Is.EqualTo(150));
            Assert.That(spec.Explosion.Damage.Radius, Is.EqualTo(8f));
            Assert.That(spec.Explosion.Damage.FullDamageRadius, Is.EqualTo(2f));
            Assert.That(spec.Explosion.StructureDamageAtCenter, Is.EqualTo(150));
        }

        [Test]
        public void AVehicleSpecWithoutDurabilityUsesTheDefault()
        {
            var spec = new VehicleSpec("car", 100, new[] { new VehicleSeatSpec(VehicleSeatRole.Driver) });

            Assert.That(spec.Durability, Is.SameAs(VehicleDurabilitySpec.Default));
        }

        [Test]
        public void AVehicleSpecKeepsTheDurabilityItWasBuiltWith()
        {
            var durability = new VehicleDurabilitySpec(0.6f, 0.3f, 10f);

            var spec = new VehicleSpec("car", 100, new[] { new VehicleSeatSpec(VehicleSeatRole.Driver) }, durability: durability);

            Assert.That(spec.Durability, Is.SameAs(durability));
        }

        [TestCase(1000, VehicleDamageStage.Intact)]
        [TestCase(501, VehicleDamageStage.Intact)]
        [TestCase(500, VehicleDamageStage.Smoking)]
        [TestCase(251, VehicleDamageStage.Smoking)]
        [TestCase(250, VehicleDamageStage.Burning)]
        [TestCase(1, VehicleDamageStage.Burning)]
        [TestCase(0, VehicleDamageStage.Destroyed)]
        [TestCase(-5, VehicleDamageStage.Destroyed)]
        public void TheStageFollowsTheHealthLeft(int health, VehicleDamageStage expected)
        {
            Assert.That(VehicleDurabilitySpec.Default.StageFor(health, 1000), Is.EqualTo(expected));
        }

        [Test]
        public void ZeroFractionsSkipThoseStages()
        {
            var spec = new VehicleDurabilitySpec(0f, 0f, 10f);

            Assert.That(spec.StageFor(1, 100), Is.EqualTo(VehicleDamageStage.Intact));
            Assert.That(spec.StageFor(0, 100), Is.EqualTo(VehicleDamageStage.Destroyed));
        }

        [Test]
        public void InvalidValuesAreRejected()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new VehicleDurabilitySpec(1.1f, 0.25f, 10f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new VehicleDurabilitySpec(-0.1f, 0f, 10f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new VehicleDurabilitySpec(0.5f, 0.6f, 10f), "burning before smoking");
            Assert.Throws<ArgumentOutOfRangeException>(() => new VehicleDurabilitySpec(0.5f, -0.1f, 10f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new VehicleDurabilitySpec(0.5f, 0.25f, -1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new VehicleDurabilitySpec(0.5f, 0.25f, float.NaN));
            Assert.Throws<ArgumentOutOfRangeException>(() => new VehicleDurabilitySpec(0.5f, 0.25f, float.PositiveInfinity));
            Assert.Throws<ArgumentOutOfRangeException>(() => VehicleDurabilitySpec.Default.StageFor(10, 0));
        }

        [Test]
        public void ObservingReportsOnlyChangesAndGoesBackWhenRepaired()
        {
            var durability = new VehicleDurability(VehicleDurabilitySpec.Default);

            Assert.That(durability.Stage, Is.EqualTo(VehicleDamageStage.Intact));
            Assert.That(durability.Observe(900, 1000), Is.False);
            Assert.That(durability.Observe(400, 1000), Is.True);
            Assert.That(durability.Stage, Is.EqualTo(VehicleDamageStage.Smoking));
            Assert.That(durability.Observe(300, 1000), Is.False);
            Assert.That(durability.Observe(200, 1000), Is.True);
            Assert.That(durability.Stage, Is.EqualTo(VehicleDamageStage.Burning));
            Assert.That(durability.Observe(1000, 1000), Is.True);
            Assert.That(durability.Stage, Is.EqualTo(VehicleDamageStage.Intact));
        }

        [Test]
        public void DestroyedIsFinal()
        {
            var durability = new VehicleDurability(VehicleDurabilitySpec.Default);

            Assert.That(durability.MarkDestroyed(), Is.True);
            Assert.That(durability.MarkDestroyed(), Is.False);
            Assert.That(durability.Observe(1000, 1000), Is.False);
            Assert.That(durability.Stage, Is.EqualTo(VehicleDamageStage.Destroyed));
            Assert.That(durability.IsDestroyed, Is.True);
        }

        [Test]
        public void ObservingZeroHealthReachesDestroyed()
        {
            var durability = new VehicleDurability(VehicleDurabilitySpec.Default);

            Assert.That(durability.Observe(0, 1000), Is.True);
            Assert.That(durability.IsDestroyed, Is.True);
        }

        [Test]
        public void BurningDrainsOnlyWhileBurningAndCarriesFractions()
        {
            var durability = new VehicleDurability(new VehicleDurabilitySpec(0.5f, 0.25f, 2f));

            Assert.That(durability.TickBurn(1f), Is.Zero, "intact vehicles do not burn");
            durability.Observe(200, 1000);

            Assert.That(durability.TickBurn(0.25f), Is.Zero, "0.5 is carried");
            Assert.That(durability.TickBurn(0.25f), Is.EqualTo(1), "0.5 + 0.5");
            Assert.That(durability.TickBurn(1f), Is.EqualTo(2));
            Assert.That(durability.TickBurn(0f), Is.Zero);

            durability.Observe(1000, 1000);
            Assert.That(durability.TickBurn(1f), Is.Zero, "a repaired vehicle stops burning");
        }

        [Test]
        public void LeavingTheFireDropsTheCarriedFraction()
        {
            var durability = new VehicleDurability(new VehicleDurabilitySpec(0.5f, 0.2f, 2f));
            durability.Observe(200, 1000);
            durability.TickBurn(0.25f);

            durability.Observe(1000, 1000);
            durability.Observe(200, 1000);

            Assert.That(durability.TickBurn(0.25f), Is.Zero, "the 0.5 from the earlier fire is gone");
        }

        [Test]
        public void ConfiguringKeepsTheStageAndUsesTheNewBurnRate()
        {
            var durability = new VehicleDurability(VehicleDurabilitySpec.Default);
            durability.Observe(200, 1000);

            durability.Configure(new VehicleDurabilitySpec(0.5f, 0.25f, 100f));

            Assert.That(durability.Stage, Is.EqualTo(VehicleDamageStage.Burning));
            Assert.That(durability.TickBurn(0.5f), Is.EqualTo(50));
            Assert.Throws<ArgumentNullException>(() => durability.Configure(null));
            Assert.Throws<ArgumentNullException>(() => new VehicleDurability(null));
        }

        [Test]
        public void TickingRejectsNegativeAndNonFiniteTime()
        {
            var durability = new VehicleDurability(VehicleDurabilitySpec.Default);

            Assert.Throws<ArgumentOutOfRangeException>(() => durability.TickBurn(-0.1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => durability.TickBurn(float.NaN));
            Assert.Throws<ArgumentOutOfRangeException>(() => durability.TickBurn(float.PositiveInfinity));
        }

        [Test]
        public void DamageIsScaledByAPartMultiplierWithTheCalculatorsRounding()
        {
            Assert.That(DamageCalculator.Scale(40, 1f), Is.EqualTo(40));
            Assert.That(DamageCalculator.Scale(40, 1.5f), Is.EqualTo(60));
            Assert.That(DamageCalculator.Scale(5, 0.5f), Is.EqualTo(3), "2.5 rounds away from zero");
            Assert.That(DamageCalculator.Scale(1, 0.1f), Is.EqualTo(1), "a hit always does at least 1");
            Assert.That(DamageCalculator.Scale(40, 0f), Is.Zero, "a 0 multiplier makes the part immune");
            Assert.That(DamageCalculator.Scale(0, 2f), Is.Zero);
            Assert.That(DamageCalculator.Scale(int.MaxValue, 2f), Is.EqualTo(int.MaxValue));
            Assert.Throws<ArgumentOutOfRangeException>(() => DamageCalculator.Scale(-1, 1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => DamageCalculator.Scale(1, -1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => DamageCalculator.Scale(1, float.NaN));
        }

        [Test]
        public void TheLogLinesCarryTheTag()
        {
            Assert.That(VehicleLog.Damaged("Jeep", 40, 960, 1000), Is.EqualTo("[Vehicle] 'Jeep' damaged dmg=40 hp=960/1000"));
            Assert.That(
                VehicleLog.Stage("Jeep", VehicleDamageStage.Smoking, 500, 1000),
                Is.EqualTo("[Vehicle] 'Jeep' stage=Smoking hp=500/1000"));
            Assert.That(
                VehicleLog.Exploded("Jeep", 1f, 0.55f, -9.5f, 2, 3, 1),
                Is.EqualTo("[Vehicle] 'Jeep' exploded at=(1.0, 0.6, -9.5) riders=2 damaged=3 killed=1"));
            Assert.That(
                VehicleLog.UnknownPart("Jeep", "turbo"),
                Is.EqualTo("[Vehicle] 'Jeep' part 'turbo' is not in the spec; it takes damage unscaled"));
        }
    }
}
