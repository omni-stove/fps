using Shooter.Core;
using Shooter.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace Shooter.Tests.EditMode
{
    public sealed class ControlPointsViewTests
    {
        private GameObject _owner;
        private ControlPointsView _points;
        private VisualElement _root;

        [SetUp]
        public void SetUp()
        {
            GameSignals.ResetAll();
            var layout = Resources.Load<VisualTreeAsset>(Hud.LayoutResourcePath);
            Assert.That(layout, Is.Not.Null, "Assets/game/Samples~/Demo/UI/Resources/Hud/Hud.uxml should be loadable from Resources");
            _root = layout.CloneTree();
            _owner = new GameObject(nameof(ControlPointsViewTests));
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
        public void ShowsThePointsOfADominationDetailUntilTheStatusIsCleared()
        {
            _points.Bind(_root);
            var states = new[] { Point(Team.Alpha, 100), new ControlPointStatus(null, -40, true) };

            GameSignals.ModeStatus.Publish(Mode(states));

            Assert.That(_points.Statuses, Is.EqualTo(states));
            Assert.That(_points.PointNames, Is.EqualTo(new[] { "A", "B" }));
            Assert.That(_points.Element.Query(className: ControlPointsView.PointClass).ToList().Count, Is.EqualTo(2));
            Assert.That(_points.Element.style.display.value, Is.EqualTo(DisplayStyle.Flex));

            GameSignals.ModeStatus.Clear();

            Assert.That(_points.IsShown, Is.False);
            Assert.That(_points.Element.style.display.value, Is.EqualTo(DisplayStyle.None));
        }

        [Test]
        public void StaysHiddenForAnotherModesDetail()
        {
            _points.Bind(_root);
            GameSignals.ModeStatus.Publish(Mode(Point(Team.Alpha, 100)));

            GameSignals.ModeStatus.Publish(new ModeStatus(60f, new[] { new SideScore(Side.Alpha, 0d), new SideScore(Side.Bravo, 0d) }, Side.Alpha, null, false, null, null));

            Assert.That(_points.IsShown, Is.False);
            Assert.That(_points.Element.style.display.value, Is.EqualTo(DisplayStyle.None));
        }

        [Test]
        public void AnUnboundViewIgnoresTheSignal()
        {
            GameSignals.ModeStatus.Publish(Mode(Point(null, 0)));

            Assert.That(_points.IsShown, Is.False);
        }

        [Test]
        public void CatchesUpWhenBound()
        {
            var states = new[] { Point(null, 20) };
            GameSignals.ModeStatus.Publish(Mode(states));

            _points.Bind(_root);

            Assert.That(_points.Statuses, Is.EqualTo(states));
            Assert.That(_points.Element.style.display.value, Is.EqualTo(DisplayStyle.Flex));
        }

        [Test]
        public void UsesTheHudTeamColors()
        {
            _points.Bind(_root);

            _owner.GetComponent<Hud>().SetTeamColors(Color.red, Color.blue);

            Assert.That(_points.ColorOf(Team.Alpha), Is.EqualTo(Color.red));
            Assert.That(_points.ColorOf(Team.Bravo), Is.EqualTo(Color.blue));
            Assert.That(_points.ColorOf(null), Is.EqualTo(TeamColors.Neutral));
        }

        [Test]
        public void ContestedPointsTakeThePalettesContestedColorAndFollowItsChanges()
        {
            var before = AppliedControlSettings.Current;
            var volume = AudioListener.volume;
            try
            {
                AppliedControlSettings.Apply(before.WithPalette(ColorPalette.Standard));
                _points.Bind(_root);
                GameSignals.ModeStatus.Publish(Mode(new ControlPointStatus(Team.Bravo, -40, true)));

                Assert.That(_points.ContestedColor, Is.EqualTo(ControlPointsView.ContestedColorOf(TeamPalette.Standard)));

                AppliedControlSettings.Apply(before.WithPalette(ColorPalette.RedGreen));

                Assert.That(_points.ContestedColor, Is.EqualTo(ControlPointsView.ContestedColorOf(TeamPalette.RedGreen)));

                // Hud の色を決め打ちにしても、競合の色はパレットに従う
                _owner.GetComponent<Hud>().SetTeamColors(Color.red, Color.blue);
                AppliedControlSettings.Apply(before.WithPalette(ColorPalette.BlueYellow));

                Assert.That(_points.ContestedColor, Is.EqualTo(ControlPointsView.ContestedColorOf(TeamPalette.BlueYellow)));
                Assert.That(_points.ColorOf(Team.Alpha), Is.EqualTo(Color.red));
            }
            finally
            {
                AppliedControlSettings.Apply(before);
                AudioListener.volume = volume;
            }
        }

        [Test]
        public void TheContestedColorIsThePalettesWithTheRingAlpha()
        {
            foreach (var palette in TeamPalette.All)
            {
                var color = ControlPointsView.ContestedColorOf(palette);
                Assert.That(color.r, Is.EqualTo(palette.Contested.R));
                Assert.That(color.g, Is.EqualTo(palette.Contested.G));
                Assert.That(color.b, Is.EqualTo(palette.Contested.B));
                Assert.That(color.a, Is.EqualTo(ControlPointsView.ContestedAlpha));
            }

            Assert.That(ControlPointsView.ContestedColorOf(TeamPalette.RedGreen), Is.Not.EqualTo(ControlPointsView.ContestedColorOf(TeamPalette.Standard)));
            Assert.Throws<System.ArgumentNullException>(() => ControlPointsView.ContestedColorOf(null));
        }

        private static ControlPointStatus Point(Team? owner, int percent)
        {
            return new ControlPointStatus(owner, percent, false);
        }

        private static ModeStatus Mode(params ControlPointStatus[] points)
        {
            var detail = new DominationStatus(0f, 0f, 60f, points, false, null);
            return new ModeStatus(60f, new[] { new SideScore(Side.Alpha, 0d), new SideScore(Side.Bravo, 0d) }, Side.Alpha, null, false, null, detail);
        }
    }
}
