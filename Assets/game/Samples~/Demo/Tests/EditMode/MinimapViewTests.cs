using System.Collections.Generic;
using System.Linq;
using Shooter.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;
using Vector3 = System.Numerics.Vector3;

namespace Shooter.Core.Tests
{
    /// <summary>
    /// ミニマップ (MinimapView) が、出された MinimapStatus から印の数と地図の上の位置を決めることと、
    /// HUD の量がすべて出すときでなければ隠れることを確かめる。
    /// </summary>
    public sealed class MinimapViewTests
    {
        private const float Tolerance = 1e-4f;

        private readonly List<GameObject> _objects = new List<GameObject>();
        private MinimapView _view;

        [SetUp]
        public void SetUp()
        {
            var hudObject = new GameObject("Hud");
            _objects.Add(hudObject);
            _view = hudObject.AddComponent<MinimapView>();
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var go in _objects)
            {
                if (go != null)
                {
                    Object.DestroyImmediate(go);
                }
            }

            _objects.Clear();
            GameSignals.ResetAll();
        }

        [Test]
        public void NorthUpPutsEachContactAtItsOffsetScaledByTheRange()
        {
            var status = Status(new Vector3(10f, 0f, 10f), 0f);

            var north = MinimapView.Place(status, Contact(MinimapContactKind.Ally, new Vector3(10f, 0f, 35f)), 50f, false);
            Assert.That(north.X, Is.EqualTo(0.5f).Within(Tolerance));
            Assert.That(north.Y, Is.EqualTo(0.25f).Within(Tolerance));
            Assert.That(north.AtEdge, Is.False);

            var east = MinimapView.Place(status, Contact(MinimapContactKind.Enemy, new Vector3(35f, 0f, 10f)), 50f, false);
            Assert.That(east.X, Is.EqualTo(0.75f).Within(Tolerance));
            Assert.That(east.Y, Is.EqualTo(0.5f).Within(Tolerance));

            // 高さは見ないので、真上と真下は中心に重なる。
            var above = MinimapView.Place(status, Contact(MinimapContactKind.Ping, new Vector3(10f, 20f, 10f)), 50f, false);
            Assert.That(above.X, Is.EqualTo(0.5f).Within(Tolerance));
            Assert.That(above.Y, Is.EqualTo(0.5f).Within(Tolerance));
            Assert.That(above.AtEdge, Is.False);
        }

        [Test]
        public void AContactBeyondTheRangeSitsOnTheRimInItsDirection()
        {
            var status = Status(new Vector3(10f, 0f, 10f), 0f);

            var south = MinimapView.Place(status, Contact(MinimapContactKind.Ping, new Vector3(10f, 0f, -90f)), 50f, false);
            Assert.That(south.X, Is.EqualTo(0.5f).Within(Tolerance));
            Assert.That(south.Y, Is.EqualTo(1f).Within(Tolerance));
            Assert.That(south.AtEdge, Is.True);

            // 北東に 100 m ずつ (半径の 2√2 倍) なら、45 度の向きの縁 (中心から 0.5) に寄る。
            var northEast = MinimapView.Place(status, Contact(MinimapContactKind.Objective, new Vector3(110f, 0f, 110f)), 50f, false);
            Assert.That(northEast.X, Is.EqualTo(0.85355f).Within(Tolerance));
            Assert.That(northEast.Y, Is.EqualTo(0.14645f).Within(Tolerance));
            Assert.That(northEast.AtEdge, Is.True);
        }

        [Test]
        public void RotatingPutsWhereTheSelfFacesAtTheTop()
        {
            // 東を向いていると、東は上、北は左に来る。味方の向きは自分の向きを引いた分だけ回る。
            var status = Status(Vector3.Zero, 90f);

            var east = MinimapView.Place(status, Contact(MinimapContactKind.Ally, new Vector3(25f, 0f, 0f), 135f), 50f, true);
            Assert.That(east.X, Is.EqualTo(0.5f).Within(Tolerance));
            Assert.That(east.Y, Is.EqualTo(0.25f).Within(Tolerance));
            Assert.That(east.RotationDegrees, Is.EqualTo(45f).Within(Tolerance));

            var north = MinimapView.Place(status, Contact(MinimapContactKind.Ally, new Vector3(0f, 0f, 25f)), 50f, true);
            Assert.That(north.X, Is.EqualTo(0.25f).Within(Tolerance));
            Assert.That(north.Y, Is.EqualTo(0.5f).Within(Tolerance));

            var northUp = MinimapView.Place(status, Contact(MinimapContactKind.Ally, new Vector3(25f, 0f, 0f), 135f), 50f, false);
            Assert.That(northUp.X, Is.EqualTo(0.75f).Within(Tolerance));
            Assert.That(northUp.Y, Is.EqualTo(0.5f).Within(Tolerance));
            Assert.That(northUp.RotationDegrees, Is.EqualTo(135f).Within(Tolerance));
        }

        [Test]
        public void ARangeThatIsNotPositiveIsRejected()
        {
            var status = Status(Vector3.Zero, 0f);
            var contact = Contact(MinimapContactKind.Ally, Vector3.Zero);
            Assert.Throws<System.ArgumentOutOfRangeException>(() => MinimapView.Place(status, contact, 0f, true));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => MinimapView.Place(status, contact, float.PositiveInfinity, true));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => _view.RangeMeters = -1f);
            Assert.That(_view.RangeMeters, Is.EqualTo(MinimapView.DefaultRangeMeters));
        }

        [Test]
        public void ColorsFollowThePaletteFromTheViewersSide()
        {
            var palette = TeamPalette.For(ColorPalette.Standard);

            Assert.That(MinimapView.ColorOf(palette, MinimapContactKind.Ally, null, null), Is.EqualTo(TeamColors.ToColor(palette.Friendly)));
            Assert.That(MinimapView.ColorOf(palette, MinimapContactKind.Enemy, null, null), Is.EqualTo(TeamColors.ToColor(palette.Enemy)));
            Assert.That(MinimapView.ColorOf(palette, MinimapContactKind.Ally, Team.Bravo, Team.Bravo), Is.EqualTo(TeamColors.ToColor(palette.Friendly)));
            Assert.That(MinimapView.ColorOf(palette, MinimapContactKind.Enemy, Team.Alpha, Team.Bravo), Is.EqualTo(TeamColors.ToColor(palette.Enemy)));
            Assert.That(MinimapView.ColorOf(palette, MinimapContactKind.Ping, Team.Alpha, Team.Alpha), Is.EqualTo(TeamColors.ToColor(palette.Highlight)));
            Assert.That(MinimapView.ColorOf(palette, MinimapContactKind.Objective, null, Team.Alpha), Is.EqualTo(TeamColors.ToColor(palette.Neutral)));
        }

        [Test]
        public void TheBoundViewDrawsOneMarkPerPublishedContactWhereItIsPlaced()
        {
            _view.Bind(CloneLayout());
            Assert.That(_view.Element, Is.Not.Null, "Hud.uxml has the minimap element");
            Assert.That(_view.Element.style.display.value, Is.EqualTo(DisplayStyle.None), "nothing is shown without a status");

            GameSignals.Minimap.Publish(Status(new Vector3(10f, 0f, 10f), 0f,
                Contact(MinimapContactKind.Ally, new Vector3(10f, 0f, 35f)),
                Contact(MinimapContactKind.Enemy, new Vector3(35f, 0f, 10f)),
                Contact(MinimapContactKind.Ping, new Vector3(10f, 0f, -90f))));

            Assert.That(_view.Element.style.display.value, Is.EqualTo(DisplayStyle.Flex));
            Assert.That(_view.Marks.Count, Is.EqualTo(3));
            var marks = _view.Element.Query<VisualElement>(className: MinimapView.ContactClass).ToList();
            Assert.That(marks.Count, Is.EqualTo(3));
            Assert.That(_view.Element.Query<VisualElement>(className: MinimapView.SelfClass).ToList().Count, Is.EqualTo(1));

            Assert.That(marks[0].ClassListContains(MinimapView.AllyClass), Is.True);
            AssertPercent(marks[0].style.left, 50f);
            AssertPercent(marks[0].style.top, 25f);
            Assert.That(marks[1].ClassListContains(MinimapView.EnemyClass), Is.True);
            AssertPercent(marks[1].style.left, 75f);
            AssertPercent(marks[1].style.top, 50f);
            Assert.That(marks[2].ClassListContains(MinimapView.PingClass), Is.True);
            Assert.That(marks[2].ClassListContains(MinimapView.EdgeClass), Is.True);
            AssertPercent(marks[2].style.left, 50f);
            AssertPercent(marks[2].style.top, 100f);

            // 1 つ減ると、要素も 1 つ減る。
            GameSignals.Minimap.Publish(Status(new Vector3(10f, 0f, 10f), 0f,
                Contact(MinimapContactKind.Objective, new Vector3(10f, 0f, 10f))));
            marks = _view.Element.Query<VisualElement>(className: MinimapView.ContactClass).ToList();
            Assert.That(marks.Count, Is.EqualTo(1));
            Assert.That(marks[0].ClassListContains(MinimapView.ObjectiveClass), Is.True);
            Assert.That(marks[0].ClassListContains(MinimapView.AllyClass), Is.False);
            AssertPercent(marks[0].style.left, 50f);
            AssertPercent(marks[0].style.top, 50f);

            GameSignals.Minimap.Clear();
            Assert.That(_view.Marks, Is.Empty);
            Assert.That(_view.Element.style.display.value, Is.EqualTo(DisplayStyle.None));
        }

        [Test]
        public void BindingPicksUpTheStatusAlreadyPublished()
        {
            GameSignals.Minimap.Publish(Status(Vector3.Zero, 0f, Contact(MinimapContactKind.Ally, new Vector3(0f, 0f, 10f))));

            _view.Bind(CloneLayout());

            Assert.That(_view.Status, Is.EqualTo(GameSignals.Minimap.Value));
            Assert.That(_view.Element.Query<VisualElement>(className: MinimapView.ContactClass).ToList().Count, Is.EqualTo(1));
        }

        [Test]
        public void TheMapHidesUnlessTheHudShowsEverything()
        {
            _view.Bind(CloneLayout());
            Assert.That(_view.IsAllowed, Is.True);

            GameSignals.HudDensity.Publish(HudDensity.Minimal);
            Assert.That(_view.IsAllowed, Is.False);
            Assert.That(_view.Element.style.visibility.value, Is.EqualTo(Visibility.Hidden));

            GameSignals.HudDensity.Publish(HudDensity.Hardcore);
            Assert.That(_view.IsAllowed, Is.False);

            GameSignals.HudDensity.Publish(HudDensity.Full);
            Assert.That(_view.IsAllowed, Is.True);
            Assert.That(_view.Element.style.visibility.value, Is.EqualTo(Visibility.Visible));
        }

        [Test]
        public void ADensityPublishedBeforeBindingHidesTheMapOnBind()
        {
            GameSignals.HudDensity.Publish(HudDensity.Minimal);

            _view.Bind(CloneLayout());

            Assert.That(_view.IsAllowed, Is.False);
            Assert.That(_view.Element.style.visibility.value, Is.EqualTo(Visibility.Hidden));
        }

        [Test]
        public void UnbindingStopsFollowingTheSignal()
        {
            _view.Bind(CloneLayout());
            var element = _view.Element;
            _view.Unbind();

            GameSignals.Minimap.Publish(Status(Vector3.Zero, 0f, Contact(MinimapContactKind.Ally, new Vector3(0f, 0f, 10f))));

            Assert.That(_view.Status, Is.Null);
            Assert.That(element.Query<VisualElement>(className: MinimapView.ContactClass).ToList(), Is.Empty);
        }

        internal static MinimapStatus Status(Vector3 self, float heading, params MinimapContact[] contacts)
        {
            return new MinimapStatus(self, heading, contacts);
        }

        internal static MinimapContact Contact(MinimapContactKind kind, Vector3 position, float heading = 0f)
        {
            return new MinimapContact(kind, position, heading, null);
        }

        internal static VisualElement CloneLayout()
        {
            var layout = Resources.Load<VisualTreeAsset>(Hud.LayoutResourcePath);
            Assert.That(layout, Is.Not.Null, "Assets/game/Samples~/Demo/UI/Resources/Hud/Hud.uxml should be loadable from Resources");
            return layout.CloneTree();
        }

        private static void AssertPercent(StyleLength length, float expected)
        {
            Assert.That(length.value.unit, Is.EqualTo(LengthUnit.Percent));
            Assert.That(length.value.value, Is.EqualTo(expected).Within(1e-2f));
        }
    }
}
