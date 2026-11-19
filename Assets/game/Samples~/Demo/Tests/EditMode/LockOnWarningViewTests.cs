using Shooter.Core;
using Shooter.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace Shooter.Tests.EditMode
{
    // Edit Mode では UIDocument が木を作らないことがあるので、Hud.uxml を複製した木を直接 Bind する。
    // 文は JapaneseLanguageSetUp が効かせる日本語の表 (Resources/Localization/ja/lock-on.json) から引く。
    public sealed class LockOnWarningViewTests
    {
        private GameObject _host;
        private LockOnWarningView _view;

        [SetUp]
        public void SetUp()
        {
            GameSignals.ResetAll();
            _host = new GameObject("TestLockOnWarning");
            _view = _host.AddComponent<LockOnWarningView>();
            var layout = Resources.Load<VisualTreeAsset>(Hud.LayoutResourcePath);
            Assert.That(layout, Is.Not.Null, "Assets/game/Samples~/Demo/UI/Resources/Hud/Hud.uxml should be loadable from Resources");
            _view.Bind(layout.CloneTree());
        }

        [TearDown]
        public void TearDown()
        {
            Localizer.Apply(GameLanguage.Japanese);
            Object.DestroyImmediate(_host);
            GameSignals.ResetAll();
        }

        [Test]
        public void TheViewIsHiddenWithoutAWarning()
        {
            Assert.That(_view.Element, Is.Not.Null, "Hud.uxml has the lock-warning element");
            Assert.That(_view.IsShown, Is.False);
            Assert.That(_view.Text, Is.Empty);
            Assert.That(_view.Element.style.display.value, Is.EqualTo(DisplayStyle.None));
        }

        [Test]
        public void TrackingShowsItsTextAndClassAndPointsTheArrow()
        {
            GameSignals.LockOnWarning.Publish(new LockOnWarningStatus(LockOnWarningStage.Tracking, 90f));

            Assert.That(_view.IsShown, Is.True);
            Assert.That(_view.Element.style.display.value, Is.EqualTo(DisplayStyle.Flex));
            Assert.That(_view.Text, Is.EqualTo("捕捉されている"));
            Assert.That(Label().text, Is.EqualTo("捕捉されている"));
            Assert.That(_view.Element.ClassListContains(LockOnWarningView.TrackingClass), Is.True);
            Assert.That(_view.Element.ClassListContains(LockOnWarningView.LockedClass), Is.False);
            Assert.That(Arrow().style.rotate.value.angle.ToDegrees(), Is.EqualTo(90f).Within(0.001f));
        }

        [Test]
        public void LockedShowsTheStrongClassAndDoesNotBlink()
        {
            GameSignals.LockOnWarning.Publish(new LockOnWarningStatus(LockOnWarningStage.Locked, -45f));

            Assert.That(_view.Text, Is.EqualTo("ロックオンされた"));
            Assert.That(_view.Element.ClassListContains(LockOnWarningView.LockedClass), Is.True);
            Assert.That(_view.Element.ClassListContains(LockOnWarningView.TrackingClass), Is.False);
            Assert.That(Arrow().style.rotate.value.angle.ToDegrees(), Is.EqualTo(-45f).Within(0.001f));

            _view.Tick(LockOnWarningView.DefaultBlinkSeconds * 1.5f);

            Assert.That(_view.IsDimmed, Is.False);
            Assert.That(_view.Element.ClassListContains(LockOnWarningView.DimClass), Is.False);
        }

        [Test]
        public void TrackingBlinksOnAndOff()
        {
            GameSignals.LockOnWarning.Publish(new LockOnWarningStatus(LockOnWarningStage.Tracking, 0f));
            var half = LockOnWarningView.DefaultBlinkSeconds;

            Assert.That(_view.Element.ClassListContains(LockOnWarningView.DimClass), Is.False, "starts lit");

            _view.Tick(half * 0.5f);
            Assert.That(_view.IsDimmed, Is.False);

            _view.Tick(half * 0.75f);
            Assert.That(_view.IsDimmed, Is.True);
            Assert.That(_view.Element.ClassListContains(LockOnWarningView.DimClass), Is.True);

            _view.Tick(half);
            Assert.That(_view.IsDimmed, Is.False);
            Assert.That(_view.Element.ClassListContains(LockOnWarningView.DimClass), Is.False);
        }

        [Test]
        public void BecomingLockedWhileDimmedShowsItLit()
        {
            GameSignals.LockOnWarning.Publish(new LockOnWarningStatus(LockOnWarningStage.Tracking, 0f));
            _view.Tick(LockOnWarningView.DefaultBlinkSeconds * 1.5f);
            Assert.That(_view.IsDimmed, Is.True);

            GameSignals.LockOnWarning.Publish(new LockOnWarningStatus(LockOnWarningStage.Locked, 0f));

            Assert.That(_view.IsDimmed, Is.False);
            Assert.That(_view.Element.ClassListContains(LockOnWarningView.DimClass), Is.False);
        }

        [Test]
        public void ANonPositiveBlinkKeepsTrackingLit()
        {
            _view.BlinkSeconds = 0f;
            GameSignals.LockOnWarning.Publish(new LockOnWarningStatus(LockOnWarningStage.Tracking, 0f));

            _view.Tick(10f);

            Assert.That(_view.IsDimmed, Is.False);
            Assert.That(_view.Element.ClassListContains(LockOnWarningView.DimClass), Is.False);
        }

        [Test]
        public void NullHidesTheViewAndDropsTheStageClasses()
        {
            GameSignals.LockOnWarning.Publish(new LockOnWarningStatus(LockOnWarningStage.Locked, 30f));

            GameSignals.LockOnWarning.Publish(null);

            Assert.That(_view.IsShown, Is.False);
            Assert.That(_view.Text, Is.Empty);
            Assert.That(_view.Element.style.display.value, Is.EqualTo(DisplayStyle.None));
            Assert.That(_view.Element.ClassListContains(LockOnWarningView.LockedClass), Is.False);
            Assert.That(_view.Element.ClassListContains(LockOnWarningView.TrackingClass), Is.False);
        }

        [Test]
        public void NoThreatHidesTheView()
        {
            GameSignals.LockOnWarning.Publish(new LockOnWarningStatus(LockOnWarningStage.Tracking, 30f));

            GameSignals.LockOnWarning.Publish(LockOnWarningStatus.None);

            Assert.That(_view.IsShown, Is.False);
            Assert.That(_view.Element.style.display.value, Is.EqualTo(DisplayStyle.None));
        }

        [Test]
        public void TheTextFollowsTheLanguage()
        {
            GameSignals.LockOnWarning.Publish(new LockOnWarningStatus(LockOnWarningStage.Tracking, 0f));

            Localizer.Apply(GameLanguage.English);

            Assert.That(_view.Text, Is.EqualTo("TRACKED"));
            Assert.That(Label().text, Is.EqualTo("TRACKED"));

            GameSignals.LockOnWarning.Publish(new LockOnWarningStatus(LockOnWarningStage.Locked, 0f));

            Assert.That(_view.Text, Is.EqualTo("MISSILE LOCK"));
        }

        [Test]
        public void AWarningPublishedBeforeBindingIsShownOnBind()
        {
            var other = new GameObject("TestLockOnWarningLate");
            try
            {
                GameSignals.LockOnWarning.Publish(new LockOnWarningStatus(LockOnWarningStage.Locked, 0f));
                var late = other.AddComponent<LockOnWarningView>();
                late.Bind(Resources.Load<VisualTreeAsset>(Hud.LayoutResourcePath).CloneTree());

                Assert.That(late.IsShown, Is.True);
                Assert.That(late.Text, Is.EqualTo("ロックオンされた"));
            }
            finally
            {
                Object.DestroyImmediate(other);
            }
        }

        private Label Label()
        {
            return _view.Element.Q<Label>(className: LockOnWarningView.TextClass);
        }

        private VisualElement Arrow()
        {
            return _view.Element.Q<VisualElement>(className: LockOnWarningView.ArrowClass);
        }
    }
}
