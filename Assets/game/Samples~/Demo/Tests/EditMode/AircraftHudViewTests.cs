using System.Linq;
using Shooter.Core;
using Shooter.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace Shooter.Tests.EditMode
{
    // Edit Mode では UIDocument が木を作らないことがあるので、Hud.uxml を複製した木を直接 Bind する。
    public sealed class AircraftHudViewTests
    {
        private GameObject _host;
        private AircraftHudView _view;

        [SetUp]
        public void SetUp()
        {
            GameSignals.ResetAll();
            _host = new GameObject("TestAircraftHud");
            _view = _host.AddComponent<AircraftHudView>();
            var layout = Resources.Load<VisualTreeAsset>(Hud.LayoutResourcePath);
            Assert.That(layout, Is.Not.Null, "Assets/game/Samples~/Demo/UI/Resources/Hud/Hud.uxml should be loadable from Resources");
            _view.Bind(layout.CloneTree());
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_host);
            GameSignals.ResetAll();
        }

        [Test]
        public void TheViewIsHiddenWithoutAnAircraft()
        {
            Assert.That(_view.Element, Is.Not.Null, "Hud.uxml has the aircraft element");
            Assert.That(_view.IsShown, Is.False);
            Assert.That(_view.Lines, Is.Empty);
            Assert.That(_view.Warning, Is.Empty);
            Assert.That(_view.Element.style.display.value, Is.EqualTo(DisplayStyle.None));
        }

        [Test]
        public void APublishedStatusShowsTheReadingsAndTheWarningOnlyWhileStalling()
        {
            AircraftSignals.Status.Publish(new AircraftStatus(50f, 120f, 0.5f, 5f, 1f, false, false));

            Assert.That(_view.IsShown, Is.True);
            Assert.That(_view.Element.style.display.value, Is.EqualTo(DisplayStyle.Flex));
            Assert.That(_view.Lines, Is.EqualTo(new[] { "速度 180 km/h", "高度 120 m", "スロットル 50%", "迎え角 5°", "1.0 G" }));
            Assert.That(RowTexts(), Is.EqualTo(_view.Lines));
            Assert.That(_view.Warning, Is.Empty);
            Assert.That(WarningRow().style.display.value, Is.EqualTo(DisplayStyle.None));

            AircraftSignals.Status.Publish(new AircraftStatus(20f, 120f, 1f, 18f, 0.5f, true, false));

            Assert.That(_view.Warning, Is.EqualTo("失速"));
            Assert.That(WarningRow().text, Is.EqualTo("失速"));
            Assert.That(WarningRow().style.display.value, Is.EqualTo(DisplayStyle.Flex));
            Assert.That(_view.Element.Children().Last(), Is.SameAs(WarningRow()), "the warning stays below the readings");
        }

        [Test]
        public void ClearingTheSignalHidesTheView()
        {
            AircraftSignals.Status.Publish(new AircraftStatus(20f, 120f, 1f, 18f, 0.5f, true, false));

            AircraftSignals.Status.Clear();

            Assert.That(_view.IsShown, Is.False);
            Assert.That(_view.Lines, Is.Empty);
            Assert.That(_view.Element.style.display.value, Is.EqualTo(DisplayStyle.None));

            AircraftSignals.Status.Publish(new AircraftStatus(50f, 120f, 0.5f, 5f, 1f, false, false));
            Assert.That(RowTexts(), Has.Length.EqualTo(5));
            Assert.That(_view.Element.Children().Last(), Is.SameAs(WarningRow()), "rows shown again go above the warning");
        }

        private string[] RowTexts()
        {
            return _view.Element.Query<Label>(className: AircraftHudView.RowClass).ToList().Select(label => label.text).ToArray();
        }

        private Label WarningRow()
        {
            return _view.Element.Q<Label>(className: AircraftHudView.WarningClass);
        }
    }
}
