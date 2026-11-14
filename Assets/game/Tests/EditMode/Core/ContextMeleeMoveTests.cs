using System;
using NUnit.Framework;

namespace Fps.Core.Tests
{
    // Unity の EditMode テストと dotnet/Fps.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class ContextMeleeMoveTests
    {
        private static ContextMeleeMove Make(
            string id = "move",
            ContextMeleeConditions required = ContextMeleeConditions.TargetStaggered,
            ContextMeleeConditions excluded = ContextMeleeConditions.None,
            float reach = 2f,
            float standDistance = 1f,
            float alignSeconds = 0.1f,
            float performSeconds = 1f,
            ContextMeleeOutcome outcome = ContextMeleeOutcome.Kill,
            int damage = 0,
            int healthReward = 0,
            int ammoReward = 0)
        {
            return new ContextMeleeMove(
                id, 0, required, excluded, reach, standDistance, alignSeconds, performSeconds, outcome, damage, healthReward, ammoReward, true);
        }

        [Test]
        public void TheMoveRejectsBadValues()
        {
            Assert.Throws<ArgumentException>(() => Make(id: string.Empty));
            Assert.Throws<ArgumentException>(() => Make(id: "two words"));
            Assert.Throws<ArgumentException>(() => Make(
                required: ContextMeleeConditions.TargetStaggered, excluded: ContextMeleeConditions.TargetStaggered | ContextMeleeConditions.SelfSprinting));
            Assert.Throws<ArgumentOutOfRangeException>(() => Make(reach: 0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => Make(reach: float.PositiveInfinity));
            Assert.Throws<ArgumentOutOfRangeException>(() => Make(standDistance: -0.1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => Make(alignSeconds: float.NaN));
            Assert.Throws<ArgumentOutOfRangeException>(() => Make(performSeconds: 0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => Make(outcome: (ContextMeleeOutcome)99));
            Assert.Throws<ArgumentOutOfRangeException>(() => Make(outcome: ContextMeleeOutcome.Damage, damage: 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => Make(damage: -1));
            Assert.Throws<ArgumentOutOfRangeException>(() => Make(healthReward: -1));
            Assert.Throws<ArgumentOutOfRangeException>(() => Make(ammoReward: -1));
        }

        [Test]
        public void TotalSecondsAddsTheAlignAndThePerformance()
        {
            Assert.That(Make(alignSeconds: 0.25f, performSeconds: 1f).TotalSeconds, Is.EqualTo(1.25f).Within(1e-6f));
        }

        [Test]
        public void TheFinisherTakesItsAmountsFromTheMeleeSpec()
        {
            var spec = new MeleeSpec(50, 2.5f, 90f, 0.5f, 0.4f, 0.2f, 0.75f, 0.9f, 15, 12, false);
            var finisher = ContextMeleeMove.Finisher(spec);

            Assert.That(finisher.Id, Is.EqualTo(ContextMeleeMove.FinisherId));
            Assert.That(finisher.Priority, Is.EqualTo(ContextMeleeMove.FinisherPriority));
            Assert.That(finisher.Required, Is.EqualTo(ContextMeleeConditions.TargetStaggered));
            Assert.That(finisher.Excluded, Is.EqualTo(ContextMeleeConditions.None));
            Assert.That(finisher.Reach, Is.EqualTo(2.5f));
            Assert.That(finisher.PerformSeconds, Is.EqualTo(0.9f));
            Assert.That(finisher.Outcome, Is.EqualTo(ContextMeleeOutcome.Kill));
            Assert.That(finisher.HealthReward, Is.EqualTo(15));
            Assert.That(finisher.AmmoReward, Is.EqualTo(12));
            Assert.That(finisher.Interruptible, Is.False);
            Assert.Throws<ArgumentNullException>(() => ContextMeleeMove.Finisher(null));
        }

        [Test]
        public void TheMeleeSpecDefaultsTheFinisherAndRejectsBadFinisherValues()
        {
            var spec = MeleeSpec.Default;
            Assert.That(spec.FinisherSeconds, Is.EqualTo(MeleeSpec.DefaultFinisherSeconds));
            Assert.That(spec.FinisherHealth, Is.EqualTo(MeleeSpec.DefaultFinisherHealth));
            Assert.That(spec.FinisherAmmo, Is.EqualTo(MeleeSpec.DefaultFinisherAmmo));
            Assert.That(spec.FinisherInterruptible, Is.EqualTo(MeleeSpec.DefaultFinisherInterruptible));

            Assert.Throws<ArgumentOutOfRangeException>(() => new MeleeSpec(50, 2f, 90f, 0.5f, 0.4f, 0.2f, 0.75f, 0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new MeleeSpec(50, 2f, 90f, 0.5f, 0.4f, 0.2f, 0.75f, float.PositiveInfinity));
            Assert.Throws<ArgumentOutOfRangeException>(() => new MeleeSpec(50, 2f, 90f, 0.5f, 0.4f, 0.2f, 0.75f, 1f, -1));
            Assert.Throws<ArgumentOutOfRangeException>(() => new MeleeSpec(50, 2f, 90f, 0.5f, 0.4f, 0.2f, 0.75f, 1f, 0, -1));
        }
    }
}
