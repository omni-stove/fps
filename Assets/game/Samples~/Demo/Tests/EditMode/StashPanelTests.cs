using System;
using NUnit.Framework;
using Shooter.Core;
using Shooter.Runtime;
using UnityEngine.UIElements;

namespace Shooter.Tests.EditMode
{
    public sealed class StashPanelTests
    {
        private const string _bandage = "00000000-0000-4000-8000-000000000201";
        private const string _ammo = "00000000-0000-4000-8000-000000000202";
        private const string _removed = "00000000-0000-4000-8000-000000000203";

        private static readonly string[] _bandageAndAmmo = { "包帯 x3", "9mm 弾 x60", "9mm 弾 x15" };
        private static readonly string[] _empty = { "倉庫は空" };

        private VisualElement _container;
        private StashPanel _panel;
        private int _cancelled;

        private static ItemCatalog Items()
        {
            return new ItemCatalog(
                new[] { new ItemSpec(_bandage, "item.bandage", maxStack: 5), new ItemSpec(_ammo, "item.ammo9mm", maxStack: 60) },
                InventoryRule.Default);
        }

        private void Open(StashStatus status)
        {
            _panel?.Dispose();
            _panel = new StashPanel(_container, status, Items());
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
            GameSignals.Stash.Clear();
        }

        [Test]
        public void ListsEachStackWithItsNameAndTheSlotsUsed()
        {
            var stash = new Stash(10, Items());
            stash.Add(_bandage, 3);
            stash.Add(_ammo, 75);

            Open(stash.Status);

            Assert.That(_panel.TitleText, Is.EqualTo("倉庫"));
            Assert.That(_panel.SlotsText, Is.EqualTo("3/10 枠"));
            Assert.That(_panel.ItemLines, Is.EqualTo(_bandageAndAmmo));
            Assert.That(_container.ClassListContains(StashPanel.PanelClass), Is.True);
            Assert.That(_container.Q<Button>(StashPanel.BackButtonName).text, Is.EqualTo("戻る"));
        }

        [Test]
        public void AnEmptyStashSaysSo()
        {
            Open(new StashStatus(60, Array.Empty<StashEntry>()));

            Assert.That(_panel.SlotsText, Is.EqualTo("0/60 枠"));
            Assert.That(_panel.ItemLines, Is.EqualTo(_empty));
        }

        [Test]
        public void AnItemMissingFromTheCatalogShowsItsId()
        {
            Open(new StashStatus(10, new[] { new StashEntry(_removed, 2) }));

            Assert.That(_panel.ItemLines, Is.EqualTo(new[] { _removed + " x2" }));
        }

        [Test]
        public void FollowsTheStashTheRaidPublishes()
        {
            Open(new StashStatus(10, Array.Empty<StashEntry>()));

            GameSignals.Stash.Publish(new StashStatus(10, new[] { new StashEntry(_bandage, 1) }));
            Assert.That(_panel.ItemLines, Is.EqualTo(new[] { "包帯 x1" }));

            GameSignals.Stash.Clear();
            Assert.That(_panel.ItemLines, Is.EqualTo(new[] { "包帯 x1" }), "clearing the signal keeps what was shown");

            _panel.Dispose();
            GameSignals.Stash.Publish(new StashStatus(10, Array.Empty<StashEntry>()));
            Assert.That(_container.childCount, Is.EqualTo(0), "a disposed panel no longer follows the stash");
        }

        [Test]
        public void BackAndCancelCloseThePanel()
        {
            Open(new StashStatus(10, Array.Empty<StashEntry>()));

            _panel.Cancel();
            Assert.That(_cancelled, Is.EqualTo(1));

            _panel.Dispose();
            _panel.Cancel();
            Assert.That(_cancelled, Is.EqualTo(1), "a disposed panel does not report");
            Assert.That(_container.ClassListContains(StashPanel.PanelClass), Is.False);
        }

        [Test]
        public void TheTextsAreReadFromTheExtractionTable()
        {
            var english = Localizer.TableOf(GameLanguage.English);

            Assert.That(english.Get("extraction.stash.title"), Is.EqualTo("Stash"));
            Assert.That(english.Format("extraction.stash.slots", 3, 10), Is.EqualTo("3/10 slots"));
            Assert.That(StashPanel.Item("包帯", 2), Is.EqualTo("包帯 x2"));
        }
    }
}
