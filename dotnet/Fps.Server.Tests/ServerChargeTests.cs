using System.IO;
using Fps.Core;
using NUnit.Framework;

namespace Fps.Server.Tests
{
    // サーバーは届いた溜めを信じず、前の射撃・持ち替えから届くまでの秒数で届く分までに下げる
    public sealed class ServerChargeTests
    {
        private const string _chargeRifleId = "0a90a121-a287-4aae-8fcf-885ddf59da79";
        private const float _tolerance = 1e-4f;

        private double _now;
        private StringWriter _log;
        private ServerPlayerWeapons _weapons;

        [SetUp]
        public void SetUp()
        {
            _now = 100d;
            _log = new StringWriter();
            _weapons = new ServerPlayerWeapons(1, _log, () => _now);
        }

        [TearDown]
        public void TearDown()
        {
            _log.Dispose();
        }

        [Test]
        public void AFullChargeHeldLongEnoughIsAccepted()
        {
            Equip(new ChargeSpec(1f, 0.25f, 3f));
            _now += 1.5d;

            Assert.That(_weapons.AcceptCharge(1f, out var fraction), Is.EqualTo(ShotRejection.None));
            Assert.That(fraction, Is.EqualTo(1f));
            Assert.That(_weapons.Sync().ChargeFraction, Is.EqualTo(1f));
        }

        [Test]
        public void AChargeLongerThanTheHeldTimeIsCutToWhatTheTimeAllows()
        {
            Equip(new ChargeSpec(2f, 0f, 3f));
            _now += 1d;

            _weapons.AcceptCharge(1f, out var fraction);

            var expected = (float)((1d + ServerPlayerWeapons.ChargeArrivalToleranceSeconds) / 2d);
            Assert.That(fraction, Is.EqualTo(expected).Within(_tolerance));
        }

        [Test]
        public void TheNextChargeIsMeasuredFromTheLastShot()
        {
            Equip(new ChargeSpec(1f, 0f, 3f));
            _now += 5d;
            _weapons.AcceptCharge(1f, out _);

            _now += 0.3d;
            _weapons.AcceptCharge(1f, out var fraction);

            Assert.That(fraction, Is.EqualTo((float)(0.3d + ServerPlayerWeapons.ChargeArrivalToleranceSeconds)).Within(_tolerance));
        }

        [Test]
        public void AShotThatCannotHaveReachedTheMinimumIsRejected()
        {
            Equip(new ChargeSpec(2f, 0.5f, 3f));
            _now += 0.5d;

            Assert.That(_weapons.AcceptCharge(1f, out var fraction), Is.EqualTo(ShotRejection.Undercharged));
            Assert.That(fraction, Is.Zero);
        }

        [Test]
        public void AShortChargeIsAcceptedWhenTheWeaponFiresBelowTheMinimum()
        {
            Equip(new ChargeSpec(2f, 0.5f, 3f, firesBelowMin: true));
            _now += 0.1d;

            Assert.That(_weapons.AcceptCharge(0.05f, out var fraction), Is.EqualTo(ShotRejection.None));
            Assert.That(fraction, Is.EqualTo(0.05f));
        }

        [Test]
        public void TheRestOfABurstKeepsTheChargeOfItsFirstRound()
        {
            Equip(new ChargeSpec(1f, 0.6f, 3f), new BurstSpec(3, 0.1f));
            _now += 0.8d;
            _weapons.AcceptCharge(0.8f, out var first);

            _now += 0.1d;
            Assert.That(_weapons.AcceptCharge(0.8f, out var second), Is.EqualTo(ShotRejection.None));
            _now += 0.1d;
            Assert.That(_weapons.AcceptCharge(0.8f, out var third), Is.EqualTo(ShotRejection.None));

            Assert.That(first, Is.EqualTo(0.8f));
            Assert.That(second, Is.EqualTo(0.8f));
            Assert.That(third, Is.EqualTo(0.8f));

            _now += 0.1d;
            Assert.That(_weapons.AcceptCharge(0.8f, out _), Is.EqualTo(ShotRejection.Undercharged), "a fourth round is a new press");
        }

        [Test]
        public void ABotWaitsUntilTheChargeIsFull()
        {
            Equip(new ChargeSpec(1f, 0.5f, 3f));

            _now += 0.9d;
            Assert.That(_weapons.IsChargedForBot(), Is.False);
            _now += 0.1d;
            Assert.That(_weapons.IsChargedForBot(), Is.True);

            _weapons.AcceptCharge(1f, out _);
            Assert.That(_weapons.IsChargedForBot(), Is.False, "the next shot charges again");
        }

        [Test]
        public void AWeaponWithoutChargeTakesNoCharge()
        {
            _weapons.Equip(new[] { TestRules.Rifle() }, 0.5f, GameRules.Default);
            _now += 5d;

            Assert.That(_weapons.AcceptCharge(1f, out var fraction), Is.EqualTo(ShotRejection.None));
            Assert.That(fraction, Is.Zero);
        }

        private void Equip(ChargeSpec charge, BurstSpec burst = null)
        {
            var spec = new WeaponSpec(
                _chargeRifleId, "weapon.chargeRifle", WeaponCategory.MarksmanRifle, new TriggerSpec(FireMode.Semi, 0.25f, burst, charge));
            _weapons.Equip(new[] { spec }, 0.5f, GameRules.Default);
        }
    }
}
