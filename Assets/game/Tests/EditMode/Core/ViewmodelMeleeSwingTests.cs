using System;
using System.Numerics;
using NUnit.Framework;

namespace Shooter.Core.Tests
{
    // Unity の EditMode テストと dotnet/Shooter.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class ViewmodelMeleeSwingTests
    {
        private const float _swing = 0.5f;
        private const float _impact = 0.2f;

        // 既定の振りかぶりの割合 (0.6) では、当たりの 0.2 秒のうち 0.12 秒までが振りかぶり。
        private const float _windupEnd = 0.12f;

        private static void Advance(ViewmodelMeleeSwing swing, float seconds)
        {
            swing.Tick(seconds);
        }

        [Test]
        public void ItStartsAtRestAndStaysThereUntilSwung()
        {
            var swing = new ViewmodelMeleeSwing();

            Advance(swing, 1f);

            Assert.That(swing.IsActive, Is.False);
            Assert.That(swing.Position, Is.EqualTo(Vector3.Zero));
            Assert.That(swing.YawRightDegrees, Is.EqualTo(0f));
            Assert.That(swing.RollLeftDegrees, Is.EqualTo(0f));
        }

        [Test]
        public void TheWindupPullsTheGunBackAndToTheRight()
        {
            var swing = new ViewmodelMeleeSwing();
            swing.Start(_swing, _impact);

            Advance(swing, _windupEnd);

            Assert.That(swing.WindupWeight, Is.EqualTo(1f).Within(1e-4f));
            Assert.That(swing.StrikeWeight, Is.EqualTo(0f).Within(1e-4f));
            Assert.That(swing.Position.Z, Is.EqualTo(-ViewmodelSwaySettings.DefaultMeleeWindupBackMeters).Within(1e-4f));
            Assert.That(swing.YawRightDegrees, Is.EqualTo(ViewmodelSwaySettings.DefaultMeleeSweepDegrees / 2f).Within(1e-3f));
            Assert.That(swing.RollLeftDegrees, Is.EqualTo(0f).Within(1e-3f));
        }

        [Test]
        public void AtTheImpactTheGunIsThrustForwardSweptLeftAndRolled()
        {
            var swing = new ViewmodelMeleeSwing();
            swing.Start(_swing, _impact);

            Advance(swing, _impact);

            Assert.That(swing.StrikeWeight, Is.EqualTo(1f).Within(1e-4f));
            Assert.That(swing.Position.Z, Is.EqualTo(ViewmodelSwaySettings.DefaultMeleeThrustMeters).Within(1e-4f));
            Assert.That(swing.YawRightDegrees, Is.EqualTo(-ViewmodelSwaySettings.DefaultMeleeSweepDegrees / 2f).Within(1e-3f));
            Assert.That(swing.RollLeftDegrees, Is.EqualTo(ViewmodelSwaySettings.DefaultMeleeRollDegrees).Within(1e-3f));
        }

        [Test]
        public void TheStrikeMovesFromTheWindupTowardTheImpactPose()
        {
            var swing = new ViewmodelMeleeSwing();
            swing.Start(_swing, _impact);

            Advance(swing, (_windupEnd + _impact) / 2f);

            Assert.That(swing.WindupWeight, Is.EqualTo(0.5f).Within(1e-3f));
            Assert.That(swing.StrikeWeight, Is.EqualTo(0.5f).Within(1e-3f));
            Assert.That(swing.YawRightDegrees, Is.EqualTo(0f).Within(1e-3f));
        }

        [Test]
        public void TheGunReturnsToRestWhenTheSwingEnds()
        {
            var swing = new ViewmodelMeleeSwing();
            swing.Start(_swing, _impact);

            Advance(swing, (_impact + _swing) / 2f);
            Assert.That(swing.IsActive, Is.True);
            Assert.That(swing.StrikeWeight, Is.EqualTo(0.5f).Within(1e-3f));

            Advance(swing, _swing);

            Assert.That(swing.IsActive, Is.False);
            Assert.That(swing.Position, Is.EqualTo(Vector3.Zero));
            Assert.That(swing.YawRightDegrees, Is.EqualTo(0f));
            Assert.That(swing.RollLeftDegrees, Is.EqualTo(0f));
        }

        [Test]
        public void SwingingAgainRestartsFromTheBeginning()
        {
            var swing = new ViewmodelMeleeSwing();
            swing.Start(_swing, _impact);
            Advance(swing, 0.3f);

            swing.Start(_swing, _impact);

            Assert.That(swing.Elapsed, Is.EqualTo(0f));
            Assert.That(swing.IsActive, Is.True);
            Assert.That(swing.Position, Is.EqualTo(Vector3.Zero));
        }

        [Test]
        public void AnImpactAtTheStartSkipsTheWindup()
        {
            var swing = new ViewmodelMeleeSwing();
            swing.Start(_swing, 0f);

            Assert.That(swing.StrikeWeight, Is.EqualTo(1f));
            Assert.That(swing.WindupWeight, Is.EqualTo(0f));
        }

        [Test]
        public void ZeroSettingsKeepTheGunStill()
        {
            var settings = new ViewmodelSwaySettings(
                meleeWindupBackMeters: 0f, meleeThrustMeters: 0f, meleeSweepDegrees: 0f, meleeRollDegrees: 0f);
            var swing = new ViewmodelMeleeSwing(settings);
            swing.Start(_swing, _impact);

            Advance(swing, _impact);

            Assert.That(swing.IsActive, Is.True);
            Assert.That(swing.Position, Is.EqualTo(Vector3.Zero));
            Assert.That(swing.YawRightDegrees, Is.EqualTo(0f));
            Assert.That(swing.RollLeftDegrees, Is.EqualTo(0f));
        }

        [Test]
        public void ResetStopsTheSwing()
        {
            var swing = new ViewmodelMeleeSwing();
            swing.Start(_swing, _impact);
            Advance(swing, _impact);

            swing.Reset();

            Assert.That(swing.IsActive, Is.False);
            Assert.That(swing.Position, Is.EqualTo(Vector3.Zero));
        }

        [Test]
        public void InvalidTimesAreRejected()
        {
            var swing = new ViewmodelMeleeSwing();

            Assert.Throws<ArgumentOutOfRangeException>(() => swing.Start(0f, 0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => swing.Start(float.PositiveInfinity, 0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => swing.Start(_swing, -0.1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => swing.Start(_swing, _swing + 0.1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => swing.Tick(-1f));
            Assert.Throws<ArgumentNullException>(() => swing.Settings = null);
        }

        [Test]
        public void TheSettingsHoldTheMeleeDefaultsAndRejectBadValues()
        {
            var settings = ViewmodelSwaySettings.Default;

            Assert.That(settings.MeleeWindupBackMeters, Is.EqualTo(0.06f));
            Assert.That(settings.MeleeThrustMeters, Is.EqualTo(0.2f));
            Assert.That(settings.MeleeSweepDegrees, Is.EqualTo(40f));
            Assert.That(settings.MeleeRollDegrees, Is.EqualTo(30f));
            Assert.That(settings.MeleeWindupFraction, Is.EqualTo(0.6f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ViewmodelSwaySettings(meleeThrustMeters: -1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ViewmodelSwaySettings(meleeSweepDegrees: float.NaN));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ViewmodelSwaySettings(meleeWindupFraction: 1.5f));
        }
    }
}
