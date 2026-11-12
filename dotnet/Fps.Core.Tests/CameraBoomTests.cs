using System;
using System.Numerics;
using NUnit.Framework;

namespace Fps.Core.Tests
{
    public sealed class CameraBoomTests
    {
        private static readonly Vector3 _shoulder = new Vector3(0.5f, 0.2f, -2.5f);

        [Test]
        public void FirstPersonPutsTheCameraOnTheEye()
        {
            Assert.That(CameraBoom.Offset(ViewMode.FirstPerson, _shoulder), Is.EqualTo(Vector3.Zero));
        }

        [Test]
        public void ThirdPersonUsesTheGivenOffset()
        {
            Assert.That(CameraBoom.Offset(ViewMode.ThirdPerson, _shoulder), Is.EqualTo(_shoulder));
        }

        [Test]
        public void RejectsUnknownMode()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => CameraBoom.Offset((ViewMode)99, _shoulder));
        }

        [Test]
        public void KeepsTheFullLengthWhenNothingBlocks()
        {
            Assert.That(CameraBoom.Length(2.5f, null), Is.EqualTo(2.5f));
            Assert.That(CameraBoom.Length(2.5f, 3f), Is.EqualTo(2.5f));
        }

        [Test]
        public void ShortensToTheWall()
        {
            Assert.That(CameraBoom.Length(2.5f, 1.2f), Is.EqualTo(1.2f));
        }

        [Test]
        public void DoesNotGoCloserThanTheMinimum()
        {
            Assert.That(CameraBoom.Length(2.5f, 0.05f, 0.2f), Is.EqualTo(0.2f));
        }

        [Test]
        public void NeverExceedsTheDesiredLength()
        {
            // 一人称 (長さ 0) で目のそばに壁があっても、カメラを目より前に出さない
            Assert.That(CameraBoom.Length(0f, 0f, 0.2f), Is.Zero);
            Assert.That(CameraBoom.Length(0.1f, 0.05f, 0.2f), Is.EqualTo(0.1f));
        }

        [Test]
        public void IgnoresNaNHit()
        {
            Assert.That(CameraBoom.Length(2.5f, float.NaN), Is.EqualTo(2.5f));
        }

        [TestCase(-1f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        public void RejectsInvalidLengths(float value)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => CameraBoom.Length(value, null));
            Assert.Throws<ArgumentOutOfRangeException>(() => CameraBoom.Length(1f, null, value));
        }
    }
}
