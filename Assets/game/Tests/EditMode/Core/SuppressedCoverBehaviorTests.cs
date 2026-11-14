using System;
using System.Numerics;
using NUnit.Framework;

namespace Fps.Core.Tests
{
    // Unity の EditMode テストと dotnet/Fps.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class SuppressedCoverBehaviorTests
    {
        private static readonly Vector3 _point = new Vector3(0f, 0f, 1f);
        private static readonly Vector3 _threat = new Vector3(0f, 0f, 10f);

        // 隠れて 1 秒、覗いて 2 秒、反応 0.1 秒。
        private static EnemySpec Spec() => new EnemySpec(20f, 15f, 1f, 2f, 0.1f, 0f, 0f, 0, 1f, 2f, 0.5f);

        private static EnemyPerception See(bool wasHit, bool suppressed) =>
            new EnemyPerception(
                _point,
                _threat,
                true,
                false,
                null,
                wasHit,
                wasHit ? _threat : (Vector3?)null,
                1f,
                EnemyPerception.FullAwareness,
                suppressed,
                suppressed ? 0.6f : 0f);

        // 遮蔽点の上で撃たれて隠れ、隠れ終わって覗き始めたところまで進める。
        private static EnemyBrain Peeking(SuppressedCoverBehavior behavior)
        {
            var brain = new EnemyBrain(Spec());
            brain.UseCover(new CoverPointSelector(new[] { new CoverPoint(_point, Vector3.UnitZ, CoverHeight.Low) }));
            if (behavior != null)
            {
                brain.AddBehavior(behavior);
            }

            brain.Tick(0.1f, See(true, false));
            Assert.That(brain.CoverPhase, Is.EqualTo(EnemyCoverPhase.Hiding));
            brain.Tick(1.1f, See(false, false));
            Assert.That(brain.CoverPhase, Is.EqualTo(EnemyCoverPhase.Peeking));
            return brain;
        }

        [Test]
        public void SuppressedWhilePeekingGoesBackToCoverAndHoldsFire()
        {
            var behavior = new SuppressedCoverBehavior();
            var brain = Peeking(behavior);

            var decision = brain.Tick(0.2f, See(false, true));

            Assert.That(behavior.IsHolding, Is.True);
            Assert.That(decision.Fire, Is.False);
            Assert.That(decision.Crouch, Is.True);
            Assert.That(decision.MoveTo, Is.EqualTo(_point));
            Assert.That(decision.LookAt, Is.EqualTo(_threat), "still faces the threat");
        }

        [Test]
        public void WithoutSuppressionThePeekIsUnchanged()
        {
            var behavior = new SuppressedCoverBehavior();
            var with = Peeking(behavior);
            var without = Peeking(null);

            var expected = without.Tick(0.2f, See(false, false));
            var actual = with.Tick(0.2f, See(false, false));

            Assert.That(behavior.IsHolding, Is.False);
            Assert.That(actual.Fire, Is.True, "peeking past the reaction time fires");
            Assert.That(actual.Fire, Is.EqualTo(expected.Fire));
            Assert.That(actual.Crouch, Is.EqualTo(expected.Crouch));
            Assert.That(actual.MoveTo, Is.EqualTo(expected.MoveTo));
        }

        [Test]
        public void SuppressedWhileHidingKeepsHidingWithoutOverriding()
        {
            var behavior = new SuppressedCoverBehavior();
            var brain = new EnemyBrain(Spec());
            brain.UseCover(new CoverPointSelector(new[] { new CoverPoint(_point, Vector3.UnitZ, CoverHeight.Low) }));
            brain.Tick(0.1f, See(true, false));
            Assert.That(brain.CoverPhase, Is.EqualTo(EnemyCoverPhase.Hiding));

            var planned = new EnemyDecision(null, _threat, false, true);
            var overridden = behavior.TryOverride(brain, 0.1f, See(false, true), planned, out var decision);

            Assert.That(overridden, Is.False);
            Assert.That(behavior.IsHolding, Is.True);
            Assert.That(decision.Crouch, Is.True);
        }

        [Test]
        public void HidingContinuesForAsLongAsTheSuppressionLasts()
        {
            var behavior = new SuppressedCoverBehavior();
            var brain = Peeking(behavior);

            // 覗く時間・隠れる時間を何度か回しても、制圧されている間は一度も撃たず立たない。
            for (var i = 0; i < 40; i++)
            {
                var decision = brain.Tick(0.25f, See(false, true));
                Assert.That(decision.Fire, Is.False, $"tick {i}");
                Assert.That(decision.Crouch, Is.True, $"tick {i}");
            }

            Assert.That(brain.State, Is.EqualTo(EnemyState.TakeCover));
        }

        [Test]
        public void TheEnemyPeeksAgainOnceTheSuppressionLifts()
        {
            var behavior = new SuppressedCoverBehavior();
            var brain = Peeking(behavior);
            brain.Tick(0.2f, See(false, true));

            EnemyDecision decision = default;
            for (var i = 0; i < 20 && !decision.Fire; i++)
            {
                decision = brain.Tick(0.2f, See(false, false));
            }

            Assert.That(behavior.IsHolding, Is.False);
            Assert.That(decision.Fire, Is.True);
            Assert.That(decision.Crouch, Is.False);
        }

        [Test]
        public void OutsideCoverItDoesNothing()
        {
            var behavior = new SuppressedCoverBehavior();
            var brain = new EnemyBrain(Spec());
            brain.AddBehavior(behavior);

            var decision = brain.Tick(0.2f, See(false, true));

            Assert.That(brain.State, Is.EqualTo(EnemyState.Attack));
            Assert.That(behavior.IsHolding, Is.False);
            Assert.That(decision.Crouch, Is.False);
        }

        [Test]
        public void PerceptionCarriesTheMetersLevel()
        {
            var meter = new SuppressionMeter(new SuppressionSpec(2f, 0.3f, 1f, 0.5f, 0.5f, 0.2f));
            meter.AddNearMiss(0f);
            var contributor = new SuppressionPerception(meter);

            var perception = contributor.Contribute(See(false, false));

            Assert.That(perception.SuppressionLevel, Is.EqualTo(0.3f).Within(1e-5f));
            Assert.That(perception.IsSuppressed, Is.False);
            Assert.That(perception.Target, Is.EqualTo(_threat), "the rest of the perception is kept");
            Assert.That(perception.HasLineOfSight, Is.True);

            meter.AddNearMiss(0f);
            Assert.That(contributor.Contribute(See(false, false)).IsSuppressed, Is.True);
            Assert.Throws<ArgumentNullException>(() => _ = new SuppressionPerception(null));
        }

        [Test]
        public void TheOldSuppressedFlagSetsAFullOrEmptyLevel()
        {
            var on = new EnemyPerception(_point, _threat, true, false, null, false, null, 1f, 1f, true);
            var off = new EnemyPerception(_point, _threat, true, false, null, false, null, 1f, 1f, false);

            Assert.That(on.SuppressionLevel, Is.EqualTo(1f));
            Assert.That(off.SuppressionLevel, Is.EqualTo(0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => off.WithSuppression(1.5f, true));
            Assert.Throws<ArgumentOutOfRangeException>(() => off.WithSuppression(float.NaN, false));
        }
    }
}
