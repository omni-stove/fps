using System;
using System.Numerics;
using NUnit.Framework;

namespace Fps.Core.Tests
{
    // Unity の EditMode テストと dotnet/Fps.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class EnemyBrainFireRangeTests
    {
        // 視認 20m、攻撃 10m、撃ち始める距離 18m、反応 0.5 秒、向きの許容 15 度。
        private static EnemySpec Spec(float fireRange = 18f) => new EnemySpec(
            20f, 10f, 1f, 2f, 0.5f, staggerResistance: 0f, fireRange: fireRange, fireToleranceDegrees: 15f);

        private static EnemyPerception Sees(Vector3 target) => new EnemyPerception(Vector3.Zero, target, true, false, null);

        private static EnemyPerception Nothing() => new EnemyPerception(Vector3.Zero, null, false, false, null);

        [Test]
        public void BeyondTheFireRangeItChasesWithoutAiming()
        {
            var brain = new EnemyBrain(Spec());

            var decision = brain.Tick(1f, Sees(new Vector3(0f, 0f, 19f)));

            Assert.That(brain.State, Is.EqualTo(EnemyState.Chase));
            Assert.That(brain.IsAiming, Is.False);
            Assert.That(decision.Fire, Is.False);
            Assert.That(decision.MoveTo, Is.EqualTo(new Vector3(0f, 0f, 19f)));
        }

        [Test]
        public void InsideTheFireRangeItStopsToAimThenFiresWhileClosingIn()
        {
            var brain = new EnemyBrain(Spec());
            var target = new Vector3(0f, 0f, 15f);

            var aiming = brain.Tick(0.1f, Sees(target));
            Assert.That(brain.State, Is.EqualTo(EnemyState.Chase));
            Assert.That(brain.IsAiming, Is.True);
            Assert.That(brain.AimRemainingSeconds, Is.EqualTo(0.5f).Within(1e-5f));
            Assert.That(aiming.Fire, Is.False, "it does not fire the moment it sees the target");
            Assert.That(aiming.MoveTo, Is.Null, "it plants its feet while it aims");
            Assert.That(aiming.LookAt, Is.EqualTo(target));

            brain.Tick(0.3f, Sees(target));
            var firing = brain.Tick(0.3f, Sees(target));
            Assert.That(firing.Fire, Is.True);
            Assert.That(firing.MoveTo, Is.EqualTo(target), "once firing it walks in");
            Assert.That(brain.AimRemainingSeconds, Is.Zero);
        }

        [Test]
        public void WalkingIntoTheAttackRangeKeepsFiringWithoutAimingAgain()
        {
            var brain = new EnemyBrain(Spec());
            brain.Tick(0f, Sees(new Vector3(0f, 0f, 15f)));
            brain.Tick(0.6f, Sees(new Vector3(0f, 0f, 15f)));
            brain.RecordShot();

            var decision = brain.Tick(0.1f, Sees(new Vector3(0f, 0f, 9f)));

            Assert.That(brain.State, Is.EqualTo(EnemyState.Attack));
            Assert.That(decision.Fire, Is.True);
            Assert.That(decision.MoveTo, Is.Null);
            Assert.That(brain.ShotsInAttack, Is.EqualTo(1), "the spread keeps narrowing");
        }

        [Test]
        public void LosingSightMakesItAimAgain()
        {
            var brain = new EnemyBrain(Spec());
            brain.Tick(0f, Sees(new Vector3(0f, 0f, 15f)));
            brain.Tick(0.6f, Sees(new Vector3(0f, 0f, 15f)));

            brain.Tick(0.1f, Nothing());
            Assert.That(brain.IsAiming, Is.False);

            var decision = brain.Tick(0.1f, Sees(new Vector3(0f, 0f, 15f)));
            Assert.That(decision.Fire, Is.False);
            Assert.That(brain.AimRemainingSeconds, Is.EqualTo(0.5f).Within(1e-5f));
        }

        [Test]
        public void AFireRangeNoLongerThanTheAttackRangeKeepsTheOldWayOfWaitingUntilAttack()
        {
            var brain = new EnemyBrain(Spec(fireRange: 0f));

            var decision = brain.Tick(1f, Sees(new Vector3(0f, 0f, 15f)));

            Assert.That(brain.State, Is.EqualTo(EnemyState.Chase));
            Assert.That(brain.IsAiming, Is.False);
            Assert.That(decision.Fire, Is.False);
            Assert.That(decision.MoveTo, Is.EqualTo(new Vector3(0f, 0f, 15f)));
        }

        [Test]
        public void TheFireRangeIsKeptBetweenTheAttackRangeAndTheSightRange()
        {
            Assert.That(Spec(fireRange: 0f).FireRange, Is.EqualTo(10f));
            Assert.That(Spec(fireRange: 50f).FireRange, Is.EqualTo(20f));
            Assert.That(EnemySpec.Default.FireRange, Is.EqualTo(EnemySpec.DefaultFireRange));
            Assert.That(EnemySpec.Default.FireToleranceDegrees, Is.EqualTo(EnemySpec.DefaultFireToleranceDegrees));
        }

        [TestCase(-1f, 15f, "fireRange")]
        [TestCase(18f, -1f, "fireToleranceDegrees")]
        [TestCase(18f, 181f, "fireToleranceDegrees")]
        public void BadFireValuesAreRejected(float fireRange, float tolerance, string param)
        {
            var e = Assert.Throws<ArgumentOutOfRangeException>(
                () => new EnemySpec(20f, 10f, 1f, 2f, 0.5f, fireRange: fireRange, fireToleranceDegrees: tolerance));
            Assert.That(e.ParamName, Is.EqualTo(param));
        }

        [Test]
        public void WaveStrengthKeepsTheFireRangeAndNarrowsTheRangeSpread()
        {
            var spec = new EnemySpec(20f, 10f, 1f, 2f, 0.5f, fireRange: 18f, fireToleranceDegrees: 15f, rangeSpreadDegrees: 4f);

            var scaled = WaveStrength.Apply(spec, 2f);

            Assert.That(scaled.FireRange, Is.EqualTo(18f));
            Assert.That(scaled.FireToleranceDegrees, Is.EqualTo(15f));
            Assert.That(scaled.RangeSpreadDegrees, Is.EqualTo(2f));
        }

        [Test]
        public void TheRangeSpreadGrowsFromTheAttackRangeToTheFireRange()
        {
            var spec = new EnemySpec(20f, 10f, 1f, 2f, 0.5f, fireRange: 18f, rangeSpreadDegrees: 4f);

            Assert.That(spec.RangeSpreadDegreesAt(5f), Is.Zero);
            Assert.That(spec.RangeSpreadDegreesAt(10f), Is.Zero);
            Assert.That(spec.RangeSpreadDegreesAt(14f), Is.EqualTo(2f).Within(1e-5f));
            Assert.That(spec.RangeSpreadDegreesAt(18f), Is.EqualTo(4f).Within(1e-5f));
            Assert.That(spec.RangeSpreadDegreesAt(20f), Is.EqualTo(4f).Within(1e-5f));
        }

        [Test]
        public void WithoutAFireRangeBeyondTheAttackRangeThereIsNoRangeSpread()
        {
            var spec = new EnemySpec(20f, 10f, 1f, 2f, 0.5f, rangeSpreadDegrees: 4f);

            Assert.That(spec.RangeSpreadDegreesAt(15f), Is.Zero);
        }

        [Test]
        public void TheKitDefaultsAimFromFurtherThanTheyStopAndSeeFurtherStill()
        {
            var spec = EnemySpec.Default;

            Assert.That(spec.AttackRange, Is.LessThan(spec.FireRange));
            Assert.That(spec.FireRange, Is.LessThan(spec.SightRange));
            Assert.That(spec.RangeSpreadDegrees, Is.EqualTo(EnemySpec.DefaultRangeSpreadDegrees));
        }
    }
}
