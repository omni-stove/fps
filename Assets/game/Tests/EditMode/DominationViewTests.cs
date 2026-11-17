using Fps.Core;
using Fps.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace Fps.Tests.EditMode
{
    public sealed class DominationViewTests
    {
        private GameObject _owner;
        private DominationView _view;
        private ControlPointsView _points;
        private VisualElement _root;

        [SetUp]
        public void SetUp()
        {
            GameSignals.ResetAll();
            var layout = Resources.Load<VisualTreeAsset>(Hud.LayoutResourcePath);
            Assert.That(layout, Is.Not.Null, "Assets/game/UI/Resources/Hud/Hud.uxml should be loadable from Resources");
            _root = layout.CloneTree();
            _owner = new GameObject(nameof(DominationViewTests));
            _view = _owner.AddComponent<DominationView>();
            _points = _owner.AddComponent<ControlPointsView>();
        }

        [TearDown]
        public void TearDown()
        {
            if (_owner != null)
            {
                Object.DestroyImmediate(_owner);
            }

            GameSignals.ResetAll();
        }

        [Test]
        public void ABoundViewShowsThePublishedScoresTimeAndOwners()
        {
            _view.Bind(_root);

            GameSignals.Domination.Publish(Status(12.9f, 3f, 90f, false, null, Point(Team.Alpha, 100), Point(null, -40)));

            Assert.That(_view.Source, Is.SameAs(GameSignals.Domination.Value));
            Assert.That(_view.AlphaScoreText, Is.EqualTo(Localizer.Resolve(DominationHudText.TeamScore(Team.Alpha, 12f))));
            Assert.That(_view.BravoScoreText, Is.EqualTo(Localizer.Resolve(DominationHudText.TeamScore(Team.Bravo, 3f))));
            Assert.That(_view.TimeText, Is.EqualTo("01:30"));
            Assert.That(
                _view.PointTexts,
                Is.EqualTo(new[] { Localizer.Resolve(DominationHudText.PointOwner(0, Team.Alpha)), Localizer.Resolve(DominationHudText.PointOwner(1, null)) }));
            Assert.That(_view.ResultText, Is.Empty);
            Assert.That(_view.Element.style.display.value, Is.EqualTo(DisplayStyle.Flex));
            Assert.That(_view.Element.Query<Label>(className: DominationView.PointClass).ToList().Count, Is.EqualTo(2));
        }

        [Test]
        public void ANewStatusRewritesTheChangedFieldsAndTheResult()
        {
            _view.Bind(_root);
            GameSignals.Domination.Publish(Status(0f, 0f, 60f, false, null, Point(null, 0)));

            GameSignals.Domination.Publish(Status(30f, 0f, 0f, true, Team.Alpha, Point(Team.Alpha, 100)));

            Assert.That(_view.AlphaScoreText, Is.EqualTo(Localizer.Resolve(DominationHudText.TeamScore(Team.Alpha, 30f))));
            Assert.That(_view.TimeText, Is.EqualTo("00:00"));
            Assert.That(_view.PointTexts, Is.EqualTo(new[] { Localizer.Resolve(DominationHudText.PointOwner(0, Team.Alpha)) }));
            Assert.That(_view.ResultText, Is.EqualTo(Localizer.Resolve(HudFormat.Result(Team.Alpha))));
            var result = _view.Element.Q<Label>(className: DominationView.ResultClass);
            Assert.That(result.text, Is.EqualTo(_view.ResultText));
            Assert.That(result.style.display.value, Is.EqualTo(DisplayStyle.Flex));
        }

        [Test]
        public void ContestedPointsGetTheContestedClass()
        {
            _view.Bind(_root);

            GameSignals.Domination.Publish(Status(0f, 0f, 60f, false, null, new ControlPointStatus(Team.Bravo, -100, true)));

            var point = _view.Element.Q<Label>(className: DominationView.PointClass);
            Assert.That(point.ClassListContains(DominationView.ContestedClass), Is.True);
        }

        [Test]
        public void ClearingTheStatusHidesThePanel()
        {
            _view.Bind(_root);
            GameSignals.Domination.Publish(Status(0f, 0f, 60f, false, null, Point(null, 0)));

            GameSignals.Domination.Clear();

            Assert.That(_view.Source, Is.Null);
            Assert.That(_view.AlphaScoreText, Is.Empty);
            Assert.That(_view.Element.style.display.value, Is.EqualTo(DisplayStyle.None));
        }

        [Test]
        public void BindingCatchesUpWithTheCurrentStatus()
        {
            GameSignals.Domination.Publish(Status(5f, 0f, 45f, false, null, Point(null, 0)));

            _view.Bind(_root);

            Assert.That(_view.TimeText, Is.EqualTo("00:45"));
            Assert.That(_view.Element.style.display.value, Is.EqualTo(DisplayStyle.Flex));
        }

        [Test]
        public void AStatusClearedWhileUnboundIsHiddenWhenBoundAgain()
        {
            _view.Bind(_root);
            GameSignals.Domination.Publish(Status(0f, 0f, 45f, false, null, Point(null, 0)));
            _view.Unbind();

            GameSignals.Domination.Clear();
            _view.Bind(_root);

            Assert.That(_view.Source, Is.Null);
            Assert.That(_view.Element.style.display.value, Is.EqualTo(DisplayStyle.None));
        }

        [Test]
        public void AnUnboundViewIgnoresTheSignal()
        {
            GameSignals.Domination.Publish(Status(0f, 0f, 45f, false, null, Point(null, 0)));

            Assert.That(_view.Source, Is.Null);
            Assert.That(_points.IsShown, Is.False);
        }

        [Test]
        public void ControlPointsShowThePublishedPoints()
        {
            _points.Bind(_root);
            var states = new[] { Point(Team.Alpha, 100), new ControlPointStatus(null, -40, true) };

            GameSignals.Domination.Publish(Status(0f, 0f, 60f, false, null, states));

            Assert.That(_points.Statuses, Is.EqualTo(states));
            Assert.That(_points.PointNames, Is.EqualTo(new[] { "A", "B" }));
            Assert.That(_points.Element.Query(className: ControlPointsView.PointClass).ToList().Count, Is.EqualTo(2));

            GameSignals.Domination.Clear();

            Assert.That(_points.IsShown, Is.False);
            Assert.That(_points.Element.style.display.value, Is.EqualTo(DisplayStyle.None));
        }

        [Test]
        public void ControlPointsCatchUpWhenBound()
        {
            var states = new[] { Point(null, 20) };
            GameSignals.Domination.Publish(Status(0f, 0f, 60f, false, null, states));

            _points.Bind(_root);

            Assert.That(_points.Statuses, Is.EqualTo(states));
            Assert.That(_points.Element.style.display.value, Is.EqualTo(DisplayStyle.Flex));
        }

        [Test]
        public void ControlPointsShownDirectlyStayWhileTheSignalIsEmpty()
        {
            _points.Bind(_root);
            var states = new[] { Point(Team.Bravo, -100) };

            _points.Show(states);
            GameSignals.Domination.Publish(Status(0f, 0f, 60f, false, null, Point(null, 0)));
            _points.Show(states);
            GameSignals.Domination.Clear();

            Assert.That(_points.Statuses, Is.EqualTo(states), "a clear only takes back what the signal showed");
        }

        private static ControlPointStatus Point(Team? owner, int percent)
        {
            return new ControlPointStatus(owner, percent, false);
        }

        private static DominationStatus Status(float alpha, float bravo, float remaining, bool ended, Team? winner, params ControlPointStatus[] points)
        {
            return new DominationStatus(alpha, bravo, remaining, points, ended, winner);
        }
    }
}
