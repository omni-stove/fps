using System.Collections.Generic;
using Shooter.Core;
using Shooter.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace Shooter.Tests.EditMode
{
    // Edit Mode では UIDocument が木を作らないことがあるので、Hud.uxml を複製した木を直接 Bind する。
    public sealed class VisionOverlayViewTests
    {
        private readonly List<Object> _created = new List<Object>();
        private VisionOverlayView _view;

        [SetUp]
        public void SetUp()
        {
            GameSignals.ResetAll();
            _view = CreateView();
        }

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
            GameSignals.ResetAll();
        }

        [Test]
        public void TheOverlayIsHiddenWithoutVision()
        {
            Assert.That(_view.Element, Is.Not.Null, "Hud.uxml has the vision-overlay element");
            Assert.That(_view.IsShown, Is.False);
            Assert.That(_view.Element.style.display.value, Is.EqualTo(DisplayStyle.None));
        }

        [Test]
        public void NightGogglesTintTheScreenWithTheDefaultNightColor()
        {
            GameSignals.Vision.Publish(new VisionStatus(VisionMode.Night, VisionSource.Goggles));

            Assert.That(_view.IsShown, Is.True);
            Assert.That(_view.Element.style.display.value, Is.EqualTo(DisplayStyle.Flex));
            Assert.That(_view.Element.style.backgroundColor.value, Is.EqualTo(VisionVisualCatalog.DefaultNightColor));
            Assert.That(_view.Element.ClassListContains(VisionOverlayView.NightClass), Is.True);
            Assert.That(_view.Element.ClassListContains(VisionOverlayView.GogglesClass), Is.True);
            Assert.That(_view.Element.ClassListContains(VisionOverlayView.ThermalClass), Is.False);
            Assert.That(_view.Element.ClassListContains(VisionOverlayView.ScopeClass), Is.False);
        }

        [Test]
        public void AThermalScopeSwapsTheColorAndTheClasses()
        {
            GameSignals.Vision.Publish(new VisionStatus(VisionMode.Night, VisionSource.Goggles));

            GameSignals.Vision.Publish(new VisionStatus(VisionMode.Thermal, VisionSource.Scope));

            Assert.That(_view.Mode, Is.EqualTo(VisionMode.Thermal));
            Assert.That(_view.Element.style.backgroundColor.value, Is.EqualTo(VisionVisualCatalog.DefaultThermalColor));
            Assert.That(_view.Element.ClassListContains(VisionOverlayView.ThermalClass), Is.True);
            Assert.That(_view.Element.ClassListContains(VisionOverlayView.ScopeClass), Is.True);
            Assert.That(_view.Element.ClassListContains(VisionOverlayView.NightClass), Is.False);
            Assert.That(_view.Element.ClassListContains(VisionOverlayView.GogglesClass), Is.False);
        }

        [Test]
        public void ClearingTheSignalHidesTheOverlay()
        {
            GameSignals.Vision.Publish(new VisionStatus(VisionMode.Thermal, VisionSource.Goggles));

            GameSignals.Vision.Clear();

            Assert.That(_view.IsShown, Is.False);
            Assert.That(_view.Status, Is.Null);
            Assert.That(_view.Element.style.display.value, Is.EqualTo(DisplayStyle.None));
            Assert.That(_view.Element.ClassListContains(VisionOverlayView.ThermalClass), Is.False);
            Assert.That(_view.Element.ClassListContains(VisionOverlayView.GogglesClass), Is.False);
        }

        [Test]
        public void TheCatalogColorTintsItsImage()
        {
            var catalog = ScriptableObject.CreateInstance<VisionVisualCatalog>();
            _created.Add(catalog);
            var image = new Texture2D(2, 2);
            _created.Add(image);
            catalog.NightImage = image;
            catalog.NightColor = Color.green;
            catalog.ThermalColor = Color.red;
            _view.Catalog = catalog;

            GameSignals.Vision.Publish(new VisionStatus(VisionMode.Night, VisionSource.Goggles));

            Assert.That(_view.Image, Is.SameAs(image));
            Assert.That(_view.Element.style.backgroundImage.value.texture, Is.SameAs(image));
            Assert.That(_view.Element.style.unityBackgroundImageTintColor.value, Is.EqualTo(Color.green));
            Assert.That(_view.Element.style.backgroundColor.value, Is.EqualTo(Color.clear), "the image carries the color");

            GameSignals.Vision.Publish(new VisionStatus(VisionMode.Thermal, VisionSource.Goggles));

            Assert.That(_view.Image, Is.Null, "no thermal image: the color alone");
            Assert.That(_view.Element.style.backgroundImage.keyword, Is.EqualTo(StyleKeyword.None));
            Assert.That(_view.Element.style.backgroundColor.value, Is.EqualTo(Color.red));
        }

        [Test]
        public void AValuePublishedBeforeBindingIsShownOnBind()
        {
            GameSignals.Vision.Publish(new VisionStatus(VisionMode.Thermal, VisionSource.Goggles));

            var late = CreateView();

            Assert.That(late.IsShown, Is.True);
            Assert.That(late.Element.style.display.value, Is.EqualTo(DisplayStyle.Flex));
            Assert.That(late.Element.style.backgroundColor.value, Is.EqualTo(VisionVisualCatalog.DefaultThermalColor));
        }

        [Test]
        public void UnbindingStopsListening()
        {
            _view.Unbind();

            GameSignals.Vision.Publish(new VisionStatus(VisionMode.Night, VisionSource.Goggles));

            Assert.That(_view.Status, Is.Null);
        }

        private VisionOverlayView CreateView()
        {
            var host = new GameObject("TestVisionOverlay");
            _created.Add(host);
            var view = host.AddComponent<VisionOverlayView>();
            view.Catalog = null;
            var layout = Resources.Load<VisualTreeAsset>(Hud.LayoutResourcePath);
            Assert.That(layout, Is.Not.Null, "Assets/game/Samples~/Demo/UI/Resources/Hud/Hud.uxml should be loadable from Resources");
            view.Bind(layout.CloneTree());
            return view;
        }
    }
}
