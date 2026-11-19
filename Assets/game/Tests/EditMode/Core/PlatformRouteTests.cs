using System;
using System.Numerics;
using NUnit.Framework;

namespace Shooter.Core.Tests
{
    // Unity の EditMode テストと dotnet/Shooter.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class PlatformRouteTests
    {
        private const float _tolerance = 1e-4f;
        private static readonly int[] _expectedCalls = { 1 };

        private static readonly Vector3[] _line = { Vector3.Zero, new Vector3(0f, 0f, 4f) };
        private static readonly Vector3[] _shaft = { Vector3.Zero, new Vector3(0f, 4f, 0f) };
        private static readonly Vector3[] _triangle = { Vector3.Zero, new Vector3(4f, 0f, 0f), new Vector3(4f, 0f, 4f) };

        [Test]
        public void TheKitDefaultGoesBackAndForthWithoutSpinning()
        {
            Assert.That(MovingPlatformSpec.Default.Kind, Is.EqualTo(PlatformRouteKind.PingPong));
            Assert.That(MovingPlatformSpec.Default.Speed, Is.EqualTo(MovingPlatformSpec.DefaultSpeed));
            Assert.That(MovingPlatformSpec.Default.WaitSeconds, Is.EqualTo(MovingPlatformSpec.DefaultWaitSeconds));
            Assert.That(MovingPlatformSpec.Default.SpinDegreesPerSecond, Is.Zero);
            Assert.That(MovingPlatformSpec.Default.DepartWhenBoarded, Is.True);
        }

        [Test]
        public void TheSpecRejectsBadValues()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new MovingPlatformSpec((PlatformRouteKind)99, 1f, 0f, 0f, true));
            Assert.Throws<ArgumentOutOfRangeException>(() => new MovingPlatformSpec(PlatformRouteKind.PingPong, -1f, 0f, 0f, true));
            Assert.Throws<ArgumentOutOfRangeException>(() => new MovingPlatformSpec(PlatformRouteKind.PingPong, float.NaN, 0f, 0f, true));
            Assert.Throws<ArgumentOutOfRangeException>(() => new MovingPlatformSpec(PlatformRouteKind.PingPong, 1f, -0.5f, 0f, true));
            Assert.Throws<ArgumentOutOfRangeException>(() => new MovingPlatformSpec(PlatformRouteKind.PingPong, 1f, 0f, float.PositiveInfinity, true));
        }

        [Test]
        public void TheRouteRejectsBadPoints()
        {
            Assert.Throws<ArgumentNullException>(() => new PlatformRoute(null, MovingPlatformSpec.Default));
            Assert.Throws<ArgumentException>(() => new PlatformRoute(Array.Empty<Vector3>(), MovingPlatformSpec.Default));
            Assert.Throws<ArgumentException>(() => new PlatformRoute(new[] { new Vector3(float.NaN, 0f, 0f) }, MovingPlatformSpec.Default));
            Assert.Throws<ArgumentNullException>(() => new PlatformRoute(_line, null));
            Assert.Throws<ArgumentOutOfRangeException>(() => new PlatformRoute(_line, MovingPlatformSpec.Default).Step(-1f));
        }

        [Test]
        public void APingPongRouteWaitsAtTheEndAndComesBack()
        {
            var route = new PlatformRoute(_line, Spec(PlatformRouteKind.PingPong, speed: 2f, wait: 1f));

            route.Step(1f);
            Assert.That(route.Position.Z, Is.EqualTo(2f).Within(_tolerance));
            Assert.That(route.IsMoving, Is.True);

            route.Step(1f);
            Assert.That(route.Position.Z, Is.EqualTo(4f).Within(_tolerance));
            Assert.That(route.IsWaiting, Is.True);
            Assert.That(route.From, Is.EqualTo(1));

            route.Step(1f);
            Assert.That(route.Position.Z, Is.EqualTo(4f).Within(_tolerance), "still waiting");

            route.Step(1f);
            Assert.That(route.Position.Z, Is.EqualTo(2f).Within(_tolerance));
            Assert.That(route.From, Is.EqualTo(1));
            Assert.That(route.To, Is.EqualTo(0));
        }

        [Test]
        public void ALoopRouteGoesFromTheLastPointToTheFirst()
        {
            var route = new PlatformRoute(_triangle, Spec(PlatformRouteKind.Loop, speed: 4f, wait: 0f));

            route.Step(1f);
            route.Step(1f);
            Assert.That(route.Position.X, Is.EqualTo(4f).Within(_tolerance));
            Assert.That(route.Position.Z, Is.EqualTo(4f).Within(_tolerance));

            route.Step(0.5f);
            Assert.That(route.From, Is.EqualTo(2));
            Assert.That(route.To, Is.EqualTo(0));
            var along = 2f / MathF.Sqrt(32f);
            Assert.That(route.Position.X, Is.EqualTo(4f - (4f * along)).Within(_tolerance));
            Assert.That(route.Position.Z, Is.EqualTo(4f - (4f * along)).Within(_tolerance));
        }

        [Test]
        public void ALongStepCarriesThePartLeftAfterAnArrivalIntoTheNextLeg()
        {
            var route = new PlatformRoute(_line, Spec(PlatformRouteKind.PingPong, speed: 2f, wait: 0.5f));

            route.Step(3f);

            // 2 秒で着き、0.5 秒待ち、残りの 0.5 秒で 1 m 戻る
            Assert.That(route.Position.Z, Is.EqualTo(3f).Within(_tolerance));
            Assert.That(route.To, Is.EqualTo(0));
        }

        [Test]
        public void ASpinningRouteKeepsTheAngleInOneTurn()
        {
            var route = new PlatformRoute(new[] { Vector3.Zero }, new MovingPlatformSpec(PlatformRouteKind.PingPong, 0f, 0f, 90f, false));

            route.Step(1f);
            Assert.That(route.SpinDegrees, Is.EqualTo(90f).Within(_tolerance));

            route.Step(3.5f);
            Assert.That(route.SpinDegrees, Is.EqualTo(45f).Within(_tolerance));
            Assert.That(route.Position, Is.EqualTo(Vector3.Zero), "a single point never moves");
        }

        [Test]
        public void PointsInTheSamePlaceDoNotHangTheStep()
        {
            var route = new PlatformRoute(new[] { Vector3.One, Vector3.One, Vector3.One }, Spec(PlatformRouteKind.Loop, speed: 1f, wait: 0f));

            route.Step(1f);

            Assert.That(route.Position, Is.EqualTo(Vector3.One));
        }

        [Test]
        public void AnElevatorWaitsUntilItIsCalled()
        {
            var route = new PlatformRoute(_shaft, Spec(PlatformRouteKind.OnCall, speed: 2f, wait: 0.5f));

            route.Step(5f);
            Assert.That(route.Position, Is.EqualTo(Vector3.Zero));
            Assert.That(route.IsIdle, Is.True);

            Assert.That(route.Call(1), Is.True);
            Assert.That(route.Call(1), Is.False, "a stop already called is not queued twice");
            route.Step(1f);
            Assert.That(route.Position.Y, Is.EqualTo(2f).Within(_tolerance));

            route.Step(1f);
            Assert.That(route.Position.Y, Is.EqualTo(4f).Within(_tolerance));
            Assert.That(route.Call(1), Is.False, "the elevator is already there");

            route.Step(10f);
            Assert.That(route.Position.Y, Is.EqualTo(4f).Within(_tolerance), "it stays at the called stop");
            Assert.That(route.IsIdle, Is.True);
        }

        [Test]
        public void AnElevatorServesTheCallsInOrder()
        {
            var floors = new[] { Vector3.Zero, new Vector3(0f, 4f, 0f), new Vector3(0f, 8f, 0f) };
            var route = new PlatformRoute(floors, Spec(PlatformRouteKind.OnCall, speed: 4f, wait: 0f));

            route.Call(2);
            route.Call(1);
            route.Step(2f);
            Assert.That(route.Position.Y, Is.EqualTo(8f).Within(_tolerance));

            route.Step(1f);
            Assert.That(route.Position.Y, Is.EqualTo(4f).Within(_tolerance));
        }

        [Test]
        public void CallNextSendsTheElevatorToTheOtherStop()
        {
            var route = new PlatformRoute(_shaft, Spec(PlatformRouteKind.OnCall, speed: 4f, wait: 0f));

            Assert.That(route.CallNext(), Is.True);
            route.Step(1f);
            Assert.That(route.Position.Y, Is.EqualTo(4f).Within(_tolerance));

            Assert.That(route.CallNext(), Is.True);
            route.Step(1f);
            Assert.That(route.Position.Y, Is.EqualTo(0f).Within(_tolerance));
        }

        [Test]
        public void OnlyAnElevatorTakesCalls()
        {
            var route = new PlatformRoute(_line, Spec(PlatformRouteKind.PingPong, speed: 1f, wait: 0f));

            Assert.That(route.Call(1), Is.False);
            Assert.That(route.CallNext(), Is.False);
            Assert.Throws<ArgumentOutOfRangeException>(() => route.Call(2));
        }

        [Test]
        public void ReversingUndoesTheLastStepAndTurnsBack()
        {
            var route = new PlatformRoute(_line, Spec(PlatformRouteKind.PingPong, speed: 2f, wait: 0f));
            route.Step(1f);
            route.Step(0.5f);

            route.Reverse();

            Assert.That(route.Position.Z, Is.EqualTo(2f).Within(_tolerance));
            Assert.That(route.From, Is.EqualTo(1));
            Assert.That(route.To, Is.EqualTo(0));

            route.Step(0.5f);
            Assert.That(route.Position.Z, Is.EqualTo(1f).Within(_tolerance));
        }

        [Test]
        public void AReversedElevatorReturnsAndThenTriesTheCallAgain()
        {
            var route = new PlatformRoute(_shaft, Spec(PlatformRouteKind.OnCall, speed: 2f, wait: 0.5f));
            route.Call(1);
            route.Step(0.5f);
            route.Step(0.5f);

            route.Reverse();

            Assert.That(route.Position.Y, Is.EqualTo(1f).Within(_tolerance));
            Assert.That(route.To, Is.EqualTo(0));
            Assert.That(route.Calls, Is.EqualTo(_expectedCalls));

            route.Step(0.5f);
            Assert.That(route.Position.Y, Is.EqualTo(0f).Within(_tolerance));
            route.Step(0.5f);
            route.Step(0.5f);
            Assert.That(route.Position.Y, Is.EqualTo(1f).Within(_tolerance), "after the wait it heads for the call again");
        }

        [Test]
        public void ReversingAtAStopFlipsTheSpinAndWaits()
        {
            var route = new PlatformRoute(new[] { Vector3.Zero }, new MovingPlatformSpec(PlatformRouteKind.PingPong, 0f, 1f, 90f, false));
            route.Step(1f);

            route.Reverse();
            Assert.That(route.SpinDegrees, Is.Zero.Within(_tolerance));
            Assert.That(route.IsWaiting, Is.True);

            route.Step(1f);
            Assert.That(route.SpinDegrees, Is.EqualTo(270f).Within(_tolerance));
        }

        private static MovingPlatformSpec Spec(PlatformRouteKind kind, float speed, float wait)
        {
            return new MovingPlatformSpec(kind, speed, wait, 0f, true);
        }
    }
}
