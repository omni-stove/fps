using System;
using NUnit.Framework;

namespace Fps.Core.Tests
{
    public sealed class LookAnglesTests
    {
        [Test]
        public void StartsLevel()
        {
            var look = new LookAngles();

            Assert.That(look.Yaw, Is.Zero);
            Assert.That(look.Pitch, Is.Zero);
            Assert.That(look.MaxPitch, Is.EqualTo(LookAngles.DefaultMaxPitch));
        }

        [TestCase(0f)]
        [TestCase(-1f)]
        [TestCase(90.5f)]
        [TestCase(float.NaN)]
        public void RejectsInvalidMaxPitch(float maxPitch)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new LookAngles(maxPitch));
        }

        [Test]
        public void LookingUpDecreasesPitch()
        {
            var look = new LookAngles();

            look.Rotate(0f, 10f);

            Assert.That(look.Pitch, Is.EqualTo(-10f));
        }

        [Test]
        public void PitchIsClampedBothWays()
        {
            var look = new LookAngles();

            look.Rotate(0f, 1000f);
            Assert.That(look.Pitch, Is.EqualTo(-85f));

            look.Rotate(0f, -1000f);
            Assert.That(look.Pitch, Is.EqualTo(85f));
        }

        [TestCase(370f, 10f)]
        [TestCase(-10f, 350f)]
        [TestCase(720f, 0f)]
        public void YawWrapsIntoOneTurn(float delta, float expected)
        {
            var look = new LookAngles();

            look.Rotate(delta, 0f);

            Assert.That(look.Yaw, Is.EqualTo(expected).Within(1e-3f));
        }

        [Test]
        public void SetAcceptsUnityEulerRange()
        {
            var look = new LookAngles();

            // Unity の eulerAngles.x は 0〜360 で返るので、350 は 10° 上向き。
            look.Set(-90f, 350f);

            Assert.That(look.Yaw, Is.EqualTo(270f).Within(1e-3f));
            Assert.That(look.Pitch, Is.EqualTo(-10f).Within(1e-3f));
        }

        [Test]
        public void SetClampsPitch()
        {
            var look = new LookAngles();

            look.Set(0f, 89f);

            Assert.That(look.Pitch, Is.EqualTo(85f));
        }

        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        [TestCase(float.NegativeInfinity)]
        public void IgnoresNonFiniteInput(float bad)
        {
            var look = new LookAngles();
            look.Rotate(30f, 5f);

            look.Rotate(bad, bad);

            Assert.That(look.Yaw, Is.EqualTo(30f));
            Assert.That(look.Pitch, Is.EqualTo(-5f));
        }

        [TestCase(0f, 0f)]
        [TestCase(180f, 180f)]
        [TestCase(190f, -170f)]
        [TestCase(-190f, 170f)]
        [TestCase(360f, 0f)]
        public void ToSignedWrapsIntoHalfTurn(float degrees, float expected)
        {
            Assert.That(LookAngles.ToSigned(degrees), Is.EqualTo(expected).Within(1e-3f));
        }
    }
}
