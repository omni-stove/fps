using System;
using System.Numerics;
using NUnit.Framework;

namespace Fps.Core.Tests
{
    // Unity の EditMode テストと dotnet/Fps.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class LadderClimbTests
    {
        // 2 進で割り切れる刻みにして、高さを足し合わせても誤差が出ないようにする。
        private const float _stepSeconds = 0.125f;

        // 高さ 3m、2 m/s で上り下りするはしご。
        private static readonly LadderSpan _span = new LadderSpan(Vector3.Zero, Vector3.UnitZ, 3f, 0.8f);
        private static readonly LadderSpec _spec = new LadderSpec(2f, LadderSpec.DefaultGrabDistance, 4f, 0.5f);

        [Test]
        public void HoldingForwardClimbsToTheTopAndStepsOff()
        {
            var climb = new LadderClimb(_span, _spec, LadderEntry.Front, 0f);

            var outcome = climb.Step(1f, false, true, _stepSeconds);
            Assert.That(outcome, Is.EqualTo(LadderOutcome.Climbing), "standing on the ground does not end the climb up");
            Assert.That(climb.Height, Is.EqualTo(0.25f));
            Assert.That(climb.VerticalSpeed, Is.EqualTo(2f));

            var steps = 1;
            while (outcome == LadderOutcome.Climbing && steps < 100)
            {
                outcome = climb.Step(1f, false, false, _stepSeconds);
                steps++;
            }

            Assert.That(outcome, Is.EqualTo(LadderOutcome.ExitTop));
            Assert.That(climb.Height, Is.EqualTo(3f));
            Assert.That(steps, Is.EqualTo(12), "3m at 2 m/s in 0.125s steps");
        }

        [Test]
        public void HoldingBackClimbsDownAndStepsOffAtTheBottom()
        {
            var climb = new LadderClimb(_span, _spec, LadderEntry.Front, 1f);

            var outcome = LadderOutcome.Climbing;
            var steps = 0;
            while (outcome == LadderOutcome.Climbing && steps < 100)
            {
                outcome = climb.Step(-1f, false, false, _stepSeconds);
                steps++;
            }

            Assert.That(outcome, Is.EqualTo(LadderOutcome.ExitBottom));
            Assert.That(climb.Height, Is.EqualTo(0f));
            Assert.That(climb.VerticalSpeed, Is.EqualTo(-2f));
        }

        [Test]
        public void TouchingGroundOnTheWayDownStepsOff()
        {
            var climb = new LadderClimb(_span, _spec, LadderEntry.Front, 2f);

            Assert.That(climb.Step(-1f, false, true, _stepSeconds), Is.EqualTo(LadderOutcome.ExitBottom));
            Assert.That(climb.Height, Is.EqualTo(1.75f));
        }

        [Test]
        public void LettingGoOfTheStickHangsInPlace()
        {
            var climb = new LadderClimb(_span, _spec, LadderEntry.Front, 1f);

            Assert.That(climb.Step(0.05f, false, false, _stepSeconds), Is.EqualTo(LadderOutcome.Climbing));
            Assert.That(climb.Height, Is.EqualTo(1f));
            Assert.That(climb.VerticalSpeed, Is.EqualTo(0f));
        }

        [Test]
        public void JumpLetsGoWithoutMoving()
        {
            var climb = new LadderClimb(_span, _spec, LadderEntry.Front, 1f);

            Assert.That(climb.Step(1f, true, false, _stepSeconds), Is.EqualTo(LadderOutcome.JumpOff));
            Assert.That(climb.Height, Is.EqualTo(1f));
            var away = _span.JumpOffVelocity(_spec.JumpOffSpeed);
            Assert.That(away.Z, Is.EqualTo(-4f), "away from the ladder");
            Assert.That(away.X, Is.EqualTo(0f));
            Assert.That(away.Y, Is.EqualTo(0f));
        }

        [Test]
        public void GrabbingFromTheTopStartsJustBelowItAndTheEntryInputClimbsDown()
        {
            var forwardIn = new LadderClimb(_span, _spec, LadderEntry.Top, 3f);
            Assert.That(forwardIn.Height, Is.EqualTo(3f - LadderSpan.TopEntryDepth).Within(1e-5f));
            Assert.That(forwardIn.IsInputLatched, Is.True);

            Assert.That(forwardIn.Step(1f, false, false, _stepSeconds), Is.EqualTo(LadderOutcome.Climbing));
            Assert.That(forwardIn.VerticalSpeed, Is.EqualTo(-2f), "walked off the edge forwards, so forward climbs down");
            Assert.That(forwardIn.Step(-1f, false, false, _stepSeconds), Is.EqualTo(LadderOutcome.Climbing));
            Assert.That(forwardIn.VerticalSpeed, Is.EqualTo(2f), "the other way climbs up");

            var backIn = new LadderClimb(_span, _spec, LadderEntry.Top, 3f);
            backIn.Step(-1f, false, false, _stepSeconds);
            Assert.That(backIn.VerticalSpeed, Is.EqualTo(-2f), "backed off the edge, so back climbs down");
        }

        [Test]
        public void ReleasingTheStickAfterATopGrabRestoresForwardAsUp()
        {
            var climb = new LadderClimb(_span, _spec, LadderEntry.Top, 3f);
            climb.Step(1f, false, false, _stepSeconds);

            climb.Step(0f, false, false, _stepSeconds);
            Assert.That(climb.IsInputLatched, Is.False);

            climb.Step(1f, false, false, _stepSeconds);
            Assert.That(climb.VerticalSpeed, Is.EqualTo(2f));
        }

        [Test]
        public void AFrontGrabStartsWhereTheFeetAreButBelowTheTopEntry()
        {
            Assert.That(new LadderClimb(_span, _spec, LadderEntry.Front, -0.2f).Height, Is.EqualTo(0f));
            Assert.That(new LadderClimb(_span, _spec, LadderEntry.Front, 1.2f).Height, Is.EqualTo(1.2f));
            Assert.That(new LadderClimb(_span, _spec, LadderEntry.Front, 3f).Height, Is.EqualTo(3f - LadderSpan.TopEntryDepth).Within(1e-5f));
            Assert.That(new LadderClimb(_span, _spec, LadderEntry.Front, 0f).IsInputLatched, Is.False);
        }

        [Test]
        public void TheFeetFollowTheHeight()
        {
            var climb = new LadderClimb(_span, _spec, LadderEntry.Front, 1f);

            Assert.That(climb.Feet(0.5f), Is.EqualTo(_span.FeetAt(1f, 0.5f)));
        }

        [Test]
        public void ArgumentsAreChecked()
        {
            Assert.Throws<ArgumentException>(() => new LadderClimb(default, _spec, LadderEntry.Front, 0f));
            Assert.Throws<ArgumentNullException>(() => new LadderClimb(_span, null, LadderEntry.Front, 0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new LadderClimb(_span, _spec, LadderEntry.None, 0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new LadderClimb(_span, _spec, LadderEntry.Front, float.NaN));

            var climb = new LadderClimb(_span, _spec, LadderEntry.Front, 0f);
            Assert.Throws<ArgumentOutOfRangeException>(() => climb.Step(float.NaN, false, false, _stepSeconds));
            Assert.Throws<ArgumentOutOfRangeException>(() => climb.Step(1f, false, false, -1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => climb.Step(1f, false, false, float.PositiveInfinity));
        }
    }
}
