using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace Shooter.Core.Tests
{
    // Unity の EditMode テストと dotnet/Shooter.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class EconomyTests
    {
        private const string _rifleId = "rifle";
        private const string _sniperId = "sniper";
        private const string _pistolId = "pistol";
        private const string _fragId = "frag";
        private const string _armorId = "armor";

        private const int _alpha0 = 0;
        private const int _alpha1 = 1;
        private const int _bravo0 = 2;

        private static readonly int[] _walletEvents = { _alpha0, _alpha0 };
        private static readonly BuyItem[] _noItem = { null };
        private static readonly int[] _alphaWallets = { _alpha0, _alpha1 };

        [Test]
        public void EveryWalletStartsAtTheStartMoneyAndCanNotBuyBeforeTheBuyTime()
        {
            var economy = NewEconomy();

            Assert.That(economy.MoneyOf(_alpha0), Is.EqualTo(800));
            Assert.That(economy.MoneyOf(_bravo0), Is.EqualTo(800));
            Assert.That(economy.IsBuyTime, Is.False);
            Assert.That(economy.StatusOf(_alpha0), Is.EqualTo(new WalletStatus(800, 16000, false)));
            Assert.That(economy.SideOf(_bravo0), Is.EqualTo(Side.Bravo));
            Assert.That(economy.LossStreakOf(Side.Alpha), Is.Zero);
            Assert.That(economy.HasPlayer(9), Is.False);
            Assert.Throws<ArgumentOutOfRangeException>(() => economy.MoneyOf(9));
            Assert.Throws<ArgumentException>(() => economy.AddPlayer(_alpha0, Side.Alpha));
        }

        [Test]
        public void ALeaverLosesTheWalletButTheSideKeepsItsLossStreak()
        {
            var economy = NewEconomy();
            economy.EndRound(Side.Alpha);

            Assert.That(economy.RemovePlayer(_bravo0), Is.True);

            Assert.That(economy.HasPlayer(_bravo0), Is.False);
            Assert.That(economy.PlayerIds, Is.EquivalentTo(_alphaWallets));
            Assert.That(economy.LossStreakOf(Side.Bravo), Is.EqualTo(1));
            Assert.That(economy.RemovePlayer(_bravo0), Is.False, "a wallet is dropped only once");
            Assert.That(economy.RewardKill(_alpha0, _bravo0), Is.Zero, "nobody is paid for someone who left");

            economy.AddPlayer(_bravo0, Side.Bravo);
            Assert.That(economy.MoneyOf(_bravo0), Is.EqualTo(800), "joining again starts a new wallet");
        }

        [Test]
        public void AKillPaysTheKillerOnlyForAnEnemy()
        {
            var economy = NewEconomy();

            Assert.That(economy.RewardKill(_alpha0, _bravo0), Is.EqualTo(300));
            Assert.That(economy.RewardKill(_alpha0, _alpha1), Is.Zero, "a team kill pays nothing");
            Assert.That(economy.RewardKill(_alpha0, _alpha0), Is.Zero, "dying by your own hand pays nothing");
            Assert.That(economy.RewardKill(9, _alpha0), Is.Zero, "someone without a wallet is paid nothing");

            Assert.That(economy.MoneyOf(_alpha0), Is.EqualTo(1100));
            Assert.That(economy.MoneyOf(_alpha1), Is.EqualTo(800));
            Assert.That(economy.MoneyOf(_bravo0), Is.EqualTo(800));
        }

        [Test]
        public void MoneyStopsAtTheMax()
        {
            var economy = new Economy(new EconomyRules(new EconomySpec(startMoney: 900, maxMoney: 1000, killReward: 300), Catalog()));
            economy.AddPlayer(_alpha0, Side.Alpha);
            economy.AddPlayer(_bravo0, Side.Bravo);

            Assert.That(economy.RewardKill(_alpha0, _bravo0), Is.EqualTo(100), "only what fits under the max is paid");
            Assert.That(economy.RewardKill(_alpha0, _bravo0), Is.Zero);
            Assert.That(economy.MoneyOf(_alpha0), Is.EqualTo(1000));
            Assert.That(economy.StatusOf(_alpha0).IsFull, Is.True);

            economy.EndRound(Side.Bravo);
            Assert.That(economy.MoneyOf(_alpha0), Is.EqualTo(1000));
            Assert.That(economy.MoneyOf(_bravo0), Is.EqualTo(1000));
        }

        [Test]
        public void TheWinnersGetTheWinRewardAndTheLosersMoreForEachLossInARow()
        {
            var economy = NewEconomy();

            economy.EndRound(Side.Alpha);
            Assert.That(economy.MoneyOf(_alpha0), Is.EqualTo(800 + 3000));
            Assert.That(economy.MoneyOf(_alpha1), Is.EqualTo(800 + 3000));
            Assert.That(economy.MoneyOf(_bravo0), Is.EqualTo(800 + 1400));
            Assert.That(economy.LossStreakOf(Side.Bravo), Is.EqualTo(1));

            economy.EndRound(Side.Alpha);
            Assert.That(economy.MoneyOf(_bravo0), Is.EqualTo(2200 + 1900));
            Assert.That(economy.LossStreakOf(Side.Bravo), Is.EqualTo(2));

            economy.EndRound(Side.Alpha);
            Assert.That(economy.MoneyOf(_bravo0), Is.EqualTo(4100 + 2400));

            economy.EndRound(Side.Bravo);
            Assert.That(economy.MoneyOf(_bravo0), Is.EqualTo(6500 + 3000));
            Assert.That(economy.LossStreakOf(Side.Bravo), Is.Zero, "a win ends the streak");
            Assert.That(economy.LossStreakOf(Side.Alpha), Is.EqualTo(1));
            Assert.That(economy.MoneyOf(_alpha0), Is.EqualTo(9800 + 1400));
        }

        [Test]
        public void TheLossRewardStopsAtTheMaxLossReward()
        {
            var economy = NewEconomy();
            for (var round = 0; round < 5; round++)
            {
                economy.EndRound(Side.Alpha);
            }

            Assert.That(economy.LossStreakOf(Side.Bravo), Is.EqualTo(5));
            Assert.That(economy.MoneyOf(_bravo0), Is.EqualTo(800 + 1400 + 1900 + 2400 + 2900 + 3400));

            economy.EndRound(Side.Alpha);

            Assert.That(economy.LossStreakOf(Side.Bravo), Is.EqualTo(6));
            Assert.That(economy.RoundRewardOf(Side.Bravo, Side.Alpha), Is.EqualTo(3400), "1400 + 5 x 500 is held at 3400");
            Assert.That(economy.MoneyOf(_bravo0), Is.EqualTo(16000), "12800 + 3400 is held at the max");
        }

        [Test]
        public void ADrawPaysTheBaseLossRewardAndKeepsTheStreaks()
        {
            var economy = NewEconomy();
            economy.EndRound(Side.Alpha);
            economy.EndRound(Side.Alpha);

            economy.EndRound(null);

            Assert.That(economy.LossStreakOf(Side.Bravo), Is.EqualTo(2));
            Assert.That(economy.MoneyOf(_bravo0), Is.EqualTo(800 + 1400 + 1900 + 1400));
            Assert.That(economy.MoneyOf(_alpha0), Is.EqualTo(800 + 3000 + 3000 + 1400));
        }

        [Test]
        public void NothingCanBeBoughtOutsideTheBuyTime()
        {
            var economy = NewEconomy();
            var asked = 0;

            Assert.That(economy.Buy(_alpha0, _pistolId, _ => { asked++; return true; }), Is.EqualTo(PurchaseVerdict.NotBuyTime));
            Assert.That(asked, Is.Zero, "the holder is not asked outside the buy time");

            economy.OpenBuyTime();
            economy.CloseBuyTime();

            Assert.That(economy.Buy(_alpha0, _pistolId), Is.EqualTo(PurchaseVerdict.NotBuyTime));
            Assert.That(economy.MoneyOf(_alpha0), Is.EqualTo(800));
            Assert.That(economy.WeaponIn(_alpha0, 1), Is.Null);
        }

        [Test]
        public void BuyingTakesThePriceAndKeepsTheWeaponInItsSlot()
        {
            var economy = NewEconomy();
            economy.OpenBuyTime();

            Assert.That(economy.Buy(_alpha0, _pistolId, null, out var item), Is.EqualTo(PurchaseVerdict.Bought));

            Assert.That(item.Id, Is.EqualTo(_pistolId));
            Assert.That(economy.MoneyOf(_alpha0), Is.EqualTo(300));
            Assert.That(economy.WeaponIn(_alpha0, 1), Is.EqualTo(_pistolId));
            Assert.That(economy.WeaponIn(_alpha0, 0), Is.Null);
            Assert.That(economy.StatusOf(_alpha0), Is.EqualTo(new WalletStatus(300, 16000, true)));
        }

        [Test]
        public void TheSameWeaponTwiceCanNotBeCarriedButAnotherReplacesItsSlot()
        {
            var economy = Rich();
            economy.OpenBuyTime();

            Assert.That(economy.Buy(_alpha0, _rifleId), Is.EqualTo(PurchaseVerdict.Bought));
            Assert.That(economy.Buy(_alpha0, _rifleId), Is.EqualTo(PurchaseVerdict.CannotCarry));
            Assert.That(economy.MoneyOf(_alpha0), Is.EqualTo(16000 - 2700));

            Assert.That(economy.Buy(_alpha0, _sniperId), Is.EqualTo(PurchaseVerdict.Bought));
            Assert.That(economy.WeaponIn(_alpha0, 0), Is.EqualTo(_sniperId));
            Assert.That(economy.MoneyOf(_alpha0), Is.EqualTo(16000 - 2700 - 4750), "the replaced weapon is not paid back");

            Assert.That(economy.Buy(_alpha0, _pistolId), Is.EqualTo(PurchaseVerdict.Bought));
            var weapons = economy.WeaponsOf(_alpha0);
            Assert.That(weapons.Count, Is.EqualTo(2));
            Assert.That(weapons[0], Is.EqualTo(new KeyValuePair<int, string>(0, _sniperId)));
            Assert.That(weapons[1], Is.EqualTo(new KeyValuePair<int, string>(1, _pistolId)));
        }

        [Test]
        public void TooLittleMoneyAnUnknownItemOrAFullHolderIsRefused()
        {
            var economy = NewEconomy();
            economy.OpenBuyTime();

            Assert.That(economy.Buy(_alpha0, _rifleId), Is.EqualTo(PurchaseVerdict.NotEnoughMoney));
            Assert.That(economy.Buy(_alpha0, "missing"), Is.EqualTo(PurchaseVerdict.UnknownItem));
            Assert.That(economy.Buy(_alpha0, null), Is.EqualTo(PurchaseVerdict.UnknownItem));
            Assert.That(economy.Buy(_alpha0, _fragId, i => i.Kind != BuyItemKind.Throwable), Is.EqualTo(PurchaseVerdict.CannotCarry));
            Assert.That(economy.Buy(_alpha0, _armorId, _ => false), Is.EqualTo(PurchaseVerdict.CannotCarry));
            Assert.That(economy.MoneyOf(_alpha0), Is.EqualTo(800));

            Assert.That(economy.Buy(_alpha0, _fragId), Is.EqualTo(PurchaseVerdict.Bought));
            Assert.That(economy.MoneyOf(_alpha0), Is.EqualTo(500));
            Assert.That(economy.WeaponsOf(_alpha0), Is.Empty, "a throwable is not kept as a weapon");
            Assert.Throws<ArgumentOutOfRangeException>(() => economy.Buy(9, _fragId));
        }

        [Test]
        public void MoneyAndWeaponsCarryOverToTheNextRound()
        {
            var economy = NewEconomy();
            economy.OpenBuyTime();
            economy.Buy(_alpha0, _pistolId);
            economy.CloseBuyTime();
            economy.RewardKill(_alpha0, _bravo0);

            economy.EndRound(Side.Alpha);
            economy.OpenBuyTime();

            Assert.That(economy.MoneyOf(_alpha0), Is.EqualTo(800 - 500 + 300 + 3000));
            Assert.That(economy.WeaponIn(_alpha0, 1), Is.EqualTo(_pistolId));
            Assert.That(economy.Buy(_alpha0, _pistolId), Is.EqualTo(PurchaseVerdict.CannotCarry), "the surviving pistol is still held");
        }

        [Test]
        public void ADeathLosesTheBoughtWeaponsButNotTheMoney()
        {
            var economy = NewEconomy();
            economy.OpenBuyTime();
            economy.Buy(_alpha0, _pistolId);

            Assert.That(economy.LoseWeapons(_alpha0), Is.True);
            Assert.That(economy.LoseWeapons(_alpha0), Is.False, "nothing is left to lose");
            Assert.That(economy.WeaponIn(_alpha0, 1), Is.Null);
            Assert.That(economy.MoneyOf(_alpha0), Is.EqualTo(300));
        }

        [Test]
        public void SwappingSidesStartsTheWalletsAgainWhenTheRulesSaySo()
        {
            var reset = NewEconomy();
            reset.OpenBuyTime();
            reset.Buy(_alpha0, _pistolId);
            reset.EndRound(Side.Alpha);

            reset.SwapSides();

            Assert.That(reset.MoneyOf(_alpha0), Is.EqualTo(800));
            Assert.That(reset.MoneyOf(_bravo0), Is.EqualTo(800));
            Assert.That(reset.LossStreakOf(Side.Bravo), Is.Zero);
            Assert.That(reset.WeaponIn(_alpha0, 1), Is.Null);
            Assert.That(reset.IsBuyTime, Is.True, "swapping does not close the buy time");

            var kept = new Economy(new EconomyRules(EconomySpec.Default, Catalog(), resetOnSwap: false));
            kept.AddPlayer(_alpha0, Side.Alpha);
            kept.AddPlayer(_bravo0, Side.Bravo);
            kept.EndRound(Side.Alpha);

            kept.SwapSides();

            Assert.That(kept.MoneyOf(_alpha0), Is.EqualTo(3800));
            Assert.That(kept.LossStreakOf(Side.Bravo), Is.EqualTo(1));
        }

        [Test]
        public void ResetClosesTheBuyTimeAndStartsOver()
        {
            var economy = NewEconomy();
            economy.OpenBuyTime();
            economy.Buy(_alpha0, _pistolId);
            economy.EndRound(Side.Bravo);

            economy.Reset();

            Assert.That(economy.IsBuyTime, Is.False);
            Assert.That(economy.MoneyOf(_alpha0), Is.EqualTo(800));
            Assert.That(economy.LossStreakOf(Side.Alpha), Is.Zero);
            Assert.That(economy.WeaponsOf(_alpha0), Is.Empty);
        }

        [Test]
        public void TheEventsTellWhoseWalletAndTheBuyTimeChanged()
        {
            var economy = NewEconomy();
            var wallets = new List<int>();
            var buyTimes = new List<bool>();
            economy.WalletChanged += wallets.Add;
            economy.BuyTimeChanged += buyTimes.Add;

            economy.OpenBuyTime();
            economy.OpenBuyTime();
            economy.Buy(_alpha0, _pistolId);
            economy.Buy(_alpha0, _rifleId);
            economy.RewardKill(_alpha0, _bravo0);
            economy.CloseBuyTime();

            Assert.That(wallets, Is.EqualTo(_walletEvents), "a refused purchase changes nothing");
            Assert.That(buyTimes, Is.EqualTo(new List<bool> { true, false }));
        }

        [Test]
        public void TheCatalogAndItsItemsRejectBadValues()
        {
            Assert.Throws<ArgumentException>(() => new BuyItem(" ", BuyItemKind.Weapon, 100));
            Assert.Throws<ArgumentOutOfRangeException>(() => new BuyItem("x", BuyItemKind.Weapon, -1));
            Assert.Throws<ArgumentOutOfRangeException>(() => new BuyItem("x", BuyItemKind.Weapon, 100, slot: -1));
            Assert.Throws<ArgumentOutOfRangeException>(() => new BuyItem("x", BuyItemKind.Throwable, 100, slot: 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => new BuyItem("x", BuyItemKind.Armor, 100));
            Assert.Throws<ArgumentOutOfRangeException>(() => new BuyItem("x", BuyItemKind.Weapon, 100, amount: 50));
            Assert.Throws<ArgumentOutOfRangeException>(() => new BuyItem("x", (BuyItemKind)9, 100));
            Assert.Throws<ArgumentException>(() => new BuyCatalog(new[] { new BuyItem("x", BuyItemKind.Weapon, 1), new BuyItem("x", BuyItemKind.Weapon, 2) }));
            Assert.Throws<ArgumentException>(() => new BuyCatalog(_noItem));
            Assert.Throws<ArgumentNullException>(() => new Economy(null));

            var armor = new BuyItem(" armor ", BuyItemKind.Armor, 650, amount: 100, nameKey: " buy.armor ");
            Assert.That(armor.Id, Is.EqualTo("armor"));
            Assert.That(armor.NameKey, Is.EqualTo("buy.armor"));
            Assert.That(armor.Amount, Is.EqualTo(100));
            Assert.That(BuyCatalog.Empty.Count, Is.Zero);
            Assert.That(EconomyRules.Default.Catalog, Is.SameAs(BuyCatalog.Empty));
            Assert.That(EconomyRules.Default.ResetOnSwap, Is.True);
        }

        private static Economy NewEconomy()
        {
            var economy = new Economy(new EconomyRules(EconomySpec.Default, Catalog()));
            economy.AddPlayer(_alpha0, Side.Alpha);
            economy.AddPlayer(_alpha1, Side.Alpha);
            economy.AddPlayer(_bravo0, Side.Bravo);
            return economy;
        }

        private static Economy Rich()
        {
            var economy = new Economy(new EconomyRules(new EconomySpec(startMoney: 16000), Catalog()));
            economy.AddPlayer(_alpha0, Side.Alpha);
            economy.AddPlayer(_bravo0, Side.Bravo);
            return economy;
        }

        private static BuyCatalog Catalog()
        {
            return new BuyCatalog(new[]
            {
                new BuyItem(_pistolId, BuyItemKind.Weapon, 500, slot: 1),
                new BuyItem(_rifleId, BuyItemKind.Weapon, 2700, slot: 0),
                new BuyItem(_sniperId, BuyItemKind.Weapon, 4750, slot: 0),
                new BuyItem(_fragId, BuyItemKind.Throwable, 300),
                new BuyItem(_armorId, BuyItemKind.Armor, 650, amount: 100),
            });
        }
    }
}
