using System;
using NUnit.Framework;

namespace Fps.Core.Tests
{
    // Unity の EditMode テストと dotnet/Fps.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class PickupRespawnTests
    {
        [Test]
        public void StartsAvailable()
        {
            var respawn = new PickupRespawn(10f);

            Assert.That(respawn.IsAvailable, Is.True);
            Assert.That(respawn.RemainingSeconds, Is.Null);
            Assert.That(respawn.Tick(5f), Is.False);
        }

        [Test]
        public void ComesBackAfterTheRespawnTime()
        {
            var respawn = new PickupRespawn(10f);

            Assert.That(respawn.Collect(), Is.True);
            Assert.That(respawn.IsAvailable, Is.False);
            Assert.That(respawn.RemainingSeconds, Is.EqualTo(10f));

            Assert.That(respawn.Tick(4f), Is.False);
            Assert.That(respawn.RemainingSeconds, Is.EqualTo(6f).Within(1e-5f));

            Assert.That(respawn.Tick(6f), Is.True);
            Assert.That(respawn.IsAvailable, Is.True);
            Assert.That(respawn.RemainingSeconds, Is.Null);
            Assert.That(respawn.Tick(1f), Is.False, "only reports the moment it came back");
        }

        [Test]
        public void CannotBeCollectedTwiceWhileGone()
        {
            var respawn = new PickupRespawn(10f);
            respawn.Collect();

            Assert.That(respawn.Collect(), Is.False);
        }

        [Test]
        public void ZeroSecondsMeansItNeverComesBack()
        {
            var respawn = new PickupRespawn(0f);
            respawn.Collect();

            Assert.That(respawn.Respawns, Is.False);
            Assert.That(respawn.Tick(1000f), Is.False);
            Assert.That(respawn.IsAvailable, Is.False);
            Assert.That(respawn.RemainingSeconds, Is.Null);
        }

        [Test]
        public void RejectsBadTimes()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new PickupRespawn(-1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new PickupRespawn(float.NaN));
            Assert.Throws<ArgumentOutOfRangeException>(() => new PickupRespawn(float.PositiveInfinity));
            Assert.Throws<ArgumentOutOfRangeException>(() => new PickupRespawn(1f).Tick(-0.1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new PickupRespawn(1f).Tick(float.NaN));
        }
    }
}
