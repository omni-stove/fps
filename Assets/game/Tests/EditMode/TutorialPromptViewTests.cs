using Fps.Core;
using Fps.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace Fps.Tests.EditMode
{
    public sealed class TutorialPromptViewTests
    {
        private GameObject _owner;
        private TutorialPromptView _view;
        private VisualElement _root;

        [SetUp]
        public void SetUp()
        {
            GameSignals.ResetAll();
            var layout = Resources.Load<VisualTreeAsset>(Hud.LayoutResourcePath);
            Assert.That(layout, Is.Not.Null, "Assets/game/UI/Resources/Hud/Hud.uxml should be loadable from Resources");
            _root = layout.CloneTree();
            _owner = new GameObject(nameof(TutorialPromptViewTests));
            _view = _owner.AddComponent<TutorialPromptView>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_owner);
            GameSignals.ResetAll();
        }

        private static TutorialObservation Frame(float delta = 1f / 60f, float moved = 0f, bool aiming = false, int hits = 0, bool reloading = false, bool crouching = false, bool inCover = false)
        {
            return new TutorialObservation(delta, moved, aiming, hits, reloading, crouching, inCover);
        }

        private static TutorialSteps AtShoot()
        {
            var steps = new TutorialSteps();
            for (var i = 0; i < 10 && steps.Current == TutorialStep.Move; i++)
            {
                steps.Observe(Frame(moved: 1f));
            }

            steps.Observe(Frame(delta: TutorialSteps.DefaultAimSeconds, aiming: true));
            Assume.That(steps.Current, Is.EqualTo(TutorialStep.Shoot));
            return steps;
        }

        private static TutorialSteps Completed()
        {
            var steps = AtShoot();
            steps.Observe(Frame(hits: TutorialSteps.DefaultHitsRequired));
            steps.Observe(Frame(reloading: true));
            steps.Observe(Frame(crouching: true));
            steps.Observe(Frame(inCover: true));
            Assume.That(steps.IsComplete, Is.True);
            return steps;
        }

        [Test]
        public void TheLayoutHasAHiddenLabel()
        {
            var element = _root.Q<Label>(TutorialPromptView.UxmlName);

            Assert.That(element, Is.Not.Null);

            // Unity 6.6 は UXML の style 属性を element.style に映さないので、付けたビューが隠したままにすることで確かめる
            _view.Bind(_root);
            Assert.That(_view.Element, Is.SameAs(element));
            Assert.That(element.style.display.value, Is.EqualTo(DisplayStyle.None));
        }

        [Test]
        public void ShowWritesTheTextAndHideHidesIt()
        {
            _view.Bind(_root);
            var label = (Label)_view.Element;

            _view.Show("[1/6] WASD で移動", completed: false);

            Assert.That(label.text, Is.EqualTo("[1/6] WASD で移動"));
            Assert.That(label.style.display.value, Is.EqualTo(DisplayStyle.Flex));
            Assert.That(label.ClassListContains(TutorialPromptView.CompletedClass), Is.False);

            _view.Show("チュートリアル完了", completed: true);
            Assert.That(label.ClassListContains(TutorialPromptView.CompletedClass), Is.True);
            Assert.That(_view.IsCompleted, Is.True);

            _view.Hide();
            Assert.That(label.style.display.value, Is.EqualTo(DisplayStyle.None));
            Assert.That(label.ClassListContains(TutorialPromptView.CompletedClass), Is.False);
            Assert.That(_view.IsShown, Is.False);
            Assert.That(_view.Text, Is.Empty);
        }

        [Test]
        public void TextShownBeforeBindingAppearsWhenBound()
        {
            _view.Show("[3/6] LMB で撃って標的に 3 回当てる (0/3)", completed: false);

            _view.Bind(_root);

            Assert.That(((Label)_view.Element).text, Does.StartWith("[3/6]"));
            Assert.That(_view.Element.style.display.value, Is.EqualTo(DisplayStyle.Flex));
        }

        [Test]
        public void ShowingNothingHides()
        {
            _view.Bind(_root);
            _view.Show("[1/6] WASD で移動", completed: false);

            _view.Show(" ", completed: false);

            Assert.That(_view.IsShown, Is.False);
            Assert.That(_view.Element.style.display.value, Is.EqualTo(DisplayStyle.None));
        }

        [Test]
        public void APublishedStatusIsShownAsItsPrompt()
        {
            _view.Bind(_root);
            var status = new TutorialStatus(new TutorialSteps(), InputActionPaths.Move, InputDeviceKind.KeyboardMouse);

            GameSignals.Tutorial.Publish(status);

            Assert.That(_view.IsShown, Is.True);
            Assert.That(_view.IsCompleted, Is.False);
            Assert.That(_view.Text, Is.EqualTo(Localizer.Resolve(TutorialText.For(status))));
            Assert.That(_view.Text, Does.StartWith("[1/6] ").And.EndWith(" で移動"));
            Assert.That(((Label)_view.Element).text, Is.EqualTo(_view.Text));
        }

        [Test]
        public void TheShootStepShowsTheHitsSoFar()
        {
            _view.Bind(_root);
            var steps = AtShoot();
            steps.Observe(Frame(hits: 2));

            GameSignals.Tutorial.Publish(new TutorialStatus(steps, InputActionPaths.Attack, InputDeviceKind.KeyboardMouse));

            Assert.That(_view.Text, Does.StartWith("[3/6] ").And.EndWith("(2/3)"));
        }

        [Test]
        public void ACompletedStatusUsesTheCompletedLook()
        {
            _view.Bind(_root);

            GameSignals.Tutorial.Publish(new TutorialStatus(Completed(), null, InputDeviceKind.KeyboardMouse));

            Assert.That(_view.Text, Is.EqualTo("チュートリアル完了"));
            Assert.That(_view.IsCompleted, Is.True);
            Assert.That(_view.Element.ClassListContains(TutorialPromptView.CompletedClass), Is.True);
        }

        [Test]
        public void ClearingTheStatusHides()
        {
            _view.Bind(_root);
            GameSignals.Tutorial.Publish(new TutorialStatus(new TutorialSteps(), InputActionPaths.Move, InputDeviceKind.KeyboardMouse));

            GameSignals.Tutorial.Clear();

            Assert.That(_view.IsShown, Is.False);
            Assert.That(_view.Element.style.display.value, Is.EqualTo(DisplayStyle.None));
        }

        [Test]
        public void BindingCatchesUpWithTheCurrentStatus()
        {
            GameSignals.Tutorial.Publish(new TutorialStatus(new TutorialSteps(), InputActionPaths.Move, InputDeviceKind.KeyboardMouse));

            _view.Bind(_root);

            Assert.That(_view.IsShown, Is.True);
            Assert.That(_view.Text, Does.StartWith("[1/6] "));
        }

        [Test]
        public void AStatusClearedWhileUnboundIsHiddenWhenBoundAgain()
        {
            _view.Bind(_root);
            GameSignals.Tutorial.Publish(new TutorialStatus(new TutorialSteps(), InputActionPaths.Move, InputDeviceKind.KeyboardMouse));
            _view.Unbind();

            GameSignals.Tutorial.Clear();
            Assert.That(_view.IsShown, Is.True, "an unbound view does not hear the signal");
            _view.Bind(_root);

            Assert.That(_view.IsShown, Is.False);
        }

        [Test]
        public void ARebindDuringAStepIsShownWithoutANewStatus()
        {
            var reload = InputSystem.actions != null ? InputSystem.actions.FindAction(InputActionPaths.Reload) : null;
            Assume.That(reload, Is.Not.Null, "the project-wide actions have Reload");
            var steps = AtShoot();
            steps.Observe(Frame(hits: TutorialSteps.DefaultHitsRequired));
            Assume.That(steps.Current, Is.EqualTo(TutorialStep.Reload));
            _view.Bind(_root);
            GameSignals.Tutorial.Publish(new TutorialStatus(steps, InputActionPaths.Reload, InputDeviceKind.KeyboardMouse));
            try
            {
                reload.ApplyBindingOverride(new InputBinding { groups = TutorialKeyLabels.KeyboardMouseGroup, overridePath = "<Keyboard>/f12" });

                _view.RefreshKeys();

                Assert.That(_view.Text, Is.EqualTo(Localizer.Resolve(TutorialText.For(GameSignals.Tutorial.Value.Value))));
                Assert.That(_view.Text, Does.StartWith("[4/6] F12 "));
            }
            finally
            {
                reload.RemoveAllBindingOverrides();
            }
        }
    }
}
