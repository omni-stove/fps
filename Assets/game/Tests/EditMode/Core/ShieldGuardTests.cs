using System;
using NUnit.Framework;

namespace Fps.Core.Tests
{
    // Unity の EditMode テストと dotnet/Fps.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class ShieldGuardTests
    {
        [Test]
        public void StartsRaised()
        {
            var guard = new ShieldGuard();

            Assert.That(guard.IsRaised, Is.True);
            Assert.That(guard.SecondsRemaining, Is.EqualTo(0f));
            Assert.That(guard.Cause, Is.Empty);
        }

        [Test]
        public void LoweringReportsOnlyTheFirstDropAndKeepsItsCause()
        {
            var guard = new ShieldGuard();

            Assert.That(guard.Lower(1f, "melee"), Is.True);
            Assert.That(guard.IsRaised, Is.False);
            Assert.That(guard.Cause, Is.EqualTo("melee"));

            Assert.That(guard.Lower(2f, "explosion"), Is.False, "it was already down");
            Assert.That(guard.SecondsRemaining, Is.EqualTo(2f), "a longer hit extends the time");
            Assert.That(guard.Cause, Is.EqualTo("melee"));
        }

        [Test]
        public void AShorterHitDoesNotCutTheTimeShort()
        {
            var guard = new ShieldGuard();
            guard.Lower(2f, "explosion");

            Assert.That(guard.Lower(0.5f, "melee"), Is.False);
            Assert.That(guard.SecondsRemaining, Is.EqualTo(2f));
        }

        [Test]
        public void TickRaisesItAgainAndReportsItOnce()
        {
            var guard = new ShieldGuard();
            guard.Lower(1f, "melee");

            Assert.That(guard.Tick(0.5f), Is.False);
            Assert.That(guard.IsRaised, Is.False);
            Assert.That(guard.Tick(0.5f), Is.True);
            Assert.That(guard.IsRaised, Is.True);
            Assert.That(guard.Cause, Is.Empty);
            Assert.That(guard.Tick(0.5f), Is.False);
        }

        [Test]
        public void ZeroSecondsDoesNotLowerIt()
        {
            var guard = new ShieldGuard();

            Assert.That(guard.Lower(0f, "melee"), Is.False);
            Assert.That(guard.IsRaised, Is.True);
        }

        [Test]
        public void ResetRaisesItAtOnce()
        {
            var guard = new ShieldGuard();
            guard.Lower(3f, "interrupt");

            guard.Reset();

            Assert.That(guard.IsRaised, Is.True);
            Assert.That(guard.Cause, Is.Empty);
        }

        [Test]
        public void InvalidArgumentsAreRejected()
        {
            var guard = new ShieldGuard();

            Assert.Throws<ArgumentOutOfRangeException>(() => guard.Lower(-1f, "melee"));
            Assert.Throws<ArgumentOutOfRangeException>(() => guard.Lower(float.PositiveInfinity, "melee"));
            Assert.Throws<ArgumentOutOfRangeException>(() => guard.Lower(float.NaN, "melee"));
            Assert.Throws<ArgumentException>(() => guard.Lower(1f, " "));
            Assert.Throws<ArgumentOutOfRangeException>(() => guard.Tick(-0.1f));
            Assert.That(guard.IsRaised, Is.True);
        }
    }
}
