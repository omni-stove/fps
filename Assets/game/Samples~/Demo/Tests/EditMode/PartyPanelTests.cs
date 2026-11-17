using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Shooter.Core;
using Shooter.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace Shooter.Tests.EditMode
{
    public sealed class PartyPanelTests
    {
        private VisualElement _container;
        private PartyPanel _panel;
        private FakePartyControl _control;
        private int _cancelled;

        [SetUp]
        public void SetUp()
        {
            _container = new VisualElement();
            _control = new FakePartyControl();
            _cancelled = 0;
            _panel = null;
        }

        [TearDown]
        public void TearDown()
        {
            _panel?.Dispose();
            GameSignals.ResetAll();
        }

        private void Open()
        {
            _panel = new PartyPanel(_container);
            _panel.Cancelled += () => _cancelled++;
        }

        private static PartyStatus InParty(bool isLocalLeader, string inviteCode, params PartyMember[] members)
        {
            return new PartyStatus(PartyStage.InParty, members, inviteCode, isLocalLeader);
        }

        [Test]
        public void OutsideAPartyNoMembersAreListedAndOnlyCreateAndJoinCanBePressed()
        {
            GameSignals.PartyControl.Publish(_control);
            GameSignals.Party.Publish(new PartyStatus(PartyStage.None, null, null, false));
            Open();

            Assert.That(_panel.StageText, Is.EqualTo("パーティに入っていない"));
            Assert.That(_panel.MemberNames, Is.Empty);
            Assert.That(_container.Q<VisualElement>(PartyPanel.NoMembersName).style.display.value, Is.EqualTo(DisplayStyle.Flex));
            Assert.That(_panel.InviteCode, Is.Empty);
            Assert.That(_panel.IsLeaderNoteShown, Is.False);
            Assert.That(_panel.CreateButton.enabledSelf, Is.True);
            Assert.That(_panel.JoinButton.enabledSelf, Is.True);
            Assert.That(_panel.CodeField.enabledSelf, Is.True);
            Assert.That(_panel.LeaveButton.enabledSelf, Is.False);
            Assert.That(_panel.Notice, Is.Null);
        }

        [Test]
        public void TheMembersOfThePartyStatusAreListedWithTheLeaderMarked()
        {
            GameSignals.PartyControl.Publish(_control);
            GameSignals.Party.Publish(InParty(false, "AbC-123", new PartyMember("Alice", true), new PartyMember("Bob", false)));
            Open();

            Assert.That(_panel.StageText, Is.EqualTo("パーティ: 2 人"));
            Assert.That(_panel.MemberNames, Is.EqualTo(new[] { "Alice", "Bob" }));
            Assert.That(_panel.MemberRows[0].ClassListContains(PartyPanel.LeaderMemberClass), Is.True);
            Assert.That(_panel.MemberRows[1].ClassListContains(PartyPanel.LeaderMemberClass), Is.False);
            Assert.That(Badges(0), Is.EqualTo(new[] { "リーダー" }));
            Assert.That(Badges(1), Is.Empty);
            Assert.That(_container.Q<VisualElement>(PartyPanel.NoMembersName).style.display.value, Is.EqualTo(DisplayStyle.None));
            Assert.That(_panel.InviteCode, Is.EqualTo("AbC-123"));
            Assert.That(_panel.IsLeaderNoteShown, Is.False, "the local player is not the leader");
        }

        [Test]
        public void ThePlayersNameIsNotReadAsRichText()
        {
            GameSignals.PartyControl.Publish(_control);
            GameSignals.Party.Publish(InParty(false, null, new PartyMember("<b>Eve</b>", true)));
            Open();

            var name = _panel.MemberRows[0].Q<Label>(className: PartyPanel.MemberNameClass);
            Assert.That(name.text, Is.EqualTo("<b>Eve</b>"));
            Assert.That(name.enableRichText, Is.False);
        }

        [Test]
        public void ANewStatusRewritesTheListAndTheButtons()
        {
            GameSignals.PartyControl.Publish(_control);
            Open();
            Assert.That(_panel.MemberNames, Is.Empty, "no status yet reads as outside a party");
            Assert.That(_panel.StageText, Is.EqualTo("パーティに入っていない"));

            GameSignals.Party.Publish(new PartyStatus(PartyStage.Joining, null, null, false));
            Assert.That(_panel.StageText, Is.EqualTo("パーティに入っている途中…"));
            Assert.That(_panel.CreateButton.enabledSelf, Is.False);
            Assert.That(_panel.JoinButton.enabledSelf, Is.False);
            Assert.That(_panel.LeaveButton.enabledSelf, Is.True, "joining can be called off");

            GameSignals.Party.Publish(InParty(true, "P1", new PartyMember("Me", true)));
            Assert.That(_panel.MemberNames, Is.EqualTo(new[] { "Me" }));
            Assert.That(_panel.IsLeaderNoteShown, Is.True);

            GameSignals.Party.Publish(InParty(false, "P1", new PartyMember("Me", false), new PartyMember("Friend", true)));
            Assert.That(_panel.MemberNames, Is.EqualTo(new[] { "Me", "Friend" }));
            Assert.That(_panel.MemberRows[1].ClassListContains(PartyPanel.LeaderMemberClass), Is.True);
            Assert.That(_panel.IsLeaderNoteShown, Is.False);

            GameSignals.Party.Publish(new PartyStatus(PartyStage.Searching, new[] { new PartyMember("Friend", true) }, "P1", false));
            Assert.That(_panel.StageText, Is.EqualTo("試合を探している…"));
            Assert.That(_panel.MemberNames, Is.EqualTo(new[] { "Friend" }));

            GameSignals.Party.Clear();
            Assert.That(_panel.MemberNames, Is.Empty);
            Assert.That(_panel.InviteCode, Is.Empty);
            Assert.That(_panel.CreateButton.enabledSelf, Is.True);
            Assert.That(_panel.LeaveButton.enabledSelf, Is.False);
        }

        [Test]
        public void CreateCallsThePartyControl()
        {
            GameSignals.PartyControl.Publish(_control);
            Open();

            Assert.That(_panel.Create(), Is.True);

            Assert.That(_control.Calls, Is.EqualTo(new[] { "Create" }));
        }

        [Test]
        public void JoinCallsThePartyControlWithTheTrimmedCode()
        {
            GameSignals.PartyControl.Publish(_control);
            Open();
            _panel.CodeField.value = "  AbC-123_x  ";

            Assert.That(_panel.Join(), Is.True);

            Assert.That(_control.Calls, Is.EqualTo(new[] { "Join:AbC-123_x" }));
            Assert.That(_panel.Notice, Is.Null);
        }

        [Test]
        public void AnEmptyCodeIsNotSentAndAsksForOne()
        {
            GameSignals.PartyControl.Publish(_control);
            Open();
            _panel.CodeField.value = "   ";

            Assert.That(_panel.Join(), Is.False);

            Assert.That(_control.Calls, Is.Empty);
            Assert.That(_panel.Notice, Is.EqualTo("招待のコードを入れる"));
            Assert.That(_panel.IsNoticeError, Is.True);
        }

        [Test]
        public void ACodeWithCharactersAPartyIdCannotHoldIsNotSent()
        {
            GameSignals.PartyControl.Publish(_control);
            Open();
            _panel.CodeField.value = "ab cd";

            Assert.That(_panel.Join(), Is.False);

            Assert.That(_control.Calls, Is.Empty);
            Assert.That(_panel.Notice, Is.EqualTo("招待のコードは 1〜64 文字の英数字・「-」・「_」"));

            _panel.CodeField.value = "abcd";
            Assert.That(_panel.Join(), Is.True);
            Assert.That(_panel.Notice, Is.Null, "a call that went through clears the notice");
        }

        [Test]
        public void TheCodeFieldTakesNoMoreThanAPartyId()
        {
            Open();

            Assert.That(_panel.CodeField.maxLength, Is.EqualTo(ConnectionExtras.MaxPartyIdLength));
        }

        [Test]
        public void LeaveCallsThePartyControlOnlyInsideAParty()
        {
            GameSignals.PartyControl.Publish(_control);
            Open();

            Assert.That(_panel.Leave(), Is.False, "nothing to leave outside a party");
            Assert.That(_control.Calls, Is.Empty);

            GameSignals.Party.Publish(InParty(true, "P1", new PartyMember("Me", true)));
            Assert.That(_panel.Create(), Is.False, "a second party cannot be made while in one");
            Assert.That(_panel.Leave(), Is.True);

            Assert.That(_control.Calls, Is.EqualTo(new[] { "Leave" }));
        }

        [Test]
        public void WithoutAPartyControlNothingCanBePressedAndItSaysSo()
        {
            GameSignals.Party.Publish(InParty(false, "P1", new PartyMember("Alice", true)));
            Open();

            Assert.That(_panel.CreateButton.enabledSelf, Is.False);
            Assert.That(_panel.JoinButton.enabledSelf, Is.False);
            Assert.That(_panel.LeaveButton.enabledSelf, Is.False);
            Assert.That(_panel.BackButton.enabledSelf, Is.True);
            Assert.That(_panel.Notice, Is.EqualTo("ここではパーティを使えない"));
            Assert.That(_panel.IsNoticeError, Is.False);
            Assert.That(_panel.Leave(), Is.False);
            Assert.That(_panel.MemberNames, Is.EqualTo(new[] { "Alice" }), "the status is still shown");

            GameSignals.PartyControl.Publish(_control);
            Assert.That(_panel.Notice, Is.Null);
            Assert.That(_panel.LeaveButton.enabledSelf, Is.True);
            Assert.That(_panel.Leave(), Is.True);
            Assert.That(_control.Calls, Is.EqualTo(new[] { "Leave" }));
        }

        [Test]
        public void AControlThatThrowsIsLoggedAndShownAsAFailure()
        {
            _control.Throw = new InvalidOperationException("the party session is already Joining");
            GameSignals.PartyControl.Publish(_control);
            Open();

            LogAssert.Expect(LogType.Error, new Regex(@"^\[Party\] could not create the party: the party session is already Joining"));
            LogAssert.Expect(LogType.Exception, new Regex("already Joining"));
            Assert.That(_panel.Create(), Is.False);

            Assert.That(_panel.Notice, Is.EqualTo("うまくいかなかった。もう一度試す"));
            Assert.That(_panel.IsNoticeError, Is.True);
        }

        [Test]
        public void TheStaticTextsComeFromTheTable()
        {
            Open();

            Assert.That(_panel.CreateButton.text, Is.EqualTo("パーティを作る"));
            Assert.That(_panel.JoinButton.text, Is.EqualTo("入る"));
            Assert.That(_panel.LeaveButton.text, Is.EqualTo("パーティを出る"));
            Assert.That(_panel.BackButton.text, Is.EqualTo("戻る"));
            Assert.That(_panel.CodeField.textEdition.placeholder, Is.EqualTo("招待のコード"));
        }

        [Test]
        public void BackCancelsThePanel()
        {
            Open();

            _panel.Cancel();

            Assert.That(_cancelled, Is.EqualTo(1));
        }

        [Test]
        public void AfterDisposingTheSignalsNoLongerReachThePanel()
        {
            GameSignals.PartyControl.Publish(_control);
            Open();
            _panel.Dispose();

            GameSignals.Party.Publish(InParty(false, "P1", new PartyMember("Alice", true)));

            Assert.That(_container.childCount, Is.EqualTo(0));
            Assert.That(_panel.MemberNames, Is.Empty);
            Assert.That(_panel.Create(), Is.False);
            _panel.Cancel();
            Assert.That(_cancelled, Is.EqualTo(0));
            Assert.That(_control.Calls, Is.Empty);
        }

        [Test]
        public void EveryPartyKeyIsInBothLanguages()
        {
            var keys = new[]
            {
                "party.title", "party.youAreLeader", "party.inviteCode", "party.members", "party.noMembers",
                "party.create", "party.join", "party.leave", "party.back",
                PartyPanel.StageNoneKey, PartyPanel.StageInPartyKey, PartyPanel.StageSearchingKey, PartyPanel.StageJoiningKey,
                PartyPanel.LeaderKey, PartyPanel.CodePlaceholderKey, PartyPanel.UnavailableKey,
                PartyPanel.CodeRequiredKey, PartyPanel.InvalidCodeKey, PartyPanel.FailedKey,
            };

            foreach (var language in new[] { GameLanguage.Japanese, GameLanguage.English })
            {
                var table = Localizer.TableOf(language);
                foreach (var key in keys)
                {
                    Assert.That(table.Contains(key), Is.True, language + " " + key);
                }
            }
        }

        private string[] Badges(int row)
        {
            return _panel.MemberRows[row].Query<Label>(className: PartyPanel.LeaderBadgeClass).ToList().Select(label => label.text).ToArray();
        }

        private sealed class FakePartyControl : IPartyControl
        {
            public readonly List<string> Calls = new List<string>();

            public event Action<PartyStatus> StatusChanged
            {
                add { }
                remove { }
            }

            public Exception Throw { get; set; }

            public PartyStatus Status => default;

            public void Create()
            {
                Record("Create");
            }

            public void Join(string inviteCode)
            {
                Record("Join:" + inviteCode);
            }

            public void Leave()
            {
                Record("Leave");
            }

            private void Record(string call)
            {
                if (Throw != null)
                {
                    throw Throw;
                }

                Calls.Add(call);
            }
        }
    }
}
