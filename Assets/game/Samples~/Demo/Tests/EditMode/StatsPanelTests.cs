using System;
using Shooter.Core;
using Shooter.Runtime;
using NUnit.Framework;
using UnityEngine.UIElements;

namespace Shooter.Tests.EditMode
{
    // 期待値は Resources/Localization/ja/stats.json の文と、AchievementSpec.Default
    // (first-blood キル 1・marksman 頭 50・veteran 試合 25・victor 勝ち 10) から出す。
    public sealed class StatsPanelTests
    {
        private VisualElement _container;
        private StatsPanel _panel;
        private int _cancelled;

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
        public void ShowsTheRecordAndThePlayTime()
        {
            Open(new PlayerProfile(0, 12, 4, 8, 3, 5, 1000, 4, 2, 7384, null));

            Assert.That(_panel.TitleText, Is.EqualTo("統計"));
            Assert.That(_panel.StatLines, Is.EqualTo(new[]
            {
                "試合数 8",
                "勝った数 3",
                "勝率 38%",
                "倒した数 12",
                "アシスト 5",
                "ヘッドショット 4",
                "近接で倒した数 2",
                "遊んだ時間 2 時間 3 分",
            }));
            Assert.That(_container.ClassListContains(StatsPanel.PanelClass), Is.True);
            Assert.That(_container.Q<Button>(StatsPanel.BackButtonName).text, Is.EqualTo("戻る"));
        }

        [Test]
        public void ANewProfileHasNoWinRateAndNoTime()
        {
            Open(PlayerProfile.Empty);

            Assert.That(_panel.StatLines[2], Is.EqualTo("勝率 0%"));
            Assert.That(_panel.StatLines[7], Is.EqualTo("遊んだ時間 0 時間 0 分"));
        }

        [Test]
        public void ListsEachAchievementWithItsProgressOrAsDone()
        {
            Open(new PlayerProfile(0, 3, 0, 30, 2, 0, 1000, 7, 0, 0, new[] { "first-blood", "veteran" }));

            Assert.That(_panel.AchievementsTitleText, Is.EqualTo("実績 2/4"));
            Assert.That(_panel.AchievementLines, Is.EqualTo(new[]
            {
                "初めての 1 人: 達成",
                "名射手: 7/50",
                "古参兵: 達成",
                "勝者: 2/10",
            }));
            Assert.That(_panel.AchievementDescriptions, Is.EqualTo(new[]
            {
                "敵を 1 人倒す",
                "ヘッドショットで 50 人倒す",
                "25 試合を遊ぶ",
                "10 試合に勝つ",
            }));
            var rows = _container.Query<VisualElement>(className: StatsPanel.AchievementClass).ToList();
            Assert.That(rows.Count, Is.EqualTo(4));
            Assert.That(rows[0].ClassListContains(StatsPanel.AchievementUnlockedClass), Is.True);
            Assert.That(rows[1].ClassListContains(StatsPanel.AchievementUnlockedClass), Is.False);
        }

        [Test]
        public void AHiddenAchievementShowsNothingUntilItIsDone()
        {
            var spec = new AchievementSpec(new[]
            {
                new AchievementDefinition("secret", "achievement.victor", "achievement.victor.description", PlayerStat.MatchesWon, 1, hidden: true),
                new AchievementDefinition("plain", null, null, PlayerStat.Kills, 5),
            });

            Open(PlayerProfile.Empty, spec);

            Assert.That(_panel.AchievementLines, Is.EqualTo(new[] { "隠し実績", "plain: 0/5" }));
            Assert.That(_panel.AchievementDescriptions, Is.Empty, "a hidden achievement and one without a description show no description");

            _panel.Show(PlayerProfile.Empty.WithAchievement("secret"));

            Assert.That(_panel.AchievementLines, Is.EqualTo(new[] { "勝者: 達成", "plain: 0/5" }));
            Assert.That(_panel.AchievementDescriptions, Is.EqualTo(new[] { "10 試合に勝つ" }));
        }

        [Test]
        public void TheCountDoesNotGoPastTheTarget()
        {
            var spec = new AchievementSpec(new[] { new AchievementDefinition("raised", null, null, PlayerStat.Kills, 5) });

            Open(new PlayerProfile(0, 9, 0, 0, 0), spec);

            Assert.That(_panel.AchievementLines, Is.EqualTo(new[] { "raised: 5/5" }), "the panel shows the profile as saved; unlocking is the recorder's job");
        }

        [Test]
        public void WithoutAchievementsThePanelSaysSo()
        {
            Open(PlayerProfile.Empty, new AchievementSpec(Array.Empty<AchievementDefinition>()));

            Assert.That(_panel.AchievementsTitleText, Is.EqualTo("実績 0/0"));
            Assert.That(_panel.AchievementLines, Is.EqualTo(new[] { "実績はありません" }));
        }

        [Test]
        public void ShowRewritesTheLinesForTheNewProfile()
        {
            Open(PlayerProfile.Empty);

            _panel.Show(new PlayerProfile(0, 1, 0, 2, 1, 0, 1000, 0, 0, 59, new[] { "first-blood" }));

            Assert.That(_panel.StatLines[0], Is.EqualTo("試合数 2"));
            Assert.That(_panel.StatLines[2], Is.EqualTo("勝率 50%"));
            Assert.That(_panel.StatLines[7], Is.EqualTo("遊んだ時間 0 時間 0 分"));
            Assert.That(_panel.StatLines.Count, Is.EqualTo(8));
            Assert.That(_panel.AchievementsTitleText, Is.EqualTo("実績 1/4"));
            Assert.That(_panel.AchievementLines[0], Is.EqualTo("初めての 1 人: 達成"));
            Assert.That(_panel.AchievementLines.Count, Is.EqualTo(4));
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
            Assert.That(_container.ClassListContains(StatsPanel.PanelClass), Is.False);
            Assert.DoesNotThrow(() => _panel.Dispose());
            _panel.Cancel();
            Assert.That(_cancelled, Is.Zero);
        }

        [Test]
        public void RejectsNulls()
        {
            Assert.Throws<ArgumentNullException>(() => new StatsPanel(null, PlayerProfile.Empty, AchievementSpec.Default));
            Assert.Throws<ArgumentNullException>(() => new StatsPanel(new VisualElement(), null, AchievementSpec.Default));
            Assert.Throws<ArgumentNullException>(() => new StatsPanel(new VisualElement(), PlayerProfile.Empty, null));
            Open(PlayerProfile.Empty);
            Assert.Throws<ArgumentNullException>(() => _panel.Show(null));
        }

        [Test]
        public void TheFormatsRejectNegativeValues()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => StatsFormat.PlayTime(-1));
            Assert.Throws<ArgumentOutOfRangeException>(() => StatsFormat.WinRate(-1, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => StatsFormat.WinRate(0, -1));
            Assert.That(StatsFormat.WinRate(1, 8), Is.EqualTo("勝率 13%"), "12.5% rounds away from zero");
        }

        private void Open(PlayerProfile profile, AchievementSpec spec = null)
        {
            _panel?.Dispose();
            _panel = new StatsPanel(_container, profile, spec ?? AchievementSpec.Default);
            _panel.Cancelled += () => _cancelled++;
        }
    }
}
