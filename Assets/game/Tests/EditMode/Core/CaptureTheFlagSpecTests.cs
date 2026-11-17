using System;
using NUnit.Framework;

namespace Shooter.Core.Tests
{
    // Unity の EditMode テストと dotnet/Shooter.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class CaptureTheFlagSpecTests
    {
        [Test]
        public void EveryValueDefaultsToItsDefaultConstant()
        {
            var spec = new CaptureTheFlagSpec();

            Assert.That(spec.ScoreLimit, Is.EqualTo(CaptureTheFlagSpec.DefaultScoreLimit));
            Assert.That(spec.TimeLimitSeconds, Is.EqualTo(CaptureTheFlagSpec.DefaultTimeLimitSeconds));
            Assert.That(spec.RespawnSeconds, Is.EqualTo(CaptureTheFlagSpec.DefaultRespawnSeconds));
            Assert.That(spec.PickupRadius, Is.EqualTo(CaptureTheFlagSpec.DefaultPickupRadius));
            Assert.That(spec.CaptureRadius, Is.EqualTo(CaptureTheFlagSpec.DefaultCaptureRadius));
            Assert.That(spec.ReturnSeconds, Is.EqualTo(CaptureTheFlagSpec.DefaultReturnSeconds));
            Assert.That(spec.PointsPerCapture, Is.EqualTo(CaptureTheFlagSpec.DefaultPointsPerCapture));
        }

        [Test]
        public void TheDefaultsAreTheDocumentedValues()
        {
            Assert.That(CaptureTheFlagSpec.DefaultScoreLimit, Is.EqualTo(3));
            Assert.That(CaptureTheFlagSpec.DefaultTimeLimitSeconds, Is.EqualTo(600f));
            Assert.That(CaptureTheFlagSpec.DefaultRespawnSeconds, Is.EqualTo(5f));
            Assert.That(CaptureTheFlagSpec.DefaultPickupRadius, Is.EqualTo(1.5f));
            Assert.That(CaptureTheFlagSpec.DefaultCaptureRadius, Is.EqualTo(2f));
            Assert.That(CaptureTheFlagSpec.DefaultReturnSeconds, Is.EqualTo(20f));
            Assert.That(CaptureTheFlagSpec.DefaultPointsPerCapture, Is.EqualTo(1));
        }

        [Test]
        public void TheSpecKeepsTheValuesItWasGiven()
        {
            var spec = new CaptureTheFlagSpec(5, 300f, 0f, 2.5f, 3f, 12f, 2);

            Assert.That(spec.ScoreLimit, Is.EqualTo(5));
            Assert.That(spec.TimeLimitSeconds, Is.EqualTo(300f));
            Assert.That(spec.RespawnSeconds, Is.EqualTo(0f));
            Assert.That(spec.PickupRadius, Is.EqualTo(2.5f));
            Assert.That(spec.CaptureRadius, Is.EqualTo(3f));
            Assert.That(spec.ReturnSeconds, Is.EqualTo(12f));
            Assert.That(spec.PointsPerCapture, Is.EqualTo(2));
        }

        [Test]
        public void TheSpecRejectsValuesOutOfRange()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new CaptureTheFlagSpec(scoreLimit: 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new CaptureTheFlagSpec(timeLimitSeconds: 0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new CaptureTheFlagSpec(timeLimitSeconds: float.PositiveInfinity));
            Assert.Throws<ArgumentOutOfRangeException>(() => new CaptureTheFlagSpec(timeLimitSeconds: float.NaN));
            Assert.Throws<ArgumentOutOfRangeException>(() => new CaptureTheFlagSpec(respawnSeconds: -0.1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new CaptureTheFlagSpec(respawnSeconds: float.PositiveInfinity));
            Assert.Throws<ArgumentOutOfRangeException>(() => new CaptureTheFlagSpec(pickupRadius: 0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new CaptureTheFlagSpec(pickupRadius: float.NaN));
            Assert.Throws<ArgumentOutOfRangeException>(() => new CaptureTheFlagSpec(captureRadius: -1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new CaptureTheFlagSpec(captureRadius: float.PositiveInfinity));
            Assert.Throws<ArgumentOutOfRangeException>(() => new CaptureTheFlagSpec(returnSeconds: 0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new CaptureTheFlagSpec(returnSeconds: float.PositiveInfinity));
            Assert.Throws<ArgumentOutOfRangeException>(() => new CaptureTheFlagSpec(pointsPerCapture: 0));
        }

        [Test]
        public void TheModeUsesTheScoreLimitAndTimeLimitOfTheSpec()
        {
            var game = new CaptureTheFlag(new CaptureTheFlagSpec(scoreLimit: 4, timeLimitSeconds: 90f));

            Assert.That(game.Session.ScoreLimit, Is.EqualTo(4d));
            Assert.That(game.Session.TimeLimitSeconds, Is.EqualTo(90f));
            Assert.That(game.Session.RemainingSeconds, Is.EqualTo(90f));
        }

        [Test]
        public void OneCaptureScoresThePointsPerCapture()
        {
            var game = new CaptureTheFlag(new CaptureTheFlagSpec(scoreLimit: 10, pointsPerCapture: 3));
            game.Session.AddPlayer(1, Side.Alpha);
            game.Session.AddPlayer(2, Side.Bravo);
            game.Session.Start();

            game.Touch(Side.Bravo, 1);
            Assert.That(game.ReachBase(1), Is.True);

            Assert.That(game.Session.GetScore(Side.Alpha), Is.EqualTo(3d));
            Assert.That(game.GetCaptures(Side.Alpha), Is.EqualTo(1), "the capture count still goes up by one");
        }

        [Test]
        public void ADroppedFlagWaitsTheReturnSecondsOfTheSpec()
        {
            var game = new CaptureTheFlag(new CaptureTheFlagSpec(returnSeconds: 7f));
            game.Session.AddPlayer(1, Side.Alpha);
            game.Session.AddPlayer(2, Side.Bravo);
            game.Session.Start();
            game.Touch(Side.Bravo, 1);

            game.ReportKill(2, 1);

            Assert.That(game.GetReturnRemaining(Side.Bravo), Is.EqualTo(7f));
        }
    }
}
