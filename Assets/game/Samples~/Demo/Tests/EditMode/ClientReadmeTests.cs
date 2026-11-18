using System;
using System.IO;
using System.Linq;
using Shooter.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Shooter.Tests.EditMode
{
    public sealed class ClientReadmeTests
    {
        private const string ReadmePath = "docs/client-readme.md";

        private static readonly string[] _quotedKeys =
        {
            "menu.quickMatch",
            "menu.party",
            "menu.joinByCode",
            "party.create",
            "party.inviteCode",
            "party.join",
            "party.members",
            "party.back",
            "party.leave",
            "customMatch.codePlaceholder",
            "customMatch.join",
            "customMatch.notFound",
            "customMatch.full",
            "customMatch.versionMismatch",
            "menu.settings",
            "settings.section.bindings",
            "settings.input.action.PushToTalk",
            "settings.off",
            "voice.local",
            "voice.settings.section",
            "voice.settings.microphone",
            "voice.settings.volume",
        };

        private GameLanguage _applied;

        [SetUp]
        public void SetUp()
        {
            _applied = Localizer.Language;
        }

        [TearDown]
        public void TearDown()
        {
            Localizer.Apply(_applied);
        }

        // 配布物の説明はキットのリポジトリにだけあり、サンプルを入れたゲームのプロジェクトには無いので、そこでは読まない。
        private static string ReadReadme()
        {
            var path = Path.Combine(Path.GetDirectoryName(Application.dataPath) ?? string.Empty, ReadmePath);
            if (!File.Exists(path))
            {
                Assert.Ignore(ReadmePath + " is not in this project; it ships only with the kit repository");
            }

            return File.ReadAllText(path);
        }

        [TestCase("en")]
        [TestCase("ja")]
        public void TheReadmeQuotesTheMenuAndPanelTextsAsTheGameShowsThem(string languageCode)
        {
            var language = new GameLanguage(languageCode);
            var readme = ReadReadme();
            Localizer.Apply(language);

            foreach (var key in _quotedKeys)
            {
                var shown = Localizer.Get(key);
                Assert.That(shown, Is.Not.EqualTo(key), key + " has no " + language + " text");
                Assert.That(readme, Does.Contain("「" + shown + "」"), key + " (" + language + ") is not quoted as the game shows it");
            }
        }

        [Test]
        public void TheReadmeNamesTheDefaultPushToTalkKey()
        {
            var readme = ReadReadme();
            var actions = InputSystem.actions;
            Assert.That(actions, Is.Not.Null, "the project-wide input actions are not set");
            var pushToTalk = actions.FindAction(InputActionPaths.PushToTalk);
            Assert.That(pushToTalk, Is.Not.Null, InputActionPaths.PushToTalk + " is not in the input actions");

            var key = pushToTalk.GetBindingDisplayString(InputBinding.MaskByGroup(PlayerBindings.KeyboardMouseGroup), InputBinding.DisplayStringOptions.IgnoreBindingOverrides);

            Assert.That(key, Is.EqualTo("Caps Lock"));
            Assert.That(readme, Does.Contain("はじめは " + key + " キー"));
        }

        // macOS は使う理由の文が Info.plist に無いアプリがマイクを開くと止めるので、説明の「マイクの許可」が成り立つにはこれが要る。
        [Test]
        public void TheProjectStatesWhyItUsesTheMicrophone()
        {
            ReadReadme();
            var path = Path.Combine(Path.GetDirectoryName(Application.dataPath) ?? string.Empty, "ProjectSettings", "ProjectSettings.asset");
            var line = File.ReadAllLines(path).Single(l => l.StartsWith("  microphoneUsageDescription:", StringComparison.Ordinal));

            Assert.That(
                line.Substring("  microphoneUsageDescription:".Length).Trim(),
                Is.EqualTo("Voice chat sends your voice to your team while you hold Push to talk."));
        }
    }
}
