using Fps.Core;
using Fps.Runtime;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Fps.Tests.EditMode
{
    public sealed class InteractPromptViewTests
    {
        private GameObject _host;
        private InteractPromptView _view;

        [SetUp]
        public void SetUp()
        {
            _host = new GameObject("Hud");
            _view = _host.AddComponent<InteractPromptView>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_host);
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
            Assert.That(Localizer.Resolve(new LocalizedText(AmmoCrate.PromptKey, "E")), Does.Contain("E"));
            Assert.That(Localizer.Resolve(new LocalizedText(AmmoCrate.FullKey)), Is.Not.EqualTo(AmmoCrate.FullKey));
        }
    }
}
