using System;
using System.Collections.Generic;
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

        private static readonly string[] _nameKeys = { "progression.nameTitle", "progression.nameHint", "progression.nameRejected" };

        private VisualElement _container;
        private ProfilePanel _panel;
        private PlayerNameStore _names;
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

        private void Open(PlayerProfile profile, ProgressionLedger ledger = null, PlayerNameStore names = null)
        {
            _panel?.Dispose();
            _panel = new ProfilePanel(_container, ledger ?? Ledger(), profile, Weapons(), Items(), names);
            _panel.Cancelled += () => _cancelled++;
        }

        [SetUp]
        public void SetUp()
        {
            _container = new VisualElement();
            _cancelled = 0;
            _panel = null;

            // 本物の名前 (PlayerNameStore.DefaultKey) を壊さないよう、テストごとに別のキーを使う
            _names = new PlayerNameStore("Shooter.Tests.ProfileName." + Guid.NewGuid().ToString("N"));
        }

        [TearDown]
        public void TearDown()
        {
            _panel?.Dispose();
            _names.Delete();
            GameSignals.Account.Clear();
            GameSignals.AccountControl.Clear();
        }

        private static FakeAccountControl PublishAccount(AccountKind kind, AccountStage stage = AccountStage.Idle,
            AccountSignInFailure failure = AccountSignInFailure.None, bool epic = true)
        {
            var control = new FakeAccountControl { Status = new AccountStatus(kind, stage, failure, epic) };
            GameSignals.AccountControl.Publish(control);
            GameSignals.Account.Publish(control.Status);
            return control;
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
        public void WithoutASignInServiceTheAccountSectionIsHidden()
        {
            Open(PlayerProfile.Empty);

            Assert.That(_panel.IsAccountShown, Is.False);
            Assert.That(_panel.PressAccount(), Is.False);
        }

        [Test]
        public void OnTheDeviceIdItOffersTheEpicAccount()
        {
            var control = PublishAccount(AccountKind.Device);
            Open(PlayerProfile.Empty);

            Assert.That(_panel.IsAccountShown, Is.True);
            Assert.That(_container.Q<VisualElement>(ProfilePanel.AccountName).Q<Label>(className: ProfilePanel.SectionTitleClass).text, Is.EqualTo("アカウント"));
            Assert.That(_panel.AccountStatusText, Is.EqualTo("この端末のアカウントで遊んでいます。セーブはこの端末の分だけ同期します"));
            Assert.That(_panel.AccountNoticeText, Is.Null);
            Assert.That(_panel.IsAccountButtonShown, Is.True);
            Assert.That(_panel.AccountButton.text, Is.EqualTo("Epic アカウントでサインイン"));
            Assert.That(_panel.AccountButton.enabledSelf, Is.True);

            Assert.That(_panel.PressAccount(), Is.True);

            Assert.That(control.Calls, Is.EqualTo(new[] { "sign-in" }));
        }

        [Test]
        public void OnAnEpicAccountItOffersSigningOut()
        {
            var control = PublishAccount(AccountKind.Epic);
            Open(PlayerProfile.Empty);

            Assert.That(_panel.AccountStatusText, Is.EqualTo("Epic アカウントでサインインしています。セーブを他の端末と同期します"));
            Assert.That(_panel.AccountButton.text, Is.EqualTo("サインアウト"));

            _panel.PressAccount();

            Assert.That(control.Calls, Is.EqualTo(new[] { "sign-out" }));
        }

        [Test]
        public void WhileSigningInTheButtonCannotBePressed()
        {
            var control = PublishAccount(AccountKind.Device, AccountStage.SigningIn);
            Open(PlayerProfile.Empty);

            Assert.That(_panel.AccountStatusText, Is.EqualTo("サインインしています…"));
            Assert.That(_panel.AccountButton.enabledSelf, Is.False);
            Assert.That(_panel.PressAccount(), Is.False);
            Assert.That(control.Calls, Is.Empty);
        }

        [Test]
        public void ACancelledOrFailedSignInIsShownUnderTheStatus()
        {
            PublishAccount(AccountKind.Device, failure: AccountSignInFailure.Cancelled);
            Open(PlayerProfile.Empty);
            Assert.That(_panel.AccountNoticeText, Is.EqualTo("サインインを取りやめました"));

            GameSignals.Account.Publish(new AccountStatus(AccountKind.Device, AccountStage.Idle, AccountSignInFailure.Failed, true));

            Assert.That(_panel.AccountNoticeText, Is.EqualTo("サインインできませんでした。時間をおいてもう一度試してください"));
        }

        [Test]
        public void TheSectionFollowsTheSignInWhileOpen()
        {
            PublishAccount(AccountKind.None, AccountStage.SigningIn);
            Open(PlayerProfile.Empty);

            GameSignals.Account.Publish(new AccountStatus(AccountKind.Epic, AccountStage.Idle, AccountSignInFailure.None, true));
            Assert.That(_panel.AccountStatusText, Is.EqualTo("Epic アカウントでサインインしています。セーブを他の端末と同期します"));
            Assert.That(_panel.AccountButton.text, Is.EqualTo("サインアウト"));

            GameSignals.AccountControl.Clear();
            Assert.That(_panel.IsAccountShown, Is.False);
        }

        [Test]
        public void WithoutEpicAccountsOnlyTheStatusIsShown()
        {
            PublishAccount(AccountKind.Device, epic: false);
            Open(PlayerProfile.Empty);

            Assert.That(_panel.IsAccountShown, Is.True);
            Assert.That(_panel.IsAccountButtonShown, Is.False);
            Assert.That(_panel.PressAccount(), Is.False);
        }

        [Test]
        public void AfterDisposeTheSignInIsNotFollowed()
        {
            var control = PublishAccount(AccountKind.Device);
            Open(PlayerProfile.Empty);
            _panel.Dispose();

            Assert.DoesNotThrow(() => GameSignals.Account.Publish(new AccountStatus(AccountKind.Epic, AccountStage.Idle, AccountSignInFailure.None, true)));
            Assert.That(_panel.PressAccount(), Is.False);
            Assert.That(control.Calls, Is.Empty);
        }

        [Test]
        public void WithoutANameStoreTheNameSectionIsHidden()
        {
            Open(PlayerProfile.Empty);

            Assert.That(_panel.IsNameShown, Is.False);
            Assert.That(_panel.CommitName(), Is.False);
            _panel.Cancel();
            Assert.That(_cancelled, Is.EqualTo(1));
        }

        [Test]
        public void TheNameSectionShowsTheSavedNameWithItsTitleAndHint()
        {
            _names.TrySave("Ace");

            Open(PlayerProfile.Empty, names: _names);

            Assert.That(_panel.IsNameShown, Is.True);
            Assert.That(_container.Q<TextField>(ProfilePanel.NameFieldName), Is.SameAs(_panel.NameField));
            Assert.That(_panel.NameField.value, Is.EqualTo("Ace"));
            Assert.That(_panel.NameField.maxLength, Is.EqualTo(16));
            Assert.That(_panel.NameTitleText, Is.EqualTo("名前"));
            Assert.That(_panel.NameHintText, Is.EqualTo("名前を入力 (16 文字まで)"));
            Assert.That(_panel.NameNoticeText, Is.Null);
        }

        [Test]
        public void WithoutASavedNameTheFieldStartsEmpty()
        {
            Open(PlayerProfile.Empty, names: _names);

            Assert.That(_panel.NameField.value, Is.EqualTo(string.Empty));
            Assert.That(_panel.NameHintText, Is.EqualTo("名前を入力 (16 文字まで)"));
        }

        [Test]
        public void ClosingSavesTheTypedNameAndItIsShownNextTime()
        {
            Open(PlayerProfile.Empty, names: _names);
            _panel.NameField.value = "  Bob  ";

            _panel.Cancel();

            Assert.That(_cancelled, Is.EqualTo(1));
            Assert.That(_names.Load(), Is.EqualTo("Bob"));

            Open(PlayerProfile.Empty, names: new PlayerNameStore(_names.Key));
            Assert.That(_panel.NameField.value, Is.EqualTo("Bob"));
        }

        [Test]
        public void CommittingTrimsTheField()
        {
            Open(PlayerProfile.Empty, names: _names);
            _panel.NameField.value = " Bob ";

            Assert.That(_panel.CommitName(), Is.True);

            Assert.That(_panel.NameField.value, Is.EqualTo("Bob"));
            Assert.That(_names.Load(), Is.EqualTo("Bob"));
        }

        [Test]
        public void ARefusedNameIsNotSavedAndTheReasonIsShownUntilAGoodOne()
        {
            _names.TrySave("Ace");
            Open(PlayerProfile.Empty, names: _names);
            _panel.NameField.value = "Ace" + (char)0x202E + "Bob";

            Assert.That(_panel.CommitName(), Is.False);

            Assert.That(_names.Load(), Is.EqualTo("Ace"));
            Assert.That(_panel.NameNoticeText, Is.EqualTo("この名前は使えません。改行や制御文字を除いて 16 文字以内にしてください"));
            Assert.That(_container.Q<Label>(ProfilePanel.NameNoticeName).text, Is.EqualTo(_panel.NameNoticeText));

            _panel.NameField.value = "Bob";

            Assert.That(_panel.CommitName(), Is.True);
            Assert.That(_names.Load(), Is.EqualTo("Bob"));
            Assert.That(_panel.NameNoticeText, Is.Null);
        }

        [Test]
        public void ARefusedNameStillClosesAndKeepsTheOldName()
        {
            _names.TrySave("Ace");
            Open(PlayerProfile.Empty, names: _names);
            _panel.NameField.value = "Ace\tBob";

            _panel.Cancel();

            Assert.That(_cancelled, Is.EqualTo(1));
            Assert.That(_names.Load(), Is.EqualTo("Ace"));
        }

        [Test]
        public void TheRefusalSurvivesARefresh()
        {
            Open(PlayerProfile.Empty, names: _names);
            _panel.NameField.value = "Ace\tBob";
            _panel.CommitName();

            _panel.Show(new PlayerProfile(600, 0, 0, 0, 0));

            Assert.That(_panel.NameNoticeText, Is.EqualTo("この名前は使えません。改行や制御文字を除いて 16 文字以内にしてください"));
            Assert.That(_panel.NameField.value, Is.EqualTo("Ace\tBob"), "a refresh does not throw away what was typed");
        }

        [Test]
        public void AnEmptyNameForgetsTheSavedOne()
        {
            _names.TrySave("Ace");
            Open(PlayerProfile.Empty, names: _names);
            _panel.NameField.value = "   ";

            _panel.Cancel();

            Assert.That(UnityEngine.PlayerPrefs.HasKey(_names.Key), Is.False);
            Assert.That(_names.Load(), Is.EqualTo(string.Empty));
            Assert.That(_panel.NameNoticeText, Is.Null);
        }

        [Test]
        public void AnUnchangedNameIsNotWrittenAgain()
        {
            _names.TrySave("Ace");
            var saves = 0;
            void Count(string key, string name) => saves++;
            PlayerNameStore.Saved += Count;
            try
            {
                Open(PlayerProfile.Empty, names: _names);
                _panel.Cancel();
            }
            finally
            {
                PlayerNameStore.Saved -= Count;
            }

            Assert.That(saves, Is.Zero);
            Assert.That(_cancelled, Is.EqualTo(1));
        }

        [Test]
        public void TheFieldUsesTheStoresLimit()
        {
            var names = new PlayerNameStore(_names.Key, 8);
            Open(PlayerProfile.Empty, names: names);

            Assert.That(_panel.NameField.maxLength, Is.EqualTo(8));
            Assert.That(_panel.NameHintText, Is.EqualTo("名前を入力 (8 文字まで)"));
        }

        [Test]
        public void AfterDisposeTheNameIsNotSaved()
        {
            Open(PlayerProfile.Empty, names: _names);
            _panel.NameField.value = "Bob";
            _panel.Dispose();

            Assert.That(_panel.CommitName(), Is.False);
            Assert.That(_names.Load(), Is.EqualTo(string.Empty));
        }

        [Test]
        public void TheNameTextsAreInBothLanguages()
        {
            foreach (var language in new[] { GameLanguage.Japanese, GameLanguage.English })
            {
                var table = Localizer.TableOf(language);
                foreach (var key in _nameKeys)
                {
                    Assert.That(table.Get(key), Is.Not.EqualTo(key), language + " " + key);
                }
            }

            var english = Localizer.TableOf(GameLanguage.English);
            Assert.That(english.Get("progression.nameTitle"), Is.EqualTo("Name"));
            Assert.That(english.Get("progression.nameHint"), Is.EqualTo("Enter a name (up to {0} characters)"));
            Assert.That(english.Get("progression.nameRejected"),
                Is.EqualTo("This name can't be used. Use up to {0} characters without line breaks or control characters"));
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

        private sealed class FakeAccountControl : IAccountControl
        {
            public List<string> Calls { get; } = new List<string>();

            public AccountStatus Status { get; set; }

            public bool SignInWithEpic()
            {
                Calls.Add("sign-in");
                return true;
            }

            public bool SignOut()
            {
                Calls.Add("sign-out");
                return true;
            }
        }
    }
}
