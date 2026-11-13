using Fps.Runtime;
using NUnit.Framework;
using UnityEngine;
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
        }

        [Test]
        public void TheLayoutHasAHiddenLabel()
        {
            var element = _root.Q<Label>(TutorialPromptView.UxmlName);

            Assert.That(element, Is.Not.Null);
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
    }
}
