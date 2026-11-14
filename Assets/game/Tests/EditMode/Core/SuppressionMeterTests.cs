using System;
using NUnit.Framework;

namespace Fps.Core.Tests
{
    // Unity の EditMode テストと dotnet/Fps.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class SuppressionMeterTests
    {
        private const float _epsilon = 1e-5f;

        // 半径 2m、かすめて 0.25、待ち 1 秒、毎秒 0.5 抜ける、0.5 で制圧・0.2 で解ける。
        private static SuppressionSpec Spec() => new SuppressionSpec(2f, 0.25f, 1f, 0.5f, 0.5f, 0.2f);

        [Test]
        public void StartsEmpty()
        {
            var meter = new SuppressionMeter(Spec());

            Assert.That(meter.Level, Is.EqualTo(0f));
            Assert.That(meter.IsSuppressed, Is.False);
            Assert.That(meter.NearMisses, Is.Zero);
        }

        [Test]
        public void CloserNearMissesBuildMore()
        {
            var meter = new SuppressionMeter(Spec());

            Assert.That(meter.AmountFor(0f), Is.EqualTo(0.25f).Within(_epsilon));
            Assert.That(meter.AmountFor(1f), Is.EqualTo(0.125f).Within(_epsilon));
            Assert.That(meter.AmountFor(2f), Is.EqualTo(0f), "the edge of the radius does not count");
            Assert.That(meter.AmountFor(5f), Is.EqualTo(0f));
        }

        [Test]
        public void NearMissesAddUpAndCountUntilSuppressed()
        {
            var meter = new SuppressionMeter(Spec());

            Assert.That(meter.AddNearMiss(0f), Is.True);
            Assert.That(meter.AddNearMiss(1f), Is.True);
            Assert.That(meter.Level, Is.EqualTo(0.375f).Within(_epsilon));
            Assert.That(meter.IsSuppressed, Is.False);

            meter.AddNearMiss(0f);
            Assert.That(meter.Level, Is.EqualTo(0.625f).Within(_epsilon));
            Assert.That(meter.IsSuppressed, Is.True);
            Assert.That(meter.NearMisses, Is.EqualTo(3));
        }

        [Test]
        public void FarShotsDoNotCount()
        {
            var meter = new SuppressionMeter(Spec());

            Assert.That(meter.AddNearMiss(2.5f), Is.False);
            Assert.That(meter.Level, Is.EqualTo(0f));
            Assert.That(meter.NearMisses, Is.Zero);
        }

        [Test]
        public void LevelNeverExceedsOne()
        {
            var meter = new SuppressionMeter(Spec());
            for (var i = 0; i < 10; i++)
            {
                meter.AddNearMiss(0f);
            }

            Assert.That(meter.Level, Is.EqualTo(1f));
        }

        [Test]
        public void LevelHoldsDuringTheDelayThenDrains()
        {
            var meter = new SuppressionMeter(Spec());
            meter.AddNearMiss(0f);
            meter.AddNearMiss(0f);

            meter.Tick(0.6f);
            Assert.That(meter.Level, Is.EqualTo(0.5f).Within(_epsilon), "still within the delay");

            // 待ちの残り 0.4 秒を使い切り、残りの 0.4 秒で 0.2 抜ける。
            meter.Tick(0.8f);
            Assert.That(meter.Level, Is.EqualTo(0.3f).Within(_epsilon));
        }

        [Test]
        public void ANewNearMissRestartsTheDelay()
        {
            var meter = new SuppressionMeter(Spec());
            meter.AddNearMiss(0f);
            meter.Tick(0.9f);

            meter.AddNearMiss(0f);
            meter.Tick(0.9f);

            Assert.That(meter.Level, Is.EqualTo(0.5f).Within(_epsilon));
        }

        [Test]
        public void SuppressionHoldsUntilTheLevelFallsBelowTheReleaseLevel()
        {
            var meter = new SuppressionMeter(Spec());
            meter.AddNearMiss(0f);
            meter.AddNearMiss(0f);
            Assert.That(meter.IsSuppressed, Is.True);

            meter.Tick(1f + 0.4f);
            Assert.That(meter.Level, Is.EqualTo(0.3f).Within(_epsilon));
            Assert.That(meter.IsSuppressed, Is.True, "0.3 is under the suppress level but above the release level");

            meter.Tick(0.4f);
            Assert.That(meter.IsSuppressed, Is.False);
        }

        [Test]
        public void DrainingToZeroForgetsTheCount()
        {
            var meter = new SuppressionMeter(Spec());
            meter.AddNearMiss(0f);

            meter.Tick(10f);

            Assert.That(meter.Level, Is.EqualTo(0f));
            Assert.That(meter.NearMisses, Is.Zero);
        }

        [Test]
        public void AZeroReleaseLevelReleasesOnceDrained()
        {
            var meter = new SuppressionMeter(new SuppressionSpec(2f, 0.5f, 0f, 1f, 0.5f, 0f));
            meter.AddNearMiss(0f);
            Assert.That(meter.IsSuppressed, Is.True);

            meter.Tick(1f);

            Assert.That(meter.IsSuppressed, Is.False);
        }

        [Test]
        public void ResetClearsEverything()
        {
            var meter = new SuppressionMeter(Spec());
            meter.AddNearMiss(0f);
            meter.AddNearMiss(0f);

            meter.Reset();

            Assert.That(meter.Level, Is.EqualTo(0f));
            Assert.That(meter.IsSuppressed, Is.False);
            Assert.That(meter.NearMisses, Is.Zero);
        }

        [Test]
        public void RejectsBadInput()
        {
            var meter = new SuppressionMeter(Spec());

            Assert.Throws<ArgumentNullException>(() => _ = new SuppressionMeter(null));
            Assert.Throws<ArgumentOutOfRangeException>(() => meter.AddNearMiss(-1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => meter.AddNearMiss(float.NaN));
            Assert.Throws<ArgumentOutOfRangeException>(() => meter.AddNearMiss(float.PositiveInfinity));
            Assert.Throws<ArgumentOutOfRangeException>(() => meter.Tick(-0.1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => meter.Tick(float.NaN));
        }

        [Test]
        public void SpecRejectsValuesOutOfRange()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => _ = new SuppressionSpec(nearMissRadius: 0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => _ = new SuppressionSpec(buildPerNearMiss: -1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => _ = new SuppressionSpec(decayDelaySeconds: float.NaN));
            Assert.Throws<ArgumentOutOfRangeException>(() => _ = new SuppressionSpec(decayPerSecond: 0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => _ = new SuppressionSpec(suppressedLevel: 1.5f));
            Assert.Throws<ArgumentOutOfRangeException>(() => _ = new SuppressionSpec(suppressedLevel: 0.4f, releasedLevel: 0.4f));
            Assert.Throws<ArgumentOutOfRangeException>(() => _ = new SuppressionSpec(maxSpreadDegrees: -1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => _ = new SuppressionSpec(maxCrosshairSpread: 2f));
            Assert.Throws<ArgumentOutOfRangeException>(() => _ = new SuppressionSpec(maxVignetteAlpha: -0.1f));
        }

        [Test]
        public void DefaultSpecUsesTheDefaults()
        {
            var spec = SuppressionSpec.Default;

            Assert.That(spec.NearMissRadius, Is.EqualTo(SuppressionSpec.DefaultNearMissRadius));
            Assert.That(spec.SuppressedLevel, Is.GreaterThan(spec.ReleasedLevel));
        }

        [Test]
        public void LogLinesStartWithTheTag()
        {
            var meter = new SuppressionMeter(Spec());
            meter.AddNearMiss(0.4f);

            Assert.That(
                SuppressionLog.NearMissed("Enemy1", 0.4f, meter),
                Is.EqualTo("[Suppression] id=Enemy1 near-miss distance=0.40 level=0.20 suppressed=false count=1"));
            Assert.That(SuppressionLog.SuppressedChanged("Enemy1", true, 0.52f), Is.EqualTo("[Suppression] id=Enemy1 suppressed=true level=0.52"));
            Assert.That(SuppressionLog.Cleared("Enemy1"), Is.EqualTo("[Suppression] id=Enemy1 cleared"));
            Assert.That(SuppressionLog.HoldCover("Enemy1", false), Is.EqualTo("[Suppression] id=Enemy1 hold-cover=false"));
            Assert.That(SuppressionLog.Configured("Player", Suppression.On), Is.EqualTo("[Suppression] id=Player rule=On"));
        }
    }
}
