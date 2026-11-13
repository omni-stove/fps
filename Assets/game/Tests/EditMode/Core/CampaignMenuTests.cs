using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace Fps.Core.Tests
{
    // Unity の EditMode テストと dotnet/Fps.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class CampaignMenuTests
    {
        private static CampaignMenu Menu(bool firstHasSave = false, bool secondHasSave = false, string preferred = null,
            CampaignDifficulty difficulty = CampaignDifficulty.Normal)
        {
            return new CampaignMenu(
                new[]
                {
                    new CampaignMenuMission("Campaign01", "Operation One", firstHasSave),
                    new CampaignMenuMission("Campaign02", "Operation Two", secondHasSave),
                },
                difficulty,
                preferred);
        }

        private static List<CampaignMenuItemKind> Kinds(CampaignMenu menu)
        {
            var kinds = new List<CampaignMenuItemKind>();
            foreach (var item in menu.Items)
            {
                kinds.Add(item.Kind);
            }

            return kinds;
        }

        [Test]
        public void AMissionWithoutASaveOffersStart()
        {
            var menu = Menu();

            Assert.That(Kinds(menu), Is.EqualTo(new[]
            {
                CampaignMenuItemKind.Mission, CampaignMenuItemKind.Mission, CampaignMenuItemKind.Difficulty,
                CampaignMenuItemKind.Start, CampaignMenuItemKind.Back,
            }));
            Assert.That(menu.SelectedMissionIndex, Is.Zero);
            Assert.That(menu.FocusedItem, Is.EqualTo(new CampaignMenuItem(CampaignMenuItemKind.Mission, 0)));
        }

        [Test]
        public void AMissionWithASaveOffersContinueAndStartOver()
        {
            var menu = Menu(firstHasSave: true);

            Assert.That(Kinds(menu), Is.EqualTo(new[]
            {
                CampaignMenuItemKind.Mission, CampaignMenuItemKind.Mission, CampaignMenuItemKind.Difficulty,
                CampaignMenuItemKind.Continue, CampaignMenuItemKind.Restart, CampaignMenuItemKind.Back,
            }));
        }

        [Test]
        public void StartsOnThePreferredMissionAndDifficulty()
        {
            var menu = Menu(preferred: "Campaign02", difficulty: CampaignDifficulty.Hard);

            Assert.That(menu.SelectedMission.Id, Is.EqualTo("Campaign02"));
            Assert.That(menu.FocusedItem, Is.EqualTo(new CampaignMenuItem(CampaignMenuItemKind.Mission, 1)));
            Assert.That(menu.Difficulty, Is.EqualTo(CampaignDifficulty.Hard));

            Assert.That(Menu(preferred: "Gone").SelectedMissionIndex, Is.Zero, "an unknown mission falls back to the first");
        }

        [Test]
        public void MovingOntoAMissionSelectsItAndSwapsTheStartItems()
        {
            var menu = Menu(secondHasSave: true);

            menu.MoveFocus(1);

            Assert.That(menu.SelectedMission.Id, Is.EqualTo("Campaign02"));
            Assert.That(menu.IndexOf(CampaignMenuItemKind.Continue), Is.GreaterThan(0));
            Assert.That(menu.IndexOf(CampaignMenuItemKind.Start), Is.EqualTo(-1));

            menu.MoveFocus(1);
            menu.MoveFocus(1);
            Assert.That(menu.FocusedItem.Kind, Is.EqualTo(CampaignMenuItemKind.Continue));

            // 先頭から上へ回り込むと「戻る」
            menu.FocusOn(0);
            menu.MoveFocus(-1);
            Assert.That(menu.FocusedItem.Kind, Is.EqualTo(CampaignMenuItemKind.Back));
            Assert.That(menu.SelectedMission.Id, Is.EqualTo("Campaign01"), "focusing the first mission selected it");
        }

        [Test]
        public void SubmitWalksFromTheMissionToTheDifficultyToTheFirstStartItem()
        {
            var menu = Menu();

            Assert.That(menu.Activate(), Is.EqualTo(CampaignMenuResultKind.None));
            Assert.That(menu.FocusedItem.Kind, Is.EqualTo(CampaignMenuItemKind.Difficulty));

            Assert.That(menu.Activate(), Is.EqualTo(CampaignMenuResultKind.None));
            Assert.That(menu.FocusedItem.Kind, Is.EqualTo(CampaignMenuItemKind.Start));

            Assert.That(menu.Activate(), Is.EqualTo(CampaignMenuResultKind.Start));
            Assert.That(menu.LastStart.Launch.MissionId, Is.EqualTo("Campaign01"));
            Assert.That(menu.LastStart.Launch.Difficulty, Is.EqualTo(CampaignDifficulty.Normal));
            Assert.That(menu.LastStart.Mode, Is.EqualTo(CampaignStartMode.FromBeginning));
            Assert.That(menu.LastStart.DeletesSave, Is.False, "there is no save to delete");
        }

        [Test]
        public void TheDifficultyCyclesAndWraps()
        {
            var menu = Menu(difficulty: CampaignDifficulty.Easy);

            Assert.That(menu.CycleDifficulty(-1), Is.True);
            Assert.That(menu.Difficulty, Is.EqualTo(CampaignDifficulty.Hard));
            menu.CycleDifficulty(1);
            menu.CycleDifficulty(1);
            Assert.That(menu.Difficulty, Is.EqualTo(CampaignDifficulty.Normal));

            Assert.That(menu.SetDifficulty(CampaignDifficulty.Easy), Is.True);
            Assert.That(menu.Activate(CampaignMenuItemKind.Start), Is.EqualTo(CampaignMenuResultKind.Start));
            Assert.That(menu.LastStart.Launch.Difficulty, Is.EqualTo(CampaignDifficulty.Easy));
        }

        [Test]
        public void ContinueStartsFromTheSaveWithoutDeletingIt()
        {
            var menu = Menu(firstHasSave: true, difficulty: CampaignDifficulty.Easy);

            Assert.That(menu.Activate(CampaignMenuItemKind.Continue), Is.EqualTo(CampaignMenuResultKind.Start));

            Assert.That(menu.LastStart.Mode, Is.EqualTo(CampaignStartMode.Continue));
            Assert.That(menu.LastStart.DeletesSave, Is.False);
            Assert.That(menu.LastStart.Launch.ToString(), Is.EqualTo("Campaign01/easy"));
        }

        [Test]
        public void StartOverAsksFirstWithCancelFocused()
        {
            var menu = Menu(firstHasSave: true);

            Assert.That(menu.Activate(CampaignMenuItemKind.Restart), Is.EqualTo(CampaignMenuResultKind.None));

            Assert.That(menu.IsConfirmingRestart, Is.True);
            Assert.That(Kinds(menu), Is.EqualTo(new[] { CampaignMenuItemKind.ConfirmRestart, CampaignMenuItemKind.CancelRestart }));
            Assert.That(menu.FocusedItem.Kind, Is.EqualTo(CampaignMenuItemKind.CancelRestart), "a stray press does not delete the save");
            Assert.That(menu.LastStart, Is.Null);
            Assert.That(menu.SelectMission(1), Is.False, "the mission cannot change under the question");
            Assert.That(menu.CycleDifficulty(1), Is.False);
            Assert.That(menu.SetDifficulty(CampaignDifficulty.Hard), Is.False);
        }

        [Test]
        public void ConfirmingStartOverDeletesTheSave()
        {
            var menu = Menu(firstHasSave: true, difficulty: CampaignDifficulty.Hard);
            menu.Activate(CampaignMenuItemKind.Restart);

            Assert.That(menu.Activate(CampaignMenuItemKind.ConfirmRestart), Is.EqualTo(CampaignMenuResultKind.Start));

            Assert.That(menu.LastStart.Mode, Is.EqualTo(CampaignStartMode.FromBeginning));
            Assert.That(menu.LastStart.DeletesSave, Is.True);
            Assert.That(menu.LastStart.Launch.Difficulty, Is.EqualTo(CampaignDifficulty.Hard));
            Assert.That(menu.IsConfirmingRestart, Is.False);
            Assert.That(menu.FocusedItem.Kind, Is.EqualTo(CampaignMenuItemKind.Restart));
        }

        [Test]
        public void CancellingTheQuestionGoesBackToStartOver()
        {
            var menu = Menu(firstHasSave: true);
            menu.Activate(CampaignMenuItemKind.Restart);

            Assert.That(menu.Cancel(), Is.EqualTo(CampaignMenuResultKind.None));
            Assert.That(menu.IsConfirmingRestart, Is.False);
            Assert.That(menu.FocusedItem.Kind, Is.EqualTo(CampaignMenuItemKind.Restart));

            menu.Activate(CampaignMenuItemKind.Restart);
            Assert.That(menu.Activate(CampaignMenuItemKind.CancelRestart), Is.EqualTo(CampaignMenuResultKind.None));
            Assert.That(menu.IsConfirmingRestart, Is.False);
            Assert.That(menu.LastStart, Is.Null);

            Assert.That(menu.Cancel(), Is.EqualTo(CampaignMenuResultKind.Back), "cancel outside the question leaves the campaign menu");
            Assert.That(menu.Activate(CampaignMenuItemKind.Back), Is.EqualTo(CampaignMenuResultKind.Back));
        }

        [Test]
        public void ItemsThatAreNotShownCannotBeActivated()
        {
            var menu = Menu();

            Assert.That(menu.Activate(CampaignMenuItemKind.Continue), Is.EqualTo(CampaignMenuResultKind.None));
            Assert.That(menu.Activate(CampaignMenuItemKind.ConfirmRestart), Is.EqualTo(CampaignMenuResultKind.None));
            Assert.That(menu.LastStart, Is.Null);
            Assert.That(menu.FocusOn(99), Is.False);
        }

        [Test]
        public void ForgettingTheSaveSwapsContinueForStart()
        {
            var menu = Menu(firstHasSave: true);
            menu.Activate(CampaignMenuItemKind.Restart);

            Assert.That(menu.ForgetSave("Campaign01"), Is.True);

            Assert.That(menu.SelectedMission.HasSave, Is.False);
            Assert.That(menu.IsConfirmingRestart, Is.False);
            Assert.That(menu.IndexOf(CampaignMenuItemKind.Continue), Is.EqualTo(-1));
            Assert.That(menu.FocusedItem.Kind, Is.EqualTo(CampaignMenuItemKind.Start));
            Assert.That(menu.ForgetSave("Gone"), Is.False);
        }

        [Test]
        public void RejectsBadMissionLists()
        {
            Assert.Throws<ArgumentNullException>(() => new CampaignMenu(null, CampaignDifficulty.Normal));
            Assert.Throws<ArgumentException>(() => new CampaignMenu(Array.Empty<CampaignMenuMission>(), CampaignDifficulty.Normal));
            Assert.Throws<ArgumentException>(() => new CampaignMenu(new CampaignMenuMission[] { null }, CampaignDifficulty.Normal));
            Assert.Throws<ArgumentException>(() => new CampaignMenu(
                new[] { new CampaignMenuMission("A", "A", false), new CampaignMenuMission("A", "B", false) },
                CampaignDifficulty.Normal));
            Assert.Throws<ArgumentOutOfRangeException>(() => new CampaignMenu(
                new[] { new CampaignMenuMission("A", "A", false) }, (CampaignDifficulty)42));
            Assert.Throws<ArgumentException>(() => new CampaignMenuMission("bad id", "A", false));
            Assert.Throws<ArgumentException>(() => new CampaignMenuMission("A", " ", false));
        }

        [Test]
        public void OnlyAStartFromTheBeginningDeletesTheSave()
        {
            var launch = new CampaignLaunch("A", CampaignDifficulty.Normal);

            Assert.Throws<ArgumentException>(() => new CampaignMenuStart(launch, CampaignStartMode.Continue, true));
            Assert.Throws<ArgumentNullException>(() => new CampaignMenuStart(null, CampaignStartMode.Continue, false));
        }

        [Test]
        public void EveryStartItemHasALabel()
        {
            foreach (CampaignMenuItemKind kind in Enum.GetValues(typeof(CampaignMenuItemKind)))
            {
                if (kind == CampaignMenuItemKind.Mission || kind == CampaignMenuItemKind.Difficulty)
                {
                    continue;
                }

                Assert.That(CampaignMenuFormat.ItemLabel(kind), Is.Not.Empty, kind.ToString());
            }

            Assert.That(CampaignMenuFormat.DifficultyLabel(CampaignDifficulty.Easy), Is.EqualTo("EASY"));
            Assert.That(CampaignMenuFormat.ConfirmRestartQuestion("Operation One"), Does.Contain("Operation One"));
        }
    }
}
