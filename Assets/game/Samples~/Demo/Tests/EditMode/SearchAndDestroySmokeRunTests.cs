using System;
using System.Linq;
using Shooter.Core;
using Shooter.Editor;
using NUnit.Framework;

namespace Shooter.Tests.EditMode
{
    // Play Mode に入る通しそのものは -executeMethod Shooter.Editor.SearchAndDestroySmokeRun.Run (または SmokeSuite.RunAll) で確かめる。
    public sealed class SearchAndDestroySmokeRunTests
    {
        private static readonly BuyItem _pistol = new BuyItem("pistol", BuyItemKind.Weapon, 500, slot: 1);
        private static readonly BuyItem _rifle = new BuyItem("rifle", BuyItemKind.Weapon, 2700, slot: 0);
        private static readonly BuyItem _frag = new BuyItem("frag", BuyItemKind.Throwable, 300);
        private static readonly BuyItem _smoke = new BuyItem("smoke", BuyItemKind.Throwable, 300);
        private static readonly BuyItem _vest = new BuyItem("vest", BuyItemKind.Armor, 650, amount: 50);

        [Test]
        public void ClassifiesTheEconomyAndRoundLines()
        {
            var rules = new EconomyRules(EconomySpec.Default, new BuyCatalog(new[] { _pistol }));

            Assert.That(SearchAndDestroySmokeRun.Classify(EconomyLog.Started(10, rules)), Is.EqualTo(SearchAndDestroySmokeRun.LineKind.EconomyStarted));
            Assert.That(SearchAndDestroySmokeRun.Classify(EconomyLog.Bought(0, _pistol, 300)), Is.EqualTo(SearchAndDestroySmokeRun.LineKind.Bought));
            Assert.That(
                SearchAndDestroySmokeRun.Classify(EconomyLog.RoundReward(3, Side.Bravo, Side.Alpha, 1400, 2200, 1)),
                Is.EqualTo(SearchAndDestroySmokeRun.LineKind.RoundReward));
            Assert.That(
                SearchAndDestroySmokeRun.Classify(EconomyLog.RoundReward(3, Side.Bravo, null, 1400, 2200, 1)),
                Is.EqualTo(SearchAndDestroySmokeRun.LineKind.RoundReward),
                "a drawn round pays too");
            Assert.That(
                SearchAndDestroySmokeRun.Classify(SearchAndDestroyLog.RoundEndLog(1, Side.Alpha, RoundEndReason.DefendersEliminated, 1, 0)),
                Is.EqualTo(SearchAndDestroySmokeRun.LineKind.RoundEnded));
        }

        [Test]
        public void LeavesOtherLinesAlone()
        {
            Assert.That(SearchAndDestroySmokeRun.Classify(null), Is.EqualTo(SearchAndDestroySmokeRun.LineKind.Other));
            Assert.That(SearchAndDestroySmokeRun.Classify(string.Empty), Is.EqualTo(SearchAndDestroySmokeRun.LineKind.Other));
            Assert.That(SearchAndDestroySmokeRun.Classify(EconomyLog.BuyTime(true, 1)), Is.EqualTo(SearchAndDestroySmokeRun.LineKind.Other), "buy=open is not a reward");
            Assert.That(
                SearchAndDestroySmokeRun.Classify(EconomyLog.Refused(0, "rifle", PurchaseVerdict.NotEnoughMoney, 800)),
                Is.EqualTo(SearchAndDestroySmokeRun.LineKind.Other));
            Assert.That(SearchAndDestroySmokeRun.Classify(EconomyLog.KillReward(1, 2, 300, 1100)), Is.EqualTo(SearchAndDestroySmokeRun.LineKind.Other));
            Assert.That(SearchAndDestroySmokeRun.Classify(EconomyLog.Equipped(0, 2)), Is.EqualTo(SearchAndDestroySmokeRun.LineKind.Other));
            Assert.That(
                SearchAndDestroySmokeRun.Classify(SearchAndDestroyLog.RoundLog(1, Side.Alpha, Side.Bravo)),
                Is.EqualTo(SearchAndDestroySmokeRun.LineKind.Other));
            Assert.That(SearchAndDestroySmokeRun.Classify(SearchAndDestroyLog.EndedLog(Side.Alpha)), Is.EqualTo(SearchAndDestroySmokeRun.LineKind.Other));
        }

        [Test]
        public void TriesTheAffordableItemsFromTheCheapestKeepingTheListOrderOnTies()
        {
            var catalog = new BuyCatalog(new[] { _rifle, _frag, _vest, _pistol, _smoke });

            Assert.That(
                SearchAndDestroySmokeRun.AffordableByPrice(catalog, 800).Select(item => item.Id),
                Is.EqualTo(new[] { "frag", "smoke", "pistol", "vest" }));
            Assert.That(
                SearchAndDestroySmokeRun.AffordableByPrice(catalog, 500).Select(item => item.Id),
                Is.EqualTo(new[] { "frag", "smoke", "pistol" }),
                "an item at exactly the money is affordable");
            Assert.That(SearchAndDestroySmokeRun.AffordableByPrice(catalog, 299), Is.Empty);
            Assert.That(SearchAndDestroySmokeRun.AffordableByPrice(BuyCatalog.Empty, 16000), Is.Empty);
            Assert.Throws<ArgumentNullException>(() => SearchAndDestroySmokeRun.AffordableByPrice(null, 800));
        }

        [Test]
        public void TheBundledEconomyLetsThePlayerBuyAWeaponWithTheStartMoney()
        {
            var asset = UnityEngine.Resources.Load<UnityEngine.TextAsset>(EconomyJson.ResourcePath);
            Assert.That(asset, Is.Not.Null, "Resources/" + EconomyJson.ResourcePath + ".json is bundled");
            var economy = EconomyJson.Parse(asset.text, EconomyJson.ResourcePath);
            UnityEngine.Resources.UnloadAsset(asset);

            Assert.That(
                SearchAndDestroySmokeRun.AffordableByPrice(economy.Catalog, economy.Spec.StartMoney).Any(item => item.Kind == BuyItemKind.Weapon),
                Is.True,
                "the first buy time has a weapon the start money pays for, so the run does not depend on grenades being thrown");
        }
    }
}
