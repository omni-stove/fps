using System;
using NUnit.Framework;

namespace Shooter.Core.Tests
{
    // Unity の EditMode テストと dotnet/Shooter.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
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
        public void TheTakedownNeedsAnUnawareTargetFromBehindAndTakesItsAmountsFromTheMeleeSpec()
        {
            var spec = new MeleeSpec(50, 2.5f, 90f, 0.5f, 0.4f, 0.2f, 0.75f, 0.9f, 15, 12, true, 2f, false, 0.6f);
            var takedown = ContextMeleeMove.Takedown(spec);

            Assert.That(takedown.Id, Is.EqualTo(ContextMeleeMove.TakedownId));
            Assert.That(takedown.Priority, Is.EqualTo(ContextMeleeMove.TakedownPriority));
            Assert.That(takedown.Priority, Is.LessThan(ContextMeleeMove.FinisherPriority));
            Assert.That(takedown.Required, Is.EqualTo(ContextMeleeConditions.TargetBehind | ContextMeleeConditions.TargetUnaware));
            Assert.That(takedown.Excluded, Is.EqualTo(ContextMeleeConditions.None));
            Assert.That(takedown.Reach, Is.EqualTo(2.5f));
            Assert.That(takedown.PerformSeconds, Is.EqualTo(2f));
            Assert.That(takedown.Outcome, Is.EqualTo(ContextMeleeOutcome.Kill));
            Assert.That(takedown.HealthReward, Is.Zero);
            Assert.That(takedown.AmmoReward, Is.Zero);
            Assert.That(takedown.Interruptible, Is.False);
            Assert.That(takedown.WitnessAwareness, Is.EqualTo(0.6f));
            Assert.Throws<ArgumentNullException>(() => ContextMeleeMove.Takedown(null));
        }

        [Test]
        public void TheExecuteMoveNeedsADownedTargetAndTakesItsAmountsFromTheExecutionSpec()
        {
            var execute = ContextMeleeMove.Execute(new ExecutionSpec(2.5f, 3f));

            Assert.That(execute.Id, Is.EqualTo(ContextMeleeMove.ExecuteId));
            Assert.That(execute.Priority, Is.EqualTo(ContextMeleeMove.ExecutePriority));
            Assert.That(execute.Priority, Is.GreaterThan(ContextMeleeMove.FinisherPriority));
            Assert.That(execute.Priority, Is.GreaterThan(ContextMeleeMove.TakedownPriority));
            Assert.That(execute.Required, Is.EqualTo(ContextMeleeConditions.TargetDowned));
            Assert.That(execute.Excluded, Is.EqualTo(ContextMeleeConditions.None));
            Assert.That(execute.Reach, Is.EqualTo(3f));
            Assert.That(execute.StandDistance, Is.Zero);
            Assert.That(execute.AlignSeconds, Is.Zero);
            Assert.That(execute.PerformSeconds, Is.EqualTo(2.5f));
            Assert.That(execute.TotalSeconds, Is.EqualTo(2.5f));
            Assert.That(execute.Outcome, Is.EqualTo(ContextMeleeOutcome.Kill));
            Assert.That(execute.HealthReward, Is.Zero);
            Assert.That(execute.AmmoReward, Is.Zero);
            Assert.That(execute.Interruptible, Is.False);
            Assert.That(execute.WitnessAwareness, Is.Zero);
            Assert.Throws<ArgumentNullException>(() => ContextMeleeMove.Execute(null));
        }

        [Test]
        public void TheExecuteMoveIsChosenOverTheTakedownForADownedTarget()
        {
            var selector = new ContextMeleeSelector();
            selector.Register(ContextMeleeMove.Takedown(MeleeSpec.Default));
            selector.Register(ContextMeleeMove.Execute(ExecutionSpec.Default));
            var everything = ContextMeleeConditions.TargetDowned | ContextMeleeConditions.TargetBehind | ContextMeleeConditions.TargetUnaware;

            Assert.That(selector.Select(new ContextMeleeSituation(everything, 1f)).Id, Is.EqualTo(ContextMeleeMove.ExecuteId));
            Assert.That(
                selector.Select(new ContextMeleeSituation(ContextMeleeConditions.TargetBehind | ContextMeleeConditions.TargetUnaware, 1f)).Id,
                Is.EqualTo(ContextMeleeMove.TakedownId));
            Assert.That(selector.Select(new ContextMeleeSituation(ContextMeleeConditions.TargetDowned, ExecutionSpec.DefaultExecuteRange + 0.1f)), Is.Null);
        }

        [Test]
        public void OnlyTheTakedownRaisesWitnessesByDefault()
        {
            Assert.That(Make().WitnessAwareness, Is.Zero);
            Assert.That(ContextMeleeMove.Finisher(MeleeSpec.Default).WitnessAwareness, Is.Zero);
            Assert.That(ContextMeleeMove.Takedown(MeleeSpec.Default).WitnessAwareness, Is.EqualTo(MeleeSpec.DefaultTakedownWitnessAwareness));
        }

        [Test]
        public void TheWitnessAwarenessMustBeALevel()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new ContextMeleeMove(
                "move", 0, ContextMeleeConditions.TargetBehind, ContextMeleeConditions.None, 2f, 1f, 0.1f, 1f, ContextMeleeOutcome.Kill, 0, 0, 0, true, -0.1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ContextMeleeMove(
                "move", 0, ContextMeleeConditions.TargetBehind, ContextMeleeConditions.None, 2f, 1f, 0.1f, 1f, ContextMeleeOutcome.Kill, 0, 0, 0, true, 1.5f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ContextMeleeMove(
                "move", 0, ContextMeleeConditions.TargetBehind, ContextMeleeConditions.None, 2f, 1f, 0.1f, 1f, ContextMeleeOutcome.Kill, 0, 0, 0, true, float.NaN));
        }

        [Test]
        public void TheMeleeSpecDefaultsTheTakedownAndRejectsBadTakedownValues()
        {
            var spec = MeleeSpec.Default;
            Assert.That(spec.TakedownSeconds, Is.EqualTo(MeleeSpec.DefaultTakedownSeconds));
            Assert.That(spec.TakedownInterruptible, Is.EqualTo(MeleeSpec.DefaultTakedownInterruptible));
            Assert.That(spec.TakedownWitnessAwareness, Is.EqualTo(MeleeSpec.DefaultTakedownWitnessAwareness));

            Assert.Throws<ArgumentOutOfRangeException>(() => new MeleeSpec(50, 2f, 90f, 0.5f, 0.4f, 0.2f, 0.75f, 1f, 0, 0, true, 0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new MeleeSpec(50, 2f, 90f, 0.5f, 0.4f, 0.2f, 0.75f, 1f, 0, 0, true, float.NaN));
            Assert.Throws<ArgumentOutOfRangeException>(() => new MeleeSpec(50, 2f, 90f, 0.5f, 0.4f, 0.2f, 0.75f, 1f, 0, 0, true, 1f, true, -0.1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new MeleeSpec(50, 2f, 90f, 0.5f, 0.4f, 0.2f, 0.75f, 1f, 0, 0, true, 1f, true, 1.1f));
            Assert.That(new MeleeSpec(50, 2f, 90f, 0.5f, 0.4f, 0.2f, 0.75f, 1f, 0, 0, true, 1f, true, 0f).TakedownWitnessAwareness, Is.Zero);
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
