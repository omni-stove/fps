using System.Collections.Generic;
using Shooter.Core;
using Shooter.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace Shooter.Tests.EditMode
{
    // Edit Mode では LateUpdate が走らないので、Refresh を直接呼ぶ
    public sealed class ScopeOverlayViewTests
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
        public void TheLayoutHasTheOverlayRightAfterTheDamageVignette()
        {
            var root = CloneLayout();

            var overlay = root.Q<VisualElement>(ScopeOverlayView.UxmlName);
            var vignette = root.Q<VisualElement>(DamageVignetteView.UxmlName);
            Assert.That(overlay, Is.Not.Null);
            Assert.That(overlay.pickingMode, Is.EqualTo(PickingMode.Ignore));
            Assert.That(overlay.parent, Is.SameAs(vignette.parent));
            Assert.That(overlay.parent.IndexOf(overlay), Is.EqualTo(vignette.parent.IndexOf(vignette) + 1));
        }

        [Test]
        public void TheHudFactoryAttachesTheOverlay()
        {
            var hud = Hud.Create("TestScopeHud");
            _created.Add(hud.gameObject);
            _created.Add(hud.OwnedPanelSettings);

            Assert.That(hud.ScopeOverlay, Is.Not.Null);
            Assert.That(hud.Ads, Is.Null, "no local player yet");
        }

        [Test]
        public void AScopeShowsTheOverlayWithTheDefaultCrosshairOnceAimedIn()
        {
            var overlay = CreateOverlay(out var element);
            var view = SightView.None;
            overlay.SightSource = () => view;
            overlay.Refresh();
            Assert.That(overlay.IsShown, Is.False);
            Assert.That(element.style.display.value, Is.EqualTo(DisplayStyle.None));

            view = SightView.Of(new SightSpec("mildot", scopeOverlay: true), ViewMode.FirstPerson, 1f);
            overlay.Refresh();

            Assert.That(overlay.IsShown, Is.True);
            Assert.That(element.style.display.value, Is.EqualTo(DisplayStyle.Flex));
            Assert.That(overlay.Reticle, Is.SameAs(ReticleCatalog.DefaultScope), "no catalog: the default scope crosshair");
            Assert.That(overlay.LensDiameter, Is.EqualTo(ReticleCatalog.DefaultLensDiameter));
            Assert.That(overlay.MaskColor, Is.EqualTo(Color.black));

            view = SightView.Of(new SightSpec("mildot", scopeOverlay: true), ViewMode.FirstPerson, 0.5f);
            overlay.Refresh();

            Assert.That(overlay.IsShown, Is.False, "the overlay goes as soon as the gun comes down");
            Assert.That(overlay.Reticle, Is.Null);
            Assert.That(element.style.display.value, Is.EqualTo(DisplayStyle.None));
        }

        [Test]
        public void ARedDotShowsOnlyTheDotWithoutTheOverlay()
        {
            var overlay = CreateOverlay(out var element);
            overlay.SightSource = () => SightView.Of(new SightSpec("redDot"), ViewMode.FirstPerson, 1f);

            overlay.Refresh();

            Assert.That(overlay.IsShown, Is.True);
            Assert.That(overlay.Shown.ScopeOverlay, Is.False);
            Assert.That(overlay.Reticle, Is.SameAs(ReticleCatalog.DefaultDot));
            Assert.That(element.style.display.value, Is.EqualTo(DisplayStyle.Flex));
        }

        [Test]
        public void TheCatalogPicksTheReticleById()
        {
            var catalog = ScriptableObject.CreateInstance<ReticleCatalog>();
            _created.Add(catalog);
            var holo = new ReticleEntry("holo", null, ReticleShape.Circle, Color.green, 0.03f);
            catalog.SetReticles(new[] { holo });
            catalog.LensDiameter = 0.8f;
            catalog.MaskColor = Color.gray;
            var overlay = CreateOverlay(out _);
            overlay.Catalog = catalog;
            var id = "holo";
            overlay.SightSource = () => SightView.Of(new SightSpec(id), ViewMode.FirstPerson, 1f);

            overlay.Refresh();

            Assert.That(overlay.Reticle, Is.SameAs(holo));
            Assert.That(overlay.LensDiameter, Is.EqualTo(0.8f));
            Assert.That(overlay.MaskColor, Is.EqualTo(Color.gray));

            id = "unknown";
            overlay.Refresh();
            Assert.That(overlay.Reticle, Is.SameAs(ReticleCatalog.DefaultDot), "an unknown id falls back to the default dot");
        }

        [Test]
        public void ThirdPersonOverTheShoulderShowsNothing()
        {
            var overlay = CreateOverlay(out _);
            overlay.SightSource = () => SightView.Of(new SightSpec("redDot"), ViewMode.ThirdPerson, 1f);

            overlay.Refresh();

            Assert.That(overlay.IsShown, Is.False);
        }

        [Test]
        public void TheReticleSizeFollowsTheLensOrTheScreen()
        {
            var entry = new ReticleEntry("x", null, ReticleShape.Dot, Color.red, 0.01f);

            // 縁あり: 1000 px × 直径 0.9 × 0.01 = 9 px。縁なし: 1000 px × 0.01 = 10 px
            Assert.That(ScopeOverlayView.ReticleSize(entry, true, 1000f, 0.9f), Is.EqualTo(9f).Within(1e-4f));
            Assert.That(ScopeOverlayView.ReticleSize(entry, false, 1000f, 0.9f), Is.EqualTo(10f).Within(1e-4f));
            Assert.That(ScopeOverlayView.ReticleSize(null, true, 1000f, 0.9f), Is.Zero);
            Assert.That(ScopeOverlayView.ReticleSize(entry, true, float.NaN, 0.9f), Is.Zero);
        }

        private ScopeOverlayView CreateOverlay(out VisualElement element)
        {
            var gameObject = new GameObject(nameof(ScopeOverlayView));
            _created.Add(gameObject);
            var root = CloneLayout();
            var overlay = gameObject.AddComponent<ScopeOverlayView>();
            overlay.Catalog = null;
            overlay.Bind(root);
            element = overlay.Element;
            return overlay;
        }

        private static VisualElement CloneLayout()
        {
            var layout = Resources.Load<VisualTreeAsset>(Hud.LayoutResourcePath);
            Assert.That(layout, Is.Not.Null, "Assets/game/Samples~/Demo/UI/Resources/Hud/Hud.uxml should be loadable from Resources");
            return layout.CloneTree();
        }
    }
}
