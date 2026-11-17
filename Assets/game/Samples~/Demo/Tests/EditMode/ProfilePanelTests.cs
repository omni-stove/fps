using System;
using Shooter.Core;
using Shooter.Runtime;
using NUnit.Framework;
using UnityEngine.UIElements;

namespace Shooter.Tests.EditMode
{
    public sealed class ProfilePanelTests
    {
        private const string _shotgun = "00000000-0000-4000-8000-000000000101";
        private const string _dmr = "00000000-0000-4000-8000-000000000102";
        private const string _suppressor = "00000000-0000-4000-8000-000000000103";
        private const string _unnamed = "00000000-0000-4000-8000-000000000104";

        private VisualElement _container;
        private ProfilePanel _panel;
        private int _cancelled;

        // レベル 2 は 500 XP、レベル 3 (最大) は 1500 XP。ショットガンはレベル 2、DMR とサプレッサーはレベル 3 で使える
        private static ProgressionLedger Ledger()
        {
            var unlocks = new[]
            {
                new ProgressionUnlock(_shotgun, 2),
                new ProgressionUnlock(_dmr, 3),
                new ProgressionUnlock(_suppressor, 3),
            };
            return new ProgressionLedger(new ProgressionSpec(100, 50, 50, 250, 500, new[] { 0, 500, 1500 }, unlocks));
        }

        private static WeaponCatalog Weapons()
        {
            return new WeaponCatalog(new[]
            {
                new WeaponSpec(_shotgun, "weapon.shotgun", WeaponCategory.Shotgun, FireMode.Auto, 30, 90, 0.1f, 2f, 25, 100f, DamageFalloff.None),
                new WeaponSpec(_dmr, "weapon.dmr", WeaponCategory.MarksmanRifle, FireMode.Auto, 30, 90, 0.1f, 2f, 25, 100f, DamageFalloff.None),
            });
        }

        private static ItemCatalog Items()
        {
            return new ItemCatalog(
                new[] { new ItemSpec(_suppressor, "item.suppressor", attachment: new AttachmentSpec("muzzle")) },
                InventoryRule.Default);
        }

        private void Open(PlayerProfile profile, ProgressionLedger ledger = null)
        {
            _panel?.Dispose();
            _panel = new ProfilePanel(_container, ledger ?? Ledger(), profile, Weapons(), Items());
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
        }

        [Test]
        public void ShowsTheLevelTheXpToTheNextLevelAndTheRecord()
        {
            Open(new PlayerProfile(700, 7, 2, 3, 1));

            Assert.That(_panel.Level, Is.EqualTo(2));
            Assert.That(_panel.LevelText, Is.EqualTo("レベル 2"));
            Assert.That(_panel.TotalXpText, Is.EqualTo("累計 700 XP"));
            Assert.That(_panel.XpToNextText, Is.EqualTo("レベル 3 まで あと 800 XP"));
            Assert.That(_panel.StatLines, Is.EqualTo(new[] { "倒した数 7", "アシスト 0", "倒れた数 2", "K/D 3.50", "試合数 3", "勝った数 1" }));
            Assert.That(_container.ClassListContains(ProfilePanel.PanelClass), Is.True);
            Assert.That(_container.Q<Button>(ProfilePanel.BackButtonName).text, Is.EqualTo("戻る"));
        }

        [Test]
        public void ListsOnlyTheThingsOfTheNearestLevelAboveWithTheirNames()
        {
            Open(new PlayerProfile(700, 0, 0, 0, 0));

            Assert.That(_panel.UnlockLines, Is.EqualTo(new[] { "レベル 3: DMR", "レベル 3: サプレッサー" }));
        }

        [Test]
        public void ANewProfileStartsAtLevelOneAndNeedsTheSecondLevelsXp()
        {
            Open(PlayerProfile.Empty);

            Assert.That(_panel.LevelText, Is.EqualTo("レベル 1"));
            Assert.That(_panel.XpToNextText, Is.EqualTo("レベル 2 まで あと 500 XP"));
            Assert.That(_panel.StatLines, Is.EqualTo(new[] { "倒した数 0", "アシスト 0", "倒れた数 0", "K/D 0.00", "試合数 0", "勝った数 0" }));
            Assert.That(_panel.UnlockLines, Is.EqualTo(new[] { "レベル 2: ショットガン" }));
        }

        [Test]
        public void AtTheLastLevelTheXpLineSaysSoAndEverythingIsUnlocked()
        {
            Open(new PlayerProfile(1500, 0, 0, 0, 0));

            Assert.That(_panel.LevelText, Is.EqualTo("レベル 3"));
            Assert.That(_panel.XpToNextText, Is.EqualTo("最大レベル"));
            Assert.That(_panel.UnlockLines, Is.EqualTo(new[] { "すべて使えます" }));
        }

        [Test]
        public void WithoutDeathsTheKillDeathRatioIsTheKills()
        {
            Open(new PlayerProfile(0, 4, 0, 1, 0));

            Assert.That(_panel.StatLines[3], Is.EqualTo("K/D 4.00"));
        }

        [Test]
        public void TheRecordShowsTheAssistsAfterTheKills()
        {
            Open(new PlayerProfile(0, 2, 5, 1, 0, 9));

            Assert.That(_panel.StatLines[0], Is.EqualTo("倒した数 2"));
            Assert.That(_panel.StatLines[1], Is.EqualTo("アシスト 9"));
            Assert.That(_panel.StatLines[1], Is.EqualTo(ProgressionFormat.Assists(9)));
            Assert.That(_panel.StatLines[3], Is.EqualTo("K/D 0.40"), "assists do not count toward K/D");
        }

        [Test]
        public void AnIdInNeitherCatalogIsShownAsTheId()
        {
            var ledger = new ProgressionLedger(new ProgressionSpec(
                100, 50, 50, 250, 500, new[] { 0, 500 }, new[] { new ProgressionUnlock(_unnamed, 2) }));
            Open(PlayerProfile.Empty, ledger);

            Assert.That(_panel.UnlockLines, Is.EqualTo(new[] { "レベル 2: " + _unnamed }));
        }

        [Test]
        public void ShowRewritesTheLinesForTheNewProfile()
        {
            Open(PlayerProfile.Empty);

            _panel.Show(new PlayerProfile(600, 3, 1, 2, 2, 4));

            Assert.That(_panel.LevelText, Is.EqualTo("レベル 2"));
            Assert.That(_panel.XpToNextText, Is.EqualTo("レベル 3 まで あと 900 XP"));
            Assert.That(_panel.StatLines, Is.EqualTo(new[] { "倒した数 3", "アシスト 4", "倒れた数 1", "K/D 3.00", "試合数 2", "勝った数 2" }));
            Assert.That(_panel.UnlockLines, Is.EqualTo(new[] { "レベル 3: DMR", "レベル 3: サプレッサー" }));
        }

        [Test]
        public void CancelTellsTheOwner()
        {
            Open(PlayerProfile.Empty);

            _panel.Cancel();

            Assert.That(_cancelled, Is.EqualTo(1));
        }

        [Test]
        public void DisposeEmptiesTheContainer()
        {
            Open(PlayerProfile.Empty);

            _panel.Dispose();

            Assert.That(_container.childCount, Is.Zero);
            Assert.That(_container.ClassListContains(ProfilePanel.PanelClass), Is.False);
            Assert.DoesNotThrow(() => _panel.Dispose());
            _panel.Cancel();
            Assert.That(_cancelled, Is.Zero);
        }

        [Test]
        public void RejectsNulls()
        {
            Assert.Throws<ArgumentNullException>(() => new ProfilePanel(null, Ledger(), PlayerProfile.Empty));
            Assert.Throws<ArgumentNullException>(() => new ProfilePanel(new VisualElement(), null, PlayerProfile.Empty));
            Assert.Throws<ArgumentNullException>(() => new ProfilePanel(new VisualElement(), Ledger(), null));
            Open(PlayerProfile.Empty);
            Assert.Throws<ArgumentNullException>(() => _panel.Show(null));
        }
    }
}
