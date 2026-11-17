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
            GameSignals.ResetAll();
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
        public void CaptionNamesTheKiller()
        {
            var caption = KillCamText.Caption(LocalizedText.Literal("Enemy-1"));

            Assert.That(caption.Key, Is.EqualTo("hud.killCam.caption"));
            Assert.That(KillCamText.Caption(new KillCamStatus("Enemy-1", "rifle", 4f, 0f, 1f)).Key, Is.EqualTo("hud.killCam.caption"));
            Assert.That(Localizer.Resolve(KillCamText.Caption(new KillCamStatus("Enemy-1", "rifle", 4f, 0f, 1f))),
                Is.EqualTo(Localizer.Resolve(caption)));
        }

        [Test]
        public void WithoutAReplayTheFrameIsHiddenAndThePresentationIsPlain()
        {
            CreateHudView(out var element);

            Assert.That(element.style.display.value, Is.EqualTo(DisplayStyle.None));
            Assert.That(KillCamPresentations.Current, Is.SameAs(KillCamPresentations.Plain), "the UI no longer registers a presentation");
        }

        [Test]
        public void ThePublishedReplayShowsTheCaptionAndProgressUntilItIsCleared()
        {
            var view = CreateHudView(out var element);
            var caption = KillCamText.Caption(LocalizedText.Literal("Enemy-1"));
            var status = new KillCamStatus("Enemy-1", "rifle", 4f, 0f, 1f);

            LogAssert.Expect(LogType.Log, new Regex(@"^\[KillCam\] showing the frame "".*Enemy-1.*""$"));
            GameSignals.KillCam.Publish(status);

            Assert.That(view.IsShown, Is.True);
            Assert.That(view.Caption, Is.EqualTo(Localizer.Resolve(caption)));
            Assert.That(view.Caption, Does.Contain("Enemy-1").And.Not.Contain("hud.killCam.caption"));
            Assert.That(element.Q<Label>(KillCamView.CaptionName).text, Is.EqualTo(view.Caption));
            Assert.That(element.style.display.value, Is.EqualTo(DisplayStyle.Flex));
            Assert.That(view.Progress, Is.EqualTo(0f));

            GameSignals.KillCam.Publish(status.At(1f));
            Assert.That(view.Progress, Is.EqualTo(0.25f));
            Assert.That(element.Q(KillCamView.ProgressName).style.width.value, Is.EqualTo(Length.Percent(25f)));
            GameSignals.KillCam.Publish(status.At(4f));
            Assert.That(view.Progress, Is.EqualTo(1f));

            LogAssert.Expect(LogType.Log, "[KillCam] hiding the frame");
            GameSignals.KillCam.Clear();
            Assert.That(view.IsShown, Is.False);
            Assert.That(view.Caption, Is.Empty);
            Assert.That(element.style.display.value, Is.EqualTo(DisplayStyle.None));
        }

        [Test]
        public void AReplayOfAnotherKillerReplacesTheCaption()
        {
            var view = CreateHudView(out _);
            LogAssert.Expect(LogType.Log, new Regex(@"^\[KillCam\] showing the frame "".*Enemy-1.*""$"));
            GameSignals.KillCam.Publish(new KillCamStatus("Enemy-1", "rifle", 4f, 2f, 1f));

            LogAssert.Expect(LogType.Log, new Regex(@"^\[KillCam\] showing the frame "".*Enemy-2.*""$"));
            GameSignals.KillCam.Publish(new KillCamStatus("Enemy-2", "pistol", 2f, 0.5f, 1f));

            Assert.That(view.Caption, Does.Contain("Enemy-2").And.Not.Contain("Enemy-1"));
            Assert.That(view.Progress, Is.EqualTo(0.25f));
        }

        [Test]
        public void AReplayPublishedBeforeBindingAppearsWhenTheElementArrives()
        {
            var host = new GameObject("Hud");
            _created.Add(host);
            var view = host.AddComponent<KillCamView>();
            GameSignals.KillCam.Publish(new KillCamStatus("Enemy-1", "rifle", 4f, 2f, 1f));
            Assert.That(view.IsShown, Is.False, "an unbound view does not follow the signal");

            var root = LoadLayout();
            LogAssert.Expect(LogType.Log, new Regex(@"^\[KillCam\] showing the frame "".*Enemy-1.*""$"));
            view.Bind(root);

            var element = root.Q(KillCamView.UxmlName);
            Assert.That(element.style.display.value, Is.EqualTo(DisplayStyle.Flex));
            Assert.That(element.Q<Label>(KillCamView.CaptionName).text, Does.Contain("Enemy-1"));
            Assert.That(view.Progress, Is.EqualTo(0.5f));
        }

        [Test]
        public void AnUnboundViewStopsFollowingTheSignal()
        {
            var view = CreateHudView(out _);
            view.Unbind();

            GameSignals.KillCam.Publish(new KillCamStatus("Enemy-1", "rifle", 4f, 0f, 1f));

            Assert.That(view.IsShown, Is.False);
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
    }
}
