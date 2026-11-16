using System.Collections.Generic;
using System.Linq;
using Fps.Core;
using Fps.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace Fps.Tests.EditMode
{
    // Edit Mode では OnEnable が走らずイベントを購読しないので、Show と Tick を直接呼ぶ。
    public sealed class DamageNumberViewTests
    {
        private readonly List<Object> _created = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (var created in _created)
            {
                if (created != null)
                {
                    Object.DestroyImmediate(created);
                }
            }

            _created.Clear();
        }

        [Test]
        public void TheLayoutHasTheDamageNumberArea()
        {
            Assert.That(CloneLayout().Q<VisualElement>(DamageNumberView.UxmlName), Is.Not.Null);
        }

        [Test]
        public void TheHudGetsTheView()
        {
            var hudObject = new GameObject("hud");
            _created.Add(hudObject);

            Hud.AddViews(hudObject);

            Assert.That(hudObject.GetComponent<DamageNumberView>(), Is.Not.Null);
        }

        [Test]
        public void WithoutACatalogNothingIsShown()
        {
            var view = CreateView(null, out _);

            view.Show(new HitConfirm(25, HitZone.Body, false, false));

            Assert.That(view.IsEnabled, Is.False);
            Assert.That(view.Numbers.Count, Is.EqualTo(0));
            Assert.That(Numbers(view).Count, Is.EqualTo(0));
            Assert.That(view.Element.style.display.value, Is.EqualTo(DisplayStyle.None));
        }

        [Test]
        public void ACatalogThatIsOffShowsNothing()
        {
            var view = CreateView(Catalog(enabled: false), out _);

            view.Show(new HitConfirm(25, HitZone.Body, false, false));

            Assert.That(view.IsEnabled, Is.False);
            Assert.That(Numbers(view).Count, Is.EqualTo(0));
        }

        [Test]
        public void TheCatalogIsOffByDefault()
        {
            var catalog = Track(ScriptableObject.CreateInstance<DamageNumberCatalog>());

            Assert.That(catalog.Enabled, Is.False);
            Assert.That(DamageNumberCatalog.ResourcePath, Is.EqualTo("Presentation/DamageNumberCatalog"));
        }

        [Test]
        public void AHitShowsItsDamageUntilTheLifetimeEnds()
        {
            var view = CreateView(Catalog(enabled: true), out _);

            view.Show(new HitConfirm(37, HitZone.Body, false, false));

            var labels = Numbers(view);
            Assert.That(labels.Count, Is.EqualTo(1));
            Assert.That(labels[0].text, Is.EqualTo("37"));
            Assert.That(labels[0].ClassListContains(DamageNumberView.HeadClass), Is.False);
            Assert.That(labels[0].ClassListContains(DamageNumberView.KillClass), Is.False);
            Assert.That(view.Element.style.display.value, Is.EqualTo(DisplayStyle.Flex));

            view.Tick(0.5f);
            Assert.That(Numbers(view).Count, Is.EqualTo(1), "still within 1 s");

            view.Tick(0.6f);
            Assert.That(Numbers(view).Count, Is.EqualTo(0));
            Assert.That(view.Element.style.display.value, Is.EqualTo(DisplayStyle.None));
        }

        [Test]
        public void HeadshotsWeakPointsAndKillsChangeTheNumber()
        {
            var view = CreateView(Catalog(enabled: true), out _);

            view.Show(new HitConfirm(80, HitZone.Head, false, false));
            view.Show(new HitConfirm(60, HitZone.WeakPoint, false, false));
            view.Show(new HitConfirm(20, HitZone.Limb, true, false));

            var labels = Numbers(view);
            Assert.That(labels[0].ClassListContains(DamageNumberView.HeadClass), Is.True);
            Assert.That(labels[1].ClassListContains(DamageNumberView.HeadClass), Is.True);
            Assert.That(labels[2].ClassListContains(DamageNumberView.HeadClass), Is.False);
            Assert.That(labels[2].ClassListContains(DamageNumberView.KillClass), Is.True);
        }

        [Test]
        public void ANumberSitsOnTheHitPointSeenByTheCamera()
        {
            var view = CreateView(Catalog(enabled: true), out _);
            view.Viewer = CreateCamera();

            view.Show(new HitConfirm(10, HitZone.Body, false, false, new System.Numerics.Vector3(0f, 0f, 10f)));

            var anchor = Anchors(view)[0];
            Assert.That(anchor.style.left.value.value, Is.EqualTo(50f).Within(0.01f));
            Assert.That(anchor.style.left.value.unit, Is.EqualTo(LengthUnit.Percent));
            Assert.That(anchor.style.top.value.value, Is.EqualTo(50f).Within(0.01f));
            Assert.That(anchor.style.translate.value.x.value, Is.EqualTo(0f));
            Assert.That(anchor.style.translate.value.y.value, Is.EqualTo(0f));
            Assert.That(anchor.style.opacity.value, Is.EqualTo(1f));

            view.Tick(0.75f);
            Assert.That(anchor.style.translate.value.y.value, Is.EqualTo(-30f).Within(1e-3f), "rises 40 px over 1 s");
            Assert.That(anchor.style.opacity.value, Is.EqualTo(0.5f).Within(1e-3f), "fades over the last half");
        }

        [Test]
        public void ANumberWithoutAPointOrBehindTheCameraSitsBesideTheCrosshair()
        {
            var view = CreateView(Catalog(enabled: true), out _);
            view.Viewer = CreateCamera();

            view.Show(new HitConfirm(10, HitZone.Body, false, false));
            view.Show(new HitConfirm(10, HitZone.Body, false, false, new System.Numerics.Vector3(0f, 0f, -10f)));

            foreach (var anchor in Anchors(view))
            {
                Assert.That(anchor.style.left.value.value, Is.EqualTo(50f));
                Assert.That(anchor.style.top.value.value, Is.EqualTo(50f));
                Assert.That(anchor.style.translate.value.x.value, Is.EqualTo(DamageNumberView.CrosshairOffsetX));
                Assert.That(anchor.style.translate.value.y.value, Is.EqualTo(-DamageNumberView.CrosshairOffsetY));
            }
        }

        [Test]
        public void TheHardcoreHudHidesAndClearsTheNumbers()
        {
            var view = CreateView(Catalog(enabled: true), out _);
            view.Show(new HitConfirm(10, HitZone.Body, false, false));

            view.SetAllowed(false);
            Assert.That(Numbers(view).Count, Is.EqualTo(0));

            view.Show(new HitConfirm(10, HitZone.Body, false, false));
            Assert.That(Numbers(view).Count, Is.EqualTo(0));
        }

        private DamageNumberView CreateView(DamageNumberCatalog catalog, out VisualElement root)
        {
            var gameObject = new GameObject(nameof(DamageNumberView));
            _created.Add(gameObject);
            root = CloneLayout();
            var view = gameObject.AddComponent<DamageNumberView>();
            view.Catalog = catalog;
            view.Bind(root);
            return view;
        }

        private DamageNumberCatalog Catalog(bool enabled)
        {
            var catalog = Track(ScriptableObject.CreateInstance<DamageNumberCatalog>());
            catalog.Enabled = enabled;
            catalog.SetSettings(new DamageNumberSettings(1f, 40f, 0.5f, 8));
            return catalog;
        }

        private Camera CreateCamera()
        {
            var cameraObject = new GameObject("viewer");
            _created.Add(cameraObject);
            cameraObject.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            var camera = cameraObject.AddComponent<Camera>();
            camera.enabled = false;
            return camera;
        }

        private T Track<T>(T created)
            where T : Object
        {
            _created.Add(created);
            return created;
        }

        private static List<VisualElement> Anchors(DamageNumberView view)
        {
            return view.Element.Query<VisualElement>(className: DamageNumberView.AnchorClass).ToList();
        }

        private static List<Label> Numbers(DamageNumberView view)
        {
            return view.Element.Query<Label>(className: DamageNumberView.NumberClass).ToList();
        }

        private static VisualElement CloneLayout()
        {
            var layout = Resources.Load<VisualTreeAsset>(Hud.LayoutResourcePath);
            Assert.That(layout, Is.Not.Null, "Assets/game/UI/Resources/Hud/Hud.uxml should be loadable from Resources");
            return layout.CloneTree();
        }
    }
}
