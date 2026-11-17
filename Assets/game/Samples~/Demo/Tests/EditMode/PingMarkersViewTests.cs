using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Fps.Core;
using Fps.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace Fps.Tests.EditMode
{
    /// <summary>
    /// 既定の印の表示 (PingMarkersView) が、画面の中の印をその位置に、外の印を端に寄せて向きを出すことと、
    /// 要素を持っている間は GameSignals.Pings を購読して出された印を出すことを確かめる。
    /// </summary>
    public sealed class PingMarkersViewTests
    {
        private readonly List<GameObject> _objects = new List<GameObject>();
        private PingController _pinger;
        private Camera _camera;
        private PingMarkersView _view;
        private float _now;

        [SetUp]
        public void SetUp()
        {
            _now = 0f;

            var cameraObject = Track(new GameObject("ViewCamera"));
            cameraObject.transform.SetPositionAndRotation(new Vector3(0f, 1f, 0f), Quaternion.identity);
            _camera = cameraObject.AddComponent<Camera>();

            // batchmode では画面の大きさが決まらないことがあるので、縦横比を決めておく。
            _camera.aspect = 16f / 9f;

            var pingerObject = Track(new GameObject("Pinger"));
            pingerObject.transform.position = new Vector3(0f, 1f, 0f);
            _pinger = pingerObject.AddComponent<PingController>();
            _pinger.Clock = () => _now;
            _pinger.Configure(GameRules.Default.WithActions(GameRules.Default.Actions.WithPing(Fps.Core.Ping.On)));

            var wall = Track(GameObject.CreatePrimitive(PrimitiveType.Cube));
            wall.transform.position = new Vector3(0f, 1f, 10f);
            wall.transform.localScale = new Vector3(4f, 4f, 0.5f);
            Physics.SyncTransforms();

            var hudObject = Track(new GameObject("Hud"));
            _view = hudObject.AddComponent<PingMarkersView>();
            _view.Viewer = _camera;
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
        public void AMarkInViewIsDrawnWhereItIs()
        {
            _pinger.TryPingAlong(new Vector3(0f, 1f, 0f), Vector3.forward, out var marker);

            LogAssert.Expect(LogType.Log, new Regex($@"^\[Ping\] screen id={marker.Id} onScreen=true edge=None "));
            _view.Show(_pinger.Markers);

            Assert.That(_view.Markers, Is.SameAs(_pinger.Markers));
            Assert.That(_view.TryGetPlacement(marker.Id, out var placement), Is.True);
            Assert.That(placement.IsOnScreen, Is.True);
            Assert.That(placement.X, Is.EqualTo(0.5f).Within(1e-3f));
            Assert.That(placement.Y, Is.EqualTo(0.5f).Within(1e-3f));
            Assert.That(_view.ShownCount, Is.EqualTo(1));
        }

        [Test]
        public void AMarkOutOfViewMovesToTheEdgeOnItsSide()
        {
            _pinger.TryPingAlong(new Vector3(0f, 1f, 0f), Vector3.forward, out var marker);
            _view.Show(_pinger.Markers);

            // 右を向くと、正面にあった印は左に外れる。真横 (90 度) はカメラの面の上で投影が決まらないので、少し手前にする。
            _camera.transform.rotation = Quaternion.Euler(0f, 80f, 0f);
            LogAssert.Expect(LogType.Log, new Regex($@"^\[Ping\] screen id={marker.Id} onScreen=false edge=Left angle=-90\.0 "));
            _view.Refresh();
            Assert.That(_view.TryGetPlacement(marker.Id, out var left), Is.True);
            Assert.That(left.IsOnScreen, Is.False);
            Assert.That(left.Edge, Is.EqualTo(PingScreenEdge.Left));

            _camera.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
            _view.Refresh();
            Assert.That(_view.TryGetPlacement(marker.Id, out var behind), Is.True);
            Assert.That(behind.Edge, Is.EqualTo(PingScreenEdge.Bottom), "a mark straight behind goes to the bottom");
        }

        [Test]
        public void TheBoundViewDrawsThePublishedMarksAndDropsThemWhenTheyExpire()
        {
            var root = CloneLayout();
            LogAssert.Expect(LogType.Log, PingLog.View(nameof(PingMarkersView)));
            _view.Bind(root);
            Assert.That(_view.Element, Is.Not.Null, "Hud.uxml has the ping-markers element");
            Assert.That(_view.Markers, Is.SameAs(_pinger.Markers), "binding picks up the marks already published");
            Assert.That(_view.Element.style.display.value, Is.EqualTo(DisplayStyle.None), "nothing is shown without marks");

            // 付けると GameSignals.Pings に出し直され、表示はそれを受けて要素を足す。
            _pinger.TryPingAlong(new Vector3(0f, 1f, 0f), Vector3.forward, out var marker);

            var marks = _view.Element.Query<VisualElement>(className: PingMarkersView.MarkerClass).ToList();
            Assert.That(marks.Count(), Is.EqualTo(1));
            Assert.That(marks[0].ClassListContains(PingMarkersView.LocationClass), Is.True);
            Assert.That(marks[0].ClassListContains(PingMarkersView.OffScreenClass), Is.False);
            Assert.That(_view.Element.style.display.value, Is.EqualTo(DisplayStyle.Flex));

            _camera.transform.rotation = Quaternion.Euler(0f, 80f, 0f);
            _view.Refresh();
            Assert.That(marks[0].ClassListContains(PingMarkersView.OffScreenClass), Is.True);

            _now = _pinger.Spec.LifetimeSeconds;
            _pinger.Tick(_now);

            Assert.That(_view.TryGetPlacement(marker.Id, out _), Is.False);
            Assert.That(_view.Element.Query<VisualElement>(className: PingMarkersView.MarkerClass).ToList(), Is.Empty);
            Assert.That(_view.Element.style.display.value, Is.EqualTo(DisplayStyle.None));
        }

        [Test]
        public void ClearingTheSignalRemovesTheMarksAndUnbindingStopsListening()
        {
            _pinger.TryPingAlong(new Vector3(0f, 1f, 0f), Vector3.forward, out var marker);
            _view.Bind(CloneLayout());
            Assert.That(_view.TryGetPlacement(marker.Id, out _), Is.True);

            _pinger.Configure(GameRules.Default.WithActions(GameRules.Default.Actions.WithPing(Fps.Core.Ping.Off)));
            Assert.That(_view.Markers, Is.Null, "turning Ping off clears the signal and the view");
            Assert.That(_view.ShownCount, Is.Zero);

            _view.Unbind();
            GameSignals.Pings.Publish(new[] { marker });
            Assert.That(_view.Markers, Is.Null, "an unbound view does not follow the signal");

            _view.Bind(CloneLayout());
            Assert.That(_view.TryGetPlacement(marker.Id, out _), Is.True, "binding again catches up with the signal");
        }

        [Test]
        public void ShowingNothingReleasesTheMarks()
        {
            _pinger.TryPingAlong(new Vector3(0f, 1f, 0f), Vector3.forward, out var marker);
            _view.Show(_pinger.Markers);

            _view.Show(null);

            Assert.That(_view.Markers, Is.Null);
            Assert.That(_view.TryGetPlacement(marker.Id, out _), Is.False);
            Assert.That(_view.ShownCount, Is.Zero);
        }

        [Test]
        public void HudAddViewsAttachesTheMarks()
        {
            var hud = Hud.Create("PingMarkersViewTests.Hud");
            try
            {
                Assert.That(hud.PingMarkers, Is.Not.Null);
                Assert.That(hud.SquadOrder, Is.Not.Null);
            }
            finally
            {
                Object.DestroyImmediate(hud.OwnedPanelSettings);
                Object.DestroyImmediate(hud.gameObject);
            }
        }

        [Test]
        public void WithoutAViewerTheLocalPlayersCameraIsUsed()
        {
            Assert.That(PingMarkersView.LocalPlayerCamera(), Is.SameAs(Camera.main), "without a local player it falls back to the main camera");

            var player = Track(new GameObject("LocalPlayer"));
            GameSignals.LocalPlayer.Publish(new LocalPlayerSignal(player, null, null, null, null, null, null, null));
            Assert.That(PingMarkersView.LocalPlayerCamera(), Is.SameAs(Camera.main), "a player without a view rig falls back too");

            var eye = new GameObject("Eye").transform;
            eye.SetParent(player.transform, false);
            var cameraObject = new GameObject("PlayerCamera");
            cameraObject.transform.SetParent(eye, false);
            var camera = cameraObject.AddComponent<Camera>();
            player.AddComponent<ViewRig>().Initialize(eye, camera);

            Assert.That(PingMarkersView.LocalPlayerCamera(), Is.SameAs(camera));
        }

        private static VisualElement CloneLayout()
        {
            var layout = Resources.Load<VisualTreeAsset>(Hud.LayoutResourcePath);
            Assert.That(layout, Is.Not.Null, "Assets/game/Samples~/Demo/UI/Resources/Hud/Hud.uxml should be loadable from Resources");
            return layout.CloneTree();
        }

        private GameObject Track(GameObject go)
        {
            _objects.Add(go);
            return go;
        }
    }
}
