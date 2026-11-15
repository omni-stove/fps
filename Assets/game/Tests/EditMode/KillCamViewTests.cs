using System.Collections.Generic;
using System.Text.RegularExpressions;
using Fps.Core;
using Fps.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace Fps.Tests.EditMode
{
    public sealed class KillCamViewTests
    {
        private readonly List<Object> _created = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            KillCamPresentations.Current = null;
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
        public void TheDefaultPresentationIsTheHudFrameUnlessReplaced()
        {
            Assert.That(KillCamPresentations.Current, Is.InstanceOf<DefaultKillCamPresentation>());
            Assert.That(KillCamPresentations.Current.PlaybackSpeed, Is.EqualTo(1f));

            var replaced = new StubPresentation();
            KillCamPresentations.Current = replaced;
            Assert.That(KillCamPresentations.Current, Is.SameAs(replaced));

            KillCamPresentations.Current = null;
            Assert.That(KillCamPresentations.Current, Is.InstanceOf<DefaultKillCamPresentation>());
        }

        [Test]
        public void TheDefaultPresentationShowsTheCaptionAndProgressOnTheHudUntilTheEnd()
        {
            var view = CreateHudView(out var element);
            Assert.That(element.style.display.value, Is.EqualTo(DisplayStyle.None));

            var presentation = new DefaultKillCamPresentation();
            var caption = KillCamText.Caption(LocalizedText.Literal("Enemy-1"));
            LogAssert.Expect(LogType.Log, new Regex(@"^\[KillCam\] showing the frame "".*Enemy-1.*""$"));
            presentation.Begin(new KillCamShot(null, null, caption, "Enemy-1", 4f));

            Assert.That(presentation.View, Is.SameAs(view));
            Assert.That(view.IsShown, Is.True);
            Assert.That(view.Caption, Is.EqualTo(Localizer.Resolve(caption)));
            Assert.That(view.Caption, Does.Contain("Enemy-1").And.Not.Contain("hud.killCam.caption"));
            Assert.That(element.Q<Label>(KillCamView.CaptionName).text, Is.EqualTo(view.Caption));
            Assert.That(element.style.display.value, Is.EqualTo(DisplayStyle.Flex));

            presentation.Progress(0.25f);
            Assert.That(view.Progress, Is.EqualTo(0.25f));
            Assert.That(element.Q(KillCamView.ProgressName).style.width.value, Is.EqualTo(Length.Percent(25f)));
            presentation.Progress(2f);
            Assert.That(view.Progress, Is.EqualTo(1f), "progress is clamped");

            LogAssert.Expect(LogType.Log, "[KillCam] hiding the frame");
            presentation.End(false);
            Assert.That(view.IsShown, Is.False);
            Assert.That(view.Caption, Is.Empty);
            Assert.That(presentation.View, Is.Null);
            Assert.That(element.style.display.value, Is.EqualTo(DisplayStyle.None));
        }

        [Test]
        public void ACaptionShownBeforeBindingAppearsWhenTheElementArrives()
        {
            var host = new GameObject("Hud");
            _created.Add(host);
            var view = host.AddComponent<KillCamView>();

            view.Show(LocalizedText.Literal("KILLCAM Enemy-1"));
            var root = LoadLayout();
            view.Bind(root);

            var element = root.Q(KillCamView.UxmlName);
            Assert.That(element.style.display.value, Is.EqualTo(DisplayStyle.Flex));
            Assert.That(element.Q<Label>(KillCamView.CaptionName).text, Is.EqualTo("KILLCAM Enemy-1"));
        }

        [Test]
        public void WithoutAHudTheDefaultPresentationDrawsNothing()
        {
            var presentation = new DefaultKillCamPresentation();

            presentation.Begin(new KillCamShot(null, null, KillCamText.Caption(LocalizedText.Literal("Turret")), "Turret", 1f));
            presentation.Progress(0.5f);
            presentation.End(true);

            Assert.That(presentation.View, Is.Null);
        }

        private KillCamView CreateHudView(out VisualElement element)
        {
            var hud = Hud.Create("KillCamViewTests.Hud", null, null, null);
            _created.Add(hud.gameObject);
            _created.Add(hud.OwnedPanelSettings);
            var view = hud.KillCam;
            Assert.That(view, Is.Not.Null, "the HUD carries the kill cam view");

            // Edit Mode では UIDocument が木を作らないことがあるので、同じレイアウトを読んで渡す
            var root = LoadLayout();
            view.Bind(root);
            element = root.Q(KillCamView.UxmlName);
            return view;
        }

        private static VisualElement LoadLayout()
        {
            var layout = Resources.Load<VisualTreeAsset>(Hud.LayoutResourcePath);
            Assert.That(layout, Is.Not.Null);
            return layout.Instantiate();
        }

        private sealed class StubPresentation : IKillCamPresentation
        {
            public float PlaybackSpeed => 1f;

            public void Begin(KillCamShot shot)
            {
            }

            public void Progress(float progress)
            {
            }

            public void End(bool completed)
            {
            }
        }
    }
}
