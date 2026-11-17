using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Shooter.Core;
using Shooter.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace Shooter.Tests.EditMode
{
    public sealed class CustomMatchPanelTests
    {
        private VisualElement _container;
        private CustomMatchPanel _panel;
        private FakeJoinByCode _joinByCode;
        private int _cancelled;
        private int _joined;
        private GameLanguage _languageBefore;

        [SetUp]
        public void SetUp()
        {
            _languageBefore = Localizer.Language;
            Localizer.Apply(GameLanguage.Japanese);
            _container = new VisualElement();
            _joinByCode = new FakeJoinByCode();
            _cancelled = 0;
            _joined = 0;
            _panel = null;
        }

        [TearDown]
        public void TearDown()
        {
            _panel?.Dispose();
            GameSignals.ResetAll();
            Localizer.Apply(_languageBefore);
        }

        private void Open()
        {
            _panel = new CustomMatchPanel(_container);
            _panel.Cancelled += () => _cancelled++;
            _panel.Joined += () => _joined++;
        }

        [Test]
        public void JoinLooksForTheEnteredCode()
        {
            GameSignals.JoinByCode.Publish(_joinByCode);
            Open();
            _panel.CodeField.value = "  ab7-K2x  ";

            Assert.That(_panel.Join(), Is.True);

            Assert.That(_joinByCode.Codes, Is.EqualTo(new[] { "AB7-K2X" }), "spaces are dropped and letters upper-cased like the server compares them");
            Assert.That(_panel.IsSearching, Is.True);
            Assert.That(_panel.Notice, Is.EqualTo("試合を探している…"));
            Assert.That(_panel.IsNoticeError, Is.False);
            Assert.That(_panel.JoinButton.enabledSelf, Is.False);
            Assert.That(_panel.CodeField.enabledSelf, Is.False);
            Assert.That(_panel.Join(), Is.False, "a second search does not start while one is running");
            Assert.That(_joinByCode.Codes, Has.Count.EqualTo(1));
        }

        [TestCase(JoinCodeOutcome.NotFound, "このコードの試合は見つからなかった。コードを確かめる")]
        [TestCase(JoinCodeOutcome.Full, "この試合は満員で入れない")]
        [TestCase(JoinCodeOutcome.VersionMismatch, "この試合とゲームのバージョンが違うので入れない")]
        [TestCase(JoinCodeOutcome.Failed, "うまくいかなかった。もう一度試す")]
        [TestCase((JoinCodeOutcome)99, "うまくいかなかった。もう一度試す")]
        public void AnOutcomeOtherThanJoinedShowsWhyAndLetsTheCodeBeTriedAgain(JoinCodeOutcome outcome, string expected)
        {
            GameSignals.JoinByCode.Publish(_joinByCode);
            Open();
            _panel.CodeField.value = "ZZZ999";
            _panel.Join();

            _joinByCode.Answer(outcome);

            Assert.That(_panel.Notice, Is.EqualTo(expected));
            Assert.That(_panel.IsNoticeError, Is.True);
            Assert.That(_panel.IsSearching, Is.False);
            Assert.That(_panel.HasJoined, Is.False);
            Assert.That(_joined, Is.Zero);
            Assert.That(_panel.JoinButton.enabledSelf, Is.True);
            Assert.That(_panel.CodeField.enabledSelf, Is.True);

            _panel.CodeField.value = "ABC234";
            Assert.That(_panel.Join(), Is.True);
            Assert.That(_joinByCode.Codes, Is.EqualTo(new[] { "ZZZ999", "ABC234" }));
            Assert.That(_panel.Notice, Is.EqualTo("試合を探している…"));
        }

        [Test]
        public void JoinedTellsTheMenuAndKeepsTheFormClosed()
        {
            GameSignals.JoinByCode.Publish(_joinByCode);
            Open();
            _panel.CodeField.value = "ABC234";
            _panel.Join();

            _joinByCode.Answer(JoinCodeOutcome.Joined);

            Assert.That(_joined, Is.EqualTo(1));
            Assert.That(_panel.HasJoined, Is.True);
            Assert.That(_panel.Notice, Is.EqualTo("試合に入っている…"));
            Assert.That(_panel.IsNoticeError, Is.False);
            Assert.That(_panel.JoinButton.enabledSelf, Is.False);
            Assert.That(_panel.Join(), Is.False);
            Assert.That(_joinByCode.Codes, Has.Count.EqualTo(1));
        }

        [Test]
        public void AnOutcomeReturnedDuringTheCallIsShown()
        {
            _joinByCode.ImmediateOutcome = JoinCodeOutcome.NotFound;
            GameSignals.JoinByCode.Publish(_joinByCode);
            Open();
            _panel.CodeField.value = "ABC234";

            Assert.That(_panel.Join(), Is.True);

            Assert.That(_panel.IsSearching, Is.False);
            Assert.That(_panel.Notice, Is.EqualTo("このコードの試合は見つからなかった。コードを確かめる"));
            Assert.That(_panel.JoinButton.enabledSelf, Is.True);
        }

        [Test]
        public void ASecondAnswerToTheSameSearchIsIgnored()
        {
            GameSignals.JoinByCode.Publish(_joinByCode);
            Open();
            _panel.CodeField.value = "ABC234";
            _panel.Join();
            var first = _joinByCode.Pending;

            first(JoinCodeOutcome.Full);
            first(JoinCodeOutcome.Joined);

            Assert.That(_panel.Notice, Is.EqualTo("この試合は満員で入れない"));
            Assert.That(_panel.HasJoined, Is.False);
            Assert.That(_joined, Is.Zero);
        }

        [Test]
        public void AnEmptyCodeIsNotLookedForAndAsksForOne()
        {
            GameSignals.JoinByCode.Publish(_joinByCode);
            Open();
            _panel.CodeField.value = "   ";

            Assert.That(_panel.Join(), Is.False);

            Assert.That(_joinByCode.Codes, Is.Empty);
            Assert.That(_panel.Notice, Is.EqualTo("参加コードを入れる"));
            Assert.That(_panel.IsNoticeError, Is.True);
            Assert.That(_panel.IsSearching, Is.False);
        }

        [Test]
        public void ACodeWithCharactersAJoinCodeCannotHoldIsNotLookedFor()
        {
            GameSignals.JoinByCode.Publish(_joinByCode);
            Open();
            _panel.CodeField.value = "AB!CD";

            Assert.That(_panel.Join(), Is.False);

            Assert.That(_joinByCode.Codes, Is.Empty);
            Assert.That(_panel.Notice, Is.EqualTo("参加コードは 1〜16 文字の英数字・「-」・「_」"));
            Assert.That(_panel.IsNoticeError, Is.True);
        }

        [Test]
        public void TheCodeFieldTakesNoMoreThanAJoinCode()
        {
            Open();

            Assert.That(_panel.CodeField.maxLength, Is.EqualTo(JoinCode.MaxLength));
        }

        [Test]
        public void WithoutAJoinByCodeNothingCanBeLookedForAndItSaysSo()
        {
            Open();

            Assert.That(_panel.JoinButton.enabledSelf, Is.False);
            Assert.That(_panel.CodeField.enabledSelf, Is.False);
            Assert.That(_panel.BackButton.enabledSelf, Is.True);
            Assert.That(_panel.Notice, Is.EqualTo("ここでは参加コードで入れない"));
            Assert.That(_panel.IsNoticeError, Is.False);
            _panel.CodeField.value = "ABC234";
            Assert.That(_panel.Join(), Is.False);

            GameSignals.JoinByCode.Publish(_joinByCode);

            Assert.That(_panel.Notice, Is.Null);
            Assert.That(_panel.JoinButton.enabledSelf, Is.True);
            Assert.That(_panel.Join(), Is.True);
            Assert.That(_joinByCode.Codes, Is.EqualTo(new[] { "ABC234" }));
        }

        [Test]
        public void AJoinByCodeThatThrowsIsLoggedAndShownAsAFailure()
        {
            _joinByCode.Throw = new InvalidOperationException("the client is already connecting");
            GameSignals.JoinByCode.Publish(_joinByCode);
            Open();
            _panel.CodeField.value = "ABC234";

            LogAssert.Expect(LogType.Log, new Regex(@"^\[CustomMatch\] looking for a match with join code ABC234"));
            LogAssert.Expect(LogType.Error, new Regex(@"^\[CustomMatch\] could not look for join code ABC234: the client is already connecting"));
            LogAssert.Expect(LogType.Exception, new Regex("already connecting"));
            Assert.That(_panel.Join(), Is.False);

            Assert.That(_panel.IsSearching, Is.False);
            Assert.That(_panel.Notice, Is.EqualTo("うまくいかなかった。もう一度試す"));
            Assert.That(_panel.IsNoticeError, Is.True);
            Assert.That(_panel.JoinButton.enabledSelf, Is.True);
        }

        [Test]
        public void TheReasonFollowsTheLanguage()
        {
            GameSignals.JoinByCode.Publish(_joinByCode);
            Open();
            _panel.CodeField.value = "ABC234";
            _panel.Join();
            _joinByCode.Answer(JoinCodeOutcome.NotFound);

            Localizer.Apply(GameLanguage.English);

            Assert.That(_panel.Notice, Is.EqualTo("No match has that code. Check the code."));
            Assert.That(_panel.JoinButton.text, Is.EqualTo("JOIN"));
            Assert.That(_panel.CodeField.textEdition.placeholder, Is.EqualTo("Join code"));
        }

        [Test]
        public void TheStaticTextsComeFromTheTable()
        {
            Open();

            Assert.That(_panel.JoinButton.text, Is.EqualTo("入る"));
            Assert.That(_panel.BackButton.text, Is.EqualTo("戻る"));
            Assert.That(_panel.CodeField.textEdition.placeholder, Is.EqualTo("参加コード"));
        }

        [Test]
        public void BackCancelsThePanel()
        {
            Open();

            _panel.Cancel();

            Assert.That(_cancelled, Is.EqualTo(1));
        }

        [Test]
        public void AnAnswerAfterDisposingIsDropped()
        {
            GameSignals.JoinByCode.Publish(_joinByCode);
            Open();
            _panel.CodeField.value = "ABC234";
            _panel.Join();
            _panel.Dispose();

            _joinByCode.Answer(JoinCodeOutcome.Joined);

            Assert.That(_container.childCount, Is.EqualTo(0));
            Assert.That(_joined, Is.Zero);
            Assert.That(_panel.Join(), Is.False);
            _panel.Cancel();
            Assert.That(_cancelled, Is.EqualTo(0));
        }

        [Test]
        public void EveryCustomMatchKeyIsInBothLanguages()
        {
            var keys = new List<string>
            {
                "customMatch.title", "customMatch.description", "customMatch.join", "customMatch.back",
                CustomMatchPanel.CodePlaceholderKey, CustomMatchPanel.UnavailableKey, CustomMatchPanel.CodeRequiredKey,
                CustomMatchPanel.InvalidCodeKey, CustomMatchPanel.SearchingKey, CustomMatchPanel.FailedKey,
            };
            foreach (JoinCodeOutcome outcome in Enum.GetValues(typeof(JoinCodeOutcome)))
            {
                keys.Add(CustomMatchPanel.OutcomeKey(outcome));
            }

            foreach (var language in new[] { GameLanguage.Japanese, GameLanguage.English })
            {
                var table = Localizer.TableOf(language);
                foreach (var key in keys)
                {
                    Assert.That(table.Contains(key), Is.True, language + " " + key);
                }
            }
        }

        private sealed class FakeJoinByCode : IJoinByCode
        {
            public readonly List<string> Codes = new List<string>();

            public Exception Throw { get; set; }

            public JoinCodeOutcome? ImmediateOutcome { get; set; }

            public Action<JoinCodeOutcome> Pending { get; private set; }

            public void Join(string code, Action<JoinCodeOutcome> done)
            {
                if (Throw != null)
                {
                    throw Throw;
                }

                Codes.Add(code);
                Pending = done;
                if (ImmediateOutcome.HasValue)
                {
                    done(ImmediateOutcome.Value);
                }
            }

            public void Answer(JoinCodeOutcome outcome)
            {
                Pending(outcome);
            }
        }
    }
}
