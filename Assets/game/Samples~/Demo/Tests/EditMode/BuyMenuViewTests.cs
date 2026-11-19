using System.Collections.Generic;
using NUnit.Framework;
using Shooter.Core;
using Shooter.Runtime;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace Shooter.Tests.EditMode
{
    // Edit Mode では UIDocument が木を作らないことがあるので、Hud.uxml を複製した木を直接 Bind する。
    public sealed class BuyMenuViewTests
    {
        private const string PistolId = "a9ae4aeb-607f-41a8-875f-2e8deed9bfa6";

        private GameObject _host;
        private BuyMenuView _view;
        private int _closed;

        private static BuyCatalog Catalog() => new BuyCatalog(new[]
        {
            new BuyItem(PistolId, BuyItemKind.Weapon, 500, slot: 1, nameKey: "weapon.ricochetPistol"),
        });

        [SetUp]
        public void SetUp()
        {
            GameSignals.ResetAll();
            _closed = 0;
            _host = new GameObject("TestBuyMenu");
            _view = _host.AddComponent<BuyMenuView>();
            _view.Closed += () => _closed++;
            _view.Bind(Layout());
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_host);
            GameSignals.ResetAll();
        }

        [Test]
        public void TheMenuSitsInTheHudRootAndIsHiddenUntilOpened()
        {
            Assert.That(_view.Element, Is.Not.Null);
            Assert.That(_view.Element.name, Is.EqualTo(BuyMenuView.UxmlName));
            Assert.That(_view.Host, Is.Not.Null);
            Assert.That(_view.Host.name, Is.EqualTo(BuyMenuView.PanelName));
            Assert.That(_view.Host.parent, Is.SameAs(_view.Element));
            Assert.That(_view.Host.style.display.value, Is.EqualTo(DisplayStyle.None));
            Assert.That(_view.IsOpen, Is.False);
        }

        [Test]
        public void OutsideTheBuyTimeItDoesNotOpen()
        {
            Assert.That(_view.Open(Catalog()), Is.False, "no wallet");

            GameSignals.Wallet.Publish(new WalletStatus(800, 16000, false));

            Assert.That(_view.Open(Catalog()), Is.False, "the round is under way");
            Assert.That(_view.IsOpen, Is.False);
            Assert.That(_view.Host.style.display.value, Is.EqualTo(DisplayStyle.None));
        }

        [Test]
        public void DuringTheBuyTimeItOpensAndBuysThroughTheSender()
        {
            GameSignals.Wallet.Publish(new WalletStatus(800, 16000, true));
            var sender = new RecordingSender();
            GameSignals.BuySender.Publish(sender);

            Assert.That(_view.Open(Catalog()), Is.True);
            Assert.That(_view.IsOpen, Is.True);
            Assert.That(_view.Host.style.display.value, Is.EqualTo(DisplayStyle.Flex));
            Assert.That(_view.Panel.Count, Is.EqualTo(1));

            Assert.That(_view.Panel.Buy(0), Is.True);
            Assert.That(sender.ItemIds, Is.EqualTo(new[] { PistolId }));
        }

        [Test]
        public void WhenTheBuyTimeEndsTheMenuCloses()
        {
            GameSignals.Wallet.Publish(new WalletStatus(800, 16000, true));
            _view.Open(Catalog());

            GameSignals.Wallet.Publish(new WalletStatus(800, 16000, false));

            Assert.That(_view.IsOpen, Is.False);
            Assert.That(_view.Panel, Is.Null);
            Assert.That(_view.Host.style.display.value, Is.EqualTo(DisplayStyle.None));
            Assert.That(_view.Host.childCount, Is.Zero);
            Assert.That(_closed, Is.EqualTo(1));
        }

        [Test]
        public void ToggleOpensAndClosesTheMenu()
        {
            GameSignals.Wallet.Publish(new WalletStatus(800, 16000, true));

            Assert.That(_view.Toggle(Catalog()), Is.True);
            Assert.That(_view.IsOpen, Is.True);

            Assert.That(_view.Toggle(Catalog()), Is.False);
            Assert.That(_view.IsOpen, Is.False);
            Assert.That(_closed, Is.EqualTo(1));
        }

        [Test]
        public void RebindingKeepsAnOpenMenuOpen()
        {
            GameSignals.Wallet.Publish(new WalletStatus(800, 16000, true));
            _view.Open(Catalog());

            _view.Bind(Layout());

            Assert.That(_view.IsOpen, Is.True);
            Assert.That(_view.Host.parent, Is.SameAs(_view.Element));
            Assert.That(_view.Panel.Count, Is.EqualTo(1));
            Assert.That(_closed, Is.Zero);
        }

        [Test]
        public void TheAnswerSecondsMustBePositive()
        {
            Assert.That(_view.AnswerSeconds, Is.EqualTo(BuyMenuPanel.DefaultAnswerSeconds));
            Assert.That(() => _view.AnswerSeconds = 0f, Throws.InstanceOf<System.ArgumentOutOfRangeException>());

            _view.AnswerSeconds = 0.5f;

            Assert.That(_view.AnswerSeconds, Is.EqualTo(0.5f));
        }

        private static VisualElement Layout()
        {
            var layout = Resources.Load<VisualTreeAsset>(Hud.LayoutResourcePath);
            Assert.That(layout, Is.Not.Null, "Assets/game/Samples~/Demo/UI/Resources/Hud/Hud.uxml should be loadable from Resources");
            return layout.CloneTree();
        }

        private sealed class RecordingSender : IBuySender
        {
            public List<string> ItemIds { get; } = new List<string>();

            public void Buy(string itemId)
            {
                ItemIds.Add(itemId);
            }
        }
    }
}
