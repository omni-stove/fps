using System.IO;
using Shooter.Runtime;
using NUnit.Framework;
using UnityEngine;

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

        [TestCase(GameLanguage.English)]
        [TestCase(GameLanguage.Japanese)]
        public void TheReadmeQuotesTheMenuAndPanelTextsAsTheGameShowsThem(GameLanguage language)
        {
            var readme = ReadReadme();
            Localizer.Apply(language);

            foreach (var key in _quotedKeys)
            {
                var shown = Localizer.Get(key);
                Assert.That(shown, Is.Not.EqualTo(key), key + " has no " + language + " text");
                Assert.That(readme, Does.Contain("「" + shown + "」"), key + " (" + language + ") is not quoted as the game shows it");
            }
        }
    }
}
