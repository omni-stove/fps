using System.Collections.Generic;
using NUnit.Framework;

namespace Fps.Core.Tests
{
    // Unity の EditMode テストと dotnet/Fps.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class HudModelTests
    {
        private const int _playerId = 0;

        private static Weapon NewWeapon(int magazine = 30, int reserve = 90, float fireInterval = 0f, float reloadSeconds = 1f)
        {
            return new Weapon(new WeaponSpec("rifle", null, FireMode.Auto, magazine, reserve, fireInterval, reloadSeconds, 25, 100f, DamageFalloff.None));
        }

        private static List<HudField> Record(HudModel model)
        {
            var changes = new List<HudField>();
            model.Changed += changes.Add;
            return changes;
        }

        [Test]
        public void StartsWithPlaceholders()
        {
            var model = new HudModel();

            Assert.That(model.HealthText, Is.EqualTo(HudFormat.NoHealth));
            Assert.That(model.AmmoText, Is.EqualTo(HudFormat.NoAmmo));
            Assert.That(model.TimeText, Is.EqualTo(HudFormat.NoTime));
        }

        [Test]
        public void BindingShowsTheCurrentValuesAtOnce()
        {
            var model = new HudModel();
            var changes = Record(model);

            model.BindHealth(new Health(100));
            model.BindWeapon(NewWeapon());
            model.BindMatch(new Match(180f, 1, _playerId));

            Assert.That(model.HealthText, Is.EqualTo("100"));
            Assert.That(model.AmmoText, Is.EqualTo("30 / 90"));
            Assert.That(model.TimeText, Is.EqualTo("03:00"));
            Assert.That(changes, Is.EqualTo(new[] { HudField.Health, HudField.Ammo, HudField.Time }));
        }

        [Test]
        public void HealthUpdatesOnDamageAndHeal()
        {
            var model = new HudModel();
            var health = new Health(100);
            model.BindHealth(health);
            var changes = Record(model);

            health.TakeDamage(25);
            Assert.That(model.HealthText, Is.EqualTo("75"));

            health.Heal(10);
            Assert.That(model.HealthText, Is.EqualTo("85"));

            health.TakeDamage(500);
            Assert.That(model.HealthText, Is.EqualTo("0"));

            Assert.That(changes, Is.EqualTo(new[] { HudField.Health, HudField.Health, HudField.Health }));
        }

        [Test]
        public void HealthDoesNotNotifyWhenNothingChanges()
        {
            var model = new HudModel();
            var health = new Health(100);
            model.BindHealth(health);
            var changes = Record(model);

            health.Heal(10);
            health.TakeDamage(0);

            Assert.That(changes, Is.Empty);
        }

        [Test]
        public void AmmoUpdatesOnFireAndReload()
        {
            var model = new HudModel();
            var weapon = NewWeapon(magazine: 30, reserve: 90, reloadSeconds: 1f);
            model.BindWeapon(weapon);
            var changes = Record(model);

            weapon.TryFire();
            weapon.TryFire();
            Assert.That(model.AmmoText, Is.EqualTo("28 / 90"));

            weapon.TryStartReload();
            weapon.Tick(0.5f);
            Assert.That(model.AmmoText, Is.EqualTo("28 / 90"), "the magazine is only refilled when the reload finishes");

            weapon.Tick(0.5f);
            Assert.That(model.AmmoText, Is.EqualTo("30 / 88"));

            Assert.That(changes, Is.EqualTo(new[] { HudField.Ammo, HudField.Ammo, HudField.Ammo }));
        }

        [Test]
        public void TimeUpdatesOnlyWhenTheShownSecondChanges()
        {
            var model = new HudModel();
            var match = new Match(65f, 1, _playerId);
            model.BindMatch(match);
            match.Start();
            var changes = Record(model);

            // 65.0 → 64.9: 表示は 01:05 のまま。
            match.Tick(0.1f);
            Assert.That(model.TimeText, Is.EqualTo("01:05"));
            Assert.That(changes, Is.Empty);

            // 64.9 → 63.9: 01:04。
            match.Tick(1f);
            Assert.That(model.TimeText, Is.EqualTo("01:04"));
            Assert.That(changes, Is.EqualTo(new[] { HudField.Time }));
        }

        [Test]
        public void TimeShowsZeroWhenTheMatchRunsOut()
        {
            var model = new HudModel();
            var match = new Match(3f, 1, _playerId);
            model.BindMatch(match);
            match.Start();

            match.Tick(10f);

            Assert.That(match.IsOver, Is.True);
            Assert.That(model.TimeText, Is.EqualTo("00:00"));
        }

        [Test]
        public void RebindingStopsListeningToTheOldSource()
        {
            var model = new HudModel();
            var oldHealth = new Health(100);
            var newHealth = new Health(50);
            model.BindHealth(oldHealth);

            model.BindHealth(newHealth);
            var changes = Record(model);
            oldHealth.TakeDamage(30);

            Assert.That(model.HealthText, Is.EqualTo("50"));
            Assert.That(changes, Is.Empty);
        }

        [Test]
        public void BindingNullRestoresThePlaceholder()
        {
            var model = new HudModel();
            model.BindHealth(new Health(100));
            model.BindWeapon(NewWeapon());
            model.BindMatch(new Match(10f, 1, _playerId));

            model.BindHealth(null);
            model.BindWeapon(null);
            model.BindMatch(null);

            Assert.That(model.HealthText, Is.EqualTo(HudFormat.NoHealth));
            Assert.That(model.AmmoText, Is.EqualTo(HudFormat.NoAmmo));
            Assert.That(model.TimeText, Is.EqualTo(HudFormat.NoTime));
        }

        [Test]
        public void UnbindAllKeepsTheTextButStopsUpdates()
        {
            var model = new HudModel();
            var health = new Health(100);
            var weapon = NewWeapon();
            var match = new Match(10f, 1, _playerId);
            model.BindHealth(health);
            model.BindWeapon(weapon);
            model.BindMatch(match);
            match.Start();

            model.UnbindAll();
            var changes = Record(model);
            health.TakeDamage(10);
            weapon.TryFire();
            match.Tick(5f);

            Assert.That(changes, Is.Empty);
            Assert.That(model.HealthText, Is.EqualTo("100"));
            Assert.That(model.AmmoText, Is.EqualTo("30 / 90"));
            Assert.That(model.TimeText, Is.EqualTo("00:10"));
        }
    }
}
