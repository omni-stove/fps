using System;
using System.Numerics;
using NUnit.Framework;

namespace Fps.Core.Tests
{
    // Unity の EditMode テストと dotnet/Fps.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class PhotoModeSpecTests
    {
        [Test]
        public void DefaultsMatchTheDefaultConstants()
        {
            var spec = PhotoModeSpec.Default;

            Assert.That(spec.Radius, Is.EqualTo(PhotoModeSpec.DefaultRadius));
            Assert.That(spec.MoveSpeed, Is.EqualTo(PhotoModeSpec.DefaultMoveSpeed));
            Assert.That(spec.MinFieldOfView, Is.EqualTo(PhotoModeSpec.DefaultMinFieldOfView));
            Assert.That(spec.MaxFieldOfView, Is.EqualTo(PhotoModeSpec.DefaultMaxFieldOfView));
            Assert.That(spec.FieldOfViewStep, Is.EqualTo(PhotoModeSpec.DefaultFieldOfViewStep));
            Assert.That(spec.MaxRoll, Is.EqualTo(PhotoModeSpec.DefaultMaxRoll));
            Assert.That(spec.RollSpeed, Is.EqualTo(PhotoModeSpec.DefaultRollSpeed));
            Assert.That(spec.StartHeight, Is.EqualTo(PhotoModeSpec.DefaultStartHeight));
            Assert.That(spec.StartBackDistance, Is.EqualTo(PhotoModeSpec.DefaultStartBackDistance));
        }

        [Test]
        public void InsideTheRadiusStaysWhereItIs()
        {
            var center = new Vector3(1f, 2f, 3f);
            var inside = center + new Vector3(2f, 0f, 0f);

            Assert.That(PhotoModeSpec.ClampWithin(inside, center, 5f), Is.EqualTo(inside));
        }

        [Test]
        public void OutsideTheRadiusIsPulledOntoTheSphere()
        {
            var center = new Vector3(1f, 2f, 3f);
            var outside = center + new Vector3(0f, 6f, 8f);

            var clamped = PhotoModeSpec.ClampWithin(outside, center, 5f);

            Assert.That((clamped - center).Length(), Is.EqualTo(5f).Within(1e-4f));
            var expected = center + new Vector3(0f, 3f, 4f);
            Assert.That((clamped - expected).Length(), Is.LessThan(1e-4f), "keeps the direction from the center");
        }

        [Test]
        public void ANonFinitePositionFallsBackToTheCenter()
        {
            var center = new Vector3(1f, 2f, 3f);

            Assert.That(PhotoModeSpec.ClampWithin(new Vector3(float.NaN, 0f, 0f), center, 5f), Is.EqualTo(center));
            Assert.That(PhotoModeSpec.ClampWithin(new Vector3(float.PositiveInfinity, 0f, 0f), center, 5f), Is.EqualTo(center));
        }

        [Test]
        public void ANegativeRadiusIsRejected()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => PhotoModeSpec.ClampWithin(Vector3.Zero, Vector3.Zero, -1f));
        }

        [Test]
        public void ZoomingInNarrowsAndStopsAtTheLimits()
        {
            var spec = new PhotoModeSpec(8f, 3f, 30f, 90f, 10f, 45f, 45f, 0f, 0f);

            Assert.That(spec.StepFieldOfView(60f, 1), Is.EqualTo(50f));
            Assert.That(spec.StepFieldOfView(60f, -2), Is.EqualTo(80f));
            Assert.That(spec.StepFieldOfView(60f, 10), Is.EqualTo(30f), "stops at the minimum");
            Assert.That(spec.StepFieldOfView(60f, -10), Is.EqualTo(90f), "stops at the maximum");
            Assert.That(spec.StepFieldOfView(float.NaN, 0), Is.EqualTo(60f), "an unknown field of view starts from the middle");
        }

        [Test]
        public void RollingFollowsTheInputAndStopsAtTheLimit()
        {
            var spec = new PhotoModeSpec(8f, 3f, 30f, 90f, 10f, 30f, 20f, 0f, 0f);

            Assert.That(spec.StepRoll(0f, 1f, 0.5f), Is.EqualTo(10f));
            Assert.That(spec.StepRoll(0f, -1f, 0.5f), Is.EqualTo(-10f));
            Assert.That(spec.StepRoll(0f, 5f, 0.5f), Is.EqualTo(10f), "the input is capped at full tilt");
            Assert.That(spec.StepRoll(25f, 1f, 1f), Is.EqualTo(30f), "stops at the maximum");
            Assert.That(spec.StepRoll(-25f, -1f, 1f), Is.EqualTo(-30f), "stops at the minimum");
            Assert.That(spec.StepRoll(5f, float.NaN, 1f), Is.EqualTo(5f), "a broken input does not tilt");
            Assert.Throws<ArgumentOutOfRangeException>(() => spec.StepRoll(0f, 1f, -0.1f));
        }

        [Test]
        public void InvalidValuesAreRejected()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new PhotoModeSpec(0f, 3f, 30f, 90f, 5f, 45f, 45f, 0f, 0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new PhotoModeSpec(8f, -1f, 30f, 90f, 5f, 45f, 45f, 0f, 0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new PhotoModeSpec(8f, 3f, 0f, 90f, 5f, 45f, 45f, 0f, 0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new PhotoModeSpec(8f, 3f, 30f, 180f, 5f, 45f, 45f, 0f, 0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new PhotoModeSpec(8f, 3f, 90f, 30f, 5f, 45f, 45f, 0f, 0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new PhotoModeSpec(8f, 3f, 30f, 90f, 5f, 200f, 45f, 0f, 0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new PhotoModeSpec(8f, 3f, 30f, 90f, 5f, 45f, float.NaN, 0f, 0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new PhotoModeSpec(8f, 3f, 30f, 90f, 5f, 45f, 45f, -1f, 0f));
        }
    }
}
