using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Shooter.Core;
using Shooter.Runtime;
using UnityEngine;
using UnityEngine.UIElements;

namespace Shooter.Tests.EditMode
{
    // 文は JapaneseLanguageSetUp が効かせる日本語の表 (Resources/Localization/ja/economy.json・items.json) から引く。
    public sealed class BuyMenuPanelTests
    {
        private const string PistolId = "a9ae4aeb-607f-41a8-875f-2e8deed9bfa6";
        private const string RifleId = "6c1f3b2a-8d4e-4f7a-9b0c-2e5d7a1f4c83";
        private const string FragId = "ff94f232-2647-4b77-a077-5693d8f954d1";
        private const string ArmorId = "armor-light";
        private const int MaxMoney = 16000;

        private static readonly string[] _namesJa = { "跳弾ピストル", "Rifle", "破片手榴弾", "armor-light" };
        private static readonly string[] _detailsJa = { "武器・枠 2", "武器・枠 1", "投げ物", "防具 +50" };
        private static readonly string[] _detailsEn = { "Weapon · slot 2", "Weapon · slot 1", "Throwable", "Armor +50" };
        private static readonly string[] _prices = { "$500", "$2700", "$300", "$650" };
        private static readonly bool[] _affordableWith800 = { true, false, true, true };
        private static readonly bool[] _affordableWith300 = { false, false, true, false };
        private static readonly string[] _pistolOnly = { PistolId };

        private VisualElement _container;
        private BuyMenuPanel _panel;
        private FakeBuySender _sender;
        private int _closed;

        private static BuyCatalog Catalog() => new BuyCatalog(new[]
        {
            new BuyItem(PistolId, BuyItemKind.Weapon, 500, slot: 1, nameKey: "weapon.ricochetPistol"),
            new BuyItem(RifleId, BuyItemKind.Weapon, 2700, slot: 0, nameKey: "weapon.rifle"),
            new BuyItem(FragId, BuyItemKind.Throwable, 300, nameKey: "grenade.frag"),
            new BuyItem(ArmorId, BuyItemKind.Armor, 650, amount: 50),
        });

        [SetUp]
        public void SetUp()
        {
            GameSignals.ResetAll();
            Localizer.Apply(GameLanguage.Japanese);
            _container = new VisualElement();
            _panel = null;
            _sender = null;
            _closed = 0;
        }

        [TearDown]
        public void TearDown()
        {
            _panel?.Dispose();
            GameSignals.ResetAll();
            Localizer.Apply(GameLanguage.Japanese);
        }

        [Test]
        public void TheStyleSheetIsInResources()
        {
            Assert.That(Resources.Load<StyleSheet>(BuyMenuPanel.StyleSheetResourcePath), Is.Not.Null);
        }

        [Test]
        public void ListsEveryItemInTheCatalogOrderWithItsNameKindAndPrice()
        {
            Open(800, Catalog());

            Assert.That(_panel.IsOpen, Is.True);
            Assert.That(_panel.Container.ClassListContains(BuyMenuPanel.PanelClass), Is.True);
            Assert.That(_panel.Title, Is.EqualTo("購入"));
            Assert.That(_panel.MoneyText, Is.EqualTo("所持金 $800"));
            Assert.That(_panel.Count, Is.EqualTo(4));
            Assert.That(Each(_panel.ItemName), Is.EqualTo(_namesJa), "an item without a name key shows its Id");
            Assert.That(Each(_panel.ItemDetail), Is.EqualTo(_detailsJa), "weapons show their slot counted from 1, armor the amount it adds");
            Assert.That(Each(_panel.ItemPrice), Is.EqualTo(_prices));
            Assert.That(Each(_panel.IsAffordable), Is.EqualTo(_affordableWith800));
            Assert.That(_panel.ItemButton(1).ClassListContains(BuyMenuPanel.UnaffordableItemClass), Is.True);
            Assert.That(Each(i => _panel.ItemButton(i).enabledSelf), Is.All.True, "an item you cannot afford stays pressable so it can say why");
            Assert.That(_panel.CloseButton.text, Is.EqualTo("閉じる"));
            Assert.That(_panel.EmptyNotice, Is.Null);
            Assert.That(_panel.Status, Is.Null);
        }

        [Test]
        public void WithoutACatalogItListsTheItemsOfTheEconomyTuning()
        {
            Open(800, null);

            var tuned = KitTuning.Economy.Catalog;
            Assert.That(_panel.Catalog.Items.Select(item => item.Id), Is.EqualTo(tuned.Items.Select(item => item.Id)));
            Assert.That(_panel.Count, Is.EqualTo(tuned.Count));
            Assert.That(_panel.Count, Is.GreaterThan(0), "Resources/Tuning/economy.json lists items");
        }

        [Test]
        public void AnEmptyCatalogSaysNothingIsForSale()
        {
            Open(800, BuyCatalog.Empty);

            Assert.That(_panel.Count, Is.Zero);
            Assert.That(_panel.EmptyNotice, Is.EqualTo("買える品が無い"));
        }

        [Test]
        public void BuyingSendsTheIdAndShowsTheItemAsBoughtOnceTheWalletDrops()
        {
            Open(800, Catalog());
            _sender = PublishSender(Catalog(), answersAtOnce: true);

            Assert.That(_panel.Buy(0), Is.True);

            Assert.That(_sender.ItemIds, Is.EqualTo(_pistolOnly));
            Assert.That(_panel.PendingItemId, Is.Null);
            Assert.That(_panel.Status, Is.EqualTo("跳弾ピストル を買った"));
            Assert.That(_panel.IsStatusError, Is.False);
            Assert.That(_panel.MoneyText, Is.EqualTo("所持金 $300"));
            Assert.That(Each(_panel.IsAffordable), Is.EqualTo(_affordableWith300));
            Assert.That(Each(i => _panel.ItemButton(i).enabledSelf), Is.All.True);
        }

        [Test]
        public void AFreeItemIsBoughtWhenTheWalletIsPublishedAgainWithTheSameMoney()
        {
            var free = new BuyCatalog(new[] { new BuyItem(FragId, BuyItemKind.Throwable, 0, nameKey: "grenade.frag") });
            Open(800, free);
            _sender = PublishSender(free, answersAtOnce: true);

            Assert.That(_panel.Buy(0), Is.True);

            Assert.That(_panel.PendingItemId, Is.Null);
            Assert.That(_panel.IsStatusError, Is.False);
            Assert.That(_panel.MoneyText, Is.EqualTo("所持金 $800"));
            _panel.Tick(10f);
            Assert.That(_panel.IsStatusError, Is.False, "the answer timeout does not turn a bought item into a failure");
        }

        [Test]
        public void WhileWaitingForAnAnswerItShowsBuyingAndDoesNotSendAgain()
        {
            Open(800, Catalog());
            _sender = PublishSender(Catalog(), answersAtOnce: false);

            Assert.That(_panel.Buy(0), Is.True);

            Assert.That(_panel.PendingItemId, Is.EqualTo(PistolId));
            Assert.That(_panel.Status, Is.EqualTo("跳弾ピストル を購入中…"));
            Assert.That(_panel.IsStatusError, Is.False);
            Assert.That(_panel.ItemButton(0).ClassListContains(BuyMenuPanel.PendingItemClass), Is.True);
            Assert.That(Each(i => _panel.ItemButton(i).enabledSelf), Is.All.False);
            Assert.That(_panel.Buy(2), Is.False, "a second press while waiting is not sent");
            Assert.That(_sender.ItemIds, Is.EqualTo(_pistolOnly));

            GameSignals.Wallet.Publish(new WalletStatus(300, MaxMoney, true));

            Assert.That(_panel.PendingItemId, Is.Null);
            Assert.That(_panel.Status, Is.EqualTo("跳弾ピストル を買った"));
            Assert.That(_panel.ItemButton(0).ClassListContains(BuyMenuPanel.PendingItemClass), Is.False);
            Assert.That(Each(i => _panel.ItemButton(i).enabledSelf), Is.All.True);
        }

        [Test]
        public void AnItemYouCannotAffordIsNotSentAndSaysTheMoneyIsShort()
        {
            Open(800, Catalog());
            _sender = PublishSender(Catalog(), answersAtOnce: true);

            Assert.That(_panel.Buy(1), Is.False);

            Assert.That(_sender.ItemIds, Is.Empty);
            Assert.That(_panel.Status, Is.EqualTo("Rifle を買うお金が足りない"));
            Assert.That(_panel.IsStatusError, Is.True);
            Assert.That(_panel.MoneyText, Is.EqualTo("所持金 $800"));
        }

        [Test]
        public void TheReasonTheSenderGivesIsShown()
        {
            Open(800, Catalog());
            _sender = PublishSender(Catalog(), answersAtOnce: false);
            _panel.Buy(2);

            _panel.ShowVerdict(PistolId, PurchaseVerdict.NotEnoughMoney);

            Assert.That(_panel.PendingItemId, Is.EqualTo(FragId), "an answer for another item is an old one and is dropped");
            Assert.That(_panel.Status, Is.EqualTo("破片手榴弾 を購入中…"));

            _panel.ShowVerdict(FragId, PurchaseVerdict.CannotCarry);

            Assert.That(_panel.PendingItemId, Is.Null);
            Assert.That(_panel.Status, Is.EqualTo("破片手榴弾 はこれ以上持てない"));
            Assert.That(_panel.IsStatusError, Is.True);
            Assert.That(Each(i => _panel.ItemButton(i).enabledSelf), Is.All.True);

            _panel.ShowVerdict("smoke-x", PurchaseVerdict.UnknownItem);

            Assert.That(_panel.Status, Is.EqualTo("smoke-x は売っていない"), "an item missing from the list shows its Id");

            _panel.ShowVerdict(RifleId, PurchaseVerdict.NotBuyTime);

            Assert.That(_panel.Status, Is.EqualTo("準備の時間しか買えない"));
        }

        [Test]
        public void WithoutAnAnswerInTimeItSaysTheItemWasNotBought()
        {
            _panel = new BuyMenuPanel(PublishWallet(800), Catalog(), 2f);
            _sender = PublishSender(Catalog(), answersAtOnce: false);
            _panel.Buy(0);

            _panel.Tick(1.9f);

            Assert.That(_panel.PendingItemId, Is.EqualTo(PistolId));

            _panel.Tick(0.2f);

            Assert.That(_panel.PendingItemId, Is.Null);
            Assert.That(_panel.Status, Is.EqualTo("跳弾ピストル を買えなかった"));
            Assert.That(_panel.IsStatusError, Is.True);
            Assert.That(Each(i => _panel.ItemButton(i).enabledSelf), Is.All.True);
        }

        [Test]
        public void WithoutASenderItSaysBuyingIsNotAvailable()
        {
            Open(800, Catalog());

            Assert.That(_panel.Buy(0), Is.False);

            Assert.That(_panel.PendingItemId, Is.Null);
            Assert.That(_panel.Status, Is.EqualTo("この試合では買えない"));
            Assert.That(_panel.IsStatusError, Is.True);
        }

        [Test]
        public void ItOpensOnlyDuringTheBuyTime()
        {
            Assert.That(() => new BuyMenuPanel(_container, Catalog()), Throws.InvalidOperationException, "no wallet");

            GameSignals.Wallet.Publish(new WalletStatus(800, MaxMoney, false));

            Assert.That(() => new BuyMenuPanel(_container, Catalog()), Throws.InvalidOperationException, "outside the buy time");
            Assert.That(BuyMenuPanel.IsBuyTime(GameSignals.Wallet.Value), Is.False);
            Assert.That(BuyMenuPanel.IsBuyTime(new WalletStatus(800, MaxMoney, true)), Is.True);
            Assert.That(BuyMenuPanel.IsBuyTime(null), Is.False);
        }

        [Test]
        public void WhenTheBuyTimeEndsItClosesAndStopsBuying()
        {
            Open(800, Catalog());
            _sender = PublishSender(Catalog(), answersAtOnce: false);

            GameSignals.Wallet.Publish(new WalletStatus(800, MaxMoney, false));

            Assert.That(_closed, Is.EqualTo(1));
            Assert.That(_panel.IsOpen, Is.False);
            Assert.That(_panel.Buy(0), Is.False);
            Assert.That(_sender.ItemIds, Is.Empty);
            Assert.That(Each(i => _panel.ItemButton(i).enabledSelf), Is.All.False);

            GameSignals.Wallet.Publish(new WalletStatus(800, MaxMoney, true));

            Assert.That(_closed, Is.EqualTo(1), "a closed panel does not open again on its own");
            Assert.That(_panel.IsOpen, Is.False);
        }

        [Test]
        public void ClearingTheWalletClosesThePanel()
        {
            Open(800, Catalog());

            GameSignals.Wallet.Clear();

            Assert.That(_closed, Is.EqualTo(1));
            Assert.That(_panel.IsOpen, Is.False);
        }

        [Test]
        public void TheCloseButtonClosesOnce()
        {
            Open(800, Catalog());

            _panel.Cancel();
            _panel.Cancel();

            Assert.That(_closed, Is.EqualTo(1));
            Assert.That(_panel.IsOpen, Is.False);
        }

        [Test]
        public void TheTextsFollowTheLanguage()
        {
            Open(800, Catalog());
            _panel.Buy(1);

            Localizer.Apply(GameLanguage.English);

            Assert.That(_panel.Title, Is.EqualTo("BUY"));
            Assert.That(_panel.MoneyText, Is.EqualTo("Money $800"));
            Assert.That(Each(_panel.ItemDetail), Is.EqualTo(_detailsEn));
            Assert.That(_panel.Status, Is.EqualTo("Not enough money for Rifle"));
            Assert.That(_panel.CloseButton.text, Is.EqualTo("CLOSE"));
        }

        [Test]
        public void DisposingEmptiesTheContainer()
        {
            Open(800, Catalog());

            _panel.Dispose();

            Assert.That(_container.childCount, Is.Zero);
            Assert.That(_container.ClassListContains(BuyMenuPanel.PanelClass), Is.False);
            Assert.That(_panel.IsOpen, Is.False);
            Assert.That(_closed, Is.Zero, "disposing is not closing by the player or by the clock");
        }

        [Test]
        public void ThePrecheckRefusesOnlyWhatIsKnownBeforeSending()
        {
            var rifle = Catalog().Items[1];
            var pistol = Catalog().Items[0];

            Assert.That(BuyMenuPanel.Precheck(pistol, new WalletStatus(500, MaxMoney, true)), Is.EqualTo(PurchaseVerdict.Bought));
            Assert.That(BuyMenuPanel.Precheck(rifle, new WalletStatus(2699, MaxMoney, true)), Is.EqualTo(PurchaseVerdict.NotEnoughMoney));
            Assert.That(BuyMenuPanel.Precheck(pistol, new WalletStatus(16000, MaxMoney, false)), Is.EqualTo(PurchaseVerdict.NotBuyTime));
            Assert.That(BuyMenuPanel.Precheck(pistol, null), Is.EqualTo(PurchaseVerdict.NotBuyTime));
            Assert.That(BuyMenuPanel.Precheck(null, new WalletStatus(500, MaxMoney, true)), Is.EqualTo(PurchaseVerdict.UnknownItem));
        }

        [Test]
        public void EveryVerdictAndKindHasTextInEveryLanguage()
        {
            foreach (var language in GameLanguages.All)
            {
                var table = Localizer.TableOf(language);
                foreach (PurchaseVerdict verdict in Enum.GetValues(typeof(PurchaseVerdict)))
                {
                    Assert.That(table.Contains(BuyMenuPanel.VerdictKeyOf(verdict)), Is.True, $"{GameLanguages.Code(language)}: {verdict}");
                }

                foreach (BuyItemKind kind in Enum.GetValues(typeof(BuyItemKind)))
                {
                    Assert.That(table.Contains(BuyMenuPanel.KindKeyOf(kind)), Is.True, $"{GameLanguages.Code(language)}: {kind}");
                }
            }

            Assert.That(() => BuyMenuPanel.VerdictKeyOf((PurchaseVerdict)99), Throws.InstanceOf<ArgumentOutOfRangeException>());
            Assert.That(() => BuyMenuPanel.KindKeyOf((BuyItemKind)99), Throws.InstanceOf<ArgumentOutOfRangeException>());
        }

        private void Open(int money, BuyCatalog catalog)
        {
            _panel = new BuyMenuPanel(PublishWallet(money), catalog);
            _panel.Closed += () => _closed++;
        }

        private VisualElement PublishWallet(int money)
        {
            GameSignals.Wallet.Publish(new WalletStatus(money, MaxMoney, true));
            return _container;
        }

        private static FakeBuySender PublishSender(BuyCatalog catalog, bool answersAtOnce)
        {
            var sender = new FakeBuySender(catalog, answersAtOnce);
            GameSignals.BuySender.Publish(sender);
            return sender;
        }

        private IList<T> Each<T>(Func<int, T> read)
        {
            return Enumerable.Range(0, _panel.Count).Select(read).ToList();
        }

        // オフラインの経済 (RoundEconomy) と同じく、answersAtOnce なら送られたその場で財布を出し直す。
        private sealed class FakeBuySender : IBuySender
        {
            private readonly BuyCatalog _catalog;
            private readonly bool _answersAtOnce;

            public FakeBuySender(BuyCatalog catalog, bool answersAtOnce)
            {
                _catalog = catalog;
                _answersAtOnce = answersAtOnce;
            }

            public List<string> ItemIds { get; } = new List<string>();

            public void Buy(string itemId)
            {
                ItemIds.Add(itemId);
                if (!_answersAtOnce || !_catalog.TryFind(itemId, out var item))
                {
                    return;
                }

                var wallet = GameSignals.Wallet.Value.Value;
                GameSignals.Wallet.Publish(new WalletStatus(wallet.Money - item.Price, wallet.Max, wallet.CanBuy));
            }
        }
    }
}
