using System;
using System.Numerics;
using NUnit.Framework;

namespace Fps.Core.Tests
{
    public sealed class CoverStateTests
    {
        private const float _frame = 0.05f;

        private static readonly Vector3 _wallNormal = new Vector3(0f, 0f, -1f);
        private static readonly Vector2 _noMove = Vector2.Zero;
        private static readonly Vector2 _back = new Vector2(0f, -1f);

        private static CoverProbeResult Wall(CoverHeight height = CoverHeight.High, CoverEdge edges = CoverEdge.None, bool canEnter = true)
        {
            return new CoverProbeResult(true, canEnter, height, edges, _wallNormal, 0.55f);
        }

        // 入って寄せ終わった (中の) 状態にする。
        private static CoverState InCover(CoverProbeResult wall)
        {
            var state = new CoverState();
            Assert.That(state.Step(true, false, _noMove, wall, 0f), Is.EqualTo(CoverTransition.Enter));
            state.Step(false, false, _noMove, wall, CoverState.EnterSeconds);
            Assert.That(state.Phase, Is.EqualTo(CoverPhase.In));
            return state;
        }

        [Test]
        public void StartsOutOfCover()
        {
            var state = new CoverState();

            Assert.That(state.Phase, Is.EqualTo(CoverPhase.Out));
            Assert.That(state.IsInCover, Is.False);
            Assert.That(state.IsPeeking, Is.False);
            Assert.That(state.EnterProgress, Is.EqualTo(0f));
            Assert.That(state.LastExitReason, Is.Null);
        }

        [Test]
        public void PressingCoverInFrontOfAWallEnters()
        {
            var state = new CoverState();

            var transition = state.Step(true, false, _noMove, Wall(CoverHeight.Low), _frame);

            Assert.That(transition, Is.EqualTo(CoverTransition.Enter));
            Assert.That(state.Phase, Is.EqualTo(CoverPhase.Entering));
            Assert.That(state.IsInCover, Is.True);
            Assert.That(state.Height, Is.EqualTo(CoverHeight.Low));
            Assert.That(state.Normal, Is.EqualTo(_wallNormal));
            Assert.That(state.FormatLog(transition), Is.EqualTo("[Cover] enter low"));
        }

        [Test]
        public void EnterLogNamesHighWalls()
        {
            var state = new CoverState();

            var transition = state.Step(true, false, _noMove, Wall(CoverHeight.High), _frame);

            Assert.That(state.FormatLog(transition), Is.EqualTo("[Cover] enter high"));
        }

        [Test]
        public void DoesNotEnterWithoutPressingCover()
        {
            var state = new CoverState();

            Assert.That(state.Step(false, true, _noMove, Wall(), _frame), Is.EqualTo(CoverTransition.None));
            Assert.That(state.IsInCover, Is.False);
        }

        [Test]
        public void DoesNotEnterWhenTheWallIsOutOfReachOrAngle()
        {
            var state = new CoverState();

            Assert.That(state.Step(true, false, _noMove, CoverProbeResult.None, _frame), Is.EqualTo(CoverTransition.None));
            Assert.That(state.Step(true, false, _noMove, Wall(canEnter: false), _frame), Is.EqualTo(CoverTransition.None));
            Assert.That(state.IsInCover, Is.False);
        }

        [Test]
        public void EnteringTakesEnterSecondsBeforeBeingIn()
        {
            var state = new CoverState();
            state.Step(true, false, _noMove, Wall(), 0f);

            state.Step(false, false, _noMove, Wall(), 0.1f);
            Assert.That(state.Phase, Is.EqualTo(CoverPhase.Entering));
            Assert.That(state.EnterProgress, Is.EqualTo(0.1f / CoverState.EnterSeconds).Within(1e-5f));

            Assert.That(state.Step(false, false, _noMove, Wall(), 0.06f), Is.EqualTo(CoverTransition.None));
            Assert.That(state.Phase, Is.EqualTo(CoverPhase.In));
            Assert.That(state.EnterProgress, Is.EqualTo(1f));
        }

        [Test]
        public void PressingCoverAgainExits()
        {
            var state = InCover(Wall());

            var transition = state.Step(true, false, _noMove, Wall(), _frame);

            Assert.That(transition, Is.EqualTo(CoverTransition.Exit));
            Assert.That(state.Phase, Is.EqualTo(CoverPhase.Out));
            Assert.That(state.LastExitReason, Is.EqualTo(CoverState.ReasonButton));
            Assert.That(state.FormatLog(transition), Is.EqualTo("[Cover] exit button"));
        }

        [Test]
        public void PressingCoverWhileEnteringExits()
        {
            var state = new CoverState();
            state.Step(true, false, _noMove, Wall(), 0f);

            Assert.That(state.Step(true, false, _noMove, Wall(), _frame), Is.EqualTo(CoverTransition.Exit));
            Assert.That(state.IsInCover, Is.False);
        }

        [Test]
        public void HoldingBackForBackHoldSecondsExits()
        {
            var state = InCover(Wall());

            Assert.That(state.Step(false, false, _back, Wall(), 0.1f), Is.EqualTo(CoverTransition.None));
            Assert.That(state.Step(false, false, _back, Wall(), 0.09f), Is.EqualTo(CoverTransition.None));
            Assert.That(state.IsInCover, Is.True);

            Assert.That(state.Step(false, false, _back, Wall(), 0.02f), Is.EqualTo(CoverTransition.Exit));
            Assert.That(state.LastExitReason, Is.EqualTo(CoverState.ReasonBack));
        }

        [Test]
        public void LettingGoOfBackRestartsTheTimer()
        {
            var state = InCover(Wall());

            state.Step(false, false, _back, Wall(), 0.15f);
            state.Step(false, false, _noMove, Wall(), _frame);
            state.Step(false, false, _back, Wall(), 0.15f);

            Assert.That(state.IsInCover, Is.True);
        }

        [Test]
        public void ForwardAndSlightBackInputDoNotExit()
        {
            var state = InCover(Wall());

            state.Step(false, false, new Vector2(0f, 1f), Wall(), 1f);
            state.Step(false, false, new Vector2(1f, -0.3f), Wall(), 1f);

            Assert.That(state.IsInCover, Is.True);
        }

        [Test]
        public void LosingTheWallExits()
        {
            var state = InCover(Wall());

            var transition = state.Step(false, false, _noMove, CoverProbeResult.None, _frame);

            Assert.That(transition, Is.EqualTo(CoverTransition.Exit));
            Assert.That(state.LastExitReason, Is.EqualTo(CoverState.ReasonLost));
            Assert.That(state.FormatLog(transition), Is.EqualTo("[Cover] exit lost"));
        }

        [Test]
        public void TheWallOnlyNeedsToRemainNotToBeFaced()
        {
            var state = InCover(Wall());

            state.Step(false, false, _noMove, Wall(canEnter: false), _frame);

            Assert.That(state.IsInCover, Is.True);
        }

        [Test]
        public void AimingBehindALowWallPeeksUpAndReleasingHides()
        {
            var state = InCover(Wall(CoverHeight.Low));

            var peek = state.Step(false, true, _noMove, Wall(CoverHeight.Low), _frame);

            Assert.That(peek, Is.EqualTo(CoverTransition.Peek));
            Assert.That(state.IsPeeking, Is.True);
            Assert.That(state.PeekOffset, Is.EqualTo(0f));
            Assert.That(state.FormatLog(peek), Is.EqualTo("[Cover] peek up"));

            Assert.That(state.Step(false, true, _noMove, Wall(CoverHeight.Low), _frame), Is.EqualTo(CoverTransition.None));

            var hide = state.Step(false, false, _noMove, Wall(CoverHeight.Low), _frame);
            Assert.That(hide, Is.EqualTo(CoverTransition.Unpeek));
            Assert.That(state.Phase, Is.EqualTo(CoverPhase.In));
            Assert.That(state.FormatLog(hide), Is.EqualTo("[Cover] unpeek"));
        }

        [Test]
        public void AimingInTheMiddleOfAHighWallDoesNotPeek()
        {
            var state = InCover(Wall(CoverHeight.High));

            Assert.That(state.Step(false, true, _noMove, Wall(CoverHeight.High), _frame), Is.EqualTo(CoverTransition.None));
            Assert.That(state.IsPeeking, Is.False);
        }

        [Test]
        public void AimingAtTheEdgeOfAHighWallPeeksOutOfThatSide()
        {
            var left = InCover(Wall(CoverHeight.High, CoverEdge.Left));
            var peekLeft = left.Step(false, true, _noMove, Wall(CoverHeight.High, CoverEdge.Left), _frame);

            Assert.That(peekLeft, Is.EqualTo(CoverTransition.Peek));
            Assert.That(left.PeekOffset, Is.EqualTo(-CoverProbe.PeekSideDistance));
            Assert.That(left.FormatLog(peekLeft), Is.EqualTo("[Cover] peek left"));

            var right = InCover(Wall(CoverHeight.High, CoverEdge.Right));
            var peekRight = right.Step(false, true, _noMove, Wall(CoverHeight.High, CoverEdge.Right), _frame);

            Assert.That(right.PeekOffset, Is.EqualTo(CoverProbe.PeekSideDistance));
            Assert.That(right.FormatLog(peekRight), Is.EqualTo("[Cover] peek right"));
        }

        [Test]
        public void ReleasingAimReturnsFromTheEdge()
        {
            var wall = Wall(CoverHeight.High, CoverEdge.Left);
            var state = InCover(wall);
            state.Step(false, true, _noMove, wall, _frame);

            Assert.That(state.Step(false, false, _noMove, wall, _frame), Is.EqualTo(CoverTransition.Unpeek));
            Assert.That(state.PeekOffset, Is.EqualTo(0f));
        }

        [Test]
        public void ANarrowPillarPeeksToTheSideLastMovedTowards()
        {
            var pillar = Wall(CoverHeight.High, CoverEdge.Left | CoverEdge.Right);
            var state = InCover(pillar);

            state.Step(false, false, new Vector2(-1f, 0f), pillar, _frame);
            state.Step(false, true, _noMove, pillar, _frame);

            Assert.That(state.PeekOffset, Is.EqualTo(-CoverProbe.PeekSideDistance));
        }

        [Test]
        public void DoesNotPeekWhileStillEntering()
        {
            var state = new CoverState();
            state.Step(true, true, _noMove, Wall(CoverHeight.Low), 0f);

            Assert.That(state.Step(false, true, _noMove, Wall(CoverHeight.Low), 0.01f), Is.EqualTo(CoverTransition.None));
            Assert.That(state.IsPeeking, Is.False);
        }

        [Test]
        public void LosingTheWallWhilePeekingExits()
        {
            var state = InCover(Wall(CoverHeight.Low));
            state.Step(false, true, _noMove, Wall(CoverHeight.Low), _frame);

            Assert.That(state.Step(false, true, _noMove, CoverProbeResult.None, _frame), Is.EqualTo(CoverTransition.Exit));
            Assert.That(state.IsPeeking, Is.False);
            Assert.That(state.PeekOffset, Is.EqualTo(0f));
        }

        [Test]
        public void TheHeightFollowsTheWallWhileInCover()
        {
            var state = InCover(Wall(CoverHeight.High));

            state.Step(false, false, _noMove, Wall(CoverHeight.Low), _frame);

            Assert.That(state.Height, Is.EqualTo(CoverHeight.Low));
        }

        [Test]
        public void CanEnterAgainAfterExiting()
        {
            var state = InCover(Wall());
            state.Step(true, false, _noMove, Wall(), _frame);

            Assert.That(state.Step(true, false, _noMove, Wall(), _frame), Is.EqualTo(CoverTransition.Enter));
            Assert.That(state.Phase, Is.EqualTo(CoverPhase.Entering));
        }

        [Test]
        public void ReleaseExitsOnlyWhenInCover()
        {
            var state = new CoverState();
            Assert.That(state.Release("death"), Is.False);

            state.Step(true, false, _noMove, Wall(), 0f);
            Assert.That(state.Release("death"), Is.True);
            Assert.That(state.IsInCover, Is.False);
            Assert.That(state.FormatLog(CoverTransition.Exit), Is.EqualTo("[Cover] exit death"));
            Assert.Throws<ArgumentException>(() => state.Release(""));
        }

        [Test]
        public void NoneHasNoLog()
        {
            Assert.That(new CoverState().FormatLog(CoverTransition.None), Is.Null);
            Assert.Throws<ArgumentOutOfRangeException>(() => new CoverState().FormatLog((CoverTransition)99));
        }

        [Test]
        public void RejectsInvalidTimeAndInput()
        {
            var state = new CoverState();

            Assert.Throws<ArgumentOutOfRangeException>(() => state.Step(false, false, _noMove, Wall(), -0.01f));
            Assert.Throws<ArgumentOutOfRangeException>(() => state.Step(false, false, _noMove, Wall(), float.NaN));
            Assert.Throws<ArgumentOutOfRangeException>(() => state.Step(false, false, new Vector2(float.NaN, 0f), Wall(), _frame));
        }
    }
}
