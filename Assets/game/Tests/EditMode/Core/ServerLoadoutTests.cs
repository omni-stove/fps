using System;
using NUnit.Framework;

namespace Fps.Core.Tests
{
    // Unity の EditMode テストと dotnet/Fps.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class ServerLoadoutTests
    {
        // 大文字にすると別の文字列になるよう、16 進の英字を含める
        private const string _rifle = "00000000-0000-4000-8000-00000000000a";
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

        // 一覧にだけある 2 本目のメイン武器
        private const string _smg = "00000000-0000-4000-8000-000000000004";

        private static WeaponSpec Smg()
        {
            return Spec(_smg, WeaponCategory.SubmachineGun, 0.5f, 20);
        }

        /// <summary>メインに AssaultRifle か SubmachineGun、サブに Handgun を入れるルール。</summary>
        private static LoadoutRule ChoiceRule()
        {
            return new LoadoutRule(new[]
            {
                new[] { WeaponCategory.AssaultRifle, WeaponCategory.SubmachineGun },
                new[] { WeaponCategory.Handgun },
            });
        }

        /// <summary>既定は rifle と handgun、一覧は rifle・handgun・smg から選べるロードアウト。</summary>
        private static ServerLoadout ChoiceLoadout()
        {
            var catalog = new WeaponCatalog(new[] { Rifle(), Handgun(), Smg() });
            return new ServerLoadout(ChoiceRule(), new[] { Rifle(), Handgun() }, catalog, 0.5f);
        }

        [Test]
        public void AcceptsAChoiceThatFitsTheRuleAndUsesItForThatPlayerOnly()
        {
            var loadout = ChoiceLoadout();

            Assert.That(loadout.TryChoose(1, new[] { _smg, _handgun }, out var reason), Is.True, reason);
            Assert.That(reason, Is.Null);
            Assert.That(loadout.HasChoice(1), Is.True);
            Assert.That(loadout.ChoiceCount, Is.EqualTo(1));
            Assert.That(loadout.WeaponsFor(1)[0].Id, Is.EqualTo(_smg));
            Assert.That(loadout.WeaponsFor(1)[1].Id, Is.EqualTo(_handgun));

            // 選んでいない人は既定の並びのまま
            Assert.That(loadout.HasChoice(2), Is.False);
            Assert.That(loadout.WeaponsFor(2), Is.SameAs(loadout.Weapons));
        }

        [Test]
        public void RejectsTwoHandgunsBecauseTheMainSlotDoesNotAcceptAHandgun()
        {
            var loadout = ChoiceLoadout();

            Assert.That(loadout.TryChoose(1, new[] { _handgun, _handgun }, out var reason), Is.False);
            Assert.That(reason, Does.Contain("slot 0 does not accept Handgun"));
            Assert.That(loadout.HasChoice(1), Is.False);
            Assert.That(loadout.WeaponsFor(1), Is.SameAs(loadout.Weapons));
        }

        [Test]
        public void RejectsTheSameWeaponInTwoSlots()
        {
            var rule = new LoadoutRule(new[]
            {
                new[] { WeaponCategory.AssaultRifle, WeaponCategory.SubmachineGun },
                new[] { WeaponCategory.AssaultRifle, WeaponCategory.SubmachineGun, WeaponCategory.Handgun },
            });
            var catalog = new WeaponCatalog(new[] { Rifle(), Handgun(), Smg() });
            var loadout = new ServerLoadout(rule, new[] { Rifle(), Handgun() }, catalog, 0.5f);

            Assert.That(loadout.TryChoose(1, new[] { _smg, _smg }, out var reason), Is.False);
            Assert.That(reason, Does.Contain("already in slot 0"));
        }

        [Test]
        public void RejectsTheWrongNumberOfWeapons()
        {
            var loadout = ChoiceLoadout();

            Assert.That(loadout.TryChoose(1, new[] { _rifle }, out var tooFew), Is.False);
            Assert.That(tooFew, Does.Contain("expected 2"));
            Assert.That(loadout.TryChoose(1, new[] { _rifle, _handgun, _smg }, out var tooMany), Is.False);
            Assert.That(tooMany, Does.Contain("expected 2"));
            Assert.That(loadout.TryChoose(1, new string[0], out var none), Is.False);
            Assert.That(none, Does.Contain("expected 2"));
            Assert.That(loadout.TryChoose(1, null, out var missing), Is.False);
            Assert.That(missing, Is.Not.Null);
        }

        [Test]
        public void RejectsIdsMissingFromTheCatalogWithoutEchoingThem()
        {
            var loadout = ChoiceLoadout();

            // 一覧に無い・大文字にした・空の Id
            Assert.That(loadout.TryChoose(1, new[] { _shotgun, _handgun }, out var unknown), Is.False);
            Assert.That(unknown, Does.Contain("slot 0").And.Contain("not in the catalog"));
            Assert.That(unknown, Does.Not.Contain(_shotgun));
            Assert.That(loadout.TryChoose(1, new[] { _rifle.ToUpperInvariant(), _handgun }, out var upper), Is.False);
            Assert.That(upper, Does.Contain("not in the catalog"));
            Assert.That(loadout.TryChoose(1, new[] { _rifle, string.Empty }, out var empty), Is.False);
            Assert.That(empty, Does.Contain("slot 1 has no weapon id"));
            Assert.That(loadout.TryChoose(1, new[] { _rifle, null }, out var nullId), Is.False);
            Assert.That(nullId, Does.Contain("slot 1 has no weapon id"));
        }

        [Test]
        public void RejectsEveryChoiceWithoutACatalog()
        {
            var loadout = Loadout();

            Assert.That(loadout.Catalog, Is.Null);
            Assert.That(loadout.TryChoose(1, new[] { _rifle, _handgun }, out var reason), Is.False);
            Assert.That(reason, Does.Contain("no weapon catalog"));
            Assert.That(loadout.WeaponsFor(1), Is.SameAs(loadout.Weapons));
        }

        [Test]
        public void ARejectedChoiceKeepsTheEarlierChoice()
        {
            var loadout = ChoiceLoadout();
            Assert.That(loadout.TryChoose(1, new[] { _smg, _handgun }, out var reason), Is.True, reason);

            Assert.That(loadout.TryChoose(1, new[] { _handgun, _handgun }, out _), Is.False);

            Assert.That(loadout.WeaponsFor(1)[0].Id, Is.EqualTo(_smg));
        }

        [Test]
        public void ALaterChoiceReplacesTheEarlierOne()
        {
            var loadout = ChoiceLoadout();
            Assert.That(loadout.TryChoose(1, new[] { _smg, _handgun }, out var first), Is.True, first);

            Assert.That(loadout.TryChoose(1, new[] { _rifle, _handgun }, out var second), Is.True, second);

            Assert.That(loadout.WeaponsFor(1)[0].Id, Is.EqualTo(_rifle));
            Assert.That(loadout.ChoiceCount, Is.EqualTo(1));
        }

        [Test]
        public void TheChoiceSurvivesTheNextMatchButNotLeaving()
        {
            var loadout = ChoiceLoadout();
            Assert.That(loadout.TryChoose(1, new[] { _smg, _handgun }, out var reason), Is.True, reason);

            loadout.ResetBudgets();
            Assert.That(loadout.WeaponsFor(1)[0].Id, Is.EqualTo(_smg), "a new match keeps the choice");

            Assert.That(loadout.Remove(1), Is.True, "the choice alone counts as something to remove");
            Assert.That(loadout.HasChoice(1), Is.False);
            Assert.That(loadout.WeaponsFor(1), Is.SameAs(loadout.Weapons));
        }

        [Test]
        public void ShotsWithAChosenWeaponAreKnown()
        {
            var loadout = ChoiceLoadout();
            Assert.That(loadout.TryChoose(1, new[] { _smg, _handgun }, out var reason), Is.True, reason);
            var smg = loadout.WeaponsFor(1)[0];

            Assert.That(loadout.Contains(_smg), Is.True);
            Assert.That(loadout.TryFire(1, smg, _smg, 0d), Is.EqualTo(ShotRejection.None));
            Assert.That(loadout.TryFire(1, smg, _shotgun, 1d), Is.EqualTo(ShotRejection.UnknownWeapon));
        }
    }
}
