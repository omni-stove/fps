using System;
using NUnit.Framework;

namespace Fps.Core.Tests
{
    // Unity の EditMode テストと dotnet/Fps.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class DamageFeedbackTests
    {
        private const float _tolerance = 1e-5f;

        private static DamageFeedbackSettings Settings()
        {
            return new DamageFeedbackSettings(
                hitPeakIntensity: 0.8f,
                fullHitDamage: 50f,
                hitFadeSeconds: 1f,
                lowHealthThreshold: 0.25f,
                lowHealthMinIntensity: 0.3f,
                lowHealthMaxIntensity: 0.7f);
        }

        [Test]
        public void NothingShowsBeforeAHit()
        {
            var feedback = new DamageFeedback(Settings());

            Assert.That(feedback.Intensity, Is.Zero);
            Assert.That(feedback.IsLowHealth, Is.False);
        }

        [Test]
        public void AHitFadesBackToZeroOverTime()
        {
            var feedback = new DamageFeedback(Settings());
            feedback.SetHealth(1f);

            Assert.That(feedback.Hit(50), Is.True);
            Assert.That(feedback.Intensity, Is.EqualTo(0.8f).Within(_tolerance), "a full hit starts at the peak");

            feedback.Tick(0.5f);
            Assert.That(feedback.Intensity, Is.EqualTo(0.4f).Within(_tolerance), "halfway through the fade");

            feedback.Tick(0.25f);
            Assert.That(feedback.Intensity, Is.EqualTo(0.2f).Within(_tolerance));

            feedback.Tick(0.25f);
            Assert.That(feedback.Intensity, Is.Zero, "back to nothing after the fade time");

            feedback.Tick(1f);
            Assert.That(feedback.Intensity, Is.Zero);
        }

        [Test]
        public void SmallerHitsAreWeakerAndBigHitsAreCapped()
        {
            var small = new DamageFeedback(Settings());
            small.Hit(10);
            Assert.That(small.Intensity, Is.EqualTo(0.8f * 10f / 50f).Within(_tolerance));

            var big = new DamageFeedback(Settings());
            big.Hit(500);
            Assert.That(big.Intensity, Is.EqualTo(0.8f).Within(_tolerance));
        }

        [Test]
        public void AWeakHitDuringAStrongFadeKeepsTheStrongerRedAndRestartsTheFade()
        {
            var feedback = new DamageFeedback(Settings());
            feedback.Hit(50);
            feedback.Tick(0.25f);
            var before = feedback.Intensity;

            feedback.Hit(5);

            Assert.That(feedback.Intensity, Is.EqualTo(before).Within(_tolerance));
            feedback.Tick(0.75f);
            Assert.That(feedback.Intensity, Is.GreaterThan(0f), "the fade restarted from the hit");
            feedback.Tick(0.25f);
            Assert.That(feedback.Intensity, Is.Zero);
        }

        [Test]
        public void NonPositiveDamageDoesNothing()
        {
            var feedback = new DamageFeedback(Settings());

            Assert.That(feedback.Hit(0), Is.False);
            Assert.That(feedback.Hit(-5), Is.False);
            Assert.That(feedback.Intensity, Is.Zero);
        }

        [Test]
        public void InvalidTicksDoNotAdvance()
        {
            var feedback = new DamageFeedback(Settings());
            feedback.Hit(50);

            feedback.Tick(-1f);
            feedback.Tick(float.NaN);
            feedback.Tick(float.PositiveInfinity);

            Assert.That(feedback.Intensity, Is.EqualTo(0.8f).Within(_tolerance));
        }

        [Test]
        public void LowHealthStartsAtTheThresholdAndRecoversAboveIt()
        {
            var feedback = new DamageFeedback(Settings());

            Assert.That(feedback.SetHealth(1f), Is.EqualTo(LowHealthChange.None));
            Assert.That(feedback.SetHealth(0.26f), Is.EqualTo(LowHealthChange.None));
            Assert.That(feedback.SetHealth(0.25f), Is.EqualTo(LowHealthChange.Started), "the threshold itself is low, like the HUD warning");
            Assert.That(feedback.IsLowHealth, Is.True);
            Assert.That(feedback.SetHealth(0.1f), Is.EqualTo(LowHealthChange.None), "still low");
            Assert.That(feedback.SetHealth(0.5f), Is.EqualTo(LowHealthChange.Recovered));
            Assert.That(feedback.IsLowHealth, Is.False);
        }

        [Test]
        public void FallingOrLosingTheHealthClearsWithoutRecovering()
        {
            var feedback = new DamageFeedback(Settings());
            feedback.SetHealth(0.2f);

            Assert.That(feedback.SetHealth(0f), Is.EqualTo(LowHealthChange.Cleared), "being down is not a recovery");
            Assert.That(feedback.SetHealth(0f), Is.EqualTo(LowHealthChange.None));

            feedback.SetHealth(0.2f);
            Assert.That(feedback.SetHealth(null), Is.EqualTo(LowHealthChange.Cleared));

            feedback.SetHealth(0.2f);
            Assert.That(feedback.SetHealth(float.NaN), Is.EqualTo(LowHealthChange.Cleared), "NaN is treated as no health");
        }

        [Test]
        public void LowHealthKeepsTheRedAndGetsStrongerAsHealthDrops()
        {
            var feedback = new DamageFeedback(Settings());

            feedback.SetHealth(0.25f);
            Assert.That(feedback.LowHealthIntensity, Is.EqualTo(0.3f).Within(_tolerance));

            feedback.SetHealth(0.125f);
            Assert.That(feedback.LowHealthIntensity, Is.EqualTo(0.5f).Within(_tolerance));

            feedback.Tick(10f);
            Assert.That(feedback.Intensity, Is.EqualTo(0.5f).Within(_tolerance), "it does not fade while low");

            feedback.Hit(50);
            Assert.That(feedback.Intensity, Is.EqualTo(0.8f).Within(_tolerance), "a hit shows over the low health red");
            feedback.Tick(1f);
            Assert.That(feedback.Intensity, Is.EqualTo(0.5f).Within(_tolerance));

            feedback.SetHealth(1f);
            Assert.That(feedback.Intensity, Is.Zero);
        }

        [Test]
        public void HealthOutsideTheRangeIsClamped()
        {
            var feedback = new DamageFeedback(Settings());

            Assert.That(feedback.SetHealth(1.5f), Is.EqualTo(LowHealthChange.None));
            Assert.That(feedback.HealthFraction, Is.EqualTo(1f));
            Assert.That(feedback.SetHealth(-1f), Is.EqualTo(LowHealthChange.None), "negative is down, not low");
            Assert.That(feedback.HealthFraction, Is.EqualTo(0f));
        }

        [Test]
        public void ResetForgetsTheHitAndTheLowHealth()
        {
            var feedback = new DamageFeedback(Settings());
            feedback.SetHealth(0.1f);
            feedback.Hit(50);

            feedback.Reset();

            Assert.That(feedback.Intensity, Is.Zero);
            Assert.That(feedback.IsLowHealth, Is.False);
            Assert.That(feedback.HealthFraction, Is.Null);
            Assert.That(feedback.SetHealth(0.1f), Is.EqualTo(LowHealthChange.Started), "low again after the reset");
        }

        [Test]
        public void TheDefaultsMatchTheHudWarning()
        {
            Assert.That(DamageFeedbackSettings.Default.LowHealthThreshold, Is.EqualTo(HudFormat.LowHealthRatio));
        }

        [Test]
        public void SettingsRejectValuesOutOfRange()
        {
            Assert.Throws<ArgumentNullException>(() => new DamageFeedback(null));
            Assert.Throws<ArgumentOutOfRangeException>(() => new DamageFeedbackSettings(0f, 50f, 1f, 0.25f, 0.3f, 0.7f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new DamageFeedbackSettings(1.1f, 50f, 1f, 0.25f, 0.3f, 0.7f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new DamageFeedbackSettings(0.8f, 0f, 1f, 0.25f, 0.3f, 0.7f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new DamageFeedbackSettings(0.8f, 50f, 0f, 0.25f, 0.3f, 0.7f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new DamageFeedbackSettings(0.8f, 50f, float.NaN, 0.25f, 0.3f, 0.7f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new DamageFeedbackSettings(0.8f, 50f, 1f, 0f, 0.3f, 0.7f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new DamageFeedbackSettings(0.8f, 50f, 1f, 1f, 0.3f, 0.7f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new DamageFeedbackSettings(0.8f, 50f, 1f, 0.25f, -0.1f, 0.7f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new DamageFeedbackSettings(0.8f, 50f, 1f, 0.25f, 0.5f, 0.4f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new DamageFeedbackSettings(0.8f, 50f, 1f, 0.25f, 0.3f, 1.1f));
        }
    }
}
