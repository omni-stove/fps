using System;
using System.Numerics;
using NUnit.Framework;

namespace Shooter.Core.Tests
{
    public sealed class CoverStateTests
    {
        private const float _frame = 0.05f;

        private static readonly Vector3 _wallNormal = new Vector3(0f, 0f, -1f);
        private static readonly Vector2 _noMove = Vector2.Zero;
        private static readonly Vector2 _back = new Vector2(0f, -1f);

        private static CoverProbeResult Wall(CoverHeight height = CoverHeight.High, CoverEdge edges = CoverEdge.None, bool canEnter = true)
        {
            return WallAt(0.55f, height, edges, canEnter);
        }

        private static CoverProbeResult WallAt(float distance, CoverHeight height = CoverHeight.High, CoverEdge edges = CoverEdge.None, bool canEnter = true)
        {
            return new CoverProbeResult(true, canEnter, height, edges, _wallNormal, distance, 2f);
        }

        // 入って寄せ終わった (中の) 状態にする。
        private static CoverState InCover(CoverProbeResult wall)
        {
            var state = new CoverState(MotorSpec.Default);
            Assert.That(state.Step(true, false, _noMove, wall, 0f), Is.EqualTo(CoverTransition.Enter));
            state.Step(false, false, _noMove, wall, MotorSpec.DefaultCoverEnterSeconds);
            Assert.That(state.Phase, Is.EqualTo(CoverPhase.In));
            return state;
        }

        [Test]
        public void StartsOutOfCover()
        {
            var state = new CoverState(MotorSpec.Default);

            Assert.That(state.Phase, Is.EqualTo(CoverPhase.Out));
            Assert.That(state.IsInCover, Is.False);
            Assert.That(state.IsPeeking, Is.False);
            Assert.That(state.EnterStep, Is.EqualTo(0f));
            Assert.That(state.EnterTurn, Is.EqualTo(0f));
            Assert.That(state.LastExitReason, Is.Null);
        }

        [Test]
        public void PressingCoverInFrontOfAWallEnters()
        {
            var state = new CoverState(MotorSpec.Default);

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
            var state = new CoverState(MotorSpec.Default);

            var transition = state.Step(true, false, _noMove, Wall(CoverHeight.High), _frame);

            Assert.That(state.FormatLog(transition), Is.EqualTo("[Cover] enter high"));
        }

        [Test]
        public void DoesNotEnterWithoutPressingCover()
        {
            var state = new CoverState(MotorSpec.Default);

            Assert.That(state.Step(false, true, _noMove, Wall(), _frame), Is.EqualTo(CoverTransition.None));
            Assert.That(state.IsInCover, Is.False);
        }

        [Test]
        public void DoesNotEnterWhenTheWallIsOutOfReachOrAngle()
        {
            var state = new CoverState(MotorSpec.Default);

            Assert.That(state.Step(true, false, _noMove, CoverProbeResult.None, _frame), Is.EqualTo(CoverTransition.None));
            Assert.That(state.Step(true, false, _noMove, Wall(canEnter: false), _frame), Is.EqualTo(CoverTransition.None));
            Assert.That(state.IsInCover, Is.False);
        }

        // 0.55 m 先の壁で半径 0 なら隙間は 0.5 m。0.1 / 0.15 秒で 1/3 m 寄せ、残りは最後の Step で寄せ切る。
        [Test]
        public void ANearWallTakesEnterSecondsBeforeBeingIn()
        {
            var state = new CoverState(MotorSpec.Default);
            state.Step(true, false, _noMove, WallAt(0.55f), 0f);

            state.Step(false, false, _noMove, WallAt(0.55f), 0.1f);
            Assert.That(state.Phase, Is.EqualTo(CoverPhase.Entering));
            Assert.That(state.EnterStep, Is.EqualTo(0.5f / 3f * 2f).Within(1e-5f));
            Assert.That(state.EnterTurn, Is.EqualTo(2f / 3f).Within(1e-5f));

            Assert.That(state.Step(false, false, _noMove, WallAt(0.55f - (1f / 3f)), 0.06f), Is.EqualTo(CoverTransition.None));
            Assert.That(state.Phase, Is.EqualTo(CoverPhase.In));
            Assert.That(state.EnterStep, Is.EqualTo(0.5f / 3f).Within(1e-5f));
            Assert.That(state.EnterTurn, Is.EqualTo(1f));
        }

        // 3.05 m 先の壁の隙間 3 m を、既定の 8 m/s で 0.1 秒ごとに 0.8 m ずつ寄せる。
        [Test]
        public void AFarWallIsRunToAtTheEnterSpeed()
        {
            var state = new CoverState(MotorSpec.Default);
            state.Step(true, false, _noMove, WallAt(3.05f), 0f);

            var distance = 3.05f;
            var steps = 0;
            while (state.Phase == CoverPhase.Entering && steps < 100)
            {
                state.Step(false, false, _noMove, WallAt(distance), 0.1f);
                distance -= state.EnterStep;
                steps++;
                if (state.Phase == CoverPhase.Entering)
                {
                    Assert.That(state.EnterStep, Is.EqualTo(0.8f).Within(1e-5f));
                }
            }

            Assert.That(state.Phase, Is.EqualTo(CoverPhase.In));
            Assert.That(steps, Is.EqualTo(4), "3 m at 0.8 m per step");
            Assert.That(distance, Is.EqualTo(0.05f).Within(1e-4f), "stops at the wall gap");
        }

        [Test]
        public void TheBodyRadiusMovesTheStopPoint()
        {
            var state = new CoverState(MotorSpec.Default) { BodyRadius = 0.4f };
            state.Step(true, false, _noMove, WallAt(1f), 0f);

            state.Step(false, false, _noMove, WallAt(1f), MotorSpec.DefaultCoverEnterSeconds);

            Assert.That(state.Phase, Is.EqualTo(CoverPhase.In));
            Assert.That(state.EnterStep, Is.EqualTo(0.55f).Within(1e-5f), "1 m minus the 0.4 m radius and the 0.05 m gap");
            Assert.Throws<ArgumentOutOfRangeException>(() => state.BodyRadius = -0.1f);
            Assert.Throws<ArgumentOutOfRangeException>(() => state.BodyRadius = float.NaN);
        }

        // 壁が近づかない (体が何かに引っかかって動けない) まま、0.15 + 4 / 8 = 0.65 秒たつと出る。
        [Test]
        public void EnteringThatCannotCloseTheGapExitsAsBlocked()
        {
            var state = new CoverState(MotorSpec.Default);
            state.Step(true, false, _noMove, WallAt(3f), 0f);

            var transition = CoverTransition.None;
            var steps = 0;
            while (transition == CoverTransition.None && steps < 100)
            {
                transition = state.Step(false, false, _noMove, WallAt(3f), 0.05f);
                steps++;
            }

            Assert.That(state.EnterTimeoutSeconds, Is.EqualTo(0.65f).Within(1e-5f));
            Assert.That(transition, Is.EqualTo(CoverTransition.Exit));
            Assert.That(steps, Is.EqualTo(13).Within(1));
            Assert.That(state.LastExitReason, Is.EqualTo(CoverState.ReasonBlocked));
            Assert.That(state.FormatLog(transition), Is.EqualTo("[Cover] exit blocked"));
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
            var state = new CoverState(MotorSpec.Default);
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
            Assert.That(left.PeekOffset, Is.EqualTo(-MotorSpec.DefaultCoverPeekSideDistance));
            Assert.That(left.FormatLog(peekLeft), Is.EqualTo("[Cover] peek left"));

            var right = InCover(Wall(CoverHeight.High, CoverEdge.Right));
            var peekRight = right.Step(false, true, _noMove, Wall(CoverHeight.High, CoverEdge.Right), _frame);

            Assert.That(right.PeekOffset, Is.EqualTo(MotorSpec.DefaultCoverPeekSideDistance));
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

            Assert.That(state.PeekOffset, Is.EqualTo(-MotorSpec.DefaultCoverPeekSideDistance));
        }

        [Test]
        public void DoesNotPeekWhileStillEntering()
        {
            var state = new CoverState(MotorSpec.Default);
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
            var state = new CoverState(MotorSpec.Default);
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
            Assert.That(new CoverState(MotorSpec.Default).FormatLog(CoverTransition.None), Is.Null);
            Assert.Throws<ArgumentOutOfRangeException>(() => new CoverState(MotorSpec.Default).FormatLog((CoverTransition)99));
        }

        [Test]
        public void RejectsInvalidTimeAndInput()
        {
            var state = new CoverState(MotorSpec.Default);

            Assert.Throws<ArgumentOutOfRangeException>(() => state.Step(false, false, _noMove, Wall(), -0.01f));
            Assert.Throws<ArgumentOutOfRangeException>(() => state.Step(false, false, _noMove, Wall(), float.NaN));
            Assert.Throws<ArgumentOutOfRangeException>(() => state.Step(false, false, new Vector2(float.NaN, 0f), Wall(), _frame));
        }
    }
}
