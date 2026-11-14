using System;
using NUnit.Framework;

namespace Fps.Core.Tests
{
    // Unity の EditMode テストと dotnet/Fps.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    // 秒数は 2 進で割り切れる値にする。0.1 などを足し重ねると丸めで境目の手前に止まり、Tick の判定がずれる。
    public sealed class ContextMeleeRunTests
    {
        private static ContextMeleeMove Move(float alignSeconds = 0.25f, float performSeconds = 1f, bool interruptible = true)
        {
            return new ContextMeleeMove(
                "move", 0, ContextMeleeConditions.TargetStaggered, ContextMeleeConditions.None, 2f, 1f, alignSeconds, performSeconds,
                ContextMeleeOutcome.Kill, 0, 0, 0, interruptible);
        }

        [Test]
        public void ItAlignsThenPerformsThenResolvesOnce()
        {
            var run = new ContextMeleeRun();
            var move = Move();
            Assert.That(run.Begin(move), Is.True);
            Assert.That(run.IsActive, Is.True);
            Assert.That(run.Phase, Is.EqualTo(ContextMeleePhase.Aligning));

            Assert.That(run.Tick(0.125f), Is.EqualTo(ContextMeleeEvents.None));
            Assert.That(run.AlignProgress, Is.EqualTo(0.5f).Within(1e-5f));

            Assert.That(run.Tick(0.125f), Is.EqualTo(ContextMeleeEvents.Aligned));
            Assert.That(run.Phase, Is.EqualTo(ContextMeleePhase.Performing));
            Assert.That(run.AlignProgress, Is.EqualTo(1f));

            Assert.That(run.Tick(0.5f), Is.EqualTo(ContextMeleeEvents.None));
            Assert.That(run.Tick(0.5f), Is.EqualTo(ContextMeleeEvents.Resolved));
            Assert.That(run.IsActive, Is.False);
            Assert.That(run.Phase, Is.EqualTo(ContextMeleePhase.Idle));
            Assert.That(run.Move, Is.Null);
            Assert.That(run.Tick(1f), Is.EqualTo(ContextMeleeEvents.None));
        }

        [Test]
        public void ALargeTickAlignsAndResolvesTogether()
        {
            var run = new ContextMeleeRun();
            run.Begin(Move());
            Assert.That(run.Tick(5f), Is.EqualTo(ContextMeleeEvents.Aligned | ContextMeleeEvents.Resolved));
            Assert.That(run.IsActive, Is.False);
        }

        [Test]
        public void AMoveWithoutAlignmentIsAlignedFromTheStart()
        {
            var run = new ContextMeleeRun();
            run.Begin(Move(alignSeconds: 0f));
            Assert.That(run.AlignProgress, Is.EqualTo(1f));
            Assert.That(run.Tick(0f), Is.EqualTo(ContextMeleeEvents.Aligned));
        }

        [Test]
        public void ItCannotBeginTwice()
        {
            var run = new ContextMeleeRun();
            var first = Move();
            Assert.That(run.Begin(first), Is.True);
            Assert.That(run.Begin(Move()), Is.False);
            Assert.That(run.Move, Is.SameAs(first));
            Assert.Throws<ArgumentNullException>(() => run.Begin(null));
        }

        [Test]
        public void OnlyAnInterruptibleMoveStopsWhenInterrupted()
        {
            var run = new ContextMeleeRun();
            run.Begin(Move(interruptible: false));
            run.Tick(0.5f);
            Assert.That(run.TryInterrupt(), Is.False);
            Assert.That(run.IsActive, Is.True);

            Assert.That(run.Cancel(), Is.True, "cancelling ignores the setting");
            Assert.That(run.IsActive, Is.False);
            Assert.That(run.Cancel(), Is.False);

            run.Begin(Move(interruptible: true));
            Assert.That(run.TryInterrupt(), Is.True);
            Assert.That(run.IsActive, Is.False);
            Assert.That(run.Elapsed, Is.EqualTo(0f));
        }

        [Test]
        public void TickRejectsBadSeconds()
        {
            var run = new ContextMeleeRun();
            Assert.Throws<ArgumentOutOfRangeException>(() => run.Tick(-0.1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => run.Tick(float.NaN));
            Assert.Throws<ArgumentOutOfRangeException>(() => run.Tick(float.PositiveInfinity));
        }
    }
}
