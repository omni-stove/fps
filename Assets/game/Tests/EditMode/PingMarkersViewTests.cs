using System.Collections.Generic;
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
    /// 既定の印の表示 (PingMarkersView) が、画面の中の印をその位置に、外の印を端に寄せて向きを出すこと、
    /// 既定の生成役 (DefaultPingViewFactory) と差し替え口 (PingUi) を確かめる。
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
            PingUi.ViewFactory = null;
            foreach (var go in _objects)
            {
                if (go != null)
                {
                    Object.DestroyImmediate(go);
                }
            }

            _objects.Clear();
        }

        [Test]
        public void AMarkInViewIsDrawnWhereItIs()
        {
            _pinger.TryPingAlong(new Vector3(0f, 1f, 0f), Vector3.forward, out var marker);

            LogAssert.Expect(LogType.Log, new Regex($@"^\[Ping\] screen id={marker.Id} onScreen=true edge=None "));
            _view.Show(_pinger);

            Assert.That(_view.Source, Is.SameAs(_pinger));
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
            _view.Show(_pinger);

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
        public void TheLayoutGetsAnElementPerMarkAndDropsItWhenTheMarkExpires()
        {
            var root = CloneLayout();
            _view.Bind(root);
            Assert.That(_view.Element, Is.Not.Null, "Hud.uxml has the ping-markers element");
            Assert.That(_view.Element.style.display.value, Is.EqualTo(DisplayStyle.None), "nothing is shown without marks");

            _pinger.TryPingAlong(new Vector3(0f, 1f, 0f), Vector3.forward, out var marker);
            _view.Show(_pinger);

            var marks = _view.Element.Query<VisualElement>(className: PingMarkersView.MarkerClass).ToList();
            Assert.That(marks, Has.Count.EqualTo(1));
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
        public void ShowingNothingReleasesTheSource()
        {
            _pinger.TryPingAlong(new Vector3(0f, 1f, 0f), Vector3.forward, out var marker);
            _view.Show(_pinger);

            _view.Show(null);

            Assert.That(_view.Source, Is.Null);
            Assert.That(_view.TryGetPlacement(marker.Id, out _), Is.False);
            Assert.That(_view.ShownCount, Is.Zero);
        }

        [Test]
        public void TheDefaultFactoryPutsTheViewOnTheDefaultHudOnly()
        {
            var factory = new DefaultPingViewFactory();
            var hud = _view.GetComponent<Hud>();

            Assert.That(factory.Create(hud), Is.SameAs(_view), "a HUD that already has the view keeps it");
            Assert.That(factory.Create(null), Is.Null, "only the default Hud gets the default view");
            Assert.That(PingUi.ViewFactory, Is.InstanceOf<DefaultPingViewFactory>(), "the UI assembly registers the default");
            Assert.That(PingUi.CreateView(null), Is.Null);
        }

        [Test]
        public void TheFactoryCanBeReplaced()
        {
            var replacement = new FixedFactory();
            PingUi.ViewFactory = replacement;

            Assert.That(PingUi.CreateView(_view.GetComponent<Hud>()), Is.SameAs(replacement.View));

            PingUi.ViewFactory = null;
            Assert.That(PingUi.ViewFactory, Is.InstanceOf<DefaultPingViewFactory>(), "null goes back to the default");
        }

        private static VisualElement CloneLayout()
        {
            var layout = Resources.Load<VisualTreeAsset>(Hud.LayoutResourcePath);
            Assert.That(layout, Is.Not.Null, "Assets/game/UI/Resources/Hud/Hud.uxml should be loadable from Resources");
            return layout.CloneTree();
        }

        private GameObject Track(GameObject go)
        {
            _objects.Add(go);
            return go;
        }

        private sealed class FixedFactory : IPingViewFactory
        {
            public IPingView View { get; } = new NullView();

            public IPingView Create(IHud hud) => View;
        }

        private sealed class NullView : IPingView
        {
            public void Show(PingController source)
            {
            }
        }
    }
}
