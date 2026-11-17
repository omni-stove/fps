using System;
using System.Linq;
using Shooter.Core;
using Shooter.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace Shooter.Tests.EditMode
{
    public sealed class LocalizedTextsTests
    {
        private GameLanguage _languageBefore;

        [SetUp]
        public void SetUp()
        {
            _languageBefore = Localizer.Language;
            Localizer.Apply(GameLanguage.Japanese);
        }

        [TearDown]
        public void TearDown()
        {
            Localizer.Apply(_languageBefore);
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

            LogAssert.Expect(LogType.Log, Localizer.LogTag + " language is now en");
            Localizer.Apply(GameLanguage.English);
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

                Localizer.Apply(GameLanguage.English);

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
            var hud = Hud.Create("LocalizedPauseHud");
            try
            {
                var pause = hud.PauseMenu;
                pause.Bind(Resources.Load<VisualTreeAsset>(Hud.LayoutResourcePath).CloneTree());
                var note = pause.Element.Q<Label>(PauseMenu.NoteLabelName);

                Assert.That(note.text, Is.EqualTo("オンラインでは試合は止まりません"));

                Localizer.Apply(GameLanguage.English);

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
            var hud = Hud.Create("UnboundPauseHud");
            try
            {
                var pause = hud.PauseMenu;
                pause.Bind(Resources.Load<VisualTreeAsset>(Hud.LayoutResourcePath).CloneTree());
                var note = pause.Element.Q<Label>(PauseMenu.NoteLabelName);
                pause.Unbind();

                Localizer.Apply(GameLanguage.English);

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
