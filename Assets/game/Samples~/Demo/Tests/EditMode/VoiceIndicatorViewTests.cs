using System.Linq;
using Shooter.Core;
using Shooter.Runtime;
using NUnit.Framework;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace Shooter.Tests.EditMode
{
    /// <summary>
    /// ボイスの欄 (<see cref="VoiceIndicatorView"/>) が <see cref="GameSignals.Voice"/> の値で、自分が送っている印と話している人の名前を出し、
    /// 文 (<see cref="VoiceText"/>) を日本語の表 (Resources/Localization/ja/voice.json) から作ることを確かめる。
    /// </summary>
    public sealed class VoiceIndicatorViewTests
    {
        private Hud _hud;
        private GameLanguage _languageBefore;

        [SetUp]
        public void SetUp()
        {
            _languageBefore = Localizer.Language;
            Localizer.Apply(GameLanguage.Japanese);
        }

        [TearDown]
        public void TearDown()
        {
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

            Localizer.Apply(_languageBefore);
        }

        [Test]
        public void TheHudCarriesTheViewAndHidesItWithoutAValue()
        {
            var view = CreateView();

            Assert.That(_hud.GetComponents<VoiceIndicatorView>(), Has.Length.EqualTo(1));

            Assert.That(view.Status, Is.Null);
            Assert.That(view.IsShown, Is.False);
            Assert.That(view.Lines, Is.Empty);
            Assert.That(view.Element.style.display.value, Is.EqualTo(DisplayStyle.None));
            Assert.That(view.Element.childCount, Is.EqualTo(0));
        }

        [Test]
        public void SpeakingParticipantsAreShownByName()
        {
            var view = CreateView();
            GameSignals.Participants.Publish(Participants());

            GameSignals.Voice.Publish(new VoiceStatus(false, new[] { 2, 3 }, true));

            Assert.That(view.IsShown, Is.True);
            Assert.That(view.ShowsLocal, Is.False);
            Assert.That(view.Lines, Is.EqualTo(new[] { "P2 が話している", "P3 が話している" }));
            Assert.That(RowTexts(view), Is.EqualTo(view.Lines));
            Assert.That(view.Element.style.display.value, Is.EqualTo(DisplayStyle.Flex));
            Assert.That(view.Element.Children().Any(row => row.ClassListContains(VoiceIndicatorView.LocalRowClass)), Is.False);

            GameSignals.Voice.Publish(new VoiceStatus(false, new[] { 3 }, true));
            Assert.That(view.Lines, Is.EqualTo(new[] { "P3 が話している" }));
            Assert.That(RowTexts(view), Is.EqualTo(view.Lines), "the row of the one who stopped is taken away");
        }

        [Test]
        public void TalkingYourselfShowsTheLocalMarkFirst()
        {
            var view = CreateView();
            GameSignals.Participants.Publish(Participants());

            GameSignals.Voice.Publish(new VoiceStatus(true, new[] { 2 }, true));

            Assert.That(view.ShowsLocal, Is.True);
            Assert.That(view.Lines, Is.EqualTo(new[] { "送信中", "P2 が話している" }));
            Assert.That(view.Element.Children().First().ClassListContains(VoiceIndicatorView.LocalRowClass), Is.True);
            Assert.That(view.Element.Children().Last().ClassListContains(VoiceIndicatorView.LocalRowClass), Is.False);

            GameSignals.Voice.Publish(new VoiceStatus(true, null, true));
            Assert.That(view.Lines, Is.EqualTo(new[] { "送信中" }));
        }

        [Test]
        public void NobodyTalkingHidesTheViewWhileVoiceIsOn()
        {
            var view = CreateView();
            GameSignals.Voice.Publish(new VoiceStatus(true, new[] { 2 }, true));

            GameSignals.Voice.Publish(new VoiceStatus(false, null, true));

            Assert.That(view.Status.HasValue, Is.True);
            Assert.That(view.IsShown, Is.False);
            Assert.That(view.Lines, Is.Empty);
            Assert.That(view.Element.childCount, Is.EqualTo(0));
            Assert.That(view.Element.style.display.value, Is.EqualTo(DisplayStyle.None));
        }

        [Test]
        public void ParticipantsPublishedAfterTheVoiceNameTheSpeaker()
        {
            var view = CreateView();
            GameSignals.Voice.Publish(new VoiceStatus(false, new[] { 2 }, true));
            Assert.That(view.Lines, Is.EqualTo(new[] { "プレイヤー 2 が話している" }), "an Id missing from the participants is named by the Id");

            GameSignals.Participants.Publish(Participants());

            Assert.That(view.Lines, Is.EqualTo(new[] { "P2 が話している" }));
        }

        [Test]
        public void ClearingTheValueHidesTheView()
        {
            var view = CreateView();
            GameSignals.Voice.Publish(new VoiceStatus(true, new[] { 2 }, true));

            GameSignals.Voice.Clear();

            Assert.That(view.Status, Is.Null);
            Assert.That(view.IsShown, Is.False);
            Assert.That(view.Element.childCount, Is.EqualTo(0));
            Assert.That(view.Element.style.display.value, Is.EqualTo(DisplayStyle.None));
        }

        [Test]
        public void ChangingTheLanguageRewritesTheRows()
        {
            var view = CreateView();
            GameSignals.Participants.Publish(Participants());
            GameSignals.Voice.Publish(new VoiceStatus(true, new[] { 2 }, true));

            Localizer.Apply(GameLanguage.English);

            Assert.That(view.Lines, Is.EqualTo(new[] { "Transmitting", "P2 is talking" }));
            Assert.That(RowTexts(view), Is.EqualTo(view.Lines));
        }

        [Test]
        public void RebindingCatchesUpWithTheValuePublishedWhileUnbound()
        {
            var view = CreateView();
            view.Unbind();
            GameSignals.Participants.Publish(Participants());

            GameSignals.Voice.Publish(new VoiceStatus(false, new[] { 3 }, true));
            Assert.That(view.IsShown, Is.False, "an unbound view does not listen");

            _hud.RebuildView();
            Assert.That(view.Lines, Is.EqualTo(new[] { "P3 が話している" }));
            Assert.That(view.Element.style.display.value, Is.EqualTo(DisplayStyle.Flex));
        }

        [Test]
        public void TheLinesAreBuiltFromTheVoiceKeys()
        {
            var participants = Participants();
            var lines = VoiceText.Lines(new VoiceStatus(true, new[] { 1, 9 }, true), participants);

            Assert.That(lines, Is.EqualTo(new[]
            {
                new LocalizedText(VoiceText.LocalKey),
                new LocalizedText(VoiceText.SpeakingKey, LocalizedText.Literal("P1")),
                new LocalizedText(VoiceText.SpeakingKey, new LocalizedText(VoiceText.UnknownSpeakerKey, 9)),
            }));
            Assert.That(VoiceText.NameOf(2, null), Is.EqualTo(new LocalizedText(VoiceText.UnknownSpeakerKey, 2)));
        }

        private VoiceIndicatorView CreateView()
        {
            _hud = Hud.Create("VoiceIndicatorTests.Hud");
            var view = _hud.VoiceIndicator;
            Assert.That(view, Is.Not.Null, "Hud.AddViews attaches the voice indicator");
            Assert.That(view.Element, Is.Not.Null);
            return view;
        }

        private static ParticipantTable Participants()
        {
            var table = new ParticipantTable();
            table.Set(new Participant(1, "P1", Team.Alpha, true));
            table.Set(new Participant(2, "P2", Team.Alpha, false));
            table.Set(new Participant(3, "P3", Team.Alpha, false));
            return table;
        }

        private static string[] RowTexts(VoiceIndicatorView view)
        {
            return view.Element.Children().Select(row => ((Label)row).text).ToArray();
        }
    }
}
