using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;

namespace Fps.Core.Tests
{
    /// <summary>
    /// ゲームが読む文字列表 (Assets/game/Localization/Resources/Strings/*.json) が読め、どの言語にも同じキーがあり、
    /// 同じキーが同じ番号の値を差し込むこと。片方の言語にだけ足したキーは、もう片方でキーそのものが画面に出てしまう。
    /// </summary>
    public sealed class LocalizationFilesTests
    {
        private const string _folder = "Assets/game/Localization/Resources/Strings";

        [Test]
        public void EveryLanguageHasAReadableTable()
        {
            foreach (var language in GameLanguages.All)
            {
                var table = Load(language);

                Assert.That(table.Count, Is.GreaterThan(0), GameLanguages.Code(language));
                foreach (var key in table.Keys)
                {
                    Assert.That(table.Get(key), Is.Not.Empty, $"{GameLanguages.Code(language)}: {key}");
                }
            }
        }

        [Test]
        public void EveryLanguageHasTheSameKeys()
        {
            var japanese = Load(GameLanguage.Japanese);
            foreach (var language in GameLanguages.All.Where(l => l != GameLanguage.Japanese))
            {
                var other = Load(language);
                var code = GameLanguages.Code(language);

                Assert.That(other.Keys.Except(japanese.Keys, StringComparer.Ordinal), Is.Empty, $"only in {code}.json");
                Assert.That(japanese.Keys.Except(other.Keys, StringComparer.Ordinal), Is.Empty, $"missing from {code}.json");
            }
        }

        [Test]
        public void EveryLanguageInsertsTheSameValues()
        {
            var japanese = Load(GameLanguage.Japanese);
            foreach (var language in GameLanguages.All.Where(l => l != GameLanguage.Japanese))
            {
                var other = Load(language);
                foreach (var key in japanese.Keys.Where(other.Contains))
                {
                    Assert.That(
                        StringTable.PlaceholdersOf(other.Get(key)),
                        Is.EqualTo(StringTable.PlaceholdersOf(japanese.Get(key))),
                        $"{GameLanguages.Code(language)}: {key}");
                }
            }
        }

        private static StringTable Load(GameLanguage language)
        {
            var path = Path.Combine(FindRepositoryRoot(), _folder, GameLanguages.Code(language) + ".json");
            Assert.That(File.Exists(path), Is.True, path);

            var warnings = new List<string>();
            Assert.That(StringTable.TryParse(File.ReadAllText(path), warnings.Add, out var table, out var error), Is.True, $"{path}: {error}");
            return table;
        }

        private static string FindRepositoryRoot()
        {
            for (var directory = new DirectoryInfo(TestContext.CurrentContext.TestDirectory); directory != null; directory = directory.Parent)
            {
                if (Directory.Exists(Path.Combine(directory.FullName, _folder)))
                {
                    return directory.FullName;
                }
            }

            Assert.Fail($"'{_folder}' was not found above {TestContext.CurrentContext.TestDirectory}");
            return null;
        }
    }
}
