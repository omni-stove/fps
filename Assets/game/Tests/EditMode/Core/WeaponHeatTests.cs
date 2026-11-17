using System;
using NUnit.Framework;

namespace Shooter.Core.Tests
{
    // Unity の EditMode テストと dotnet/Shooter.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class WeaponHeatTests
    {
        // 0.25 刻みにするのは、float の足し引きで誤差が出ず、熱をそのまま比べられるため。
        private static WeaponHeat Create() => new WeaponHeat(new HeatSpec(0.25f, 0.5f, 0.5f, 1f));

        [Test]
        public void ItStartsCoolAndReadyToFire()
        {
            var heat = Create();

            Assert.That(heat.Fraction, Is.EqualTo(0f));
            Assert.That(heat.IsOverheated, Is.False);
            Assert.That(heat.CanFire, Is.True);
            Assert.That(heat.OverheatRemainingSeconds, Is.EqualTo(0f));
            Assert.Throws<ArgumentNullException>(() => new WeaponHeat(null));
        }

        [Test]
        public void TheShotThatReachesTheTopReportsTheOverheat()
        {
            var heat = Create();

            Assert.That(heat.AddShot(), Is.False);
            Assert.That(heat.AddShot(), Is.False);
            Assert.That(heat.AddShot(), Is.False);
            Assert.That(heat.Fraction, Is.EqualTo(0.75f));

            Assert.That(heat.AddShot(), Is.True);
            Assert.That(heat.Fraction, Is.EqualTo(1f));
            Assert.That(heat.IsOverheated, Is.True);
            Assert.That(heat.CanFire, Is.False);
            Assert.That(heat.OverheatRemainingSeconds, Is.EqualTo(1f));
            Assert.Throws<InvalidOperationException>(() => heat.AddShot());
        }

        [Test]
        public void TenTenthsReachTheTopDespiteFloatRounding()
        {
            var heat = new WeaponHeat(new HeatSpec(0.1f, 0.4f, 0.3f, 1.5f));
            var overheated = false;
            for (var i = 0; i < 10; i++)
            {
                overheated = heat.AddShot();
            }

            Assert.That(overheated, Is.True);
            Assert.That(heat.Fraction, Is.EqualTo(1f));
        }

        [Test]
        public void OnlyTheTickThatCoolsItFullyReportsTheCooling()
        {
            var heat = Create();
            for (var i = 0; i < 4; i++)
            {
                heat.AddShot();
            }

            Assert.That(heat.Tick(1f), Is.False, "held at the top for the overheat seconds");
            Assert.That(heat.Fraction, Is.EqualTo(1f));
            Assert.That(heat.OverheatRemainingSeconds, Is.EqualTo(0f));

            Assert.That(heat.Tick(1.5f), Is.False);
            Assert.That(heat.Fraction, Is.EqualTo(0.25f));
            Assert.That(heat.CanFire, Is.False, "partly cool is not enough");

            Assert.That(heat.Tick(0.5f), Is.True);
            Assert.That(heat.Fraction, Is.EqualTo(0f));
            Assert.That(heat.CanFire, Is.True);

            Assert.That(heat.Tick(1f), Is.False, "already cool");
        }

        [Test]
        public void CoolingWaitsForTheDelayAfterEachShot()
        {
            var heat = Create();
            heat.AddShot();
            heat.AddShot();

            heat.Tick(0.5f);
            Assert.That(heat.Fraction, Is.EqualTo(0.5f));

            heat.Tick(0.5f);
            Assert.That(heat.Fraction, Is.EqualTo(0.25f));
        }

        [Test]
        public void ResetCoolsItAtOnce()
        {
            var heat = Create();
            for (var i = 0; i < 4; i++)
            {
                heat.AddShot();
            }

            heat.Reset();

            Assert.That(heat.Fraction, Is.EqualTo(0f));
            Assert.That(heat.CanFire, Is.True);
            heat.AddShot();
            heat.Tick(0.5f);
            Assert.That(heat.Fraction, Is.EqualTo(0.25f), "a reset does not skip the delay after the next shot");
        }

        [Test]
        public void NegativeTimeIsRejected()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => Create().Tick(-0.1f));
        }
    }
}
