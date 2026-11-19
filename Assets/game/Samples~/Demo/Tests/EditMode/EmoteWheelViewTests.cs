using System.Collections.Generic;
using System.Linq;
using Shooter.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace Shooter.Tests.EditMode
{
    // Edit Mode では UIDocument が木を作らないことがあるので、Hud.uxml を複製した木を直接 Bind する。
    public sealed class EmoteWheelViewTests
    {
        private const float _frame = 0.1f;

        private GameObject _host;
        private EmoteWheelView _view;
        private GameLanguage _languageBefore;

        [SetUp]
        public void SetUp()
        {
            GameSignals.ResetAll();
            _languageBefore = Localizer.Language;
            Localizer.Apply(GameLanguage.Japanese);
            _host = new GameObject("TestEmoteWheel");
            _view = _host.AddComponent<EmoteWheelView>();
            var layout = Resources.Load<VisualTreeAsset>(Hud.LayoutResourcePath);
            Assert.That(layout, Is.Not.Null, "Assets/game/Samples~/Demo/UI/Resources/Hud/Hud.uxml should be loadable from Resources");
            _view.Bind(layout.CloneTree());
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_host);
            GameSignals.ResetAll();
            Localizer.Apply(_languageBefore);
        }

        [TestCase(0f, 1f, 0)]
        [TestCase(1f, 0f, 1)]
        [TestCase(0f, -1f, 2)]
        [TestCase(-1f, 0f, 3)]
        [TestCase(0.6f, 0.8f, 0)]
        [TestCase(0.8f, 0.6f, 1)]
        [TestCase(-0.8f, 0.6f, 3)]
        public void TheDirectionPicksTheSlotClockwiseFromTheTop(float x, float y, int expected)
        {
            Assert.That(EmoteWheelView.IndexAt(new Vector2(x, y), 4, 0.35f), Is.EqualTo(expected));
        }

        [Test]
        public void SixSlotsAreSixtyDegreesApart()
        {
            var at60 = new Vector2(Mathf.Sin(60f * Mathf.Deg2Rad), Mathf.Cos(60f * Mathf.Deg2Rad));
            var at200 = new Vector2(Mathf.Sin(200f * Mathf.Deg2Rad), Mathf.Cos(200f * Mathf.Deg2Rad));
            var at340 = new Vector2(Mathf.Sin(340f * Mathf.Deg2Rad), Mathf.Cos(340f * Mathf.Deg2Rad));

            Assert.That(EmoteWheelView.IndexAt(at60, 6, 0.35f), Is.EqualTo(1));
            Assert.That(EmoteWheelView.IndexAt(at200, 6, 0.35f), Is.EqualTo(3));
            Assert.That(EmoteWheelView.IndexAt(at340, 6, 0.35f), Is.EqualTo(0), "past 330 degrees wraps back to the top slot");
        }

        [Test]
        public void ADirectionInsideTheDeadZoneOrNoSlotsPicksNothing()
        {
            Assert.That(EmoteWheelView.IndexAt(new Vector2(0.2f, 0.2f), 4, 0.35f), Is.EqualTo(-1));
            Assert.That(EmoteWheelView.IndexAt(Vector2.zero, 4, 0f), Is.EqualTo(-1));
            Assert.That(EmoteWheelView.IndexAt(Vector2.up, 0, 0.35f), Is.EqualTo(-1));
            Assert.That(EmoteWheelView.IndexAt(new Vector2(float.NaN, 1f), 4, 0.35f), Is.EqualTo(-1));
        }

        [Test]
        public void TheSlotsSitOnTheCircleStartingAtTheTop()
        {
            var top = EmoteWheelView.SlotOffset(0, 4, 100f);
            var right = EmoteWheelView.SlotOffset(1, 4, 100f);
            var bottom = EmoteWheelView.SlotOffset(2, 4, 100f);

            Assert.That(top.x, Is.EqualTo(0f).Within(0.001f));
            Assert.That(top.y, Is.EqualTo(-100f).Within(0.001f), "UI y grows downwards, so the top slot is at -radius");
            Assert.That(right.x, Is.EqualTo(100f).Within(0.001f));
            Assert.That(right.y, Is.EqualTo(0f).Within(0.001f));
            Assert.That(bottom.y, Is.EqualTo(100f).Within(0.001f));
        }

        [Test]
        public void TheLayoutHasTheWheelAndItStartsHidden()
        {
            Assert.That(_view.Element, Is.Not.Null, "Hud.uxml has the emote-wheel element");
            Assert.That(_view.IsOpen, Is.False);
            Assert.That(_view.Element.style.display.value, Is.EqualTo(DisplayStyle.None));
            Assert.That(_view.HoldSeconds, Is.EqualTo(EmoteWheelView.DefaultHoldSeconds));
            Assert.That(_view.DeadZone, Is.EqualTo(EmoteWheelView.DefaultDeadZone));
            Assert.That(_view.PointerRadius, Is.EqualTo(EmoteWheelView.DefaultPointerRadius));
        }

        [Test]
        public void HoldingDoesNotOpenWithoutEmotes()
        {
            HoldFor(2f);

            Assert.That(_view.IsOpen, Is.False);
            Assert.That(_view.Slots, Is.Empty);
            Assert.That(_view.Element.style.display.value, Is.EqualTo(DisplayStyle.None));
        }

        [Test]
        public void HoldingDoesNotOpenWhenTheControlHasNoEmotes()
        {
            var control = new FakeEmotes();
            GameSignals.Emotes.Publish(control);

            HoldFor(2f);

            Assert.That(_view.IsOpen, Is.False);
            Assert.That(_view.Element.style.display.value, Is.EqualTo(DisplayStyle.None));

            _view.Advance(false, Vector2.zero, Vector2.zero, _frame);
            Assert.That(control.Played, Is.Empty);
        }

        [Test]
        public void ATapShorterThanTheHoldNeitherOpensNorPlays()
        {
            var control = new FakeEmotes("wave", "salute");
            GameSignals.Emotes.Publish(control);
            _view.HoldSeconds = 0.5f;

            _view.Advance(true, Vector2.zero, Vector2.zero, 0.2f);
            _view.Advance(true, Vector2.zero, Vector2.zero, 0.2f);
            Assert.That(_view.IsOpen, Is.False);
            _view.Advance(false, Vector2.zero, Vector2.zero, 0.2f);

            _view.Advance(true, Vector2.zero, Vector2.zero, 0.2f);
            Assert.That(_view.IsOpen, Is.False, "the held time starts over after a release");
            Assert.That(control.Played, Is.Empty);
        }

        [Test]
        public void HoldingOpensTheWheelWithTheEmoteNames()
        {
            GameSignals.Emotes.Publish(new FakeEmotes("wave", "salute", "cheer", "game-added"));

            HoldFor(EmoteWheelView.DefaultHoldSeconds);

            Assert.That(_view.IsOpen, Is.True);
            Assert.That(_view.Element.style.display.value, Is.EqualTo(DisplayStyle.Flex));
            Assert.That(_view.Slots.Select(slot => slot.EmoteId), Is.EqualTo(new[] { "wave", "salute", "cheer", "game-added" }));
            Assert.That(_view.Slots.Select(slot => slot.Name), Is.EqualTo(new[] { "手を振る", "敬礼", "喜ぶ", "game-added" }),
                "an id without a name in the table is shown as it is");
            Assert.That(SlotNames(), Is.EqualTo(new[] { "手を振る", "敬礼", "喜ぶ", "game-added" }));
            Assert.That(_view.SelectedIndex, Is.EqualTo(-1));
            Assert.That(_view.CenterText, Is.EqualTo("やめる"));
            Assert.That(CenterLabel().text, Is.EqualTo("やめる"));
        }

        [Test]
        public void ReleasingAfterPointingWithTheMousePlaysThatEmote()
        {
            var control = new FakeEmotes("wave", "salute", "cheer", "point");
            GameSignals.Emotes.Publish(control);
            HoldFor(EmoteWheelView.DefaultHoldSeconds);

            _view.Advance(true, new Vector2(EmoteWheelView.DefaultPointerRadius, 0f), Vector2.zero, _frame);

            Assert.That(_view.SelectedIndex, Is.EqualTo(1));
            Assert.That(_view.SelectedEmoteId, Is.EqualTo("salute"));
            Assert.That(_view.CenterText, Is.EqualTo("敬礼"));
            Assert.That(CenterLabel().text, Is.EqualTo("敬礼"));
            Assert.That(SlotElements().Select(slot => slot.ClassListContains(EmoteWheelView.SelectedSlotClass)),
                Is.EqualTo(new[] { false, true, false, false }));
            Assert.That(control.Played, Is.Empty, "nothing plays until the key is released");

            _view.Advance(false, Vector2.zero, Vector2.zero, _frame);

            Assert.That(control.Played, Is.EqualTo(new[] { "salute" }));
            Assert.That(_view.IsOpen, Is.False);
            Assert.That(_view.Element.style.display.value, Is.EqualTo(DisplayStyle.None));
            Assert.That(_view.Element.childCount, Is.EqualTo(0));
        }

        [Test]
        public void TheMousePointerAddsUpAndStaysWithinTheRadius()
        {
            var control = new FakeEmotes("wave", "salute", "cheer", "point");
            GameSignals.Emotes.Publish(control);
            HoldFor(EmoteWheelView.DefaultHoldSeconds);

            _view.Advance(true, new Vector2(0f, -30f), Vector2.zero, _frame);
            Assert.That(_view.SelectedIndex, Is.EqualTo(-1), "a small move stays inside the dead zone");

            _view.Advance(true, new Vector2(0f, -1000f), Vector2.zero, _frame);
            Assert.That(_view.SelectedIndex, Is.EqualTo(2));

            _view.Advance(true, new Vector2(-1000f, 1000f), Vector2.zero, _frame);
            Assert.That(_view.SelectedIndex, Is.EqualTo(3), "the clamped pointer turns with one move instead of travelling far past the rim");

            _view.Advance(false, Vector2.zero, Vector2.zero, _frame);
            Assert.That(control.Played, Is.EqualTo(new[] { "point" }));
        }

        [Test]
        public void TheStickPicksASlotAndKeepsItWhenLetGo()
        {
            var control = new FakeEmotes("wave", "salute", "cheer", "point");
            GameSignals.Emotes.Publish(control);
            HoldFor(EmoteWheelView.DefaultHoldSeconds);

            _view.Advance(true, Vector2.zero, new Vector2(0f, -1f), _frame);
            Assert.That(_view.SelectedEmoteId, Is.EqualTo("cheer"));

            _view.Advance(true, Vector2.zero, Vector2.zero, _frame);
            Assert.That(_view.SelectedEmoteId, Is.EqualTo("cheer"), "a stick springing back to the centre keeps the last slot");

            _view.Advance(false, Vector2.zero, Vector2.zero, _frame);
            Assert.That(control.Played, Is.EqualTo(new[] { "cheer" }));
        }

        [Test]
        public void ReleasingWithNothingPickedClosesWithoutPlaying()
        {
            var control = new FakeEmotes("wave", "salute");
            GameSignals.Emotes.Publish(control);
            HoldFor(EmoteWheelView.DefaultHoldSeconds);

            _view.Advance(true, new Vector2(0f, EmoteWheelView.DefaultPointerRadius), Vector2.zero, _frame);
            Assert.That(_view.SelectedEmoteId, Is.EqualTo("wave"));
            _view.Advance(true, new Vector2(0f, -EmoteWheelView.DefaultPointerRadius), Vector2.zero, _frame);
            Assert.That(_view.SelectedIndex, Is.EqualTo(-1), "moving the pointer back to the centre picks nothing again");

            _view.Advance(false, Vector2.zero, Vector2.zero, _frame);

            Assert.That(_view.IsOpen, Is.False);
            Assert.That(control.Played, Is.Empty);
        }

        [Test]
        public void ARefusedEmoteStillClosesTheWheel()
        {
            var control = new FakeEmotes("wave") { Accepts = false };
            GameSignals.Emotes.Publish(control);
            HoldFor(EmoteWheelView.DefaultHoldSeconds);
            _view.Advance(true, Vector2.zero, Vector2.up, _frame);

            _view.Advance(false, Vector2.zero, Vector2.zero, _frame);

            Assert.That(control.Played, Is.EqualTo(new[] { "wave" }));
            Assert.That(_view.IsOpen, Is.False);
        }

        [Test]
        public void ClearingTheEmotesWhileOpenClosesWithoutPlaying()
        {
            var control = new FakeEmotes("wave", "salute");
            GameSignals.Emotes.Publish(control);
            HoldFor(EmoteWheelView.DefaultHoldSeconds);
            _view.Advance(true, Vector2.zero, Vector2.up, _frame);

            GameSignals.Emotes.Clear();

            Assert.That(_view.IsOpen, Is.False);
            Assert.That(_view.Element.style.display.value, Is.EqualTo(DisplayStyle.None));

            GameSignals.Emotes.Publish(control);
            HoldFor(1f);
            Assert.That(_view.IsOpen, Is.False, "the wheel waits for a release before it opens again");

            _view.Advance(false, Vector2.zero, Vector2.zero, _frame);
            Assert.That(control.Played, Is.Empty);

            HoldFor(EmoteWheelView.DefaultHoldSeconds);
            Assert.That(_view.IsOpen, Is.True, "a new hold after the release opens it");
        }

        [Test]
        public void ReplacingTheControlWhileOpenClosesWithoutPlayingOnEither()
        {
            var first = new FakeEmotes("wave", "salute");
            var second = new FakeEmotes("salute", "wave");
            GameSignals.Emotes.Publish(first);
            HoldFor(EmoteWheelView.DefaultHoldSeconds);
            _view.Advance(true, Vector2.zero, Vector2.up, _frame);

            GameSignals.Emotes.Publish(first);
            Assert.That(_view.IsOpen, Is.True, "publishing the same control again keeps the wheel open");

            GameSignals.Emotes.Publish(second);
            Assert.That(_view.IsOpen, Is.False);

            _view.Advance(false, Vector2.zero, Vector2.zero, _frame);
            Assert.That(first.Played, Is.Empty);
            Assert.That(second.Played, Is.Empty);
        }

        [Test]
        public void SwitchingTheLanguageWhileOpenRenamesTheSlots()
        {
            GameSignals.Emotes.Publish(new FakeEmotes("wave", "salute"));
            HoldFor(EmoteWheelView.DefaultHoldSeconds);

            Localizer.Apply(GameLanguage.English);

            Assert.That(SlotNames(), Is.EqualTo(new[] { "Wave", "Salute" }));
            Assert.That(CenterLabel().text, Is.EqualTo("Cancel"));
        }

        private void HoldFor(float seconds)
        {
            var elapsed = 0f;
            do
            {
                _view.Advance(true, Vector2.zero, Vector2.zero, _frame);
                elapsed += _frame;
            }
            while (elapsed < seconds - 0.0001f);
        }

        private List<VisualElement> SlotElements()
        {
            return _view.Element.Query<VisualElement>(className: EmoteWheelView.SlotClass).ToList();
        }

        private string[] SlotNames()
        {
            return _view.Element.Query<Label>(className: EmoteWheelView.SlotNameClass).ToList().Select(label => label.text).ToArray();
        }

        private Label CenterLabel()
        {
            return _view.Element.Q<Label>(className: EmoteWheelView.CenterClass);
        }

        private sealed class FakeEmotes : IEmoteControl
        {
            private readonly string[] _ids;

            public FakeEmotes(params string[] ids)
            {
                _ids = ids;
            }

            public bool Accepts { get; set; } = true;

            public List<string> Played { get; } = new List<string>();

            public IReadOnlyList<string> EmoteIds => _ids;

            public bool Play(string emoteId)
            {
                Played.Add(emoteId);
                return Accepts;
            }
        }
    }
}
