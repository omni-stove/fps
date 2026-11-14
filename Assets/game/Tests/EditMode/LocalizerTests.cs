using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Fps.Core;
using Fps.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Fps.Tests.EditMode
{
    /// <summary>
    /// <see cref="Localizer"/> が Resources の ja.json / en.json を読み、今効いている設定の言語で文字を引き、言語が変わったときだけ知らせること。
    /// </summary>
    public sealed class LocalizerTests
    {
        private ControlSettings _appliedBefore;

        [SetUp]
        public void SetUp()
        {
            _appliedBefore = AppliedControlSettings.Current;
            AppliedControlSettings.Apply(ControlSettings.Default);
        }

        [TearDown]
        public void TearDown()
        {
            AppliedControlSettings.Apply(_appliedBefore);
        }

        [Test]
        public void EveryLanguageHasATableInResources()
        {
            foreach (var language in GameLanguages.All)
            {
                var path = Localizer.ResourcePathOf(language);
                var asset = Resources.Load<TextAsset>(path);

                Assert.That(asset, Is.Not.Null, path);
                Assert.That(StringTable.TryParse(asset.text, null, out var table, out var error), Is.True, $"{path}: {error}");
                Assert.That(table.Contains("settings.title"), Is.True, path);
            }
        }

        [Test]
        public void GetUsesTheLanguageOfTheAppliedSettings()
        {
            Assert.That(Localizer.Language, Is.EqualTo(GameLanguage.Japanese));
            Assert.That(Localizer.Get("settings.title"), Is.EqualTo("設定"));

            AppliedControlSettings.Apply(ControlSettings.Default.WithLanguage(GameLanguage.English));

            Assert.That(Localizer.Language, Is.EqualTo(GameLanguage.English));
            Assert.That(Localizer.Get("settings.title"), Is.EqualTo("Settings"));
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void FormatInsertsTheValues()
        {
            AppliedControlSettings.Apply(ControlSettings.Default.WithLanguage(GameLanguage.English));

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
                AppliedControlSettings.Apply(ControlSettings.Default.With(ControlValue.MasterVolume, 30));
                AppliedControlSettings.Apply(ControlSettings.Default.WithLanguage(GameLanguage.English));
                AppliedControlSettings.Apply(ControlSettings.Default.WithLanguage(GameLanguage.English).With(ControlValue.MasterVolume, 30));
                AppliedControlSettings.Apply(ControlSettings.Default);
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

                AppliedControlSettings.Apply(ControlSettings.Default.WithLanguage(GameLanguage.English));
            }
            finally
            {
                Localizer.Changed -= throwing;
                Localizer.Changed -= reading;
            }

            Assert.That(seen, Is.EqualTo(new[] { "Settings" }));
        }

        [TestCase(SystemLanguage.Japanese, GameLanguage.Japanese)]
        [TestCase(SystemLanguage.English, GameLanguage.English)]
        [TestCase(SystemLanguage.French, GameLanguage.English)]
        [TestCase(SystemLanguage.ChineseSimplified, GameLanguage.English)]
        [TestCase(SystemLanguage.Unknown, GameLanguage.English)]
        public void TheFirstLanguageFollowsTheOperatingSystem(SystemLanguage system, GameLanguage expected)
        {
            Assert.That(Localizer.FromSystemLanguage(system), Is.EqualTo(expected));
        }

        [Test]
        public void ActionLabelsFollowTheLanguage()
        {
            Assert.That(PlayerBindings.LabelOf("Move", "up"), Is.EqualTo("前へ移動"));
            Assert.That(PlayerBindings.LabelOf("Reload"), Is.EqualTo("リロード"));

            AppliedControlSettings.Apply(ControlSettings.Default.WithLanguage(GameLanguage.English));

            Assert.That(PlayerBindings.LabelOf("Move", "up"), Is.EqualTo("Move forward"));
            Assert.That(PlayerBindings.LabelOf("Reload"), Is.EqualTo("Reload"));
            Assert.That(PlayerBindings.LabelOf("SomethingNew"), Is.EqualTo("SomethingNew"), "an action without a label keeps its name");
            LogAssert.NoUnexpectedReceived();
        }
    }
}
