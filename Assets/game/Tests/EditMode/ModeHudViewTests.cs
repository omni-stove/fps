using System;
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
        private static readonly ModeHudScoreLine[] _scores =
        {
            new ModeHudScoreLine(Side.Bravo, "BRAVO 1", true, false),
            new ModeHudScoreLine(Side.Alpha, "ALPHA 3", false, true),
            new ModeHudScoreLine(new Side(2), "SIDE 2 0", false, false),
        };

        private static readonly string[] _lines = { "リスポーンまで 2 秒", null, string.Empty, "拠点 A: ALPHA" };

        private GameObject _owner;
        private ModeHudView _view;
        private VisualElement _root;

        [SetUp]
        public void SetUp()
        {
            var layout = Resources.Load<VisualTreeAsset>(Hud.LayoutResourcePath);
            Assert.That(layout, Is.Not.Null, "Assets/game/UI/Resources/Hud/Hud.uxml should be loadable from Resources");
            _root = layout.CloneTree();
            _owner = new GameObject(nameof(ModeHudViewTests));
            _view = _owner.AddComponent<ModeHudView>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_owner);
        }

        [Test]
        public void TheLayoutHasAHiddenPanel()
        {
            var element = _root.Q<VisualElement>(ModeHudView.UxmlName);

            Assert.That(element, Is.Not.Null);
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

            _view.Show("01:30", _scores, _lines, string.Empty);

            Assert.That(_view.Element.style.display.value, Is.EqualTo(DisplayStyle.Flex));
            Assert.That(_view.Element.Q<Label>(className: ModeHudView.TimeClass).text, Is.EqualTo("01:30"));

            var scores = _view.Element.Query<Label>(className: ModeHudView.ScoreClass).ToList();
            Assert.That(scores.Count, Is.EqualTo(3));
            Assert.That(scores[0].text, Is.EqualTo("BRAVO 1"));
            Assert.That(scores[0].ClassListContains(ModeHudView.OwnScoreClass), Is.True);
            Assert.That(scores[1].ClassListContains(ModeHudView.LeadingScoreClass), Is.True);
            Assert.That(scores[1].ClassListContains(ModeHudView.OwnScoreClass), Is.False);

            var lines = _view.Element.Query<Label>(className: ModeHudView.LineClass).ToList();
            Assert.That(lines.Count, Is.EqualTo(2), "null and empty lines are dropped");
            Assert.That(lines[1].text, Is.EqualTo("拠点 A: ALPHA"));

            var result = _view.Element.Q<Label>(className: ModeHudView.ResultClass);
            Assert.That(result.style.display.value, Is.EqualTo(DisplayStyle.None));

            _view.Show("00:00", new[] { _scores[1] }, null, "ALPHA の勝ち");

            Assert.That(_view.Element.Query<Label>(className: ModeHudView.ScoreClass).ToList().Count, Is.EqualTo(1));
            Assert.That(_view.Element.Query<Label>(className: ModeHudView.LineClass).ToList(), Is.Empty);
            Assert.That(result.text, Is.EqualTo("ALPHA の勝ち"));
            Assert.That(result.style.display.value, Is.EqualTo(DisplayStyle.Flex));
            Assert.That(_view.ResultText, Is.EqualTo("ALPHA の勝ち"));
        }

        [Test]
        public void ContentShownBeforeBindingAppearsWhenBound()
        {
            _view.Show("02:00", _scores, null, string.Empty);

            _view.Bind(_root);

            Assert.That(_view.Element.style.display.value, Is.EqualTo(DisplayStyle.Flex));
            Assert.That(_view.Element.Query<Label>(className: ModeHudView.ScoreClass).ToList().Count, Is.EqualTo(3));
        }

        [Test]
        public void HideHidesThePanel()
        {
            _view.Bind(_root);
            _view.Show("01:00", _scores, null, string.Empty);

            _view.Hide();

            Assert.That(_view.IsShown, Is.False);
            Assert.That(_view.Element.style.display.value, Is.EqualTo(DisplayStyle.None));
        }

        [Test]
        public void ShowRejectsNullScores()
        {
            Assert.Throws<ArgumentNullException>(() => _view.Show("00:10", null, null, null));
        }
    }
}
