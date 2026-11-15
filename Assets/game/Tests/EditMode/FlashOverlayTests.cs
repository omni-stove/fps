using System.Collections.Generic;
using Fps.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace Fps.Tests.EditMode
{
    // Edit Mode では OnEnable と LateUpdate が走らないので、Track と Refresh を直接呼ぶ
    public sealed class FlashOverlayTests
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
        public void TheLayoutHasTheOverlayOverTheHudButUnderThePauseMenu()
        {
            var root = CloneLayout();

            var overlay = root.Q<VisualElement>(FlashOverlay.UxmlName);
            var pause = root.Q<VisualElement>("pause-menu");
            Assert.That(overlay, Is.Not.Null);
            Assert.That(overlay.pickingMode, Is.EqualTo(PickingMode.Ignore));
            Assert.That(overlay.parent, Is.SameAs(pause.parent));
            Assert.That(overlay.parent.IndexOf(overlay), Is.EqualTo(pause.parent.IndexOf(pause) - 1));
        }

        [Test]
        public void TheHudFactoryAttachesTheOverlay()
        {
            var hud = Hud.Create("TestFlashHud", null, null, null);
            _created.Add(hud.gameObject);
            _created.Add(hud.OwnedPanelSettings);

            Assert.That(hud.FlashOverlay, Is.Not.Null);
        }

        [Test]
        public void TheScreenIsWhiteWhileBlindAndFadesOverTheLastSeconds()
        {
            var overlay = CreateOverlay(out var element);
            var remaining = 0f;
            overlay.Track(() => remaining);
            Assert.That(element.style.display.value, Is.EqualTo(DisplayStyle.None));

            remaining = 3f;
            overlay.Refresh();
            Assert.That(overlay.Opacity, Is.EqualTo(1f));
            Assert.That(element.style.display.value, Is.EqualTo(DisplayStyle.Flex));
            Assert.That(element.style.backgroundColor.value, Is.EqualTo(Color.white), "with no catalog the screen is plain white");

            remaining = overlay.FadeSeconds / 2f;
            overlay.Refresh();
            Assert.That(overlay.Opacity, Is.EqualTo(0.5f).Within(1e-4f));
            Assert.That(element.style.opacity.value, Is.EqualTo(0.5f).Within(1e-4f));

            remaining = 0f;
            overlay.Refresh();
            Assert.That(overlay.Opacity, Is.Zero);
            Assert.That(element.style.display.value, Is.EqualTo(DisplayStyle.None));
        }

        [Test]
        public void AShortBlindnessIsLighterFromTheStart()
        {
            Assert.That(FlashOverlay.OpacityFor(4f, 1.5f), Is.EqualTo(1f));
            Assert.That(FlashOverlay.OpacityFor(0.3f, 1.5f), Is.EqualTo(0.2f).Within(1e-4f));
            Assert.That(FlashOverlay.OpacityFor(0f, 1.5f), Is.Zero);
            Assert.That(FlashOverlay.OpacityFor(float.NaN, 1.5f), Is.Zero);
        }

        [Test]
        public void TheCatalogSetsTheColorAndHowLongItFades()
        {
            var catalog = ScriptableObject.CreateInstance<FlashOverlayCatalog>();
            _created.Add(catalog);
            catalog.Color = new Color(1f, 0.9f, 0.8f, 1f);
            catalog.FadeSeconds = 2f;
            var overlay = CreateOverlay(out var element);
            overlay.Catalog = catalog;

            overlay.Track(() => 1f);

            Assert.That(overlay.Opacity, Is.EqualTo(0.5f).Within(1e-4f));
            Assert.That(element.style.backgroundColor.value, Is.EqualTo(catalog.Color));
            catalog.FadeSeconds = 0f;
            Assert.That(catalog.FadeSeconds, Is.EqualTo(FlashOverlayCatalog.MinFadeSeconds));
        }

        private FlashOverlay CreateOverlay(out VisualElement element)
        {
            var gameObject = new GameObject(nameof(FlashOverlay));
            _created.Add(gameObject);
            var root = CloneLayout();
            var overlay = gameObject.AddComponent<FlashOverlay>();
            overlay.Catalog = null;
            overlay.Bind(root);
            element = overlay.Element;
            return overlay;
        }

        private static VisualElement CloneLayout()
        {
            var layout = Resources.Load<VisualTreeAsset>(Hud.LayoutResourcePath);
            Assert.That(layout, Is.Not.Null, "Assets/game/UI/Resources/Hud/Hud.uxml should be loadable from Resources");
            return layout.CloneTree();
        }
    }
}
