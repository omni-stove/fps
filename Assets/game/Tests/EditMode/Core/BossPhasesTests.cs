using System;
using NUnit.Framework;

namespace Fps.Core.Tests
{
    // Unity の EditMode テストと dotnet/Fps.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class BossPhasesTests
    {
        private static BossPhases TwoPhases()
        {
            return new BossPhases(new[]
            {
                new BossPhase("Guarded", null),
                new BossPhase("Exposed", BossPhaseTrigger.HealthAtOrBelow(0.5f), moveSpeedMultiplier: 2f, exposedWeakPoints: new[] { "Core" }),
            });
        }

        [Test]
        public void StartsInTheFirstPhase()
        {
            var phases = TwoPhases();

            Assert.That(phases.CurrentIndex, Is.Zero);
            Assert.That(phases.Current.Id, Is.EqualTo("Guarded"));
            Assert.That(phases.IsFinal, Is.False);
            Assert.That(phases.SecondsInPhase, Is.Zero);
        }

        [Test]
        public void AdvancesWhenHealthReachesTheFraction()
        {
            var phases = TwoPhases();

            Assert.That(phases.Update(0.51f, 0f).HasValue, Is.False, "above half stays");

            var change = phases.Update(0.5f, 0f);

            Assert.That(change.HasValue, Is.True, "exactly half advances");
            Assert.That(change.Value.PreviousIndex, Is.Zero);
            Assert.That(change.Value.Index, Is.EqualTo(1));
            Assert.That(change.Value.Previous.Id, Is.EqualTo("Guarded"));
            Assert.That(change.Value.Current.Id, Is.EqualTo("Exposed"));
            Assert.That(change.Value.HealthFraction, Is.EqualTo(0.5f));
            Assert.That(change.Value.IsReset, Is.False);
            Assert.That(phases.Current.Exposes("Core"), Is.True);
            Assert.That(phases.Current.MoveSpeedMultiplier, Is.EqualTo(2f));
            Assert.That(phases.IsFinal, Is.True);
        }

        [Test]
        public void NeverGoesBackWhenHealthRises()
        {
            var phases = TwoPhases();
            phases.Update(0.4f, 0f);

            Assert.That(phases.Update(1f, 0f).HasValue, Is.False);
            Assert.That(phases.CurrentIndex, Is.EqualTo(1));
        }

        [Test]
        public void SkipsPhasesWhoseTriggersAreAlreadyMetAndReportsOneChange()
        {
            var phases = new BossPhases(new[]
            {
                new BossPhase("A", null),
                new BossPhase("B", BossPhaseTrigger.HealthAtOrBelow(0.75f)),
                new BossPhase("C", BossPhaseTrigger.HealthAtOrBelow(0.5f)),
                new BossPhase("D", BossPhaseTrigger.HealthAtOrBelow(0.25f)),
            });

            var change = phases.Update(0.3f, 0f);

            Assert.That(change.HasValue, Is.True);
            Assert.That(change.Value.PreviousIndex, Is.Zero);
            Assert.That(change.Value.Index, Is.EqualTo(2));
            Assert.That(change.Value.Current.Id, Is.EqualTo("C"));
        }

        [Test]
        public void CustomTriggerSeesTheSecondsInThePhase()
        {
            var phases = new BossPhases(new[]
            {
                new BossPhase("Intro", null),
                new BossPhase("Fight", BossPhaseTrigger.When("after 3s", c => c.SecondsInPhase >= 3f)),
                new BossPhase("Late", BossPhaseTrigger.When("after 2s more", c => c.SecondsInPhase >= 2f)),
            });

            Assert.That(phases.Update(1f, 2f).HasValue, Is.False);
            Assert.That(phases.SecondsInPhase, Is.EqualTo(2f));

            var change = phases.Update(1f, 1.5f);

            Assert.That(change.HasValue, Is.True);
            Assert.That(change.Value.Current.Id, Is.EqualTo("Fight"));
            Assert.That(phases.SecondsInPhase, Is.Zero, "the clock restarts with each phase, so Late does not follow at once");
            Assert.That(phases.Update(1f, 1.5f).HasValue, Is.False);
            Assert.That(phases.Update(1f, 0.5f).Value.Current.Id, Is.EqualTo("Late"));
        }

        [Test]
        public void CustomTriggerGetsTheHealthAndThePhaseIndex()
        {
            BossPhaseContext seen = default;
            var phases = new BossPhases(new[]
            {
                new BossPhase("A", null),
                new BossPhase("B", BossPhaseTrigger.When("record", c =>
                {
                    seen = c;
                    return false;
                })),
            });

            phases.Update(0.8f, 0.25f);

            Assert.That(seen.HealthFraction, Is.EqualTo(0.8f));
            Assert.That(seen.SecondsInPhase, Is.EqualTo(0.25f));
            Assert.That(seen.PhaseIndex, Is.Zero);
        }

        [Test]
        public void AdvanceMovesOnWithoutTheTriggerAndStopsAtTheLastPhase()
        {
            var phases = TwoPhases();

            var change = phases.Advance(0.9f);

            Assert.That(change.HasValue, Is.True);
            Assert.That(change.Value.Index, Is.EqualTo(1));
            Assert.That(change.Value.HealthFraction, Is.EqualTo(0.9f));
            Assert.That(phases.Advance(0.9f).HasValue, Is.False);
        }

        [Test]
        public void ResetGoesBackToTheFirstPhase()
        {
            var phases = TwoPhases();
            Assert.That(phases.Reset(1f).HasValue, Is.False, "nothing to report in the first phase");
            phases.Update(0.2f, 0f);

            var change = phases.Reset(1f);

            Assert.That(change.HasValue, Is.True);
            Assert.That(change.Value.IsReset, Is.True);
            Assert.That(change.Value.PreviousIndex, Is.EqualTo(1));
            Assert.That(change.Value.Index, Is.Zero);
            Assert.That(phases.CurrentIndex, Is.Zero);
            Assert.That(phases.SecondsInPhase, Is.Zero);
        }

        [Test]
        public void RefusesInvalidPhaseLists()
        {
            Assert.Throws<ArgumentNullException>(() => new BossPhases(null));
            Assert.Throws<ArgumentException>(() => new BossPhases(Array.Empty<BossPhase>()));
            Assert.Throws<ArgumentException>(() => new BossPhases(new BossPhase[] { null }));

            // 最初の段階に条件・2 番目以降に条件が無い・Id の重なり。
            Assert.Throws<ArgumentException>(
                () => new BossPhases(new[] { new BossPhase("A", BossPhaseTrigger.HealthAtOrBelow(0.5f)) }));
            Assert.Throws<ArgumentException>(
                () => new BossPhases(new[] { new BossPhase("A", null), new BossPhase("B", null) }));
            Assert.Throws<ArgumentException>(
                () => new BossPhases(new[] { new BossPhase("A", null), new BossPhase("A", BossPhaseTrigger.HealthAtOrBelow(0.5f)) }));
        }

        [TestCase(-0.1f)]
        [TestCase(1.1f)]
        [TestCase(float.NaN)]
        public void RefusesHealthOutsideZeroToOne(float fraction)
        {
            var phases = TwoPhases();

            Assert.Throws<ArgumentOutOfRangeException>(() => phases.Update(fraction, 0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => phases.Advance(fraction));
            Assert.Throws<ArgumentOutOfRangeException>(() => phases.Reset(fraction));
        }

        [TestCase(-1f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        public void RefusesBadDeltaSeconds(float deltaSeconds)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => TwoPhases().Update(1f, deltaSeconds));
        }

        [TestCase(0f)]
        [TestCase(1.5f)]
        [TestCase(float.NaN)]
        public void HealthTriggerRefusesFractionsOutsideTheRange(float fraction)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => BossPhaseTrigger.HealthAtOrBelow(fraction));
        }

        [Test]
        public void HealthTriggerDescribesItself()
        {
            var trigger = BossPhaseTrigger.HealthAtOrBelow(0.5f);

            Assert.That(trigger.HealthFraction, Is.EqualTo(0.5f));
            Assert.That(trigger.Description, Is.EqualTo("health<=0.5"));
        }

        [Test]
        public void CustomTriggerNeedsADescriptionAndACondition()
        {
            Assert.Throws<ArgumentException>(() => BossPhaseTrigger.When(" ", _ => true));
            Assert.Throws<ArgumentNullException>(() => BossPhaseTrigger.When("x", null));
            Assert.That(BossPhaseTrigger.When("x", _ => true).HealthFraction, Is.Null);
        }

        [Test]
        public void PhaseDropsRepeatedWeakPointsAndRefusesBadValues()
        {
            var phase = new BossPhase("A", null, exposedWeakPoints: new[] { "Core", "Core", "Eye" });

            Assert.That(phase.ExposedWeakPoints, Is.EqualTo(new[] { "Core", "Eye" }));
            Assert.That(phase.Exposes("Eye"), Is.True);
            Assert.That(phase.Exposes("Tail"), Is.False);
            Assert.That(new BossPhase("B", null).ExposedWeakPoints, Is.Empty);

            Assert.Throws<ArgumentException>(() => new BossPhase(" ", null));
            Assert.Throws<ArgumentException>(() => new BossPhase("A", null, exposedWeakPoints: new[] { "" }));
            Assert.Throws<ArgumentOutOfRangeException>(() => new BossPhase("A", null, moveSpeedMultiplier: 0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new BossPhase("A", null, moveSpeedMultiplier: float.PositiveInfinity));
        }

        [TestCase(0, 100, 0f)]
        [TestCase(-5, 100, 0f)]
        [TestCase(50, 100, 0.5f)]
        [TestCase(150, 100, 1f)]
        public void HealthFractionIsClampedToZeroToOne(int current, int max, float expected)
        {
            Assert.That(BossPhases.HealthFraction(current, max), Is.EqualTo(expected));
        }

        [Test]
        public void HealthFractionRefusesANonPositiveMax()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => BossPhases.HealthFraction(1, 0));
        }
    }
}
