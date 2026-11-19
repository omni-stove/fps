using Shooter.Core;
using Shooter.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace Shooter.Tests.EditMode
{
    // Edit Mode では UIDocument が木を作らないことがあるので、Hud.uxml を複製した木を直接 Bind する。
    // 文は JapaneseLanguageSetUp が効かせる日本語の表 (Resources/Localization/ja/stamina.json) から引く。
    public sealed class StaminaViewTests
    {
        private GameObject _host;
        private StaminaView _view;

        [SetUp]
        public void SetUp()
        {
            GameSignals.ResetAll();
            _host = new GameObject("TestStamina");
            _view = _host.AddComponent<StaminaView>();
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
        public void TheViewIsHiddenWithoutStamina()
        {
            Assert.That(_view.Element, Is.Not.Null, "Hud.uxml has the stamina element");
            Assert.That(_view.IsShown, Is.False);
            Assert.That(_view.Reading, Is.Empty);
            Assert.That(_view.Element.style.display.value, Is.EqualTo(DisplayStyle.None));
        }

        [Test]
        public void TheFillWidthFollowsTheAmount()
        {
            GameSignals.Stamina.Publish(new StaminaStatus(30f, 120f, false));

            Assert.That(_view.IsShown, Is.True);
            Assert.That(_view.Element.style.display.value, Is.EqualTo(DisplayStyle.Flex));
            Assert.That(_view.FillPercent, Is.EqualTo(25f).Within(0.001f));
            Assert.That(FillWidth().unit, Is.EqualTo(LengthUnit.Percent));
            Assert.That(FillWidth().value, Is.EqualTo(25f).Within(0.001f));
            Assert.That(_view.Reading, Is.EqualTo("スタミナ 25%"));
            Assert.That(_view.Element.tooltip, Is.EqualTo("スタミナ 25%"));
            Assert.That(ReadingLabelText(), Is.EqualTo("スタミナ 25%"));

            GameSignals.Stamina.Publish(new StaminaStatus(90f, 120f, false));

            Assert.That(FillWidth().value, Is.EqualTo(75f).Within(0.001f));
            Assert.That(_view.Reading, Is.EqualTo("スタミナ 75%"));
            Assert.That(ReadingLabelText(), Is.EqualTo("スタミナ 75%"));
        }

        [Test]
        public void AFullGaugeIsHidden()
        {
            GameSignals.Stamina.Publish(new StaminaStatus(50f, 100f, false));
            GameSignals.Stamina.Publish(new StaminaStatus(100f, 100f, false));

            Assert.That(_view.IsShown, Is.False);
            Assert.That(_view.Element.style.display.value, Is.EqualTo(DisplayStyle.None));
            Assert.That(_view.Reading, Is.Empty);
            Assert.That(_view.Element.tooltip, Is.Empty);
            Assert.That(ReadingLabelText(), Is.Empty);
        }

        [Test]
        public void ADepletedGaugeTakesTheDepletedClassUntilItRecovers()
        {
            GameSignals.Stamina.Publish(new StaminaStatus(0f, 100f, true));

            Assert.That(_view.IsShown, Is.True);
            Assert.That(_view.Element.ClassListContains(StaminaView.DepletedClass), Is.True);
            Assert.That(FillWidth().value, Is.EqualTo(0f).Within(0.001f));
            Assert.That(_view.Reading, Is.EqualTo("スタミナ切れ 0%"));
            Assert.That(ReadingLabelText(), Is.EqualTo("スタミナ切れ 0%"));

            GameSignals.Stamina.Publish(new StaminaStatus(40f, 100f, true));

            Assert.That(_view.Element.ClassListContains(StaminaView.DepletedClass), Is.True, "depleted is taken from the status, not from the amount");

            GameSignals.Stamina.Publish(new StaminaStatus(40f, 100f, false));

            Assert.That(_view.Element.ClassListContains(StaminaView.DepletedClass), Is.False);
            Assert.That(_view.Reading, Is.EqualTo("スタミナ 40%"));
        }

        [Test]
        public void AFullGaugeStillShowsWhileDepleted()
        {
            GameSignals.Stamina.Publish(new StaminaStatus(100f, 100f, true));

            Assert.That(_view.IsShown, Is.True);
            Assert.That(_view.Element.ClassListContains(StaminaView.DepletedClass), Is.True);
        }

        [Test]
        public void ClearingTheSignalHidesTheGauge()
        {
            GameSignals.Stamina.Publish(new StaminaStatus(0f, 100f, true));

            GameSignals.Stamina.Clear();

            Assert.That(_view.IsShown, Is.False);
            Assert.That(_view.Status, Is.Null);
            Assert.That(_view.Element.style.display.value, Is.EqualTo(DisplayStyle.None));
            Assert.That(_view.Element.ClassListContains(StaminaView.DepletedClass), Is.False);
            Assert.That(_view.Reading, Is.Empty);
            Assert.That(ReadingLabelText(), Is.Empty);
        }

        [Test]
        public void ShowingNullHidesTheGauge()
        {
            _view.Show(new StaminaStatus(20f, 100f, false));

            _view.Show(null);

            Assert.That(_view.IsShown, Is.False);
            Assert.That(_view.Element.style.display.value, Is.EqualTo(DisplayStyle.None));
        }

        [Test]
        public void AValuePublishedBeforeBindingIsShownOnBind()
        {
            GameSignals.Stamina.Publish(new StaminaStatus(10f, 40f, false));
            var host = new GameObject("TestStaminaLate");
            try
            {
                var view = host.AddComponent<StaminaView>();
                view.Bind(Resources.Load<VisualTreeAsset>(Hud.LayoutResourcePath).CloneTree());

                Assert.That(view.IsShown, Is.True);
                Assert.That(view.FillPercent, Is.EqualTo(25f).Within(0.001f));
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void EveryKeyIsInBothLanguages()
        {
            foreach (var language in new[] { GameLanguage.Japanese, GameLanguage.English })
            {
                var table = Localizer.TableOf(language);
                foreach (var key in new[] { StaminaText.ReadingKey, StaminaText.DepletedKey })
                {
                    Assert.That(table.Get(key), Is.Not.EqualTo(key), language + " " + key);
                }
            }
        }

        [Test]
        public void TheEnglishReadingUsesTheSamePercent()
        {
            var table = Localizer.TableOf(GameLanguage.English);

            Assert.That(table.Resolve(StaminaText.Reading(new StaminaStatus(1f, 3f, false))), Is.EqualTo("Stamina 33%"));
            Assert.That(table.Resolve(StaminaText.Reading(new StaminaStatus(0f, 3f, true))), Is.EqualTo("Out of stamina 0%"));
        }

        [Test]
        public void TheReadingLabelIsAddedWhenTheLayoutHasNone()
        {
            var root = Resources.Load<VisualTreeAsset>(Hud.LayoutResourcePath).CloneTree();
            root.Q<Label>(StaminaView.ReadingName)?.RemoveFromHierarchy();
            var host = new GameObject("TestStaminaNoLabel");
            try
            {
                var view = host.AddComponent<StaminaView>();
                view.Bind(root);
                GameSignals.Stamina.Publish(new StaminaStatus(30f, 120f, false));

                Assert.That(view.Element.Q<Label>(StaminaView.ReadingName).text, Is.EqualTo("スタミナ 25%"));
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void UnbindingRemovesTheReadingLabel()
        {
            var element = _view.Element;
            Assert.That(element.Q<Label>(StaminaView.ReadingName), Is.Not.Null);

            _view.Unbind();

            Assert.That(element.Q<Label>(StaminaView.ReadingName), Is.Null);
        }

        private string ReadingLabelText()
        {
            return _view.Element.Q<Label>(StaminaView.ReadingName).text;
        }

        private Length FillWidth()
        {
            return _view.Element.Q<VisualElement>(StaminaView.FillName).style.width.value;
        }
    }
}
