using System;
using System.Collections.Generic;
using NUnit.Framework;
using Shooter.Core;
using Shooter.Runtime;
using UnityEngine.UIElements;

namespace Shooter.Tests.EditMode
{
    public sealed class CosmeticsPanelTests
    {
        private const string Desert = "skin-desert";
        private const string Night = "skin-night";
        private const string Tiger = "camo-tiger";

        private VisualElement _container;
        private FakeLocker _locker;
        private CosmeticsPanel _panel;
        private int _cancelled;

        private static CosmeticCatalog Catalog()
        {
            // キットの言語の表に見た目の名前は無いので、名前のキーを持たせず Id で出す
            return new CosmeticCatalog(new[]
            {
                new CosmeticItem(Desert, CosmeticKind.Skin, null, "body-desert"),
                new CosmeticItem(Night, CosmeticKind.Skin, null, "body-night", 3),
                new CosmeticItem(Tiger, CosmeticKind.Camo, null, "paint-tiger"),
            });
        }

        private void Open(CosmeticCatalog catalog = null)
        {
            _panel?.Dispose();
            _locker = new FakeLocker(catalog ?? Catalog(), level: 1);
            _panel = new CosmeticsPanel(_container, _locker);
            _panel.Cancelled += () => _cancelled++;
        }

        [SetUp]
        public void SetUp()
        {
            _container = new VisualElement();
            _cancelled = 0;
            _panel = null;
        }

        [TearDown]
        public void TearDown()
        {
            _panel?.Dispose();
            GameSignals.Cosmetics.Clear();
        }

        [Test]
        public void ListsNoneAndEachLookOfTheKindWithItsState()
        {
            Open();

            Assert.That(_panel.TitleText, Is.EqualTo("見た目"));
            Assert.That(_panel.SectionTitleText(CosmeticKind.Skin), Is.EqualTo("スキン"));
            Assert.That(_panel.SectionTitleText(CosmeticKind.Camo), Is.EqualTo("迷彩"));
            Assert.That(_panel.OptionLines(CosmeticKind.Skin), Is.EqualTo(new[] { "なし", Desert, Night + " (レベル 3 で解除)" }));
            Assert.That(_panel.OptionLines(CosmeticKind.Camo), Is.EqualTo(new[] { "なし", Tiger }), "a look without a name key shows its id");
            Assert.That(_panel.Option(CosmeticKind.Skin, Night).enabledSelf, Is.False, "a locked look cannot be chosen");
            Assert.That(_panel.Option(CosmeticKind.Skin, Night).ClassListContains(CosmeticsPanel.OptionLockedClass), Is.True);
            Assert.That(_panel.Option(CosmeticKind.Skin, null).ClassListContains(CosmeticsPanel.OptionEquippedClass), Is.True, "nothing is worn yet");
            Assert.That(_container.Q<Button>(CosmeticsPanel.BackButtonName).text, Is.EqualTo("戻る"));
            Assert.That(_container.ClassListContains(CosmeticsPanel.PanelClass), Is.True);
        }

        [Test]
        public void ChoosingALookEquipsItAndMarksIt()
        {
            Open();

            Assert.That(_panel.Choose(CosmeticKind.Skin, Desert), Is.True);

            Assert.That(_locker.EquippedId(CosmeticKind.Skin), Is.EqualTo(Desert));
            Assert.That(_panel.OptionLines(CosmeticKind.Skin), Is.EqualTo(new[] { "なし", Desert + " (装備中)", Night + " (レベル 3 で解除)" }));
            Assert.That(_panel.Option(CosmeticKind.Skin, Desert).ClassListContains(CosmeticsPanel.OptionEquippedClass), Is.True);
            Assert.That(_panel.Option(CosmeticKind.Skin, null).ClassListContains(CosmeticsPanel.OptionEquippedClass), Is.False);
            Assert.That(_panel.StatusText, Is.Empty);

            Assert.That(_panel.Choose(CosmeticKind.Skin, null), Is.True);
            Assert.That(_locker.EquippedId(CosmeticKind.Skin), Is.Null, "none takes the look off");
        }

        [Test]
        public void ARefusedLookSaysSo()
        {
            Open();

            Assert.That(_panel.Choose(CosmeticKind.Skin, Night), Is.False);

            Assert.That(_panel.StatusText, Is.EqualTo("この見た目は着けられません"));
            Assert.That(_locker.EquippedId(CosmeticKind.Skin), Is.Null);
        }

        [Test]
        public void FollowsTheLockerWhenALookUnlocks()
        {
            Open();

            _locker.Level = 3;
            _locker.RaiseChanged();

            Assert.That(_panel.OptionLines(CosmeticKind.Skin), Does.Contain(Night));
            Assert.That(_panel.Option(CosmeticKind.Skin, Night).enabledSelf, Is.True);

            _panel.Dispose();
            _locker.Level = 1;
            _locker.RaiseChanged();
            Assert.That(_container.childCount, Is.Zero, "a disposed panel no longer follows the locker");
        }

        [Test]
        public void AKindWithoutLooksSaysSoButStillOffersNone()
        {
            Open(new CosmeticCatalog(new[] { new CosmeticItem(Desert, CosmeticKind.Skin, null, "body-desert") }));

            Assert.That(_panel.IsEmptyShown(CosmeticKind.Camo), Is.True);
            Assert.That(_panel.IsEmptyShown(CosmeticKind.Skin), Is.False);
            Assert.That(_panel.OptionLines(CosmeticKind.Camo), Is.EqualTo(new[] { "なし" }));
            Assert.That(_container.Q<Label>(className: CosmeticsPanel.EmptyClass).text, Is.EqualTo("まだありません"));
        }

        [Test]
        public void BackAndCancelCloseThePanel()
        {
            Open();

            _panel.Cancel();
            Assert.That(_cancelled, Is.EqualTo(1));

            _panel.Dispose();
            _panel.Cancel();
            Assert.That(_cancelled, Is.EqualTo(1), "a disposed panel does not report");
            Assert.That(_panel.Choose(CosmeticKind.Skin, Desert), Is.False, "a disposed panel does not equip");
            Assert.That(_container.ClassListContains(CosmeticsPanel.PanelClass), Is.False);
        }

        [Test]
        public void OpenNeedsAPublishedLocker()
        {
            GameSignals.Cosmetics.Clear();
            var error = Assert.Throws<InvalidOperationException>(() => CosmeticsPanel.Open(_container));
            Assert.That(error.Message, Does.Contain("GameSignals.Cosmetics"));

            var locker = new FakeLocker(Catalog(), 1);
            GameSignals.Cosmetics.Publish(locker);
            _panel = CosmeticsPanel.Open(_container);

            Assert.That(_panel.Locker, Is.SameAs(locker));
            Assert.Throws<ArgumentNullException>(() => new CosmeticsPanel(null, locker));
            Assert.Throws<ArgumentNullException>(() => new CosmeticsPanel(new VisualElement(), null));
        }

        [Test]
        public void TheTextsAreReadFromTheCosmeticsTable()
        {
            var english = Localizer.TableOf(GameLanguage.English);

            Assert.That(english.Get("cosmetics.title"), Is.EqualTo("Cosmetics"));
            Assert.That(english.Format("cosmetics.locked", "Night", 3), Is.EqualTo("Night (unlocks at level 3)"));
            Assert.That(CosmeticsPanel.Equipped("砂漠"), Is.EqualTo("砂漠 (装備中)"));
            Assert.That(CosmeticsPanel.OptionName(CosmeticKind.Camo, Tiger), Is.EqualTo("cosmetics-camo-item-camo-tiger"));
            Assert.That(CosmeticsPanel.OptionName(CosmeticKind.Skin, null), Is.EqualTo("cosmetics-skin-none"));
        }

        private sealed class FakeLocker : ICosmeticLocker
        {
            private readonly Dictionary<CosmeticKind, string> _equipped = new Dictionary<CosmeticKind, string>();

            public FakeLocker(CosmeticCatalog catalog, int level)
            {
                Catalog = catalog;
                Level = level;
            }

            public event Action Changed;

            public CosmeticCatalog Catalog { get; }

            public int Level { get; set; }

            public string EquippedId(CosmeticKind kind)
            {
                return _equipped.TryGetValue(kind, out var id) ? id : null;
            }

            public bool IsUnlocked(string cosmeticId)
            {
                var item = Catalog.Find(cosmeticId);
                return item != null && item.IsUnlockedAt(Level);
            }

            public bool Equip(CosmeticKind kind, string cosmeticId)
            {
                if (cosmeticId == null)
                {
                    _equipped.Remove(kind);
                    RaiseChanged();
                    return true;
                }

                var item = Catalog.Find(cosmeticId);
                if (item == null || item.Kind != kind || !item.IsUnlockedAt(Level))
                {
                    return false;
                }

                _equipped[kind] = cosmeticId;
                RaiseChanged();
                return true;
            }

            public void RaiseChanged()
            {
                Changed?.Invoke();
            }
        }
    }
}
