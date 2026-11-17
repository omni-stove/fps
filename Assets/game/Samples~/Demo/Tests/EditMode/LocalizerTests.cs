using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Shooter.Core;
using Shooter.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Shooter.Tests.EditMode
{
    /// <summary>
    /// <see cref="Localizer"/> が Resources の ja.json / en.json を読み、今の言語で文字を引き、言語が変わったときだけ知らせること。
    /// </summary>
    public sealed class LocalizerTests
    {
        private ControlSettings _appliedBefore;
        private GameLanguage _languageBefore;

        [SetUp]
        public void SetUp()
        {
            _appliedBefore = AppliedControlSettings.Current;
            _languageBefore = Localizer.Language;
            Localizer.Apply(GameLanguage.Japanese);
        }

        [TearDown]
        public void TearDown()
        {
            AppliedControlSettings.Apply(_appliedBefore);
            Localizer.Apply(_languageBefore);
        }

        [Test]
        public void EveryLanguageHasATableInResources()
        {
            foreach (var language in GameLanguages.All)
            {
                // Strings/<言語コード>.json は移す前の古いキーの置き場で、後から足した言語には無くてよい。
                var path = Localizer.ResourcePathOf(language);
                var asset = Resources.Load<TextAsset>(path);
                string error;
                if (asset != null)
                {
                    Assert.That(StringTable.TryParse(asset.text, null, out _, out error), Is.True, $"{path}: {error}");
                }

                var settingsPath = Localizer.LayerFolderOf(language) + "/settings";
                var settings = Resources.Load<TextAsset>(settingsPath);
                Assert.That(settings, Is.Not.Null, settingsPath);
                Assert.That(StringTable.TryParse(settings.text, null, out var table, out error), Is.True, $"{settingsPath}: {error}");
                Assert.That(table.Contains("settings.title"), Is.True, settingsPath);
                Assert.That(table.Contains(Localizer.NativeNameKey), Is.True, settingsPath);
            }
        }

        [Test]
        public void GetUsesTheCurrentLanguage()
        {
            Assert.That(Localizer.Language, Is.EqualTo(GameLanguage.Japanese));
            Assert.That(Localizer.Get("settings.title"), Is.EqualTo("設定"));

            LogAssert.Expect(LogType.Log, Localizer.LogTag + " language is now en");
            Localizer.Apply(GameLanguage.English);

            Assert.That(Localizer.Language, Is.EqualTo(GameLanguage.English));
            Assert.That(Localizer.Get("settings.title"), Is.EqualTo("Settings"));
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void FormatInsertsTheValues()
        {
            Localizer.Apply(GameLanguage.English);

            Assert.That(Localizer.Format("settings.rebind.conflict", "W", "Move forward"), Is.EqualTo("W is also assigned to “Move forward”."));
        }

        [Test]
        public void AMissingKeyShowsTheKeyWithAWarning()
        {
            var key = "tests.missing." + Guid.NewGuid().ToString("N");
            LogAssert.Expect(LogType.Warning, new Regex(@"^\[Localization\] ja: .*" + Regex.Escape(key)));

            Assert.That(Localizer.Get(key), Is.EqualTo(key));
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void TryGetDoesNotWarn()
        {
            Assert.That(Localizer.TryGet("settings.title", out var text), Is.True);
            Assert.That(text, Is.EqualTo("設定"));
            Assert.That(Localizer.TryGet("tests.missing", out text), Is.False);
            Assert.That(text, Is.Null);
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void ChangedIsRaisedOnlyWhenTheLanguageChanges()
        {
            var received = new List<GameLanguage>();
            Action<GameLanguage> record = received.Add;
            Localizer.Changed += record;
            try
            {
                Localizer.Apply(GameLanguage.Japanese);
                AppliedControlSettings.Apply(_appliedBefore.With(ControlValue.MasterVolume, 30));
                Localizer.Apply(GameLanguage.English);
                Localizer.Apply(GameLanguage.English);
                Localizer.Apply(GameLanguage.Japanese);
            }
            finally
            {
                Localizer.Changed -= record;
            }

            Assert.That(received, Is.EqualTo(new[] { GameLanguage.English, GameLanguage.Japanese }));
        }

        [Test]
        public void TheLanguageIsUpdatedBeforeHandlersRunAndAThrowingHandlerDoesNotStopTheOthers()
        {
            var seen = new List<string>();
            Action<GameLanguage> throwing = _ => throw new InvalidOperationException("boom");
            Action<GameLanguage> reading = _ => seen.Add(Localizer.Get("settings.title"));
            Localizer.Changed += throwing;
            Localizer.Changed += reading;
            try
            {
                LogAssert.Expect(LogType.Error, new Regex(@"^\[Localization\] .*boom"));
                LogAssert.Expect(LogType.Exception, new Regex("boom"));

                Localizer.Apply(GameLanguage.English);
            }
            finally
            {
                Localizer.Changed -= throwing;
                Localizer.Changed -= reading;
            }

            Assert.That(seen, Is.EqualTo(new[] { "Settings" }));
        }

        [TestCase(SystemLanguage.Japanese, "ja")]
        [TestCase(SystemLanguage.English, "en")]
        [TestCase(SystemLanguage.French, "en")]
        [TestCase(SystemLanguage.ChineseSimplified, "en")]
        [TestCase(SystemLanguage.Unknown, "en")]
        public void TheFirstLanguageFollowsTheOperatingSystem(SystemLanguage system, string expected)
        {
            Assert.That(Localizer.FromSystemLanguage(system), Is.EqualTo(new GameLanguage(expected)));
        }

        [Test]
        public void ApplyRejectsALanguageWithoutACode()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => Localizer.Apply(default));
            Assert.That(Localizer.Language, Is.EqualTo(GameLanguage.Japanese));
        }

        [Test]
        public void ActionLabelsFollowTheLanguage()
        {
            Assert.That(InputActionLabels.LabelOf("Move", "up"), Is.EqualTo("前へ移動"));
            Assert.That(InputActionLabels.LabelOf("Reload"), Is.EqualTo("リロード"));

            LogAssert.Expect(LogType.Log, Localizer.LogTag + " language is now en");
            Localizer.Apply(GameLanguage.English);

            Assert.That(InputActionLabels.LabelOf("Move", "up"), Is.EqualTo("Move forward"));
            Assert.That(InputActionLabels.LabelOf("Reload"), Is.EqualTo("Reload"));
            Assert.That(InputActionLabels.LabelOf("SomethingNew"), Is.EqualTo("SomethingNew"), "an action without a label keeps its name");
            Assert.That(InputActionLabels.LabelOf("SomethingNew", "up"), Is.EqualTo("SomethingNew (up)"));
            Assert.That(InputActionLabels.LabelOf(new BindingConflict("Move", "up", 1)), Is.EqualTo("Move forward"));
            LogAssert.NoUnexpectedReceived();
        }
    }
}
