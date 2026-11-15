using System;
using System.Collections.Generic;
using Fps.Core;
using Fps.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace Fps.Tests.EditMode
{
    public sealed class ModeHudViewTests
    {
        private const string ConnectionCaptionKey = "hud.scoreboard.connectionCaption";

        private static readonly ModeHudScoreLine[] _scores =
        {
            new ModeHudScoreLine(Side.Bravo, LocalizedText.Literal("BRAVO 1"), true, false),
            new ModeHudScoreLine(Side.Alpha, LocalizedText.Literal("ALPHA 3"), false, true),
            new ModeHudScoreLine(new Side(2), LocalizedText.Literal("SIDE 2 0"), false, false),
        };

        private static readonly LocalizedText[] _lines =
        {
            LocalizedText.Literal("リスポーンまで 2 秒"), default, LocalizedText.Literal(string.Empty), LocalizedText.Literal("拠点 A: ALPHA"),
        };

        private GameObject _owner;
        private ModeHudView _view;
        private VisualElement _root;

        [SetUp]
        public void SetUp()
        {
            GameSignals.ResetAll();
            var layout = Resources.Load<VisualTreeAsset>(Hud.LayoutResourcePath);
            Assert.That(layout, Is.Not.Null, "Assets/game/UI/Resources/Hud/Hud.uxml should be loadable from Resources");
            _root = layout.CloneTree();
            _owner = new GameObject(nameof(ModeHudViewTests));
            _view = _owner.AddComponent<ModeHudView>();
        }

        [TearDown]
        public void TearDown()
        {
            if (_owner != null)
            {
                Object.DestroyImmediate(_owner);
            }

            ModeHudFormatters.Unregister(typeof(BaseDetail));
            ModeHudFormatters.Unregister(typeof(DerivedDetail));
            GameSignals.ResetAll();
        }

        [Test]
        public void ABoundViewShowsThePublishedStatusInItsTexts()
        {
            _view.Bind(_root);

            GameSignals.ModeStatus.Publish(Status(null, Text("01:30"), _lines, Text("ALPHA の勝ち")));

            Assert.That(_view.IsShown, Is.True);
            Assert.That(_view.TimeText, Is.EqualTo("01:30"));
            Assert.That(_view.ScoreTexts, Is.EqualTo(new[] { "BRAVO 1", "ALPHA 3", "SIDE 2 0" }));
            Assert.That(_view.Scores[0].IsOwn, Is.True);
            Assert.That(_view.Lines, Is.EqualTo(new[] { "リスポーンまで 2 秒", "拠点 A: ALPHA" }));
            Assert.That(_view.ResultText, Is.EqualTo("ALPHA の勝ち"));
            Assert.That(_view.Element.style.display.value, Is.EqualTo(DisplayStyle.Flex));
        }

        [Test]
        public void ClearingTheStatusHidesThePanel()
        {
            _view.Bind(_root);
            GameSignals.ModeStatus.Publish(Status(null, Text("01:00"), null, default));

            GameSignals.ModeStatus.Clear();

            Assert.That(_view.IsShown, Is.False);
            Assert.That(_view.Element.style.display.value, Is.EqualTo(DisplayStyle.None));
        }

        [Test]
        public void BindingCatchesUpWithTheCurrentStatus()
        {
            GameSignals.ModeStatus.Publish(Status(null, Text("00:45"), null, default));

            _view.Bind(_root);

            Assert.That(_view.IsShown, Is.True);
            Assert.That(_view.TimeText, Is.EqualTo("00:45"));
        }

        [Test]
        public void AStatusClearedWhileUnboundIsHiddenWhenBoundAgain()
        {
            _view.Bind(_root);
            GameSignals.ModeStatus.Publish(Status(null, Text("00:45"), null, default));
            _view.Unbind();

            GameSignals.ModeStatus.Clear();
            _view.Bind(_root);

            Assert.That(_view.IsShown, Is.False);
            Assert.That(_view.Element.style.display.value, Is.EqualTo(DisplayStyle.None));
        }

        [Test]
        public void AnUnboundViewIgnoresTheSignal()
        {
            _view.Bind(_root);
            _view.Unbind();

            GameSignals.ModeStatus.Publish(Status(null, Text("00:30"), null, default));

            Assert.That(_view.IsShown, Is.False);
        }

        [Test]
        public void ADestroyedViewStopsListening()
        {
            _view.Bind(_root);
            Object.DestroyImmediate(_owner);

            Assert.DoesNotThrow(() => GameSignals.ModeStatus.Publish(Status(null, Text("00:30"), null, default)));
            Assert.DoesNotThrow(() => GameSignals.ModeStatus.Publish(Status(null, Text("00:29"), null, default)));
        }

        [Test]
        public void TheFormatterRegisteredForTheDetailTypeWritesThePanel()
        {
            ModeHudFormatters.Register<BaseDetail>(new FixedFormatter("base"));
            _view.Bind(_root);

            GameSignals.ModeStatus.Publish(Status(new BaseDetail(), Text("01:00"), _lines, default));

            Assert.That(_view.TimeText, Is.EqualTo("base time"));
            Assert.That(_view.ScoreTexts, Is.EqualTo(new[] { "base score" }));
            Assert.That(_view.Lines, Is.EqualTo(new[] { "base line" }));
            Assert.That(_view.ResultText, Is.EqualTo("base result"));
        }

        [Test]
        public void TheFormatterLookupFallsBackToTheBaseTypeThenToTheStatusTexts()
        {
            ModeHudFormatters.Register<BaseDetail>(new FixedFormatter("base"));
            var derived = Status(new DerivedDetail(), Text("01:00"), null, default);
            var unknown = Status(new object(), Text("01:00"), null, default);
            var none = Status(null, Text("01:00"), null, default);

            Assert.That(ModeHudFormatters.For(derived), Is.InstanceOf<FixedFormatter>());
            Assert.That(ModeHudFormatters.For(unknown), Is.SameAs(ModeHudFormatters.StatusText));
            Assert.That(ModeHudFormatters.For(none), Is.SameAs(ModeHudFormatters.StatusText));

            var exact = new FixedFormatter("derived");
            ModeHudFormatters.Register<DerivedDetail>(exact);
            Assert.That(ModeHudFormatters.For(derived), Is.SameAs(exact), "the exact type wins over its base");

            Assert.That(ModeHudFormatters.Unregister(typeof(DerivedDetail)), Is.True);
            Assert.That(ModeHudFormatters.Unregister(typeof(DerivedDetail)), Is.False);
        }

        [Test]
        public void TheFormattersRejectNull()
        {
            Assert.Throws<ArgumentNullException>(() => ModeHudFormatters.Register<BaseDetail>(null));
            Assert.Throws<ArgumentNullException>(() => ModeHudFormatters.Register(null, new FixedFormatter("x")));
            Assert.Throws<ArgumentNullException>(() => ModeHudFormatters.For(null));
        }

        [Test]
        public void ShowingAStatusDirectlyAndShowingNullHides()
        {
            _view.Bind(_root);

            _view.Show(Status(null, Text("00:10"), null, default));
            Assert.That(_view.TimeText, Is.EqualTo("00:10"));

            _view.Show((ModeStatus)null);
            Assert.That(_view.IsShown, Is.False);
        }

        [Test]
        public void TheLayoutHasAHiddenPanel()
        {
            var element = _root.Q<VisualElement>(ModeHudView.UxmlName);

            Assert.That(element, Is.Not.Null);

            // Unity 6.6 は UXML の style 属性を element.style に映さないので、付けたビューが隠したままにすることで確かめる
            _view.Bind(_root);
            Assert.That(_view.Element, Is.SameAs(element));
            Assert.That(element.style.display.value, Is.EqualTo(DisplayStyle.None));
        }

        [Test]
        public void HudAddsTheView()
        {
            var hud = Hud.Create("ModeHudViewTests.Hud", null, null, null);
            try
            {
                Assert.That(hud.ModeHud, Is.Not.Null, "Hud.AddViews attaches the mode view");
            }
            finally
            {
                var panelSettings = hud.OwnedPanelSettings;
                Object.DestroyImmediate(hud.gameObject);
                if (panelSettings != null)
                {
                    Object.DestroyImmediate(panelSettings);
                }
            }
        }

        [Test]
        public void ShowWritesTimeScoresLinesAndResult()
        {
            _view.Bind(_root);

            _view.Show(Text("01:30"), _scores, _lines, default);

            Assert.That(_view.Element.style.display.value, Is.EqualTo(DisplayStyle.Flex));
            Assert.That(_view.Element.Q<Label>(className: ModeHudView.TimeClass).text, Is.EqualTo("01:30"));

            var scores = _view.Element.Query<Label>(className: ModeHudView.ScoreClass).ToList();
            Assert.That(scores.Count, Is.EqualTo(3));
            Assert.That(scores[0].text, Is.EqualTo("BRAVO 1"));
            Assert.That(scores[0].ClassListContains(ModeHudView.OwnScoreClass), Is.True);
            Assert.That(scores[1].ClassListContains(ModeHudView.LeadingScoreClass), Is.True);
            Assert.That(scores[1].ClassListContains(ModeHudView.OwnScoreClass), Is.False);

            var lines = _view.Element.Query<Label>(className: ModeHudView.LineClass).ToList();
            Assert.That(lines.Count, Is.EqualTo(2), "default and empty lines are dropped");
            Assert.That(lines[1].text, Is.EqualTo("拠点 A: ALPHA"));

            var result = _view.Element.Q<Label>(className: ModeHudView.ResultClass);
            Assert.That(result.style.display.value, Is.EqualTo(DisplayStyle.None));

            _view.Show(Text("00:00"), new[] { _scores[1] }, null, Text("ALPHA の勝ち"));

            Assert.That(_view.Element.Query<Label>(className: ModeHudView.ScoreClass).ToList().Count, Is.EqualTo(1));
            Assert.That(_view.Element.Query<Label>(className: ModeHudView.LineClass).ToList(), Is.Empty);
            Assert.That(result.text, Is.EqualTo("ALPHA の勝ち"));
            Assert.That(result.style.display.value, Is.EqualTo(DisplayStyle.Flex));
            Assert.That(_view.ResultText, Is.EqualTo("ALPHA の勝ち"));
        }

        [Test]
        public void ContentShownBeforeBindingAppearsWhenBound()
        {
            _view.Show(Text("02:00"), _scores, null, Text(string.Empty));

            _view.Bind(_root);

            Assert.That(_view.Element.style.display.value, Is.EqualTo(DisplayStyle.Flex));
            Assert.That(_view.Element.Query<Label>(className: ModeHudView.ScoreClass).ToList().Count, Is.EqualTo(3));
        }

        [Test]
        public void HideHidesThePanel()
        {
            _view.Bind(_root);
            _view.Show(Text("01:00"), _scores, null, default);

            _view.Hide();

            Assert.That(_view.IsShown, Is.False);
            Assert.That(_view.Element.style.display.value, Is.EqualTo(DisplayStyle.None));
        }

        [Test]
        public void ShowRejectsNullScores()
        {
            Assert.Throws<ArgumentNullException>(() => _view.Show(Text("00:10"), null, null, default));
        }

        [Test]
        public void ShowResolvesKeysInTheCurrentLanguage()
        {
            _view.Bind(_root);

            _view.Show(new LocalizedText(ConnectionCaptionKey), _scores, new[] { new LocalizedText(ConnectionCaptionKey) }, default);

            var expected = Localizer.Get(ConnectionCaptionKey);
            Assert.That(expected, Is.Not.EqualTo(ConnectionCaptionKey), "the key exists in the current language's table");
            Assert.That(_view.TimeText, Is.EqualTo(expected));
            Assert.That(_view.Lines, Is.EqualTo(new[] { expected }));
            Assert.That(_view.Element.Q<Label>(className: ModeHudView.TimeClass).text, Is.EqualTo(expected));
        }

        [Test]
        public void ScoreLinesAreResolvedInTheCurrentLanguage()
        {
            _view.Bind(_root);
            var scores = new[] { new ModeHudScoreLine(Side.Alpha, ModeHudText.ScoreText(Side.Alpha, 3d), true, true) };

            _view.Show(Text("01:00"), scores, null, ModeHudText.Remaining(Side.Bravo, 2));

            var expected = Localizer.Resolve(ModeHudText.ScoreText(Side.Alpha, 3d));
            Assert.That(expected, Does.Not.StartWith("hud."), "the key exists in the current language's table");
            Assert.That(_view.ScoreTexts, Is.EqualTo(new[] { expected }));
            Assert.That(_view.Element.Q<Label>(className: ModeHudView.ScoreClass).text, Is.EqualTo(expected));
            Assert.That(_view.ResultText, Is.EqualTo(Localizer.Resolve(ModeHudText.Remaining(Side.Bravo, 2))));
        }

        [Test]
        public void TheViewIsTheHudsModeHud()
        {
            var hud = Hud.Create("ModeHudViewTests.Hud", null, null, null);
            try
            {
                IHud asInterface = hud;
                Assert.That(asInterface.ModeHud, Is.SameAs(hud.ModeHud));
            }
            finally
            {
                var panelSettings = hud.OwnedPanelSettings;
                Object.DestroyImmediate(hud.gameObject);
                if (panelSettings != null)
                {
                    Object.DestroyImmediate(panelSettings);
                }
            }
        }

        private static LocalizedText Text(string text)
        {
            return LocalizedText.Literal(text);
        }

        private static ModeStatus Status(object detail, LocalizedText time, LocalizedText[] lines, LocalizedText result)
        {
            return new ModeStatus(60f, null, Side.Bravo, null, false, null, detail, time, _scores, lines, result);
        }

        private class BaseDetail
        {
        }

        private sealed class DerivedDetail : BaseDetail
        {
        }

        private sealed class FixedFormatter : IModeHudFormatter
        {
            private readonly string _prefix;

            public FixedFormatter(string prefix)
            {
                _prefix = prefix;
            }

            public LocalizedText Time(ModeStatus status) => Text(_prefix + " time");

            public IReadOnlyList<ModeHudScoreLine> Scores(ModeStatus status) =>
                new[] { new ModeHudScoreLine(Side.Alpha, Text(_prefix + " score"), false, false) };

            public void AppendLines(ModeStatus status, List<LocalizedText> lines) => lines.Add(Text(_prefix + " line"));

            public LocalizedText Result(ModeStatus status) => Text(_prefix + " result");
        }
    }
}
