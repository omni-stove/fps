using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Shooter.Runtime;
using NUnit.Framework;

namespace Shooter.Core.Tests
{
    // 書式のテストは日本語の文を期待しているので、エディターや OS の言語で結果が変わらないよう、
    // この名前空間のテストの間は画面の文字をゲームと同じ日本語の文字列表から引かせる。
    [SetUpFixture]
    public sealed class JapaneseGameTextSetUp
    {
        public const string StringsFolder = "Assets/game/Samples~/Demo/Localization/Resources/Strings";

        private Func<StringTable> _previous;
        private Func<GameLanguageList> _previousLanguages;

        [OneTimeSetUp]
        public void UseJapanese()
        {
            _previous = GameText.Source;
            _previousLanguages = GameLanguages.Source;
            var languages = LoadLanguageList();
            GameLanguages.Use(() => languages);
            var table = LoadTable(GameLanguage.Japanese);
            GameText.Use(() => table);
        }

        [OneTimeTearDown]
        public void Restore()
        {
            GameText.Use(_previous);
            GameLanguages.Use(_previousLanguages);
        }

        public const string LayerFolder = "Assets/game/Samples~/Demo/Resources/Localization";

        public const string LanguageListFolder = LayerFolder + "/Languages";

        // ゲーム (Shooter.Runtime の Localizer) と同じ結果になるよう、同じくファイル名 (拡張子なし) の順に重ねて読む。
        public static GameLanguageList LoadLanguageList()
        {
            var folder = Path.Combine(FindRepositoryRoot(), LanguageListFolder);
            Assert.That(Directory.Exists(folder), Is.True, folder);
            var files = Directory.GetFiles(folder, "*.json")
                .OrderBy(Path.GetFileNameWithoutExtension, StringComparer.Ordinal)
                .Select(path => new KeyValuePair<string, string>(path, File.ReadAllText(path)))
                .ToList();

            var problems = new List<string>();
            Assert.That(GameLanguageList.TryRead(files, problems.Add, out var list, out var error), Is.True, error);
            Assert.That(problems, Is.Empty);
            return list;
        }

        public static StringTable LoadTable(GameLanguage language)
        {
            return StringTable.Merge(LoadLayers(language).Select(layer => layer.Value));
        }

        // ゲーム (Shooter.Runtime の Localizer) と同じ順に重ねる。
        public static IReadOnlyList<KeyValuePair<string, StringTable>> LoadLayers(GameLanguage language)
        {
            var root = FindRepositoryRoot();
            var code = GameLanguages.Code(language);
            var paths = new List<string>();
            var single = Path.Combine(root, StringsFolder, code + ".json");
            if (File.Exists(single))
            {
                paths.Add(single);
            }

            var folder = Path.Combine(root, LayerFolder, code);
            if (Directory.Exists(folder))
            {
                paths.AddRange(Directory.GetFiles(folder, "*.json")
                    .OrderBy(Path.GetFileNameWithoutExtension, StringComparer.Ordinal));
            }

            Assert.That(paths, Is.Not.Empty, $"no strings for {code} in {single} or {folder}");

            var layers = new List<KeyValuePair<string, StringTable>>();
            foreach (var path in paths)
            {
                Assert.That(StringTable.TryParse(File.ReadAllText(path), null, out var table, out var error), Is.True, $"{path}: {error}");
                layers.Add(new KeyValuePair<string, StringTable>(path, table));
            }

            return layers;
        }

        public static string FindRepositoryRoot()
        {
            foreach (var start in new[] { TestContext.CurrentContext.TestDirectory, Environment.CurrentDirectory })
            {
                if (string.IsNullOrEmpty(start))
                {
                    continue;
                }

                for (var directory = new DirectoryInfo(start); directory != null; directory = directory.Parent)
                {
                    if (Directory.Exists(Path.Combine(directory.FullName, StringsFolder)))
                    {
                        return directory.FullName;
                    }
                }
            }

            Assert.Fail($"'{StringsFolder}' was not found above {TestContext.CurrentContext.TestDirectory} or {Environment.CurrentDirectory}");
            return null;
        }
    }
}
