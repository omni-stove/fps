using System;
using System.Numerics;
using NUnit.Framework;

namespace Fps.Core.Tests
{
    // Unity の EditMode テストと dotnet/Fps.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class ContextMeleeSelectorTests
    {
        private static ContextMeleeMove Move(
            string id,
            int priority,
            ContextMeleeConditions required,
            ContextMeleeConditions excluded = ContextMeleeConditions.None,
            float reach = 2f)
        {
            return new ContextMeleeMove(id, priority, required, excluded, reach, 1f, 0.1f, 1f, ContextMeleeOutcome.Kill, 0, 0, 0, true);
        }

        private static ContextMeleeSituation At(ContextMeleeConditions conditions, float distance = 1f)
        {
            return new ContextMeleeSituation(conditions, distance);
        }

        [Test]
        public void WithNoMovesNothingIsSelected()
        {
            var selector = new ContextMeleeSelector();
            Assert.That(selector.Select(At(ContextMeleeConditions.TargetStaggered)), Is.Null);
            Assert.That(selector.MaxReach(), Is.EqualTo(0f));
        }

        [Test]
        public void AMoveIsSelectedOnlyWhenAllItsConditionsHold()
        {
            var selector = new ContextMeleeSelector();
            var takedown = Move("takedown", 10, ContextMeleeConditions.TargetBehind | ContextMeleeConditions.TargetUnaware);
            selector.Register(takedown);

            Assert.That(selector.Select(At(ContextMeleeConditions.TargetBehind)), Is.Null);
            Assert.That(selector.Select(At(ContextMeleeConditions.TargetUnaware)), Is.Null);
            Assert.That(
                selector.Select(At(ContextMeleeConditions.TargetBehind | ContextMeleeConditions.TargetUnaware | ContextMeleeConditions.SelfGrounded)),
                Is.SameAs(takedown));
        }

        [Test]
        public void AnExcludedConditionBlocksTheMove()
        {
            var selector = new ContextMeleeSelector();
            selector.Register(Move("ground", 0, ContextMeleeConditions.TargetStaggered, ContextMeleeConditions.SelfSprinting));

            Assert.That(selector.Select(At(ContextMeleeConditions.TargetStaggered)), Is.Not.Null);
            Assert.That(selector.Select(At(ContextMeleeConditions.TargetStaggered | ContextMeleeConditions.SelfSprinting)), Is.Null);
        }

        [Test]
        public void ATargetBeyondTheMovesReachIsNotSelected()
        {
            var selector = new ContextMeleeSelector();
            selector.Register(Move("short", 0, ContextMeleeConditions.TargetStaggered, reach: 1.5f));

            Assert.That(selector.Select(At(ContextMeleeConditions.TargetStaggered, 1.5f)), Is.Not.Null);
            Assert.That(selector.Select(At(ContextMeleeConditions.TargetStaggered, 1.6f)), Is.Null);
        }

        [Test]
        public void TheHighestPriorityMatchWinsAndTiesGoToTheFirstRegistered()
        {
            var selector = new ContextMeleeSelector();
            var low = Move("low", 1, ContextMeleeConditions.TargetStaggered);
            var firstHigh = Move("first-high", 5, ContextMeleeConditions.TargetStaggered);
            var secondHigh = Move("second-high", 5, ContextMeleeConditions.TargetStaggered);
            selector.Register(low);
            selector.Register(firstHigh);
            selector.Register(secondHigh);

            Assert.That(selector.Moves, Is.EqualTo(new[] { firstHigh, secondHigh, low }));
            Assert.That(selector.Select(At(ContextMeleeConditions.TargetStaggered)), Is.SameAs(firstHigh));
        }

        [Test]
        public void AHigherPriorityMoveThatDoesNotMatchFallsThroughToTheNext()
        {
            var selector = new ContextMeleeSelector();
            var downed = Move("downed", 50, ContextMeleeConditions.TargetDowned);
            var staggered = Move("staggered", 10, ContextMeleeConditions.TargetStaggered);
            selector.Register(downed);
            selector.Register(staggered);

            Assert.That(selector.Select(At(ContextMeleeConditions.TargetStaggered)), Is.SameAs(staggered));
            Assert.That(selector.Select(At(ContextMeleeConditions.TargetStaggered | ContextMeleeConditions.TargetDowned)), Is.SameAs(downed));
        }

        [Test]
        public void RegisteringTheSameIdReplacesTheMove()
        {
            var selector = new ContextMeleeSelector();
            selector.Register(Move("finisher", 1, ContextMeleeConditions.TargetStaggered));
            var replacement = Move("finisher", 1, ContextMeleeConditions.TargetDowned);
            selector.Register(replacement);

            Assert.That(selector.Moves.Count, Is.EqualTo(1));
            Assert.That(selector.Find("finisher"), Is.SameAs(replacement));
            Assert.That(selector.Select(At(ContextMeleeConditions.TargetStaggered)), Is.Null);

            Assert.That(selector.Unregister("finisher"), Is.True);
            Assert.That(selector.Unregister("finisher"), Is.False);
            Assert.That(selector.Find("finisher"), Is.Null);
        }

        [Test]
        public void MaxReachIsTheFarthestRegisteredMove()
        {
            var selector = new ContextMeleeSelector();
            selector.Register(Move("a", 0, ContextMeleeConditions.TargetStaggered, reach: 1.5f));
            selector.Register(Move("b", 0, ContextMeleeConditions.TargetDowned, reach: 3f));
            Assert.That(selector.MaxReach(), Is.EqualTo(3f));
        }

        [Test]
        public void NullArgumentsAreRejected()
        {
            var selector = new ContextMeleeSelector();
            Assert.Throws<ArgumentNullException>(() => selector.Register(null));
            Assert.Throws<ArgumentNullException>(() => ContextMeleeSelector.Matches(null, At(ContextMeleeConditions.None)));
        }

        [Test]
        public void TheSituationRejectsABadDistance()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new ContextMeleeSituation(ContextMeleeConditions.None, -0.1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ContextMeleeSituation(ContextMeleeConditions.None, float.NaN));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ContextMeleeSituation(ContextMeleeConditions.None, float.PositiveInfinity));
        }

        [Test]
        public void BehindMeansInTheRearArcOfTheTargetsFacing()
        {
            var target = Vector3.Zero;
            var facing = Vector3.UnitZ;

            Assert.That(ContextMeleeSelector.IsBehind(target, facing, new Vector3(0f, 0f, -2f), 90f), Is.True);
            Assert.That(ContextMeleeSelector.IsBehind(target, facing, new Vector3(0.5f, 0f, -2f), 90f), Is.True);
            Assert.That(ContextMeleeSelector.IsBehind(target, facing, new Vector3(0f, 0f, 2f), 90f), Is.False, "in front");
            Assert.That(ContextMeleeSelector.IsBehind(target, facing, new Vector3(2f, 0f, 0f), 90f), Is.False, "to the side");
            Assert.That(ContextMeleeSelector.IsBehind(target, facing, new Vector3(2f, 0f, -0.1f), 200f), Is.True, "a wide arc reaches the side");
        }

        [Test]
        public void BehindIsFalseWhenTheDirectionIsUndefined()
        {
            Assert.That(ContextMeleeSelector.IsBehind(Vector3.Zero, Vector3.UnitY, new Vector3(0f, 0f, -2f), 90f), Is.False, "facing straight up");
            Assert.That(ContextMeleeSelector.IsBehind(Vector3.Zero, Vector3.UnitZ, new Vector3(0f, 3f, 0f), 90f), Is.False, "directly above");
            Assert.That(ContextMeleeSelector.IsBehind(Vector3.Zero, Vector3.UnitZ, new Vector3(float.NaN, 0f, -2f), 90f), Is.False);
            Assert.That(ContextMeleeSelector.IsBehind(Vector3.Zero, Vector3.UnitZ, new Vector3(0f, 0f, -2f), 0f), Is.False);
        }

        [Test]
        public void TheStandPointKeepsTheApproachDirectionAndTheAttackersHeight()
        {
            var point = ContextMeleeSelector.StandPoint(new Vector3(3f, 1f, 0f), new Vector3(0f, 0.2f, 0f), Vector3.UnitZ, 1.2f);
            Assert.That(point.X, Is.EqualTo(1.2f).Within(1e-5f));
            Assert.That(point.Y, Is.EqualTo(1f).Within(1e-5f));
            Assert.That(point.Z, Is.EqualTo(0f).Within(1e-5f));
        }

        [Test]
        public void TheStandPointFallsBackToTheTargetsFrontWhenOverlapping()
        {
            var point = ContextMeleeSelector.StandPoint(Vector3.Zero, Vector3.Zero, -Vector3.UnitX, 1f);
            Assert.That(point.X, Is.EqualTo(-1f).Within(1e-5f));
            Assert.That(point.Z, Is.EqualTo(0f).Within(1e-5f));
            Assert.Throws<ArgumentOutOfRangeException>(() => ContextMeleeSelector.StandPoint(Vector3.Zero, Vector3.UnitZ, Vector3.UnitZ, -1f));
        }
    }
}
