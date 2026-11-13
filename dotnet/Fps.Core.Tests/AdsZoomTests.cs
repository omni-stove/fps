using System;
using NUnit.Framework;

namespace Fps.Core.Tests
{
    public sealed class AdsZoomTests
    {
        private const float _hip = 60f;
        private const float _ads = 40f;

        [Test]
        public void StartsAtTheHip()
        {
            var zoom = new AdsZoom(0.2f);

            Assert.That(zoom.TransitionSeconds, Is.EqualTo(0.2f));
            Assert.That(zoom.IsAiming, Is.False);
            Assert.That(zoom.Progress, Is.Zero);
            Assert.That(zoom.FieldOfView(_hip, _ads), Is.EqualTo(_hip));
        }

        [Test]
        public void ZoomsInOverTheTransitionAndStopsAtTheAdsFieldOfView()
        {
            var zoom = new AdsZoom(0.2f);

            Assert.That(zoom.Step(true, 0.05f), Is.EqualTo(0.25f).Within(1e-6f));
            Assert.That(zoom.IsAiming, Is.True);
            var quarter = zoom.FieldOfView(_hip, _ads);
            Assert.That(quarter, Is.LessThan(_hip).And.GreaterThan(_ads));

            zoom.Step(true, 0.05f);
            Assert.That(zoom.FieldOfView(_hip, _ads), Is.EqualTo(50f).Within(1e-4f), "half way is half the angle");
            Assert.That(zoom.FieldOfView(_hip, _ads), Is.LessThan(quarter));

            zoom.Step(true, 1f);
            Assert.That(zoom.Progress, Is.EqualTo(1f));
            Assert.That(zoom.FieldOfView(_hip, _ads), Is.EqualTo(_ads));
        }

        [Test]
        public void ZoomsBackOutFromWhereverItIs()
        {
            var zoom = new AdsZoom(0.2f);
            zoom.Step(true, 0.15f);

            Assert.That(zoom.Step(false, 0.05f), Is.EqualTo(0.5f).Within(1e-6f), "releasing half way does not jump back to the hip");
            Assert.That(zoom.IsAiming, Is.False);

            zoom.Step(false, 1f);
            Assert.That(zoom.Progress, Is.Zero);
            Assert.That(zoom.FieldOfView(_hip, _ads), Is.EqualTo(_hip));
        }

        [Test]
        public void ZeroTransitionSwitchesAtOnce()
        {
            var zoom = new AdsZoom(0f);

            Assert.That(zoom.Step(true, 0f), Is.EqualTo(1f));
            Assert.That(zoom.Step(false, 0f), Is.Zero);
        }

        [Test]
        public void ResetReturnsToTheHipImmediately()
        {
            var zoom = new AdsZoom(0.2f);
            zoom.Step(true, 1f);

            zoom.Reset();

            Assert.That(zoom.IsAiming, Is.False);
            Assert.That(zoom.Progress, Is.Zero);
        }

        [Test]
        public void InterpolationEasesAtBothEnds()
        {
            var nearStart = AdsZoom.Interpolate(_hip, _ads, 0.1f);
            var nearEnd = AdsZoom.Interpolate(_hip, _ads, 0.9f);

            Assert.That(_hip - nearStart, Is.LessThan(0.1f * (_hip - _ads)));
            Assert.That(nearEnd - _ads, Is.LessThan(0.1f * (_hip - _ads)));
            Assert.That(AdsZoom.Interpolate(_hip, _ads, -1f), Is.EqualTo(_hip));
            Assert.That(AdsZoom.Interpolate(_hip, _ads, 2f), Is.EqualTo(_ads));
        }

        [Test]
        public void SensitivityFollowsTheTangentOfHalfTheFieldOfView()
        {
            Assert.That(AdsZoom.SensitivityMultiplier(_hip, _hip), Is.EqualTo(1f));

            var expected = Math.Tan(20d * Math.PI / 180d) / Math.Tan(30d * Math.PI / 180d);
            Assert.That(AdsZoom.SensitivityMultiplier(_hip, _ads), Is.EqualTo((float)expected).Within(1e-5f));
            Assert.That(AdsZoom.SensitivityMultiplier(_hip, _ads), Is.LessThan(1f));
            Assert.That(AdsZoom.SensitivityMultiplier(_hip, 90f), Is.GreaterThan(1f));
        }

        [Test]
        public void SensitivityDropsAsTheZoomProceeds()
        {
            var zoom = new AdsZoom(0.2f);
            var previous = 1f;
            for (var i = 0; i < 4; i++)
            {
                zoom.Step(true, 0.05f);
                var multiplier = AdsZoom.SensitivityMultiplier(_hip, zoom.FieldOfView(_hip, _ads));
                Assert.That(multiplier, Is.LessThan(previous));
                previous = multiplier;
            }

            Assert.That(previous, Is.EqualTo(AdsZoom.SensitivityMultiplier(_hip, _ads)).Within(1e-6f));
        }

        [TestCase(-0.1f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        public void RejectsInvalidTransitionSeconds(float seconds)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new AdsZoom(seconds));
        }

        [TestCase(-0.1f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        public void RejectsInvalidDeltaSeconds(float seconds)
        {
            var zoom = new AdsZoom(0.2f);

            Assert.Throws<ArgumentOutOfRangeException>(() => zoom.Step(true, seconds));
            Assert.That(zoom.Progress, Is.Zero);
            Assert.That(zoom.IsAiming, Is.False);
        }

        [TestCase(0f)]
        [TestCase(-10f)]
        [TestCase(180f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        public void RejectsFieldsOfViewThatCannotBeProjected(float fieldOfView)
        {
            Assert.That(AdsZoom.IsValidFieldOfView(fieldOfView), Is.False);
            Assert.Throws<ArgumentOutOfRangeException>(() => AdsZoom.Interpolate(fieldOfView, _ads, 0.5f));
            Assert.Throws<ArgumentOutOfRangeException>(() => AdsZoom.Interpolate(_hip, fieldOfView, 0.5f));
            Assert.Throws<ArgumentOutOfRangeException>(() => AdsZoom.SensitivityMultiplier(fieldOfView, _ads));
            Assert.Throws<ArgumentOutOfRangeException>(() => AdsZoom.SensitivityMultiplier(_hip, fieldOfView));
        }

        [Test]
        public void RejectsNaNProgress()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => AdsZoom.Interpolate(_hip, _ads, float.NaN));
        }
    }
}
