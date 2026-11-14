using System;
using System.Linq;
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
    /// UXML にキーで書いた文字 (<see cref="LocalizedTexts"/>) と、コードで入れた文字が今の言語で出て、
    /// 言語を変えると開いている画面 (メインメニュー・ポーズ・スコアボード) も書き換わること。
    /// </summary>
    public sealed class LocalizedTextsTests
    {
        private ControlSettings _appliedBefore;

        [SetUp]
        public void SetUp()
        {
            _appliedBefore = AppliedControlSettings.Current;
            AppliedControlSettings.Apply(_appliedBefore.WithLanguage(GameLanguage.Japanese));
        }

        [TearDown]
        public void TearDown()
        {
            AppliedControlSettings.Apply(_appliedBefore);
        }

        [Test]
        public void OnlyTextsMarkedAsKeysAreReplaced()
        {
            var root = new VisualElement();
            var keyed = new Label(LocalizedTexts.KeyPrefix + "pause.title");
            var plain = new Label("--:--");
            var bare = new Label(LocalizedTexts.KeyPrefix);
            root.Add(keyed);
            root.Add(plain);
            root.Add(bare);

            var texts = LocalizedTexts.Bind(root);

            Assert.That(texts.Count, Is.EqualTo(1));
            Assert.That(keyed.text, Is.EqualTo("ポーズ"));
            Assert.That(texts.KeyOf(keyed), Is.EqualTo("pause.title"));
            Assert.That(plain.text, Is.EqualTo("--:--"));
            Assert.That(bare.text, Is.EqualTo(LocalizedTexts.KeyPrefix));
            Assert.That(texts.KeyOf(plain), Is.Null);

            AppliedControlSettings.Apply(AppliedControlSettings.Current.WithLanguage(GameLanguage.English));
            texts.Refresh();

            Assert.That(keyed.text, Is.EqualTo("Paused"));
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void BindRejectsNull()
        {
            Assert.Throws<ArgumentNullException>(() => LocalizedTexts.Bind(null));
        }

        [TestCase(Hud.LayoutResourcePath)]
        [TestCase(MainMenuScreen.LayoutResourcePath)]
        public void EveryKeyInTheLayoutsIsInEveryLanguage(string layoutPath)
        {
            var layout = Resources.Load<VisualTreeAsset>(layoutPath);
            Assert.That(layout, Is.Not.Null, layoutPath);
            var keys = layout.CloneTree().Query<TextElement>().ToList()
                .Select(e => LocalizedTexts.TryReadKey(e.text, out var key) ? key : null)
                .Where(k => k != null)
                .ToList();

            Assert.That(keys, Is.Not.Empty, layoutPath);
            foreach (var language in GameLanguages.All)
            {
                var table = Localizer.TableOf(language);
                foreach (var key in keys)
                {
                    Assert.That(table.Contains(key), Is.True, $"{GameLanguages.Code(language)}: {key} ({layoutPath})");
                }
            }
        }

        [Test]
        public void TheMainMenuIsRewrittenWhenTheLanguageChanges()
        {
            var owner = new GameObject("LocalizedMainMenu");
            try
            {
                var screen = owner.AddComponent<MainMenuScreen>();
                screen.Bind(Resources.Load<VisualTreeAsset>(MainMenuScreen.LayoutResourcePath).CloneTree());
                var range = screen.Buttons[MainMenuScreen.ButtonNames.ToList().IndexOf(MainMenuScreen.RangeButtonName)];

                Assert.That(range.text, Is.EqualTo("射撃場"));

                AppliedControlSettings.Apply(AppliedControlSettings.Current.WithLanguage(GameLanguage.English));

                Assert.That(range.text, Is.EqualTo("FIRING RANGE"));
                Assert.That(screen.Buttons.Select(b => b.text), Has.None.StartsWith(LocalizedTexts.KeyPrefix));
            }
            finally
            {
                Object.DestroyImmediate(owner);
            }
        }

        [Test]
        public void TheOnlinePauseNoteIsRewrittenWhenTheLanguageChanges()
        {
            var hud = Hud.Create("LocalizedPauseHud", null, null, null);
            try
            {
                var pause = hud.PauseMenu;
                pause.Bind(Resources.Load<VisualTreeAsset>(Hud.LayoutResourcePath).CloneTree());
                var note = pause.Element.Q<Label>(PauseMenu.NoteLabelName);

                Assert.That(note.text, Is.EqualTo("オンラインでは試合は止まりません"));

                AppliedControlSettings.Apply(AppliedControlSettings.Current.WithLanguage(GameLanguage.English));

                Assert.That(note.text, Is.EqualTo("The match does not stop while online"));
            }
            finally
            {
                var settings = hud.OwnedPanelSettings;
                Object.DestroyImmediate(hud.gameObject);
                if (settings != null)
                {
                    Object.DestroyImmediate(settings);
                }
            }
        }

        [Test]
        public void AnUnboundViewNoLongerFollowsTheLanguage()
        {
            var hud = Hud.Create("UnboundPauseHud", null, null, null);
            try
            {
                var pause = hud.PauseMenu;
                pause.Bind(Resources.Load<VisualTreeAsset>(Hud.LayoutResourcePath).CloneTree());
                var note = pause.Element.Q<Label>(PauseMenu.NoteLabelName);
                pause.Unbind();

                AppliedControlSettings.Apply(AppliedControlSettings.Current.WithLanguage(GameLanguage.English));

                Assert.That(note.text, Is.EqualTo("オンラインでは試合は止まりません"));
            }
            finally
            {
                var settings = hud.OwnedPanelSettings;
                Object.DestroyImmediate(hud.gameObject);
                if (settings != null)
                {
                    Object.DestroyImmediate(settings);
                }
            }
        }
    }
}
