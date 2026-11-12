using System;
using NUnit.Framework;

namespace Fps.Core.Tests
{
    // Unity の EditMode テストと dotnet/Fps.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class RespawnCountdownTests
    {
        [Test]
        public void DoesNotFireBeforeStarting()
        {
            var countdown = new RespawnCountdown(3f);

            Assert.That(countdown.IsRunning, Is.False);
            Assert.That(countdown.RemainingSeconds, Is.EqualTo(0f));
            Assert.That(countdown.Tick(10f), Is.False);
        }

        [Test]
        public void FiresOnceWhenTheDelayHasPassed()
        {
            var countdown = new RespawnCountdown(3f);
            countdown.Start();

            Assert.That(countdown.IsRunning, Is.True);
            Assert.That(countdown.RemainingSeconds, Is.EqualTo(3f));
            Assert.That(countdown.Tick(1f), Is.False);
            Assert.That(countdown.Tick(1.5f), Is.False);
            Assert.That(countdown.RemainingSeconds, Is.EqualTo(0.5f).Within(1e-5f));

            Assert.That(countdown.Tick(0.5f), Is.True);
            Assert.That(countdown.IsRunning, Is.False);
            Assert.That(countdown.RemainingSeconds, Is.EqualTo(0f));
            Assert.That(countdown.Tick(1f), Is.False);
        }

        [Test]
        public void FiresOnTheFirstTickWhenTheFrameOvershoots()
        {
            var countdown = new RespawnCountdown(3f);
            countdown.Start();

            Assert.That(countdown.Tick(5f), Is.True);
        }

        [Test]
        public void ZeroDelayFiresOnTheNextTick()
        {
            var countdown = new RespawnCountdown(0f);
            countdown.Start();

            Assert.That(countdown.Tick(0f), Is.True);
        }

        [Test]
        public void StartingAgainRestartsFromTheFullDelay()
        {
            var countdown = new RespawnCountdown(3f);
            countdown.Start();
            countdown.Tick(2f);

            countdown.Start();

            Assert.That(countdown.RemainingSeconds, Is.EqualTo(3f));
            Assert.That(countdown.Tick(2f), Is.False);
        }

        [Test]
        public void CancelStopsIt()
        {
            var countdown = new RespawnCountdown(3f);
            countdown.Start();

            countdown.Cancel();

            Assert.That(countdown.IsRunning, Is.False);
            Assert.That(countdown.Tick(10f), Is.False);
        }

        [TestCase(-1f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        public void RejectsBadDelays(float delay)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new RespawnCountdown(delay));
        }

        [TestCase(-0.1f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        public void RejectsBadDeltas(float delta)
        {
            var countdown = new RespawnCountdown(3f);
            countdown.Start();

            Assert.Throws<ArgumentOutOfRangeException>(() => countdown.Tick(delta));
        }
    }
}
