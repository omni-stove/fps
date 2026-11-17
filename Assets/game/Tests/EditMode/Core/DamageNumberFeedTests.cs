using System;
using System.Numerics;
using NUnit.Framework;

namespace Shooter.Core.Tests
{
    // Unity の EditMode テストと dotnet/Shooter.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class DamageNumberFeedTests
    {
        private static readonly DamageNumberSettings _settings = new DamageNumberSettings(1f, 40f, 0.5f, 3);

        private static HitConfirm Hit(int damage, HitZone zone = HitZone.Body, bool killed = false)
        {
            return new HitConfirm(damage, zone, killed, downed: false, new Vector3(1f, 2f, 3f));
        }

        [Test]
        public void AddedNumbersCarryTheHitAndGetIncreasingIds()
        {
            var feed = new DamageNumberFeed(_settings);

            var first = feed.Add(Hit(25));
            var second = feed.Add(Hit(100, HitZone.Head, killed: true));

            Assert.That(first, Is.EqualTo(1));
            Assert.That(second, Is.EqualTo(2));
            Assert.That(feed.Numbers.Count, Is.EqualTo(2));
            Assert.That(feed.Numbers[0].Damage, Is.EqualTo(25));
            Assert.That(feed.Numbers[0].Point, Is.EqualTo(new Vector3(1f, 2f, 3f)));
            Assert.That(feed.Numbers[1].Zone, Is.EqualTo(HitZone.Head));
            Assert.That(feed.Numbers[1].Killed, Is.True);
        }

        [Test]
        public void AHitWithoutDamageShowsNothing()
        {
            var feed = new DamageNumberFeed(_settings);

            Assert.That(feed.Add(Hit(0)), Is.EqualTo(0));
            Assert.That(feed.Numbers.Count, Is.EqualTo(0));
        }

        [Test]
        public void TheOldestNumberGoesWhenTheLimitIsPassed()
        {
            var feed = new DamageNumberFeed(_settings);

            feed.Add(Hit(1));
            feed.Add(Hit(2));
            feed.Add(Hit(3));
            feed.Add(Hit(4));

            Assert.That(feed.Numbers.Count, Is.EqualTo(3));
            Assert.That(feed.Numbers[0].Damage, Is.EqualTo(2));
            Assert.That(feed.Numbers[2].Damage, Is.EqualTo(4));
        }

        [Test]
        public void ANumberRisesFadesInItsLastHalfAndGoesAfterItsLifetime()
        {
            var feed = new DamageNumberFeed(_settings);
            feed.Add(Hit(10));

            Assert.That(feed.Rise(feed.Numbers[0]), Is.EqualTo(0f));
            Assert.That(feed.Opacity(feed.Numbers[0]), Is.EqualTo(1f));

            feed.Tick(0.25f);
            Assert.That(feed.Rise(feed.Numbers[0]), Is.EqualTo(10f).Within(1e-4f));
            Assert.That(feed.Opacity(feed.Numbers[0]), Is.EqualTo(1f));

            feed.Tick(0.5f);
            Assert.That(feed.Rise(feed.Numbers[0]), Is.EqualTo(30f).Within(1e-4f));
            Assert.That(feed.Opacity(feed.Numbers[0]), Is.EqualTo(0.5f).Within(1e-4f));

            feed.Tick(0.25f);
            Assert.That(feed.Numbers.Count, Is.EqualTo(0));
        }

        [Test]
        public void WithoutAFadeTheNumberStaysOpaque()
        {
            var feed = new DamageNumberFeed(new DamageNumberSettings(1f, 0f, 0f, 1));
            feed.Add(Hit(10));

            feed.Tick(0.9f);

            Assert.That(feed.Opacity(feed.Numbers[0]), Is.EqualTo(1f));
            Assert.That(feed.Rise(feed.Numbers[0]), Is.EqualTo(0f));
        }

        [TestCase(0f)]
        [TestCase(-1f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        public void TickIgnoresANonPositiveOrNonFiniteStep(float delta)
        {
            var feed = new DamageNumberFeed(_settings);
            feed.Add(Hit(10));

            feed.Tick(delta);

            Assert.That(feed.Numbers[0].AgeSeconds, Is.EqualTo(0f));
        }

        [Test]
        public void ClearRemovesEveryNumber()
        {
            var feed = new DamageNumberFeed(_settings);
            feed.Add(Hit(10));
            feed.Add(Hit(20));

            feed.Clear();

            Assert.That(feed.Numbers.Count, Is.EqualTo(0));
        }

        [Test]
        public void TheDefaultsMatchTheirConstants()
        {
            var settings = DamageNumberSettings.Default;

            Assert.That(settings.LifetimeSeconds, Is.EqualTo(0.8f));
            Assert.That(settings.RiseDistance, Is.EqualTo(48f));
            Assert.That(settings.FadeFraction, Is.EqualTo(0.5f));
            Assert.That(settings.MaxCount, Is.EqualTo(24));
        }

        [Test]
        public void SettingsRejectOutOfRangeValues()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new DamageNumberSettings(0f, 1f, 0.5f, 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => new DamageNumberSettings(float.NaN, 1f, 0.5f, 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => new DamageNumberSettings(1f, -1f, 0.5f, 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => new DamageNumberSettings(1f, 1f, 1.5f, 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => new DamageNumberSettings(1f, 1f, -0.1f, 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => new DamageNumberSettings(1f, 1f, 0.5f, 0));
            Assert.Throws<ArgumentNullException>(() => new DamageNumberFeed(null));
        }

        [Test]
        public void TheHitPointTakesPartInEquality()
        {
            var at = new HitConfirm(10, HitZone.Body, false, false, new Vector3(1f, 0f, 0f));
            var elsewhere = new HitConfirm(10, HitZone.Body, false, false, new Vector3(2f, 0f, 0f));
            var unknown = new HitConfirm(10, HitZone.Body, false, false);

            Assert.That(unknown.Point, Is.Null);
            Assert.That(at, Is.EqualTo(new HitConfirm(10, HitZone.Body, false, false, new Vector3(1f, 0f, 0f))));
            Assert.That(at, Is.Not.EqualTo(elsewhere));
            Assert.That(at, Is.Not.EqualTo(unknown));
        }
    }
}
