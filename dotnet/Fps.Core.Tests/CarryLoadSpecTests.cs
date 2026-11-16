using System;
using NUnit.Framework;

namespace Fps.Core.Tests
{
    public sealed class CarryLoadSpecTests
    {
        private const float _tolerance = 1e-5f;

        private static WeaponSpec Weapon(string id, float weight, float moveSpeedMultiplier = 1f)
        {
            return new WeaponSpec(id, null, WeaponCategory.AssaultRifle, weight: weight, moveSpeedMultiplier: moveSpeedMultiplier);
        }

        private static readonly WeaponSpec _rifle = Weapon("6c1f3b2a-8d4e-4f7a-9b0c-2e5d7a1f4c83", 4f, 0.9f);
        private static readonly WeaponSpec _pistol = Weapon("b4e2a9d7-3c16-4a58-8f2e-7d0b9c6a1e45", 1f, 1.1f);
        private static readonly WeaponSpec[] _both = { _rifle, _pistol };

        [Test]
        public void TheDefaultIgnoresWeightAndAppliesTheHeldWeaponsMultiplierToEveryGait()
        {
            var carry = CarryLoadSpec.Default;

            Assert.That(carry.SpeedMultiplier(_rifle, _both), Is.EqualTo(0.9f).Within(_tolerance));
            Assert.That(carry.SpeedMultiplier(_pistol, _both), Is.EqualTo(1.1f).Within(_tolerance));
            Assert.That(carry.Gaits, Is.EqualTo(CarryGaits.All));
        }

        [Test]
        public void HeldWeightSlowsByTheLossPerKgAboveTheFreeWeight()
        {
            var carry = new CarryLoadSpec(CarrySource.Held, CarrySource.None, 2f, 0.1f, 0f, CarryGaits.All);

            // 4 kg − 2 kg = 2 kg 超え → 1 − 0.2
            Assert.That(carry.SpeedMultiplier(_rifle, _both), Is.EqualTo(0.8f).Within(_tolerance));
            // 1 kg は遅くならない重さの内
            Assert.That(carry.SpeedMultiplier(_pistol, _both), Is.EqualTo(1f).Within(_tolerance));
        }

        [Test]
        public void CarriedWeightSumsEveryWeaponWhicheverIsHeld()
        {
            var carry = new CarryLoadSpec(CarrySource.Carried, CarrySource.None, 0f, 0.1f, 0f, CarryGaits.All);

            // 4 + 1 = 5 kg → 0.5
            Assert.That(carry.SpeedMultiplier(_rifle, _both), Is.EqualTo(0.5f).Within(_tolerance));
            Assert.That(carry.SpeedMultiplier(_pistol, _both), Is.EqualTo(0.5f).Within(_tolerance));
        }

        [Test]
        public void WeightNeverDropsTheMultiplierBelowTheFloor()
        {
            var carry = new CarryLoadSpec(CarrySource.Carried, CarrySource.None, 0f, 1f, 0.3f, CarryGaits.All);

            Assert.That(carry.SpeedMultiplier(_rifle, _both), Is.EqualTo(0.3f).Within(_tolerance));
        }

        [Test]
        public void CarriedMultipliersMultiplyAndComeAfterTheWeightFloor()
        {
            var carry = new CarryLoadSpec(CarrySource.Carried, CarrySource.Carried, 0f, 1f, 0.5f, CarryGaits.All);

            // 重さで 0.5 に止まり、0.9 × 1.1 を掛ける
            Assert.That(carry.SpeedMultiplier(_rifle, _both), Is.EqualTo(0.5f * 0.9f * 1.1f).Within(_tolerance));
        }

        [Test]
        public void NoneCountsNothing()
        {
            var carry = new CarryLoadSpec(CarrySource.None, CarrySource.None, 0f, 1f, 0f, CarryGaits.All);

            Assert.That(carry.SpeedMultiplier(_rifle, _both), Is.EqualTo(1f));
        }

        [Test]
        public void NothingHeldOrCarriedMovesAtFullSpeed()
        {
            var carry = new CarryLoadSpec(CarrySource.Carried, CarrySource.Held, 0f, 0.1f, 0f, CarryGaits.All);

            Assert.That(carry.SpeedMultiplier(null, null), Is.EqualTo(1f));
            Assert.That(carry.SpeedMultiplier(null, new WeaponSpec[] { null }), Is.EqualTo(1f));
        }

        [Test]
        public void ForGaitAppliesOnlyToTheChosenGaits()
        {
            var carry = new CarryLoadSpec(CarrySource.Held, CarrySource.Held, 0f, 0f, 0f, CarryGaits.Sprint | CarryGaits.Prone);

            Assert.That(carry.ForGait(CarryGaits.Sprint, 0.7f), Is.EqualTo(0.7f));
            Assert.That(carry.ForGait(CarryGaits.Prone, 0.7f), Is.EqualTo(0.7f));
            Assert.That(carry.ForGait(CarryGaits.Walk, 0.7f), Is.EqualTo(1f));
            Assert.That(carry.ForGait(CarryGaits.Crouch, 0.7f), Is.EqualTo(1f));
        }

        [TestCase(-1f, 0f, 0f, "freeWeight")]
        [TestCase(float.PositiveInfinity, 0f, 0f, "freeWeight")]
        [TestCase(0f, -0.1f, 0f, "speedLossPerKg")]
        [TestCase(0f, float.NaN, 0f, "speedLossPerKg")]
        [TestCase(0f, 0f, -0.1f, "minWeightMultiplier")]
        [TestCase(0f, 0f, 1.1f, "minWeightMultiplier")]
        public void RejectsOutOfRangeNumbers(float free, float loss, float min, string param)
        {
            var e = Assert.Throws<ArgumentOutOfRangeException>(
                () => new CarryLoadSpec(CarrySource.Held, CarrySource.Held, free, loss, min, CarryGaits.All));

            Assert.That(e.ParamName, Is.EqualTo(param));
        }

        [Test]
        public void RejectsUndefinedSourcesAndGaits()
        {
            Assert.That(
                Assert.Throws<ArgumentOutOfRangeException>(
                    () => new CarryLoadSpec((CarrySource)9, CarrySource.Held, 0f, 0f, 0f, CarryGaits.All)).ParamName,
                Is.EqualTo("weightSource"));
            Assert.That(
                Assert.Throws<ArgumentOutOfRangeException>(
                    () => new CarryLoadSpec(CarrySource.Held, (CarrySource)9, 0f, 0f, 0f, CarryGaits.All)).ParamName,
                Is.EqualTo("multiplierSource"));
            Assert.That(
                Assert.Throws<ArgumentOutOfRangeException>(
                    () => new CarryLoadSpec(CarrySource.Held, CarrySource.Held, 0f, 0f, 0f, (CarryGaits)16)).ParamName,
                Is.EqualTo("gaits"));
        }

        [Test]
        public void WeaponSpecDefaultsToWeightlessAndUnscaled()
        {
            var spec = new WeaponSpec("6c1f3b2a-8d4e-4f7a-9b0c-2e5d7a1f4c83", null, WeaponCategory.AssaultRifle);

            Assert.That(spec.Weight, Is.EqualTo(0f));
            Assert.That(spec.MoveSpeedMultiplier, Is.EqualTo(1f));
        }

        [Test]
        public void WeaponSpecKeepsWeightWhenTheFireIntervalChanges()
        {
            var changed = _rifle.WithFireInterval(0.5f);

            Assert.That(changed.Weight, Is.EqualTo(4f));
            Assert.That(changed.MoveSpeedMultiplier, Is.EqualTo(0.9f));
        }

        [TestCase(-1f, 1f, "weight")]
        [TestCase(float.NaN, 1f, "weight")]
        [TestCase(1f, -0.5f, "moveSpeedMultiplier")]
        [TestCase(1f, float.PositiveInfinity, "moveSpeedMultiplier")]
        public void WeaponSpecRejectsOutOfRangeCarryValues(float weight, float multiplier, string param)
        {
            var e = Assert.Throws<ArgumentOutOfRangeException>(
                () => Weapon("6c1f3b2a-8d4e-4f7a-9b0c-2e5d7a1f4c83", weight, multiplier));

            Assert.That(e.ParamName, Is.EqualTo(param));
        }
    }
}
