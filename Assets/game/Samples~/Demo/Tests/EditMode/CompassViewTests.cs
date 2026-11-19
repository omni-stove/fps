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
    public sealed class CompassViewTests
    {
        private const float Tolerance = 1e-4f;

        private readonly List<GameObject> _objects = new List<GameObject>();
        private CompassView _view;

        [SetUp]
        public void SetUp()
        {
            var hudObject = new GameObject("Hud");
            _objects.Add(hudObject);
            _view = hudObject.AddComponent<CompassView>();
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
        public void BearingsAreMeasuredClockwiseFromNorth()
        {
            Assert.That(CompassView.BearingDegrees(Vector3.Zero, new Vector3(0f, 0f, 10f)), Is.EqualTo(0f).Within(Tolerance));
            Assert.That(CompassView.BearingDegrees(Vector3.Zero, new Vector3(10f, 0f, 0f)), Is.EqualTo(90f).Within(Tolerance));
            Assert.That(CompassView.BearingDegrees(Vector3.Zero, new Vector3(0f, 0f, -10f)), Is.EqualTo(180f).Within(Tolerance));
            Assert.That(CompassView.BearingDegrees(Vector3.Zero, new Vector3(-10f, 0f, 0f)), Is.EqualTo(270f).Within(Tolerance));
            Assert.That(CompassView.BearingDegrees(Vector3.Zero, new Vector3(0f, 30f, 0f)), Is.Null, "straight above has no bearing");
        }

        [Test]
        public void TheHeadingSitsInTheMiddleAndTheSpanReachesTheEdges()
        {
            Assert.That(CompassView.BandPosition(0f, 0f, 180f), Is.EqualTo(0.5f).Within(Tolerance));
            Assert.That(CompassView.BandPosition(0f, 90f, 180f), Is.EqualTo(1f).Within(Tolerance));
            Assert.That(CompassView.BandPosition(0f, 270f, 180f), Is.EqualTo(0f).Within(Tolerance));
            Assert.That(CompassView.BandPosition(350f, 10f, 180f), Is.EqualTo(0.5f + 20f / 180f).Within(Tolerance), "wraps across north");
            Assert.That(CompassView.BandPosition(10f, 350f, 180f), Is.EqualTo(0.5f - 20f / 180f).Within(Tolerance));
            Assert.That(CompassView.BandPosition(0f, 180f, 180f), Is.Null, "behind is off the band");
            Assert.That(CompassView.BandPosition(0f, 30f, 40f), Is.Null);
            Assert.Throws<System.ArgumentOutOfRangeException>(() => CompassView.BandPosition(0f, 0f, 0f));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => CompassView.BandPosition(0f, 0f, 361f));
        }

        [Test]
        public void TheCardinalKeysAreInTheMinimapTables()
        {
            Assert.That(CompassView.CardinalKey(0), Is.EqualTo("minimap.compass.N"));
            Assert.That(CompassView.CardinalKey(7), Is.EqualTo("minimap.compass.NW"));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => CompassView.CardinalKey(8));

            foreach (var language in new[] { GameLanguage.English, GameLanguage.Japanese })
            {
                var minimap = JapaneseGameTextSetUp.LoadLayers(language)
                    .Single(layer => layer.Key.EndsWith("minimap.json", System.StringComparison.Ordinal))
                    .Value;
                for (var i = 0; i < CompassView.CardinalSuffixes.Count; i++)
                {
                    Assert.That(minimap.Keys, Does.Contain(CompassView.CardinalKey(i)), language.ToString());
                }
            }
        }

        [Test]
        public void TurningMovesThePointsAndPinsAlongTheBand()
        {
            _view.Bind(MinimapViewTests.CloneLayout());
            Assert.That(_view.Element, Is.Not.Null, "Hud.uxml has the compass element");
            Assert.That(_view.Element.style.display.value, Is.EqualTo(DisplayStyle.None), "nothing is shown without a status");

            var labels = _view.Element.Query<Label>(className: CompassView.LabelClass).ToList();
            Assert.That(labels.Count, Is.EqualTo(8));
            Assert.That(labels[0].text, Is.EqualTo("北"));
            Assert.That(labels[2].text, Is.EqualTo("東"));

            // 北を向いて、北東のピン・東の目標・後ろの敵 (コンパスには出さない) と、南のピン (帯の外)。
            GameSignals.Minimap.Publish(MinimapViewTests.Status(Vector3.Zero, 0f,
                MinimapViewTests.Contact(MinimapContactKind.Ping, new Vector3(10f, 0f, 10f)),
                MinimapViewTests.Contact(MinimapContactKind.Objective, new Vector3(10f, 0f, 0f)),
                MinimapViewTests.Contact(MinimapContactKind.Enemy, new Vector3(0f, 0f, 10f)),
                MinimapViewTests.Contact(MinimapContactKind.Ping, new Vector3(0f, 0f, -10f))));

            Assert.That(_view.Element.style.display.value, Is.EqualTo(DisplayStyle.Flex));
            Assert.That(_view.Markers.Count, Is.EqualTo(2));
            Assert.That(_view.Markers[0].Kind, Is.EqualTo(MinimapContactKind.Ping));
            Assert.That(_view.Markers[0].X, Is.EqualTo(0.75f).Within(Tolerance));
            Assert.That(_view.Markers[1].Kind, Is.EqualTo(MinimapContactKind.Objective));
            Assert.That(_view.Markers[1].X, Is.EqualTo(1f).Within(Tolerance));
            Assert.That(_view.Element.Query<VisualElement>(className: CompassView.MarkerClass).ToList().Count, Is.EqualTo(2));
            Assert.That(_view.TryGetCardinalPosition(0, out var north), Is.True);
            Assert.That(north, Is.EqualTo(0.5f).Within(Tolerance));
            AssertPercent(labels[0].style.left, 50f);
            Assert.That(_view.TryGetCardinalPosition(4, out _), Is.False, "south is behind");
            Assert.That(labels[4].style.display.value, Is.EqualTo(DisplayStyle.None));

            // 東を向くと、東が中央に、北が左端に来る。
            GameSignals.Minimap.Publish(MinimapViewTests.Status(Vector3.Zero, 90f,
                MinimapViewTests.Contact(MinimapContactKind.Ping, new Vector3(10f, 0f, 10f)),
                MinimapViewTests.Contact(MinimapContactKind.Objective, new Vector3(10f, 0f, 0f))));

            Assert.That(_view.TryGetCardinalPosition(2, out var east), Is.True);
            Assert.That(east, Is.EqualTo(0.5f).Within(Tolerance));
            Assert.That(_view.TryGetCardinalPosition(0, out north), Is.True);
            Assert.That(north, Is.EqualTo(0f).Within(Tolerance));
            AssertPercent(labels[2].style.left, 50f);
            Assert.That(_view.Markers[0].X, Is.EqualTo(0.25f).Within(Tolerance));
            Assert.That(_view.Markers[1].X, Is.EqualTo(0.5f).Within(Tolerance));

            GameSignals.Minimap.Clear();
            Assert.That(_view.Markers, Is.Empty);
            Assert.That(_view.Element.style.display.value, Is.EqualTo(DisplayStyle.None));
        }

        [Test]
        public void TheBandHidesWithTheMapUnlessTheHudShowsEverything()
        {
            _view.Bind(MinimapViewTests.CloneLayout());

            GameSignals.HudDensity.Publish(HudDensity.Minimal);
            Assert.That(_view.IsAllowed, Is.False);
            Assert.That(_view.Element.style.visibility.value, Is.EqualTo(Visibility.Hidden));

            GameSignals.HudDensity.Publish(HudDensity.Full);
            Assert.That(_view.IsAllowed, Is.True);
            Assert.That(_view.Element.style.visibility.value, Is.EqualTo(Visibility.Visible));
        }

        [Test]
        public void BetweenMapUpdatesTheBandFollowsTheLiveHeadingOnline()
        {
            _view.Bind(MinimapViewTests.CloneLayout());
            var live = 90f;
            GameSignals.LocalHeadingDegrees.Publish(() => live);

            // 知らせの向きは北のままでも、今の向き (東) を中央に置く。
            GameSignals.Minimap.Publish(MinimapViewTests.Status(Vector3.Zero, 0f,
                MinimapViewTests.Contact(MinimapContactKind.Objective, new Vector3(10f, 0f, 0f))));
            Assert.That(_view.HeadingDegrees, Is.EqualTo(90f).Within(Tolerance));
            Assert.That(_view.TryGetCardinalPosition(2, out var east), Is.True);
            Assert.That(east, Is.EqualTo(0.5f).Within(Tolerance));
            Assert.That(_view.Markers[0].X, Is.EqualTo(0.5f).Within(Tolerance));

            // 次の知らせを待たずに、フレームごとに向きを読み直す。
            live = 45f;
            _view.FollowHeading();
            Assert.That(_view.HeadingDegrees, Is.EqualTo(45f).Within(Tolerance));
            Assert.That(_view.TryGetCardinalPosition(0, out var north), Is.True);
            Assert.That(north, Is.EqualTo(0.25f).Within(Tolerance));
            Assert.That(_view.Markers[0].X, Is.EqualTo(0.75f).Within(Tolerance));
            var labels = _view.Element.Query<Label>(className: CompassView.LabelClass).ToList();
            AssertPercent(labels[0].style.left, 25f);

            live = -30f;
            _view.FollowHeading();
            Assert.That(_view.HeadingDegrees, Is.EqualTo(330f).Within(Tolerance), "wraps into 0 to 360");

            live = float.NaN;
            _view.FollowHeading();
            Assert.That(_view.HeadingDegrees, Is.EqualTo(0f).Within(Tolerance), "a broken reading falls back to the map's heading");

            GameSignals.LocalHeadingDegrees.Clear();
            live = 90f;
            _view.FollowHeading();
            Assert.That(_view.HeadingDegrees, Is.EqualTo(0f).Within(Tolerance), "without a live source the map's heading is used");
        }

        [Test]
        public void BetweenMapUpdatesTheBandFollowsTheLocalPlayerOffline()
        {
            _view.Bind(MinimapViewTests.CloneLayout());
            var player = new GameObject("Player");
            _objects.Add(player);
            player.transform.rotation = Quaternion.Euler(0f, 90f, 0f);
            GameSignals.LocalPlayer.Publish(new LocalPlayerSignal(player, null, null, null, null, null, null, null));

            GameSignals.Minimap.Publish(MinimapViewTests.Status(Vector3.Zero, 0f));
            Assert.That(_view.HeadingDegrees, Is.EqualTo(90f).Within(Tolerance));

            player.transform.rotation = Quaternion.Euler(0f, 270f, 0f);
            _view.FollowHeading();
            Assert.That(_view.HeadingDegrees, Is.EqualTo(270f).Within(Tolerance));
            Assert.That(_view.TryGetCardinalPosition(6, out var west), Is.True);
            Assert.That(west, Is.EqualTo(0.5f).Within(Tolerance));

            // 真上を向いて方位が出ないときは、知らせの向きにする。
            player.transform.rotation = Quaternion.Euler(-90f, 0f, 0f);
            _view.FollowHeading();
            Assert.That(_view.HeadingDegrees, Is.EqualTo(0f).Within(Tolerance));
        }

        [Test]
        public void AStatusShownByHandKeepsItsOwnHeading()
        {
            _view.Bind(MinimapViewTests.CloneLayout());
            GameSignals.LocalHeadingDegrees.Publish(() => 90f);

            _view.Show(MinimapViewTests.Status(Vector3.Zero, 180f));
            _view.FollowHeading();

            Assert.That(_view.HeadingDegrees, Is.EqualTo(180f).Within(Tolerance));
        }

        private static void AssertPercent(StyleLength length, float expected)
        {
            Assert.That(length.value.unit, Is.EqualTo(LengthUnit.Percent));
            Assert.That(length.value.value, Is.EqualTo(expected).Within(1e-2f));
        }
    }
}
