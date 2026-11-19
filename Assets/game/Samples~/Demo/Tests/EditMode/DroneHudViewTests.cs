using Shooter.Core;
using Shooter.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace Shooter.Tests.EditMode
{
    // Edit Mode では UIDocument が木を作らないことがあるので、Hud.uxml を複製した木を直接 Bind する。
    // 文は JapaneseLanguageSetUp が効かせる日本語の表 (Resources/Localization/ja/hud.json) から引く。
    public sealed class DroneHudViewTests
    {
        private GameObject _host;
        private DroneHudView _view;

        [SetUp]
        public void SetUp()
        {
            GameSignals.ResetAll();
            _host = new GameObject("TestDroneHud");
            _view = _host.AddComponent<DroneHudView>();
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
        public void TheViewIsHiddenWithoutADrone()
        {
            Assert.That(_view.Element, Is.Not.Null, "Hud.uxml has the drone-hud element");
            Assert.That(_view.IsShown, Is.False);
            Assert.That(_view.Element.style.display.value, Is.EqualTo(DisplayStyle.None));
            Assert.That(_view.BatteryText, Is.Empty);
            Assert.That(_view.OutOfRangeText, Is.Empty);
            Assert.That(_view.RecallText, Is.Empty);
        }

        [Test]
        public void WhilePilotingItShowsTheBatteryButNotTheRecall()
        {
            GameSignals.Drone.Publish(new DroneStatus(0.756f, 0.9f, 2, true));

            Assert.That(_view.IsShown, Is.True);
            Assert.That(_view.Element.style.display.value, Is.EqualTo(DisplayStyle.Flex));
            Assert.That(_view.Element.ClassListContains(DroneHudView.PilotingClass), Is.True);
            Assert.That(_view.BatteryText, Is.EqualTo("電池 76%"));
            Assert.That(Line(DroneHudView.BatteryName).style.display.value, Is.EqualTo(DisplayStyle.Flex));
            Assert.That(_view.ShowsOutOfRange, Is.False);
            Assert.That(Line(DroneHudView.OutOfRangeName).style.display.value, Is.EqualTo(DisplayStyle.None));
            Assert.That(_view.ShowsRecall, Is.False, "abilities cannot be used while piloting, so the recall tap is not offered");
            Assert.That(Line(DroneHudView.RecallName).style.display.value, Is.EqualTo(DisplayStyle.None));
        }

        [Test]
        public void NearTheEdgeOfTheRangeItWarns()
        {
            GameSignals.Drone.Publish(new DroneStatus(0.5f, 0.15f, 0, true));

            Assert.That(_view.ShowsOutOfRange, Is.True);
            Assert.That(_view.OutOfRangeText, Is.EqualTo("範囲外: 電波が届かない"));
            Assert.That(Line(DroneHudView.OutOfRangeName).style.display.value, Is.EqualTo(DisplayStyle.Flex));
            Assert.That(Line(DroneHudView.OutOfRangeName).ClassListContains(DroneHudView.WarningClass), Is.True);

            GameSignals.Drone.Publish(new DroneStatus(0.5f, 0.6f, 0, true));

            Assert.That(_view.ShowsOutOfRange, Is.False);
            Assert.That(Line(DroneHudView.OutOfRangeName).style.display.value, Is.EqualTo(DisplayStyle.None));
        }

        [Test]
        public void TheWarningThresholdCanBeChanged()
        {
            GameSignals.Drone.Publish(new DroneStatus(0.5f, 0.4f, 0, true));
            Assert.That(_view.ShowsOutOfRange, Is.False);

            _view.RangeWarningFraction = 0.5f;

            Assert.That(_view.ShowsOutOfRange, Is.True);
            Assert.That(_view.OutOfRangeText, Is.EqualTo("範囲外: 電波が届かない"));
        }

        [Test]
        public void AfterGettingOutItOffersTheRecallOnTheAbilityKey()
        {
            GameSignals.Drone.Publish(new DroneStatus(0.3f, 0.8f, 1, false));

            Assert.That(_view.Element.ClassListContains(DroneHudView.PilotingClass), Is.False);
            Assert.That(_view.ShowsRecall, Is.True);
            Assert.That(_view.RecallText, Is.EqualTo("[アビリティ 1] ドローンを戻す"));
            Assert.That(Line(DroneHudView.RecallName).style.display.value, Is.EqualTo(DisplayStyle.Flex));

            _view.RecallActionName = "Ability2";

            Assert.That(_view.RecallText, Is.EqualTo("[アビリティ 2] ドローンを戻す"));
        }

        [Test]
        public void ClearingTheSignalHidesEverything()
        {
            GameSignals.Drone.Publish(new DroneStatus(0.3f, 0.1f, 1, false));

            GameSignals.Drone.Clear();

            Assert.That(_view.IsShown, Is.False);
            Assert.That(_view.Status, Is.Null);
            Assert.That(_view.Element.style.display.value, Is.EqualTo(DisplayStyle.None));
            Assert.That(_view.Element.ClassListContains(DroneHudView.PilotingClass), Is.False);
            Assert.That(Line(DroneHudView.BatteryName).text, Is.Empty);
            Assert.That(_view.OutOfRangeText, Is.Empty);
            Assert.That(_view.RecallText, Is.Empty);
        }

        [Test]
        public void AValuePublishedBeforeBindingIsShownOnBind()
        {
            GameSignals.Drone.Publish(new DroneStatus(1f, 1f, 0, true));
            var host = new GameObject("TestDroneHudLate");
            try
            {
                var view = host.AddComponent<DroneHudView>();
                view.Bind(Resources.Load<VisualTreeAsset>(Hud.LayoutResourcePath).CloneTree());

                Assert.That(view.IsShown, Is.True);
                Assert.That(view.BatteryText, Is.EqualTo("電池 100%"));
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void UnbindingHidesTheSlotAndStopsListening()
        {
            GameSignals.Drone.Publish(new DroneStatus(0.5f, 0.5f, 0, true));
            var element = _view.Element;

            _view.Unbind();
            GameSignals.Drone.Publish(new DroneStatus(0.2f, 0.5f, 0, true));

            Assert.That(element.style.display.value, Is.EqualTo(DisplayStyle.None));
            Assert.That(element.Q<Label>(DroneHudView.BatteryName), Is.Null, "the rows the view added are taken out");
            Assert.That(_view.Status.Value.BatteryFraction, Is.EqualTo(0.5f), "a value sent after unbinding is not taken");
        }

        [Test]
        public void TheBatteryPercentRoundsHalfUp()
        {
            Assert.That(DroneHudText.BatteryPercent(new DroneStatus(0.125f, 1f, 0, false)), Is.EqualTo(13));
            Assert.That(DroneHudText.BatteryPercent(new DroneStatus(0f, 1f, 0, false)), Is.Zero);
            Assert.That(DroneHudText.Battery(new DroneStatus(0.5f, 1f, 0, false)), Is.EqualTo(new LocalizedText(DroneHudText.BatteryKey, 50)));
            Assert.That(DroneHudText.Recall(null), Is.EqualTo(new LocalizedText(DroneHudText.RecallKey, string.Empty)));
            Assert.That(DroneHudText.DefaultRecallActionName, Is.EqualTo("Ability1"));
        }

        private Label Line(string name)
        {
            var label = _view.Element.Q<Label>(name);
            Assert.That(label, Is.Not.Null, name + " is added under drone-hud");
            return label;
        }
    }
}
