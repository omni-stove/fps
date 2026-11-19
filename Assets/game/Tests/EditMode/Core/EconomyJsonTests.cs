using System;
using NUnit.Framework;

namespace Shooter.Core.Tests
{
    // Unity の EditMode テストと dotnet/Shooter.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class EconomyJsonTests
    {
        private const string _source = "economy.json";

        private static readonly string[] _expectedItemNames =
        {
            "startMoney", "maxMoney", "killReward", "roundWinReward", "roundLossReward", "lossStreakBonus", "maxLossReward", "resetOnSwap", "items",
        };

        private static readonly string[] _expectedBuyItemNames = { "id", "kind", "price", "slot", "amount", "nameKey" };

        [Test]
        public void ReadsEveryItem()
        {
            var rules = EconomyJson.Parse(
                "{\"startMoney\": 1000, \"maxMoney\": 9000, \"killReward\": 250, \"roundWinReward\": 3250, \"roundLossReward\": 1900,"
                + " \"lossStreakBonus\": 400, \"maxLossReward\": 2900, \"resetOnSwap\": false, \"items\": ["
                + "{\"id\": \"rifle\", \"kind\": \"Weapon\", \"price\": 2700, \"slot\": 0, \"nameKey\": \"weapon.rifle\"},"
                + " {\"id\": \"pistol\", \"kind\": \"Weapon\", \"price\": 500, \"slot\": 1},"
                + " {\"id\": \"frag\", \"kind\": \"Throwable\", \"price\": 300},"
                + " {\"id\": \"vest\", \"kind\": \"Armor\", \"price\": 650, \"amount\": 100}]}",
                _source);

            var spec = rules.Spec;
            Assert.That(spec.StartMoney, Is.EqualTo(1000));
            Assert.That(spec.MaxMoney, Is.EqualTo(9000));
            Assert.That(spec.KillReward, Is.EqualTo(250));
            Assert.That(spec.RoundWinReward, Is.EqualTo(3250));
            Assert.That(spec.RoundLossReward, Is.EqualTo(1900));
            Assert.That(spec.LossStreakBonus, Is.EqualTo(400));
            Assert.That(spec.MaxLossReward, Is.EqualTo(2900));
            Assert.That(rules.ResetOnSwap, Is.False);

            Assert.That(rules.Catalog.Count, Is.EqualTo(4));
            Assert.That(rules.Catalog.TryFind("rifle", out var rifle), Is.True);
            Assert.That(rifle.Kind, Is.EqualTo(BuyItemKind.Weapon));
            Assert.That(rifle.Price, Is.EqualTo(2700));
            Assert.That(rifle.Slot, Is.Zero);
            Assert.That(rifle.NameKey, Is.EqualTo("weapon.rifle"));
            Assert.That(rules.Catalog.Items[1].Slot, Is.EqualTo(1));
            Assert.That(rules.Catalog.Items[1].NameKey, Is.EqualTo(string.Empty));
            Assert.That(rules.Catalog.Items[2].Kind, Is.EqualTo(BuyItemKind.Throwable));
            Assert.That(rules.Catalog.Items[3].Kind, Is.EqualTo(BuyItemKind.Armor));
            Assert.That(rules.Catalog.Items[3].Amount, Is.EqualTo(100));
        }

        [Test]
        public void LeftOutItemsUseTheDefaults()
        {
            var empty = EconomyJson.Parse("{}", _source);
            var noItems = EconomyJson.Parse("{\"items\": []}", _source);

            Assert.That(empty.Spec.StartMoney, Is.EqualTo(EconomySpec.DefaultStartMoney));
            Assert.That(empty.Spec.MaxMoney, Is.EqualTo(EconomySpec.DefaultMaxMoney));
            Assert.That(empty.Spec.KillReward, Is.EqualTo(EconomySpec.DefaultKillReward));
            Assert.That(empty.Spec.RoundWinReward, Is.EqualTo(EconomySpec.DefaultRoundWinReward));
            Assert.That(empty.Spec.RoundLossReward, Is.EqualTo(EconomySpec.DefaultRoundLossReward));
            Assert.That(empty.Spec.LossStreakBonus, Is.EqualTo(EconomySpec.DefaultLossStreakBonus));
            Assert.That(empty.Spec.MaxLossReward, Is.EqualTo(EconomySpec.DefaultMaxLossReward));
            Assert.That(empty.ResetOnSwap, Is.EqualTo(EconomyRules.DefaultResetOnSwap));
            Assert.That(empty.Catalog, Is.SameAs(BuyCatalog.Empty));
            Assert.That(noItems.Catalog.Count, Is.Zero);
        }

        [Test]
        public void TheItemNamesMatchTheNetMatchFile()
        {
            Assert.That(EconomyJson.ItemNames, Is.EqualTo(_expectedItemNames));
            Assert.That(EconomyJson.BuyItemNames, Is.EqualTo(_expectedBuyItemNames));
            Assert.That(EconomyJson.StartMoneyItem, Is.EqualTo(NetMatchFile.StartMoneyItem));
            Assert.That(EconomyJson.MaxLossRewardItem, Is.EqualTo(NetMatchFile.MaxLossRewardItem));
            Assert.That(EconomyJson.ResourcePath, Is.EqualTo("Tuning/economy"));
        }

        [Test]
        public void AnOutOfRangeValueNamesItsItem()
        {
            var start = Assert.Throws<TuningJsonException>(() => EconomyJson.Parse("{\"startMoney\": 20000}", _source));
            var price = Assert.Throws<TuningJsonException>(
                () => EconomyJson.Parse("{\"items\": [{\"id\": \"a\", \"kind\": \"Weapon\", \"price\": -1}]}", _source));
            var slot = Assert.Throws<TuningJsonException>(
                () => EconomyJson.Parse("{\"items\": [{\"id\": \"a\", \"kind\": \"Throwable\", \"price\": 1, \"slot\": 2}]}", _source));
            var twice = Assert.Throws<TuningJsonException>(
                () => EconomyJson.Parse(
                    "{\"items\": [{\"id\": \"a\", \"kind\": \"Weapon\", \"price\": 1}, {\"id\": \"a\", \"kind\": \"Armor\", \"price\": 1, \"amount\": 5}]}",
                    _source));

            Assert.That(start.SourceName, Is.EqualTo(_source));
            Assert.That(start.Item, Is.EqualTo("startMoney"));
            Assert.That(start.InnerException, Is.InstanceOf<ArgumentOutOfRangeException>());
            Assert.That(price.Item, Is.EqualTo("items[0].price"));
            Assert.That(slot.Item, Is.EqualTo("items[0].slot"));
            Assert.That(twice.Item, Is.EqualTo("items"));
            Assert.That(twice.Message, Does.Contain("listed twice"));
        }

        [Test]
        public void AnUnknownOrMissingItemAWrongKindOrBrokenJsonIsRejected()
        {
            var unknown = Assert.Throws<TuningJsonException>(() => EconomyJson.Parse("{\"money\": 2}", _source));
            var unknownInItem = Assert.Throws<TuningJsonException>(
                () => EconomyJson.Parse("{\"items\": [{\"id\": \"a\", \"kind\": \"Weapon\", \"price\": 1, \"cost\": 2}]}", _source));
            var noPrice = Assert.Throws<TuningJsonException>(
                () => EconomyJson.Parse("{\"items\": [{\"id\": \"a\", \"kind\": \"Weapon\"}]}", _source));
            var noKind = Assert.Throws<TuningJsonException>(
                () => EconomyJson.Parse("{\"items\": [{\"id\": \"a\", \"price\": 1}]}", _source));
            var wrongKind = Assert.Throws<TuningJsonException>(
                () => EconomyJson.Parse("{\"items\": [{\"id\": \"a\", \"kind\": \"Boots\", \"price\": 1}]}", _source));
            var wrongType = Assert.Throws<TuningJsonException>(() => EconomyJson.Parse("{\"resetOnSwap\": 1}", _source));
            var broken = Assert.Throws<TuningJsonException>(() => EconomyJson.Parse("{\"items\": [", _source));

            Assert.That(unknown.Item, Is.EqualTo("money"));
            Assert.That(unknown.Message, Does.Contain("startMoney"), "lists the known items");
            Assert.That(unknownInItem.Item, Is.EqualTo("items[0].cost"));
            Assert.That(noPrice.Item, Is.EqualTo("items[0].price"));
            Assert.That(noKind.Item, Is.EqualTo("items[0].kind"));
            Assert.That(wrongKind.Item, Is.EqualTo("items[0].kind"));
            Assert.That(wrongKind.Message, Does.Contain("Throwable"), "lists the kinds");
            Assert.That(wrongType.Item, Is.EqualTo("resetOnSwap"));
            Assert.That(broken.Item, Is.Null);
            Assert.Throws<ArgumentNullException>(() => EconomyJson.Parse(null, _source));
        }
    }
}
