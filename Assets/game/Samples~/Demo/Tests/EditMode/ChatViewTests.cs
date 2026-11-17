using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Shooter.Core;
using Shooter.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace Shooter.Tests.EditMode
{
    public sealed class ChatViewTests
    {
        private const int AliceId = 1;
        private const int BobId = 2;

        private Hud _hud;
        private ChatView _view;
        private InputActionAsset _actions;

        [SetUp]
        public void SetUp()
        {
            GameSignals.Participants.Publish(new FakeParticipants(
                new Participant(AliceId, "Alice", Team.Alpha, true),
                new Participant(BobId, "Bob", Team.Bravo, false)));
            _hud = Hud.Create("ChatTests.Hud");
            _view = _hud.Chat;
            Assert.That(_view, Is.Not.Null, "Hud.AddViews attaches the chat");
        }

        [TearDown]
        public void TearDown()
        {
            if (_view != null)
            {
                _view.Actions = null;
            }

            GameSignals.ResetAll();
            if (_hud != null)
            {
                var settings = _hud.OwnedPanelSettings;
                Object.DestroyImmediate(_hud.gameObject);
                if (settings != null)
                {
                    Object.DestroyImmediate(settings);
                }

                _hud = null;
            }

            if (_actions != null)
            {
                _actions.Disable();
                Object.DestroyImmediate(_actions);
                _actions = null;
            }
        }

        [Test]
        public void TheViewIsHiddenUntilALineArrives()
        {
            Assert.That(_view.Element, Is.Not.Null, "Hud.uxml has the chat element");
            Assert.That(_view.Element.name, Is.EqualTo(ChatView.UxmlName));
            Assert.That(_view.Lines, Is.Empty);
            Assert.That(_view.Element.style.display.value, Is.EqualTo(DisplayStyle.None));
            Assert.That(_view.IsOpen, Is.False);
        }

        [Test]
        public void PublishedLinesShowWithTheSendersNameAndTeamLinesAreColoured()
        {
            GameSignals.Chat.Publish(new[]
            {
                new ChatLine(AliceId, ChatChannel.All, "hello", 1d),
                new ChatLine(BobId, ChatChannel.Team, "push B", 2d),
            });

            Assert.That(_view.Lines, Is.EqualTo(new[] { "Alice: hello", "[チーム] Bob: push B" }));
            Assert.That(RowTexts(), Is.EqualTo(_view.Lines));
            Assert.That(_view.IsTeamLine(0), Is.False);
            Assert.That(_view.IsTeamLine(1), Is.True);
            Assert.That(Rows()[0].ClassListContains(ChatView.TeamRowClass), Is.False);
            Assert.That(Rows()[1].ClassListContains(ChatView.TeamRowClass), Is.True);
            Assert.That(_view.Element.style.display.value, Is.EqualTo(DisplayStyle.Flex));
        }

        [Test]
        public void ASenderMissingFromTheParticipantsIsShownByTheirId()
        {
            GameSignals.Chat.Publish(new[] { new ChatLine(7, ChatChannel.All, "hi", 0d) });

            Assert.That(_view.Lines, Is.EqualTo(new[] { "?7: hi" }));
        }

        [Test]
        public void ThePlayersTextIsNotReadAsRichText()
        {
            GameSignals.Chat.Publish(new[] { new ChatLine(AliceId, ChatChannel.All, "<color=red>{0}</color>", 0d) });

            Assert.That(_view.Lines, Is.EqualTo(new[] { "Alice: <color=red>{0}</color>" }));
            Assert.That(Rows()[0].enableRichText, Is.False);
        }

        [Test]
        public void OnlyTheMostRecentLinesAreShown()
        {
            _view.VisibleLines = 2;

            GameSignals.Chat.Publish(new[]
            {
                new ChatLine(AliceId, ChatChannel.All, "one", 1d),
                new ChatLine(BobId, ChatChannel.All, "two", 2d),
                new ChatLine(AliceId, ChatChannel.Team, "three", 3d),
            });

            Assert.That(_view.Lines, Is.EqualTo(new[] { "Bob: two", "[チーム] Alice: three" }));
            Assert.That(_view.IsTeamLine(1), Is.True);
        }

        [Test]
        public void LinesFadeAfterAWhileAndALaterLineStartsItsOwnWait()
        {
            _view.FadeSeconds = 5f;
            var first = new ChatLine(AliceId, ChatChannel.All, "first", 1d);
            GameSignals.Chat.Publish(new[] { first });

            _view.Advance(4.9f);
            Assert.That(_view.IsFadedLine(0), Is.False);

            GameSignals.Chat.Publish(new[] { first, new ChatLine(BobId, ChatChannel.All, "second", 6d) });
            _view.Advance(0.2f);

            Assert.That(_view.IsFadedLine(0), Is.True, "the first line keeps the time it arrived when the list is published again");
            Assert.That(_view.IsFadedLine(1), Is.False);
            Assert.That(Rows()[0].ClassListContains(ChatView.FadedRowClass), Is.True);
            Assert.That(Rows()[1].ClassListContains(ChatView.FadedRowClass), Is.False);
        }

        [Test]
        public void OpeningTheInputBringsFadedLinesBack()
        {
            _view.FadeSeconds = 1f;
            _view.Sender = (channel, text) => { };
            GameSignals.Chat.Publish(new[] { new ChatLine(AliceId, ChatChannel.All, "old", 0d) });
            _view.Advance(2f);
            Assert.That(_view.IsFadedLine(0), Is.True);

            _view.Open(ChatChannel.All);
            Assert.That(_view.IsFadedLine(0), Is.False);

            _view.Cancel();
            Assert.That(_view.IsFadedLine(0), Is.True);
        }

        [Test]
        public void SubmittingTheInputCallsTheSenderWithTheChannelAndTrimmedText()
        {
            var sent = new List<(ChatChannel, string)>();
            _view.Sender = (channel, text) => sent.Add((channel, text));

            Assert.That(_view.Open(ChatChannel.Team), Is.True);
            Assert.That(_view.IsOpen, Is.True);
            Assert.That(_view.Channel, Is.EqualTo(ChatChannel.Team));
            Assert.That(_view.Element.style.display.value, Is.EqualTo(DisplayStyle.Flex), "the input shows even with no lines yet");
            Assert.That(InputRow().style.display.value, Is.EqualTo(DisplayStyle.Flex));
            Assert.That(InputRow().ClassListContains(ChatView.TeamInputClass), Is.True);
            Assert.That(_view.Element.Q<Label>(ChatView.PromptName).text, Is.EqualTo("チーム:"));

            _view.Field.value = "  on my way  ";
            Assert.That(_view.Submit(), Is.True);

            Assert.That(sent, Is.EqualTo(new[] { (ChatChannel.Team, "on my way") }));
            Assert.That(_view.IsOpen, Is.False);
            Assert.That(_view.Field.value, Is.Empty);
            Assert.That(InputRow().style.display.value, Is.EqualTo(DisplayStyle.None));
            Assert.That(_view.Element.style.display.value, Is.EqualTo(DisplayStyle.None));
        }

        [Test]
        public void TheAllChannelPromptsForEveryone()
        {
            _view.Sender = (channel, text) => { };

            _view.Open(ChatChannel.All);

            Assert.That(_view.Element.Q<Label>(ChatView.PromptName).text, Is.EqualTo("全体:"));
            Assert.That(InputRow().ClassListContains(ChatView.TeamInputClass), Is.False);
        }

        [Test]
        public void EmptyTextClosesWithoutSending()
        {
            var calls = 0;
            _view.Sender = (channel, text) => calls++;
            _view.Open(ChatChannel.All);
            _view.Field.value = "   ";

            Assert.That(_view.Submit(), Is.False);
            Assert.That(calls, Is.EqualTo(0));
            Assert.That(_view.IsOpen, Is.False);
        }

        [Test]
        public void CancellingClosesWithoutSending()
        {
            var calls = 0;
            _view.Sender = (channel, text) => calls++;
            _view.Open(ChatChannel.All);
            _view.Field.value = "never mind";

            _view.Cancel();

            Assert.That(calls, Is.EqualTo(0));
            Assert.That(_view.IsOpen, Is.False);
            Assert.That(_view.Submit(), Is.False, "nothing to submit once closed");
        }

        [Test]
        public void WithoutASenderTheInputDoesNotOpen()
        {
            Assert.That(_view.Open(ChatChannel.All), Is.False);
            Assert.That(_view.IsOpen, Is.False);
            Assert.That(_view.Element.style.display.value, Is.EqualTo(DisplayStyle.None));
        }

        [Test]
        public void ASenderThatThrowsKeepsTheInputOpenWithTheText()
        {
            _view.Sender = (channel, text) => throw new InvalidOperationException("not connected");
            _view.Open(ChatChannel.All);
            _view.Field.value = "retry me";

            LogAssert.Expect(LogType.Error, new Regex(@"^\[Chat\] could not send to All: not connected"));
            LogAssert.Expect(LogType.Exception, new Regex("not connected"));
            Assert.That(_view.Submit(), Is.False);

            Assert.That(_view.IsOpen, Is.True);
            Assert.That(_view.Field.value, Is.EqualTo("retry me"));
        }

        [Test]
        public void TypingStopsThePlayersActionsLikeThePauseMenuAndClosingGivesThemBack()
        {
            _actions = ScriptableObject.CreateInstance<InputActionAsset>();
            var player = _actions.AddActionMap("Player");
            var move = player.AddAction("Move");
            var pause = player.AddAction("Pause");
            player.AddAction("Chat");
            player.AddAction("TeamChat");
            var alreadyOff = player.AddAction("Fire");
            var ui = _actions.AddActionMap("UI");
            var navigate = ui.AddAction("Navigate");
            _actions.Enable();
            alreadyOff.Disable();
            _view.Actions = _actions;
            _view.Sender = (channel, text) => { };

            _view.Open(ChatChannel.All);

            Assert.That(move.enabled, Is.False);
            Assert.That(pause.enabled, Is.False, "Esc while typing must not open the pause menu");
            Assert.That(navigate.enabled, Is.True, "only the player's map is stopped");
            Assert.That(_view.SuspendedActions, Is.EquivalentTo(new[] { "Player/Move", "Player/Pause", "Player/Chat", "Player/TeamChat" }));

            _view.Field.value = "go";
            _view.Submit();

            Assert.That(move.enabled, Is.True);
            Assert.That(pause.enabled, Is.True);
            Assert.That(alreadyOff.enabled, Is.False, "an action that was off before typing stays off");
            Assert.That(_view.SuspendedActions, Is.Empty);
        }

        [Test]
        public void ThePublishedChatSenderOpensTheInputAndSendsTheTrimmedText()
        {
            var published = new FakeChatSender();
            GameSignals.ChatSender.Publish(published);

            Assert.That(_view.CanSend, Is.True);
            Assert.That(_view.Open(ChatChannel.Team), Is.True);
            _view.Field.value = "  push B  ";
            Assert.That(_view.Submit(), Is.True);

            Assert.That(published.Sent, Is.EqualTo(new[] { (ChatChannel.Team, "push B") }));
            Assert.That(_view.IsOpen, Is.False);
            Assert.That(_view.Field.value, Is.Empty);
        }

        [Test]
        public void AnExplicitSenderIsUsedBeforeThePublishedOne()
        {
            var published = new FakeChatSender();
            GameSignals.ChatSender.Publish(published);
            var sent = new List<(ChatChannel, string)>();
            _view.Sender = (channel, text) => sent.Add((channel, text));

            _view.Open(ChatChannel.All);
            _view.Field.value = "hi";
            Assert.That(_view.Submit(), Is.True);

            Assert.That(sent, Is.EqualTo(new[] { (ChatChannel.All, "hi") }));
            Assert.That(published.Sent, Is.Empty);
        }

        [Test]
        public void AfterThePublishedSenderIsClearedTheInputDoesNotOpen()
        {
            var published = new FakeChatSender();
            GameSignals.ChatSender.Publish(published);
            GameSignals.ChatSender.ClearIfCurrent(published);

            Assert.That(_view.CanSend, Is.False);
            Assert.That(_view.Open(ChatChannel.All), Is.False);
            Assert.That(_view.IsOpen, Is.False);
        }

        [Test]
        public void ThePublishedSenderGoingAwayWhileTypingClosesTheInputAndGivesTheActionsBack()
        {
            _actions = ScriptableObject.CreateInstance<InputActionAsset>();
            var player = _actions.AddActionMap("Player");
            var move = player.AddAction("Move");
            player.AddAction("Chat");
            player.AddAction("TeamChat");
            _actions.Enable();
            _view.Actions = _actions;
            var published = new FakeChatSender();
            GameSignals.ChatSender.Publish(published);
            _view.Open(ChatChannel.All);
            _view.Field.value = "too late";
            Assert.That(move.enabled, Is.False);

            GameSignals.ChatSender.ClearIfCurrent(published);

            Assert.That(_view.IsOpen, Is.False);
            Assert.That(_view.Field.value, Is.Empty);
            Assert.That(InputRow().style.display.value, Is.EqualTo(DisplayStyle.None));
            Assert.That(move.enabled, Is.True);
            Assert.That(published.Sent, Is.Empty);
        }

        [Test]
        public void ThePublishedSenderBeingReplacedWhileTypingKeepsTheInputAndSendsToTheNewOne()
        {
            var first = new FakeChatSender();
            var second = new FakeChatSender();
            GameSignals.ChatSender.Publish(first);
            _view.Open(ChatChannel.All);
            _view.Field.value = "gg";

            GameSignals.ChatSender.Publish(second);

            Assert.That(_view.IsOpen, Is.True);
            Assert.That(_view.Submit(), Is.True);
            Assert.That(first.Sent, Is.Empty);
            Assert.That(second.Sent, Is.EqualTo(new[] { (ChatChannel.All, "gg") }));
        }

        [Test]
        public void AnExplicitSenderKeepsTheInputOpenWhenThePublishedOneGoesAway()
        {
            var published = new FakeChatSender();
            GameSignals.ChatSender.Publish(published);
            _view.Sender = (channel, text) => { };
            _view.Open(ChatChannel.All);

            GameSignals.ChatSender.ClearIfCurrent(published);

            Assert.That(_view.IsOpen, Is.True);
        }

        [Test]
        public void APublishedSenderThatThrowsKeepsTheInputOpenWithTheText()
        {
            GameSignals.ChatSender.Publish(new FakeChatSender { Failure = new InvalidOperationException("disposed") });
            _view.Open(ChatChannel.Team);
            _view.Field.value = "retry me";

            LogAssert.Expect(LogType.Error, new Regex(@"^\[Chat\] could not send to Team: disposed"));
            LogAssert.Expect(LogType.Exception, new Regex("disposed"));
            Assert.That(_view.Submit(), Is.False);

            Assert.That(_view.IsOpen, Is.True);
            Assert.That(_view.Field.value, Is.EqualTo("retry me"));
        }

        [Test]
        public void EveryChatKeyIsInBothLanguages()
        {
            foreach (var language in new[] { GameLanguage.Japanese, GameLanguage.English })
            {
                var table = Localizer.TableOf(language);
                foreach (var key in new[] { ChatView.LineKey, ChatView.TeamLineKey, ChatView.AllPromptKey, ChatView.TeamPromptKey })
                {
                    Assert.That(table.Contains(key), Is.True, language + " " + key);
                }
            }
        }

        private Label[] Rows()
        {
            return _view.Element.Q<VisualElement>(ChatView.LinesName).Children().Cast<Label>().ToArray();
        }

        private string[] RowTexts()
        {
            return Rows().Select(row => row.text).ToArray();
        }

        private VisualElement InputRow()
        {
            return _view.Element.Q<VisualElement>(ChatView.InputRowName);
        }

        private sealed class FakeParticipants : IParticipants
        {
            private readonly Dictionary<int, Participant> _byId;

            public FakeParticipants(params Participant[] participants)
            {
                _byId = participants.ToDictionary(p => p.Id);
            }

            public bool TryGet(int id, out Participant participant)
            {
                return _byId.TryGetValue(id, out participant);
            }
        }

        private sealed class FakeChatSender : IChatSender
        {
            public List<(ChatChannel, string)> Sent { get; } = new List<(ChatChannel, string)>();

            public Exception Failure { get; set; }

            public void Send(ChatChannel channel, string text)
            {
                if (Failure != null)
                {
                    throw Failure;
                }

                Sent.Add((channel, text));
            }
        }
    }
}
