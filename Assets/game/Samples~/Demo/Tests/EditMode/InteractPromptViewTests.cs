using Shooter.Core;
using Shooter.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace Shooter.Tests.EditMode
{
    public sealed class InteractPromptViewTests
    {
        private GameObject _host;
        private InteractPromptView _view;

        private const string Interact = "Player/Interact";

        [SetUp]
        public void SetUp()
        {
            GameSignals.ResetAll();
            _host = new GameObject("Hud");
            _view = _host.AddComponent<InteractPromptView>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_host);
            GameSignals.ResetAll();
        }

        [Test]
        public void TheLatestOwnerReplacesTheTextAndOnlyItCanHideIt()
        {
            var first = new object();
            var second = new object();

            _view.Show(first, LocalizedText.Literal("first"));
            _view.Show(second, LocalizedText.Literal("second"));
            _view.Hide(first);

            Assert.That(_view.IsShown, Is.True, "another owner's hide leaves the prompt alone");
            Assert.That(_view.Text, Is.EqualTo("second"));
            Assert.That(_view.Owner, Is.SameAs(second));

            _view.Hide(second);
            Assert.That(_view.IsShown, Is.False);
            Assert.That(_view.Text, Is.Empty);
        }

        [Test]
        public void BlankTextHidesTheOwnersPrompt()
        {
            var owner = new object();
            _view.Show(owner, LocalizedText.Literal("resupply"));

            _view.Show(owner, LocalizedText.Literal("  "));

            Assert.That(_view.IsShown, Is.False);
        }

        [Test]
        public void TheCratePromptTextsExistInEveryLanguage()
        {
            Assert.That(Localizer.Resolve(AmmoCratePrompt.Describe(new AmmoCrateHint(true, Interact), "E")), Does.Contain("E"));
            Assert.That(AmmoCratePrompt.Describe(new AmmoCrateHint(false, Interact), "E"), Is.EqualTo(new LocalizedText(AmmoCratePrompt.FullKey)));
            Assert.That(Localizer.Resolve(new LocalizedText(AmmoCratePrompt.FullKey)), Is.Not.EqualTo(AmmoCratePrompt.FullKey));
            Assert.That(AmmoCratePrompt.Describe(new AmmoCrateHint(true, Interact), null), Is.EqualTo(new LocalizedText(AmmoCratePrompt.PromptKey, string.Empty)));
        }

        [Test]
        public void EachHintDetailHasItsOwnText()
        {
            Assert.That(InteractPromptView.TryDescribe(new AmmoCrateHint(true, Interact), "E", out var crate), Is.True);
            Assert.That(crate, Is.EqualTo(new LocalizedText(AmmoCratePrompt.PromptKey, "E")));

            Assert.That(InteractPromptView.TryDescribe(new PickupHint(PickupHintAction.Health, null, null, 0, Interact), "E", out var pickup), Is.True);
            Assert.That(pickup, Is.EqualTo(new LocalizedText(PickupPrompt.HealthKey, "E")));

            Assert.That(InteractPromptView.TryDescribe("unknown", "E", out var unknown), Is.False);
            Assert.That(unknown.IsEmpty, Is.True);

            Assert.That(InteractPromptView.InputActionPathOf(new PickupHint(PickupHintAction.Health, null, null, 0, Interact)), Is.EqualTo(Interact));
            Assert.That(InteractPromptView.InputActionPathOf(new AmmoCrateHint(false, "Player/Use")), Is.EqualTo("Player/Use"));
            Assert.That(InteractPromptView.InputActionPathOf(null), Is.Empty);
        }

        [Test]
        public void ABoundViewShowsThePublishedHintAndHidesItWhenCleared()
        {
            var root = BindToLayout();
            var source = new object();
            var detail = new PickupHint(PickupHintAction.Health, null, null, 0, Interact);

            GameSignals.InteractHint.Publish(new InteractHint(source, detail));

            Assert.That(_view.IsShown, Is.True);
            Assert.That(_view.Owner, Is.SameAs(source));
            Assert.That(_view.HintKeyLabel, Is.Not.Null);
            Assert.That(_view.Text, Is.EqualTo(Localizer.Resolve(new LocalizedText(PickupPrompt.HealthKey, _view.HintKeyLabel))));
            var label = root.Q<Label>(InteractPromptView.UxmlName);
            Assert.That(label.text, Is.EqualTo(_view.Text));
            Assert.That(label.style.display.value, Is.EqualTo(DisplayStyle.Flex));

            GameSignals.InteractHint.Clear();

            Assert.That(_view.IsShown, Is.False);
            Assert.That(_view.HintKeyLabel, Is.Null);
            Assert.That(label.style.display.value, Is.EqualTo(DisplayStyle.None));
        }

        [Test]
        public void BindingShowsTheHintAlreadyPublished()
        {
            var source = new object();
            GameSignals.InteractHint.Publish(new InteractHint(source, new AmmoCrateHint(false, Interact)));

            BindToLayout();

            Assert.That(_view.IsShown, Is.True);
            Assert.That(_view.Text, Is.EqualTo(Localizer.Resolve(new LocalizedText(AmmoCratePrompt.FullKey))));
        }

        [Test]
        public void AHintTheViewCannotDescribeIsNotShown()
        {
            BindToLayout();

            GameSignals.InteractHint.Publish(new InteractHint(new object(), "unknown"));

            Assert.That(_view.IsShown, Is.False);
        }

        [Test]
        public void AnUnboundViewStopsFollowingTheSignal()
        {
            BindToLayout();
            _view.Unbind();

            GameSignals.InteractHint.Publish(new InteractHint(new object(), new AmmoCrateHint(false, Interact)));

            Assert.That(_view.IsShown, Is.False);
        }

        private VisualElement BindToLayout()
        {
            var layout = Resources.Load<VisualTreeAsset>(Hud.LayoutResourcePath);
            Assert.That(layout, Is.Not.Null, "Assets/game/Samples~/Demo/UI/Resources/Hud/Hud.uxml should be loadable from Resources");
            var root = layout.CloneTree();
            _view.Bind(root);
            return root;
        }
    }
}
