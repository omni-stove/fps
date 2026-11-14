using System;
using System.Numerics;
using NUnit.Framework;

namespace Fps.Core.Tests
{
    // Unity の EditMode テストと dotnet/Fps.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class ViewmodelSwayTests
    {
        private const float _frame =1f / 60f;

        private static void Run(ViewmodelSway sway, float seconds, float speed = 0f, float yawRate = 0f, float pitchRate = 0f, float ads = 0f)
        {
            var frames = (int)MathF.Round(seconds / _frame);
            for (var i = 0; i < frames; i++)
            {
                sway.Tick(_frame, speed, yawRate, pitchRate, ads);
            }
        }

        private static float Magnitude(ViewmodelSway sway)
        {
            return sway.Position.Length() + MathF.Abs(sway.PitchUpDegrees) + MathF.Abs(sway.YawRightDegrees);
        }

        [Test]
        public void StandingStillStaysExactlyAtRest()
        {
            var sway = new ViewmodelSway();

            Run(sway, 3f);

            Assert.That(sway.IsAtRest, Is.True);
            Assert.That(sway.Position, Is.EqualTo(Vector3.Zero));
            Assert.That(sway.PitchUpDegrees, Is.EqualTo(0f));
            Assert.That(sway.YawRightDegrees, Is.EqualTo(0f));
        }

        [Test]
        public void WalkingBobsAndSettlesBackToZeroAfterStopping()
        {
            var sway = new ViewmodelSway();
            var maxX = 0f;
            var minY = 0f;
            for (var i = 0; i < 120; i++)
            {
                sway.Tick(_frame, 5f, 0f, 0f, 0f);
                maxX = MathF.Max(maxX, MathF.Abs(sway.Position.X));
                minY = MathF.Min(minY, sway.Position.Y);
            }

            Assert.That(maxX, Is.GreaterThan(ViewmodelSwaySettings.DefaultBobHorizontalMeters * 0.5f), "sways left and right");
            Assert.That(minY, Is.LessThan(-ViewmodelSwaySettings.DefaultBobVerticalMeters * 0.5f), "dips with each step");
            Assert.That(sway.Position.Y, Is.LessThanOrEqualTo(0f), "never rises above the rest pose");

            Run(sway, 5f);

            Assert.That(sway.IsAtRest, Is.True);
            Assert.That(sway.Position, Is.EqualTo(Vector3.Zero));
        }

        [Test]
        public void SlowerWalkingBobsLess()
        {
            var slow = new ViewmodelSway();
            var fast = new ViewmodelSway();

            Run(slow, 2f, speed: 1f);
            Run(fast, 2f, speed: 5f);

            Assert.That(slow.BobWeight, Is.LessThan(fast.BobWeight));
            Assert.That(fast.BobWeight, Is.EqualTo(1f).Within(1e-3f));
        }

        [Test]
        public void TurningLagsTheOppositeWayAndCatchesUp()
        {
            var sway = new ViewmodelSway();

            Run(sway, 0.5f, yawRate: 90f, pitchRate: 60f);

            Assert.That(sway.YawRightDegrees, Is.LessThan(0f), "turning right leaves the gun behind on the left");
            Assert.That(sway.PitchUpDegrees, Is.LessThan(0f), "looking up leaves the gun lower");
            Assert.That(sway.YawRightDegrees, Is.GreaterThanOrEqualTo(-ViewmodelSwaySettings.DefaultMaxLookLagDegrees));

            Run(sway, 3f);

            Assert.That(sway.IsAtRest, Is.True);
        }

        [Test]
        public void FastTurningIsCappedAtTheMaxLag()
        {
            var sway = new ViewmodelSway(new ViewmodelSwaySettings(maxLookLagDegrees: 2f));

            Run(sway, 2f, yawRate: 5000f);

            Assert.That(sway.YawRightDegrees, Is.EqualTo(-2f).Within(1e-3f));
        }

        [Test]
        public void FiringKicksTheGunUpAndBackThenItReturns()
        {
            var sway = new ViewmodelSway();

            sway.Kick();

            Assert.That(sway.PitchUpDegrees, Is.EqualTo(ViewmodelSwaySettings.DefaultKickPitchDegrees).Within(1e-5f));
            Assert.That(sway.Position.Z, Is.EqualTo(-ViewmodelSwaySettings.DefaultKickBackMeters).Within(1e-5f));

            Run(sway, 0.1f);
            var partly = sway.PitchUpDegrees;
            Assert.That(partly, Is.GreaterThan(0f).And.LessThan(ViewmodelSwaySettings.DefaultKickPitchDegrees), "returns over time");

            Run(sway, 2f);

            Assert.That(sway.IsAtRest, Is.True);
            Assert.That(sway.PitchUpDegrees, Is.EqualTo(0f));
        }

        [Test]
        public void RapidFireStacksUpToTheLimit()
        {
            var sway = new ViewmodelSway(new ViewmodelSwaySettings(maxKicks: 2f));

            for (var i = 0; i < 10; i++)
            {
                sway.Kick();
            }

            Assert.That(sway.Kicks, Is.EqualTo(2f));
        }

        [Test]
        public void AimingDownSightsDampensTheSway()
        {
            var hip = new ViewmodelSway();
            var ads = new ViewmodelSway();

            for (var i = 0; i < 40; i++)
            {
                hip.Tick(_frame, 5f, 120f, 0f, 0f);
                ads.Tick(_frame, 5f, 120f, 0f, 1f);
            }

            hip.Kick();
            ads.Kick();

            Assert.That(Magnitude(ads), Is.GreaterThan(0f));
            Assert.That(Magnitude(ads), Is.LessThan(Magnitude(hip) * 0.6f));
            Assert.That(MathF.Abs(ads.YawRightDegrees), Is.EqualTo(MathF.Abs(hip.YawRightDegrees) * ViewmodelSwaySettings.DefaultAdsSwayScale).Within(1e-4f));
        }

        [Test]
        public void ZeroDeltaChangesNothing()
        {
            var sway = new ViewmodelSway();
            sway.Kick();
            var before = sway.Kicks;

            sway.Tick(0f, 5f, 500f, 0f, 0f);

            Assert.That(sway.Kicks, Is.EqualTo(before));
            Assert.That(sway.BobWeight, Is.EqualTo(0f));
        }

        [Test]
        public void ResetReturnsToRest()
        {
            var sway = new ViewmodelSway();
            Run(sway, 0.5f, speed: 5f, yawRate: 90f);
            sway.Kick();

            sway.Reset();

            Assert.That(sway.IsAtRest, Is.True);
            Assert.That(sway.Position, Is.EqualTo(Vector3.Zero));
        }

        [Test]
        public void RejectsInvalidInput()
        {
            var sway = new ViewmodelSway();

            Assert.Throws<ArgumentOutOfRangeException>(() => sway.Tick(-1f, 0f, 0f, 0f, 0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => sway.Tick(float.NaN, 0f, 0f, 0f, 0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => sway.Tick(_frame, -1f, 0f, 0f, 0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => sway.Tick(_frame, 0f, float.PositiveInfinity, 0f, 0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => sway.Tick(_frame, 0f, 0f, float.NaN, 0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => sway.Tick(_frame, 0f, 0f, 0f, float.NaN));
            Assert.Throws<ArgumentNullException>(() => sway.Settings = null);
        }

        [Test]
        public void SettingsRejectOutOfRangeValues()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new ViewmodelSwaySettings(bobHorizontalMeters: -0.1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ViewmodelSwaySettings(bobReferenceSpeed: 0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ViewmodelSwaySettings(kickReturnPerSecond: float.PositiveInfinity));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ViewmodelSwaySettings(adsSwayScale: 1.5f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ViewmodelSwaySettings(adsKickScale: float.NaN));
        }
    }
}
