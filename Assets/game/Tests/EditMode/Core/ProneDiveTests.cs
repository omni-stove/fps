using System;
using System.Numerics;
using NUnit.Framework;

namespace Fps.Core.Tests
{
    // Unity の EditMode テストと dotnet/Fps.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class ProneDiveTests
    {
        private const float _tolerance = 1e-4f;

        [Test]
        public void ReadsDistanceAndSecondsFromTheSpec()
        {
            var dive = new ProneDive(new ProneSpec(diveDistance: 3f, diveSeconds: 0.6f));

            Assert.That(dive.Distance, Is.EqualTo(3f));
            Assert.That(dive.Seconds, Is.EqualTo(0.6f));
            Assert.That(dive.IsDiving, Is.False);
            Assert.That(dive.Velocity, Is.EqualTo(Vector2.Zero));
            Assert.Throws<ArgumentNullException>(() => new ProneDive(null));
        }

        [TestCase(0.01f)]
        [TestCase(0.016f)]
        [TestCase(0.1f)]
        [TestCase(1f)]
        public void CoversTheDistanceWhateverTheFrameStep(float step)
        {
            var dive = new ProneDive(ProneSpec.Default);
            Assert.That(dive.TryStart(new Vector2(0f, 2f)), Is.True);

            var total = Vector2.Zero;
            for (var i = 0; i < 1000 && dive.IsDiving; i++)
            {
                total += dive.Step(step);
            }

            Assert.That(dive.IsDiving, Is.False);
            Assert.That(total.X, Is.EqualTo(0f).Within(_tolerance));
            Assert.That(total.Y, Is.EqualTo(ProneSpec.DefaultDiveDistance).Within(_tolerance));
        }

        [Test]
        public void SlowsDownToAStop()
        {
            var dive = new ProneDive(ProneSpec.Default);
            dive.TryStart(Vector2.UnitX);

            var start = dive.Velocity.Length();
            var first = dive.Step(0.1f).Length();
            var second = dive.Step(0.1f).Length();

            Assert.That(start, Is.EqualTo(2f * ProneSpec.DefaultDiveDistance / ProneSpec.DefaultDiveSeconds).Within(_tolerance));
            Assert.That(second, Is.LessThan(first));
            Assert.That(dive.Velocity.Length(), Is.LessThan(start));
        }

        [Test]
        public void DoesNotRestartWhileDivingAndCanBeCancelled()
        {
            var dive = new ProneDive(ProneSpec.Default);
            dive.TryStart(Vector2.UnitY);

            Assert.That(dive.TryStart(Vector2.UnitX), Is.False);
            Assert.That(dive.Direction, Is.EqualTo(Vector2.UnitY));
            Assert.That(dive.Cancel(), Is.True);
            Assert.That(dive.Cancel(), Is.False);
            Assert.That(dive.Step(0.1f), Is.EqualTo(Vector2.Zero));
            Assert.That(dive.TryStart(Vector2.UnitX), Is.True, "a new dive after the cancel");
        }

        [Test]
        public void RejectsBadDirectionsAndSteps()
        {
            var dive = new ProneDive(ProneSpec.Default);

            Assert.Throws<ArgumentOutOfRangeException>(() => dive.TryStart(Vector2.Zero));
            Assert.Throws<ArgumentOutOfRangeException>(() => dive.TryStart(new Vector2(float.NaN, 1f)));
            Assert.Throws<ArgumentOutOfRangeException>(() => dive.Step(-0.1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => dive.Step(float.PositiveInfinity));
        }
    }
}
