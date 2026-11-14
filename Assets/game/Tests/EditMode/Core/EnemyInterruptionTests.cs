using System;
using NUnit.Framework;

namespace Fps.Core.Tests
{
    // Unity の EditMode テストと dotnet/Fps.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class EnemyInterruptionTests
    {
        [Test]
        public void StartsInactive()
        {
            var interruption = new EnemyInterruption();

            Assert.That(interruption.IsActive, Is.False);
            Assert.That(interruption.Reason, Is.Empty);
            Assert.That(interruption.Tick(1f), Is.False, "nothing ends when nothing started");
        }

        [Test]
        public void EndsAfterTheGivenSeconds()
        {
            var interruption = new EnemyInterruption();

            Assert.That(interruption.Begin(1f, "flinch"), Is.True);
            Assert.That(interruption.IsActive, Is.True);
            Assert.That(interruption.Reason, Is.EqualTo("flinch"));

            Assert.That(interruption.Tick(0.6f), Is.False);
            Assert.That(interruption.SecondsRemaining, Is.EqualTo(0.4f).Within(1e-5f));
            Assert.That(interruption.Tick(0.6f), Is.True, "reports the tick it ended on");
            Assert.That(interruption.IsActive, Is.False);
            Assert.That(interruption.SecondsRemaining, Is.Zero);
            Assert.That(interruption.Tick(0.1f), Is.False, "ends only once");
        }

        [Test]
        public void LongerInterruptionWinsOverAShorterOne()
        {
            var interruption = new EnemyInterruption();
            interruption.Begin(3f, "takedown");

            Assert.That(interruption.Begin(0.5f, "flinch"), Is.False);
            Assert.That(interruption.Reason, Is.EqualTo("takedown"));
            Assert.That(interruption.SecondsRemaining, Is.EqualTo(3f));

            Assert.That(interruption.Begin(5f, "stun"), Is.True);
            Assert.That(interruption.Reason, Is.EqualTo("stun"));
            Assert.That(interruption.SecondsRemaining, Is.EqualTo(5f));
        }

        [Test]
        public void ClearStopsAtOnce()
        {
            var interruption = new EnemyInterruption();
            interruption.Begin(2f, "flinch");

            interruption.Clear();

            Assert.That(interruption.IsActive, Is.False);
            Assert.That(interruption.Reason, Is.Empty);
        }

        [Test]
        public void RejectsInvalidArguments()
        {
            var interruption = new EnemyInterruption();

            Assert.Throws<ArgumentOutOfRangeException>(() => interruption.Begin(0f, "flinch"));
            Assert.Throws<ArgumentOutOfRangeException>(() => interruption.Begin(-1f, "flinch"));
            Assert.Throws<ArgumentOutOfRangeException>(() => interruption.Begin(float.NaN, "flinch"));
            Assert.Throws<ArgumentOutOfRangeException>(() => interruption.Begin(float.PositiveInfinity, "flinch"));
            Assert.Throws<ArgumentException>(() => interruption.Begin(1f, null));
            Assert.Throws<ArgumentException>(() => interruption.Begin(1f, string.Empty));
            Assert.Throws<ArgumentException>(() => interruption.Begin(1f, "two words"));
            Assert.Throws<ArgumentOutOfRangeException>(() => interruption.Tick(-0.1f));
            Assert.That(interruption.IsActive, Is.False);
        }
    }
}
