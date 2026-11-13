using System;
using NUnit.Framework;

namespace Fps.Core.Tests
{
    // Unity の EditMode テストと dotnet/Fps.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class ServerDrainTests
    {
        private static string[] Args(string commandLine)
        {
            return commandLine.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
        }

        [Test]
        public void TheTimeoutDefaultsTo600SecondsWhenTheFlagIsAbsent()
        {
            Assert.That(ServerDrain.ResolveTimeoutSeconds(Args("fps-server -batchmode -mode server"), 0), Is.EqualTo(600));
            Assert.That(ServerDrain.DefaultTimeoutSeconds, Is.EqualTo(600));
        }

        [TestCase("-drain-timeout 0", 0)]
        [TestCase("-drain-timeout 30", 30)]
        [TestCase("-DRAIN-TIMEOUT 30", 30)]
        public void AWrittenTimeoutIsKeptIncludingZero(string flags, int parsed)
        {
            Assert.That(ServerDrain.ResolveTimeoutSeconds(Args("fps-server -mode server " + flags), parsed), Is.EqualTo(parsed));
        }

        [Test]
        public void ResolveNeedsArguments()
        {
            Assert.Throws<ArgumentNullException>(() => ServerDrain.ResolveTimeoutSeconds(null, 0));
        }

        [TestCase(-1)]
        [TestCase(NetLaunchOptions.MaxDrainTimeoutSeconds + 1)]
        public void RejectsTimeoutsOutOfRange(int seconds)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new ServerDrain(seconds));
        }

        [Test]
        public void TheFirstRequestHoldsTheQuitWhileAMatchIsBeingPlayed()
        {
            var drain = new ServerDrain(600);

            Assert.That(drain.State, Is.EqualTo(ServerDrainState.Idle));
            Assert.That(drain.IsDraining, Is.False);
            Assert.That(drain.Request(10f, false, 1), Is.True);
            Assert.That(drain.IsDraining, Is.True);
            Assert.That(drain.Deadline, Is.EqualTo(610f));
            Assert.That(drain.Stop, Is.EqualTo(ServerDrainStop.None));
        }

        [Test]
        public void StopsWhenTheMatchEnds()
        {
            var drain = new ServerDrain(600);
            drain.Request(10f, false, 2);

            Assert.That(drain.Tick(100f, false, 2), Is.False);
            Assert.That(drain.Tick(101f, true, 2), Is.True);
            Assert.That(drain.State, Is.EqualTo(ServerDrainState.Finished));
            Assert.That(drain.Stop, Is.EqualTo(ServerDrainStop.MatchEnded));
            Assert.That(drain.DescribeStop(), Is.EqualTo("the match ended"));
        }

        [Test]
        public void StopsWhenEveryoneLeaves()
        {
            var drain = new ServerDrain(600);
            drain.Request(10f, false, 1);

            Assert.That(drain.Tick(20f, false, 0), Is.True);
            Assert.That(drain.Stop, Is.EqualTo(ServerDrainStop.NoPlayers));
        }

        [Test]
        public void StopsWhenTheTimeoutPasses()
        {
            var drain = new ServerDrain(30);
            drain.Request(10f, false, 1);

            Assert.That(drain.Tick(39.9f, false, 1), Is.False);
            Assert.That(drain.Tick(40f, false, 1), Is.True);
            Assert.That(drain.Stop, Is.EqualTo(ServerDrainStop.TimedOut));
            Assert.That(drain.DescribeStop(), Does.Contain("30s"));
        }

        [Test]
        public void ASecondRequestStopsAtOnce()
        {
            var drain = new ServerDrain(600);
            drain.Request(10f, false, 1);

            Assert.That(drain.Request(11f, false, 1), Is.False);
            Assert.That(drain.Stop, Is.EqualTo(ServerDrainStop.RequestedAgain));
            Assert.That(drain.IsDraining, Is.False);
        }

        [TestCase(false, 0, ServerDrainStop.NoPlayers)]
        [TestCase(true, 3, ServerDrainStop.MatchEnded)]
        public void DoesNotHoldWhenThereIsNothingToWaitFor(bool matchOver, int players, ServerDrainStop expected)
        {
            var drain = new ServerDrain(600);

            Assert.That(drain.Request(10f, matchOver, players), Is.False);
            Assert.That(drain.Stop, Is.EqualTo(expected));
        }

        [Test]
        public void AZeroTimeoutDoesNotHold()
        {
            var drain = new ServerDrain(0);

            Assert.That(drain.Request(10f, false, 4), Is.False);
            Assert.That(drain.Stop, Is.EqualTo(ServerDrainStop.TimedOut));
        }

        [Test]
        public void TheDecisionIsKeptAfterStopping()
        {
            var drain = new ServerDrain(600);
            drain.Request(10f, false, 1);
            drain.Tick(20f, true, 1);

            // 待ち終えた後に自分で呼ぶ終了も、ここを通る
            Assert.That(drain.Request(21f, false, 1), Is.False);
            Assert.That(drain.Tick(22f, false, 0), Is.False);
            Assert.That(drain.Stop, Is.EqualTo(ServerDrainStop.MatchEnded));
        }

        [Test]
        public void TickBeforeARequestDoesNothing()
        {
            var drain = new ServerDrain(600);

            Assert.That(drain.Tick(1000f, true, 0), Is.False);
            Assert.That(drain.State, Is.EqualTo(ServerDrainState.Idle));
        }
    }
}
