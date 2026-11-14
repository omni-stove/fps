using System;
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
    /// <summary>
    /// <see cref="SubtitleView"/> が、設定で字幕を On にしている間だけ音の合図を「[種類 向き]」の行にして画面の下に出し、
    /// 同じ種類を 1 行にまとめて 3 秒で消すことを確かめる。
    /// Edit Mode では MonoBehaviour の OnEnable が走らず <see cref="SubtitleFeed"/> を購読しないので、Show と Tick を直接呼ぶ。
    /// </summary>
    public sealed class SubtitleViewTests
    {
        private readonly List<Object> _created = new List<Object>();
        private ControlSettings _appliedBefore;
        private float _volumeBefore;

        [SetUp]
        public void SetUp()
        {
            _appliedBefore = AppliedControlSettings.Current;
            _volumeBefore = AudioListener.volume;
            AppliedControlSettings.Apply(ControlSettings.Default.WithShowSubtitles(true));
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var created in _created)
            {
                if (created != null)
                {
                    Object.DestroyImmediate(created);
                }
            }

            _created.Clear();
            SubtitleFeed.ClearSubscribers();
            AppliedControlSettings.Apply(_appliedBefore);
            AudioListener.volume = _volumeBefore;
        }

        [Test]
        public void TheLayoutHasTheSubtitleArea()
        {
            Assert.That(CloneLayout().Q<VisualElement>(SubtitleView.UxmlName), Is.Not.Null);
        }

        [Test]
        public void HudAddViewsAttachesTheSubtitles()
        {
            var hud = Hud.Create("SubtitleViewTests.Hud", null, null, null);
            _created.Add(hud.gameObject);
            _created.Add(hud.OwnedPanelSettings);

            Assert.That(hud.Subtitles, Is.Not.Null, "Hud.AddViews attaches the subtitles");
        }

        // 射撃場で自分が撃つと正面、後ろから撃たれると後ろ
        [Test]
        public void GunshotsAreShownWithTheirDirection()
        {
            var view = CreateBoundView();

            view.Show(new SoundCueSource(SoundCueKind.Gunshot, new Vector3(0f, 1.6f, 1f), 120f));
            Assert.That(Texts(view), Is.EqualTo(new[] { "[銃声 正面]" }));
            Assert.That(view.Element.style.display.value, Is.EqualTo(DisplayStyle.Flex));

            view.Show(new SoundCueSource(SoundCueKind.Gunshot, new Vector3(0f, 1.6f, -15f), 120f));
            Assert.That(Texts(view), Is.EqualTo(new[] { "[銃声 後ろ]" }), "the same kind stays on one line");
        }

        [Test]
        public void DifferentKindsGetTheirOwnLines()
        {
            var view = CreateBoundView();

            view.Show(new SoundCueSource(SoundCueKind.Footstep, new Vector3(-5f, 0f, 0f), 30f));
            view.Show(new SoundCueSource(SoundCueKind.Explosion, new Vector3(0f, 0f, 60f), 500f));
            view.Show(new SoundCueSource(SoundCueKind.Reload, null, 120f));

            Assert.That(Texts(view), Is.EqualTo(new[] { "[足音 左]", "[爆発 正面 遠く]", "[リロード]" }));
            Assert.That(
                view.Element.Query<Label>(className: SubtitleView.LineClass).ToList(),
                Has.Count.EqualTo(3));
        }

        [Test]
        public void ALineDisappearsThreeSecondsAfterItsLastCue()
        {
            var view = CreateBoundView();
            view.Show(new SoundCueSource(SoundCueKind.Gunshot, new Vector3(10f, 0f, 0f), 120f));

            view.Tick(2f);
            view.Show(new SoundCueSource(SoundCueKind.Gunshot, new Vector3(10f, 0f, 0f), 120f));
            view.Tick(2.5f);
            Assert.That(Texts(view), Is.EqualTo(new[] { "[銃声 右]" }), "the second shot extended it");

            view.Tick(0.6f);
            Assert.That(view.Lines, Is.Empty);
            Assert.That(view.Element.style.display.value, Is.EqualTo(DisplayStyle.None));
            Assert.That(view.Element.Query<Label>(className: SubtitleView.LineClass).ToList(), Is.Empty);
        }

        [Test]
        public void SoundsTooFarToHearAreNotShown()
        {
            var view = CreateBoundView();

            view.Show(new SoundCueSource(SoundCueKind.Footstep, new Vector3(0f, 0f, 31f), 30f));

            Assert.That(view.Lines, Is.Empty);
        }

        [Test]
        public void NothingIsShownWhileSubtitlesAreOff()
        {
            AppliedControlSettings.Apply(ControlSettings.Default);
            var view = CreateBoundView();

            view.Show(new SoundCueSource(SoundCueKind.Gunshot, new Vector3(0f, 0f, 5f), 120f));

            Assert.That(view.ShowsSubtitles, Is.False);
            Assert.That(view.Lines, Is.Empty);
            Assert.That(view.Element.style.display.value, Is.EqualTo(DisplayStyle.None));
        }

        [Test]
        public void TheViewKeepsItsLinesWhenTheTreeIsRebuilt()
        {
            var view = CreateBoundView();
            view.Show(new SoundCueSource(SoundCueKind.Explosion, new Vector3(0f, 0f, 5f), 500f));
            var oldElement = view.Element;

            view.Bind(CloneLayout());

            Assert.That(oldElement.Query<Label>(className: SubtitleView.LineClass).ToList(), Is.Empty);
            Assert.That(Texts(view), Is.EqualTo(new[] { "[爆発 正面]" }));
        }

        [Test]
        public void AThrowingSubscriberDoesNotStopTheOthers()
        {
            var received = new List<SoundCueSource>();
            Action<SoundCueSource> thrower = _ => throw new InvalidOperationException("boom");
            SubtitleFeed.CuePlayed += thrower;
            SubtitleFeed.CuePlayed += received.Add;
            LogAssert.Expect(LogType.Exception, new Regex("boom"));

            SubtitleFeed.Raise(SoundCueKind.Reload, Vector3.one, 120f);

            Assert.That(received, Has.Count.EqualTo(1));
            Assert.That(received[0].Kind, Is.EqualTo(SoundCueKind.Reload));
            Assert.That(received[0].Position, Is.EqualTo((Vector3?)Vector3.one));
            Assert.That(received[0].AudibleDistance, Is.EqualTo(120f));
        }

        private SubtitleView CreateBoundView()
        {
            var gameObject = new GameObject(nameof(SubtitleView));
            _created.Add(gameObject);
            var view = gameObject.AddComponent<SubtitleView>();

            var viewer = new GameObject("viewer");
            _created.Add(viewer);
            viewer.transform.SetPositionAndRotation(new Vector3(0f, 1.6f, 0f), Quaternion.identity);
            view.Viewer = viewer.transform;

            view.Bind(CloneLayout());
            return view;
        }

        private static string[] Texts(SubtitleView view)
        {
            var labels = view.Element.Query<Label>(className: SubtitleView.LineClass).ToList();
            var texts = new string[labels.Count];
            for (var i = 0; i < labels.Count; i++)
            {
                texts[i] = labels[i].text;
            }

            return texts;
        }

        private static VisualElement CloneLayout()
        {
            var layout = Resources.Load<VisualTreeAsset>(Hud.LayoutResourcePath);
            Assert.That(layout, Is.Not.Null, "Assets/game/UI/Resources/Hud/Hud.uxml should be loadable from Resources");
            return layout.CloneTree();
        }
    }
}
