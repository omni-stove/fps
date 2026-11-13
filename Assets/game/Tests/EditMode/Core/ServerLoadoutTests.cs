using System;
using NUnit.Framework;

namespace Fps.Core.Tests
{
    // Unity の EditMode テストと dotnet/Fps.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class ServerLoadoutTests
    {
        private const string _rifle = "00000000-0000-4000-8000-000000000001";
        private const string _handgun = "00000000-0000-4000-8000-000000000002";

        // ロードアウトに無い Id
        private const string _shotgun = "00000000-0000-4000-8000-000000000003";

        private static WeaponSpec Spec(string id, WeaponCategory category, float fireInterval, int damage)
        {
            return new WeaponSpec(id, null, category, FireMode.Auto, 30, 90, fireInterval, 2f, damage, 100f, DamageFalloff.None);
        }

        private static WeaponSpec Rifle()
        {
            return Spec(_rifle, WeaponCategory.AssaultRifle, 1f, 25);
        }

        private static WeaponSpec Handgun()
        {
            return Spec(_handgun, WeaponCategory.Handgun, 2f, 34);
        }

        private static LoadoutRule Rule()
        {
            return new LoadoutRule(new[]
            {
                new[] { WeaponCategory.AssaultRifle },
                new[] { WeaponCategory.Handgun },
            });
        }

        private static ServerLoadout Loadout(int burst = 1)
        {
            return new ServerLoadout(Rule(), new[] { Rifle(), Handgun() }, 0.5f, burst);
        }

        [Test]
        public void RejectsInvalidArguments()
        {
            var weapons = new[] { Rifle(), Handgun() };
            Assert.Throws<ArgumentNullException>(() => new ServerLoadout(null, weapons, 0.5f));
            Assert.Throws<ArgumentNullException>(() => new ServerLoadout(Rule(), null, 0.5f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ServerLoadout(Rule(), weapons, -1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ServerLoadout(Rule(), weapons, float.NaN));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ServerLoadout(Rule(), weapons, float.PositiveInfinity));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ServerLoadout(Rule(), weapons, 0.5f, 0));
        }

        [Test]
        public void RejectsWeaponsThatDoNotFitTheRule()
        {
            Assert.Throws<ArgumentException>(() => new ServerLoadout(Rule(), new[] { Rifle() }, 0.5f));
            Assert.Throws<ArgumentException>(() => new ServerLoadout(Rule(), new[] { Handgun(), Rifle() }, 0.5f));
            Assert.Throws<ArgumentException>(() => new ServerLoadout(Rule(), new[] { Rifle(), null }, 0.5f));
        }

        [Test]
        public void KeepsTheWeaponsInSlotOrder()
        {
            var weapons = new[] { Rifle(), Handgun() };
            var loadout = new ServerLoadout(Rule(), weapons, 0.5f);

            // 渡した配列を後から書き換えても変わらない
            weapons[0] = Handgun();

            Assert.That(loadout.Weapons.Count, Is.EqualTo(2));
            Assert.That(loadout.Weapons[0].Id, Is.EqualTo(_rifle));
            Assert.That(loadout.Weapons[1].Id, Is.EqualTo(_handgun));
            Assert.That(loadout.SwitchSeconds, Is.EqualTo(0.5f));
            Assert.That(loadout.Rule.SlotCount, Is.EqualTo(2));
            Assert.That(loadout.Contains(_rifle), Is.True);
            Assert.That(loadout.Contains(_shotgun), Is.False);
            Assert.That(loadout.Contains(null), Is.False);
            Assert.That(loadout.PlayerCount, Is.Zero);
        }

        [Test]
        public void ChecksTheWeaponIdOfAShot()
        {
            var loadout = Loadout();
            var rifle = loadout.Weapons[0];

            Assert.That(loadout.CheckWeapon(rifle, _rifle), Is.EqualTo(ShotRejection.None));
            Assert.That(loadout.CheckWeapon(rifle, _handgun), Is.EqualTo(ShotRejection.WeaponMismatch));
            Assert.That(loadout.CheckWeapon(rifle, _shotgun), Is.EqualTo(ShotRejection.UnknownWeapon));
            Assert.That(loadout.CheckWeapon(rifle, null), Is.EqualTo(ShotRejection.UnknownWeapon));
            Assert.That(loadout.CheckWeapon(rifle, _rifle.ToUpperInvariant()), Is.EqualTo(ShotRejection.UnknownWeapon));
            Assert.That(loadout.CheckWeapon(null, _rifle), Is.EqualTo(ShotRejection.NoWeapon));
        }

        [Test]
        public void LimitsTheFireRateWithTheHeldWeapon()
        {
            var loadout = Loadout();
            var handgun = loadout.Weapons[1];

            Assert.That(loadout.TryFire(1, handgun, _handgun, 0d), Is.EqualTo(ShotRejection.None));
            Assert.That(loadout.TryFire(1, handgun, _handgun, 1d), Is.EqualTo(ShotRejection.FireRateExceeded));
            Assert.That(loadout.TryFire(1, handgun, _handgun, 2d), Is.EqualTo(ShotRejection.None));
        }

        [Test]
        public void RebuildsTheBudgetWithTheIntervalOfTheNewWeaponAfterASwitch()
        {
            var loadout = Loadout();
            var rifle = loadout.Weapons[0];
            var handgun = loadout.Weapons[1];

            Assert.That(loadout.TryFire(1, rifle, _rifle, 0d), Is.EqualTo(ShotRejection.None));
            Assert.That(loadout.TryGetBudget(1, out var budget), Is.True);
            Assert.That(budget.FireInterval, Is.EqualTo(1f));

            // 持ち替えた武器は満タンの制限から撃てるが、以後は handgun の間隔 (2 秒) で回復する
            Assert.That(loadout.TryFire(1, handgun, _handgun, 0.5d), Is.EqualTo(ShotRejection.None));
            Assert.That(loadout.TryGetBudget(1, out budget), Is.True);
            Assert.That(budget.FireInterval, Is.EqualTo(2f));
            Assert.That(loadout.TryFire(1, handgun, _handgun, 1.5d), Is.EqualTo(ShotRejection.FireRateExceeded));
            Assert.That(loadout.TryFire(1, handgun, _handgun, 2.5d), Is.EqualTo(ShotRejection.None));
        }

        [Test]
        public void WrongWeaponShotsDoNotUseTheBudget()
        {
            var loadout = Loadout();
            var rifle = loadout.Weapons[0];

            Assert.That(loadout.TryFire(1, rifle, _handgun, 0d), Is.EqualTo(ShotRejection.WeaponMismatch));
            Assert.That(loadout.TryFire(1, rifle, _shotgun, 0d), Is.EqualTo(ShotRejection.UnknownWeapon));
            Assert.That(loadout.TryFire(1, null, _rifle, 0d), Is.EqualTo(ShotRejection.NoWeapon));
            Assert.That(loadout.PlayerCount, Is.Zero);
            Assert.That(loadout.TryFire(1, rifle, _rifle, 0d), Is.EqualTo(ShotRejection.None));
        }

        [Test]
        public void EachPlayerHasItsOwnBudget()
        {
            var loadout = Loadout();
            var rifle = loadout.Weapons[0];

            Assert.That(loadout.TryFire(1, rifle, _rifle, 0d), Is.EqualTo(ShotRejection.None));
            Assert.That(loadout.TryFire(2, rifle, _rifle, 0d), Is.EqualTo(ShotRejection.None));
            Assert.That(loadout.TryFire(1, rifle, _rifle, 0d), Is.EqualTo(ShotRejection.FireRateExceeded));
            Assert.That(loadout.PlayerCount, Is.EqualTo(2));
        }

        [Test]
        public void ResetBudgetsRefillsEveryone()
        {
            var loadout = Loadout();
            var handgun = loadout.Weapons[1];
            Assert.That(loadout.TryFire(1, handgun, _handgun, 0d), Is.EqualTo(ShotRejection.None));

            loadout.ResetBudgets();

            Assert.That(loadout.PlayerCount, Is.Zero);
            Assert.That(loadout.TryFire(1, handgun, _handgun, 0d), Is.EqualTo(ShotRejection.None));
        }

        [Test]
        public void RemoveTakesTheBudgetAway()
        {
            var loadout = Loadout();
            var rifle = loadout.Weapons[0];
            Assert.That(loadout.TryFire(1, rifle, _rifle, 0d), Is.EqualTo(ShotRejection.None));

            Assert.That(loadout.Remove(1), Is.True);
            Assert.That(loadout.Remove(1), Is.False);
            Assert.That(loadout.TryGetBudget(1, out var budget), Is.False);
            Assert.That(budget, Is.Null);
        }
    }
}
