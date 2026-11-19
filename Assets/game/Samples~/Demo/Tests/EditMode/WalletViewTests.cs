using NUnit.Framework;
using Shooter.Core;
using Shooter.Runtime;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace Shooter.Tests.EditMode
{
    // Edit Mode では UIDocument が木を作らないことがあるので、Hud.uxml を複製した木を直接 Bind する。
    // 文は JapaneseLanguageSetUp が効かせる日本語の表 (Resources/Localization/ja/hud.json) から引く。
    public sealed class WalletViewTests
    {
        private GameObject _host;
        private WalletView _view;

        [SetUp]
        public void SetUp()
        {
            GameSignals.ResetAll();
            Localizer.Apply(GameLanguage.Japanese);
            _host = new GameObject("TestWallet");
            _view = _host.AddComponent<WalletView>();
            _view.Bind(Layout());
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_host);
            GameSignals.ResetAll();
            Localizer.Apply(GameLanguage.Japanese);
        }

        [Test]
        public void TheViewIsHiddenWithoutAWallet()
        {
            Assert.That(_view.Element, Is.Not.Null, "Hud.uxml has the wallet element");
            Assert.That(_view.Element.name, Is.EqualTo(WalletView.UxmlName));
            Assert.That(_view.IsShown, Is.False);
            Assert.That(_view.Element.style.display.value, Is.EqualTo(DisplayStyle.None));
            Assert.That(_view.MoneyText, Is.Empty);
        }

        [Test]
        public void APublishedWalletShowsTheMoney()
        {
            GameSignals.Wallet.Publish(new WalletStatus(3400, 16000, false));

            Assert.That(_view.IsShown, Is.True);
            Assert.That(_view.Element.style.display.value, Is.EqualTo(DisplayStyle.Flex));
            Assert.That(_view.MoneyText, Is.EqualTo("所持金 $3400"));
            Assert.That(MoneyLabel().text, Is.EqualTo("所持金 $3400"));
            Assert.That(MoneyLabel().ClassListContains(WalletView.MoneyClass), Is.True);
            Assert.That(_view.Element.ClassListContains(WalletView.BuyTimeClass), Is.False);
            Assert.That(_view.Element.ClassListContains(WalletView.FullClass), Is.False);
        }

        [Test]
        public void EachNewWalletReplacesTheMoney()
        {
            GameSignals.Wallet.Publish(new WalletStatus(800, 16000, true));
            GameSignals.Wallet.Publish(new WalletStatus(300, 16000, true));

            Assert.That(_view.MoneyText, Is.EqualTo("所持金 $300"));
            Assert.That(_view.Element.Query<Label>(WalletView.MoneyName).ToList(), Has.Count.EqualTo(1));
        }

        [Test]
        public void TheBuyTimeAndAFullWalletAreMarked()
        {
            GameSignals.Wallet.Publish(new WalletStatus(800, 16000, true));

            Assert.That(_view.Element.ClassListContains(WalletView.BuyTimeClass), Is.True);
            Assert.That(_view.Element.ClassListContains(WalletView.FullClass), Is.False);

            GameSignals.Wallet.Publish(new WalletStatus(16000, 16000, false));

            Assert.That(_view.Element.ClassListContains(WalletView.BuyTimeClass), Is.False);
            Assert.That(_view.Element.ClassListContains(WalletView.FullClass), Is.True);
            Assert.That(_view.MoneyText, Is.EqualTo("所持金 $16000"));
        }

        [Test]
        public void ClearingTheWalletHidesIt()
        {
            GameSignals.Wallet.Publish(new WalletStatus(16000, 16000, true));

            GameSignals.Wallet.Clear();

            Assert.That(_view.IsShown, Is.False);
            Assert.That(_view.Status, Is.Null);
            Assert.That(_view.Element.style.display.value, Is.EqualTo(DisplayStyle.None));
            Assert.That(MoneyLabel().text, Is.Empty);
            Assert.That(_view.Element.ClassListContains(WalletView.BuyTimeClass), Is.False);
            Assert.That(_view.Element.ClassListContains(WalletView.FullClass), Is.False);
        }

        [Test]
        public void TheMoneyFollowsTheLanguage()
        {
            GameSignals.Wallet.Publish(new WalletStatus(3400, 16000, false));

            Localizer.Apply(GameLanguage.English);

            Assert.That(_view.MoneyText, Is.EqualTo("$3400"));
        }

        [Test]
        public void AWalletPublishedBeforeBindingIsShownOnBind()
        {
            GameSignals.Wallet.Publish(new WalletStatus(1200, 16000, true));
            var host = new GameObject("TestWalletLate");
            try
            {
                var view = host.AddComponent<WalletView>();
                view.Bind(Layout());

                Assert.That(view.IsShown, Is.True);
                Assert.That(view.MoneyText, Is.EqualTo("所持金 $1200"));
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void UnbindingRemovesTheLabelAndStopsListening()
        {
            GameSignals.Wallet.Publish(new WalletStatus(800, 16000, true));
            var element = _view.Element;

            _view.Unbind();
            GameSignals.Wallet.Publish(new WalletStatus(500, 16000, true));

            Assert.That(element.Q<Label>(WalletView.MoneyName), Is.Null);
            Assert.That(element.style.display.value, Is.EqualTo(DisplayStyle.None));
            Assert.That(element.ClassListContains(WalletView.BuyTimeClass), Is.False);
            Assert.That(_view.Status.Value.Money, Is.EqualTo(800), "a wallet sent after unbinding is not taken");
        }

        private static VisualElement Layout()
        {
            var layout = Resources.Load<VisualTreeAsset>(Hud.LayoutResourcePath);
            Assert.That(layout, Is.Not.Null, "Assets/game/Samples~/Demo/UI/Resources/Hud/Hud.uxml should be loadable from Resources");
            return layout.CloneTree();
        }

        private Label MoneyLabel()
        {
            var label = _view.Element.Q<Label>(WalletView.MoneyName);
            Assert.That(label, Is.Not.Null);
            return label;
        }
    }
}
