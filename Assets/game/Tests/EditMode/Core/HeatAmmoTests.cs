using System;
using NUnit.Framework;

namespace Shooter.Core.Tests
{
    // Unity の EditMode テストと dotnet/Shooter.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class HeatAmmoTests
    {
        private const string _energyRifleId = "00000000-0000-4000-8000-0000000000e1";

        // 0.25 刻みにするのは、float の足し引きで誤差が出ず、熱をそのまま比べられるため。
        private static HeatAmmo Create(float heatPerShot = 0.25f, float coolPerSecond = 0.5f, float coolDelaySeconds = 0.5f, float overheatSeconds = 1f)
        {
            var spec = new AmmoSpec(5, 0, 1f, kind: AmmoKind.Heat, heat: new HeatSpec(heatPerShot, coolPerSecond, coolDelaySeconds, overheatSeconds));
            return (HeatAmmo)AmmoModels.For(spec, GameRules.Default);
        }

        [Test]
        public void EachShotAddsHeatAndLeavesTheMagazineAlone()
        {
            var ammo = Create();

            Assert.That(ammo.HeatFraction, Is.EqualTo(0f));
            ammo.Consume();
            ammo.Consume();

            Assert.That(ammo.HeatFraction, Is.EqualTo(0.5f));
            Assert.That(ammo.AmmoInMagazine, Is.EqualTo(5));
            Assert.That(ammo.CanFire, Is.True);
            Assert.That(ammo.IsOverheated, Is.False);
        }

        [Test]
        public void HeatCoolsEverySecondOnlyAfterTheDelay()
        {
            var ammo = Create();
            ammo.Consume();
            ammo.Consume();

            ammo.Tick(0.5f);
            Assert.That(ammo.HeatFraction, Is.EqualTo(0.5f), "still within the cool delay");

            ammo.Tick(0.5f);
            Assert.That(ammo.HeatFraction, Is.EqualTo(0.25f));

            ammo.Consume();
            ammo.Tick(0.75f);
            Assert.That(ammo.HeatFraction, Is.EqualTo(0.375f), "a shot restarts the delay, and a tick spanning its end cools only the rest");

            ammo.Tick(10f);
            Assert.That(ammo.HeatFraction, Is.EqualTo(0f), "heat stops at zero");
        }

        [Test]
        public void ReachingTheTopLocksFiringForTheOverheatSecondsAndUntilFullyCool()
        {
            var ammo = Create();
            for (var i = 0; i < 4; i++)
            {
                ammo.Consume();
            }

            Assert.That(ammo.HeatFraction, Is.EqualTo(1f));
            Assert.That(ammo.IsOverheated, Is.True);
            Assert.That(ammo.CanFire, Is.False);
            Assert.Throws<InvalidOperationException>(ammo.Consume);

            ammo.Tick(1f);
            Assert.That(ammo.HeatFraction, Is.EqualTo(1f), "heat holds at the top for the overheat seconds");
            Assert.That(ammo.CanFire, Is.False);

            ammo.Tick(1f);
            Assert.That(ammo.HeatFraction, Is.EqualTo(0.5f));
            Assert.That(ammo.CanFire, Is.False, "partly cool is not enough");

            ammo.Tick(0.5f);
            Assert.That(ammo.HeatFraction, Is.EqualTo(0.25f));
            Assert.That(ammo.CanFire, Is.False);

            ammo.Tick(0.5f);
            Assert.That(ammo.HeatFraction, Is.EqualTo(0f));
            Assert.That(ammo.IsOverheated, Is.False);
            Assert.That(ammo.CanFire, Is.True);
        }

        [Test]
        public void OneLongTickRunsTheHoldAndTheCoolingTogether()
        {
            var ammo = Create(heatPerShot: 1f);
            ammo.Consume();

            ammo.Tick(1.5f);
            Assert.That(ammo.HeatFraction, Is.EqualTo(0.75f));

            ammo.Tick(5f);
            Assert.That(ammo.CanFire, Is.True);
        }

        [Test]
        public void ShotsThatAddUpToOneInFloatStillOverheat()
        {
            var ammo = Create(heatPerShot: 0.1f);
            for (var i = 0; i < 10; i++)
            {
                Assert.That(ammo.CanFire, Is.True, "shot " + i);
                ammo.Consume();
            }

            Assert.That(ammo.IsOverheated, Is.True);
            Assert.That(ammo.HeatFraction, Is.EqualTo(1f));
        }

        [Test]
        public void ItNeverReloadsOrTakesAmmo()
        {
            var ammo = Create();
            var reloaded = 0;
            ammo.Reloaded += () => reloaded++;
            ammo.Consume();

            Assert.That(ammo.TryStartReload(), Is.False);
            Assert.That(ammo.IsReloading, Is.False);
            Assert.That(ammo.TryActiveReload(out _), Is.False);
            ammo.CancelReload();
            Assert.That(ammo.ReserveAmmoRoom, Is.Zero);
            Assert.That(ammo.AddReserveAmmo(10), Is.Zero);
            Assert.That(ammo.ReserveAmmo, Is.Zero);
            ammo.Tick(10f);
            Assert.That(reloaded, Is.Zero);
            Assert.Throws<ArgumentOutOfRangeException>(() => ammo.AddReserveAmmo(-1));
            Assert.Throws<ArgumentOutOfRangeException>(() => ammo.Tick(-1f));
        }

        [Test]
        public void TheWeaponStopsFiringWhileOverheatedAndFiresAgainOnceCool()
        {
            var spec = new WeaponSpec(
                _energyRifleId,
                null,
                WeaponCategory.AssaultRifle,
                ammo: new AmmoSpec(1, 0, 1f, kind: AmmoKind.Heat, heat: new HeatSpec(0.5f, 1f, 1f, 0.5f)));
            var weapon = new Weapon(spec);

            Assert.That(weapon.TryFire(), Is.True);
            weapon.Tick(spec.FireInterval);
            Assert.That(weapon.TryFire(), Is.True);
            weapon.Tick(spec.FireInterval);
            Assert.That(weapon.TryFire(), Is.False);
            Assert.That(weapon.TryStartReload(), Is.False);

            weapon.Tick(1.5f);
            Assert.That(weapon.TryFire(), Is.True);
        }
    }
}
