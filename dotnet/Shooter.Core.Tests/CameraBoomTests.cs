using System;
using System.Numerics;
using NUnit.Framework;

namespace Shooter.Core.Tests
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
            Assert.Throws<ArgumentOutOfRangeException>(() => CameraBoom.Offset((ViewMode)99, _shoulder, ShoulderSide.Left));
        }

        [Test]
        public void LeftShoulderMirrorsTheOffsetSideways()
        {
            Assert.That(
                CameraBoom.Offset(ViewMode.ThirdPerson, _shoulder, ShoulderSide.Left),
                Is.EqualTo(new Vector3(-0.5f, 0.2f, -2.5f)));
        }

        [Test]
        public void RightShoulderKeepsTheOffset()
        {
            Assert.That(CameraBoom.Offset(ViewMode.ThirdPerson, _shoulder, ShoulderSide.Right), Is.EqualTo(_shoulder));
        }

        [TestCase(ViewMode.FirstPerson)]
        [TestCase(ViewMode.ThirdPerson)]
        public void TwoArgumentOverloadMatchesTheRightShoulder(ViewMode mode)
        {
            Assert.That(
                CameraBoom.Offset(mode, _shoulder),
                Is.EqualTo(CameraBoom.Offset(mode, _shoulder, ShoulderSide.Right)));
        }

        [Test]
        public void FirstPersonIgnoresTheShoulder()
        {
            Assert.That(CameraBoom.Offset(ViewMode.FirstPerson, _shoulder, ShoulderSide.Left), Is.EqualTo(Vector3.Zero));
        }

        [Test]
        public void RejectsUnknownShoulder()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => CameraBoom.Offset(ViewMode.ThirdPerson, _shoulder, (ShoulderSide)99));
            Assert.Throws<ArgumentOutOfRangeException>(() => CameraBoom.Offset(ViewMode.FirstPerson, _shoulder, (ShoulderSide)99));
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

        [Test]
        public void TowardEyeMovesTheCameraOntoTheEyeAsTheBlendGrows()
        {
            Assert.That(CameraBoom.TowardEye(_shoulder, 0f), Is.EqualTo(_shoulder));
            Assert.That(CameraBoom.TowardEye(_shoulder, 1f), Is.EqualTo(Vector3.Zero));

            // 半分では smoothstep でも半分
            var half = CameraBoom.TowardEye(_shoulder, 0.5f);
            Assert.That(half.X, Is.EqualTo(0.25f).Within(1e-6f));
            Assert.That(half.Z, Is.EqualTo(-1.25f).Within(1e-6f));

            // 4 分の 1 では 0.25² × (3 − 0.5) = 0.15625 だけ寄る
            Assert.That(CameraBoom.TowardEye(_shoulder, 0.25f).Z, Is.EqualTo(-2.5f * (1f - 0.15625f)).Within(1e-6f));
        }

        [Test]
        public void TowardEyeClampsTheBlend()
        {
            Assert.That(CameraBoom.TowardEye(_shoulder, -1f), Is.EqualTo(_shoulder));
            Assert.That(CameraBoom.TowardEye(_shoulder, 2f), Is.EqualTo(Vector3.Zero));
            Assert.That(CameraBoom.TowardEye(_shoulder, float.NaN), Is.EqualTo(_shoulder));
        }
    }
}
