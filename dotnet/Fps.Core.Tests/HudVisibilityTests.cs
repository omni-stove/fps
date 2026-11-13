using System;
using NUnit.Framework;

namespace Fps.Core.Tests
{
    public sealed class HudVisibilityTests
    {
        [Test]
        public void FullShowsEverythingAtTheNormalCrosshairSize()
        {
            var visibility = HudVisibility.For(HudDensity.Full);

            Assert.That(visibility.Density, Is.EqualTo(HudDensity.Full));
            Assert.That(visibility.Health, Is.True);
            Assert.That(visibility.Ammo, Is.True);
            Assert.That(visibility.ReserveAmmo, Is.True);
            Assert.That(visibility.WeaponName, Is.True);
            Assert.That(visibility.Time, Is.True);
            Assert.That(visibility.Score, Is.True);
            Assert.That(visibility.Crosshair, Is.True);
            Assert.That(visibility.CrosshairScale, Is.EqualTo(HudVisibility.FullCrosshairScale));
            Assert.That(visibility.HitMarker, Is.True);
            Assert.That(visibility.DamageDirection, Is.True);
            Assert.That(visibility.KillFeed, Is.True);
        }

        [Test]
        public void MinimalShowsOnlyTheMagazineAndASmallerCrosshair()
        {
            var visibility = HudVisibility.For(HudDensity.Minimal);

            Assert.That(visibility.Density, Is.EqualTo(HudDensity.Minimal));
            Assert.That(visibility.Ammo, Is.True);
            Assert.That(visibility.ReserveAmmo, Is.False);
            Assert.That(visibility.Crosshair, Is.True);
            Assert.That(visibility.CrosshairScale, Is.EqualTo(HudVisibility.SmallCrosshairScale));
            Assert.That(visibility.CrosshairScale, Is.LessThan(HudVisibility.FullCrosshairScale));
            Assert.That(visibility.CrosshairScale, Is.GreaterThan(0f));
        }

        [Test]
        public void MinimalKeepsTheOtherItemsOfFull()
        {
            var visibility = HudVisibility.For(HudDensity.Minimal);

            Assert.That(visibility.Health, Is.True);
            Assert.That(visibility.WeaponName, Is.True);
            Assert.That(visibility.Time, Is.True);
            Assert.That(visibility.Score, Is.True);
            Assert.That(visibility.HitMarker, Is.True);
            Assert.That(visibility.DamageDirection, Is.True);
            Assert.That(visibility.KillFeed, Is.True);
        }

        [Test]
        public void HardcoreHidesHealthAmmoCrosshairAndHitMarker()
        {
            var visibility = HudVisibility.For(HudDensity.Hardcore);

            Assert.That(visibility.Density, Is.EqualTo(HudDensity.Hardcore));
            Assert.That(visibility.Health, Is.False);
            Assert.That(visibility.Ammo, Is.False);
            Assert.That(visibility.ReserveAmmo, Is.False);
            Assert.That(visibility.WeaponName, Is.False);
            Assert.That(visibility.Crosshair, Is.False);
            Assert.That(visibility.HitMarker, Is.False);
        }

        [Test]
        public void HardcoreKeepsTheKillFeedAndTheScore()
        {
            var visibility = HudVisibility.For(HudDensity.Hardcore);

            Assert.That(visibility.KillFeed, Is.True);
            Assert.That(visibility.Score, Is.True);
            Assert.That(visibility.Time, Is.True);
            Assert.That(visibility.DamageDirection, Is.True);
        }

        [Test]
        public void EveryDensityHasItsOwnVisibility()
        {
            foreach (HudDensity density in Enum.GetValues<HudDensity>())
            {
                var visibility = HudVisibility.For(density);

                Assert.That(visibility.Density, Is.EqualTo(density));
                Assert.That(HudVisibility.For(density), Is.SameAs(visibility));
            }
        }

        [Test]
        public void TheDefaultRulesShowEverything()
        {
            Assert.That(HudVisibility.For(GameRules.Default.HudDensity), Is.SameAs(HudVisibility.Full));
        }

        [TestCase(-1)]
        [TestCase(3)]
        [TestCase(99)]
        public void RejectsAnUndefinedDensity(int value)
        {
            var ex = Assert.Throws<ArgumentOutOfRangeException>(() => HudVisibility.For((HudDensity)value));

            Assert.That(ex.ParamName, Is.EqualTo("density"));
        }
    }
}
