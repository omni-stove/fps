using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;

namespace Fps.Core.Tests
{
    /// <summary>
    /// この名前空間のテストの間、Core の画面の文字 (<see cref="GameText"/>) をゲームと同じ日本語の文字列表
    /// (<see cref="StringsFolder"/> の ja.json に <see cref="LayersFolder"/> の ja/ を重ねたもの) から引かせ、終わったら前の引き先に戻す。
    /// 書式のテストは日本語の文を期待しているので、エディターや OS の言語で結果が変わらないようにする。
    /// Unity (Fps.Core.Tests) でも dotnet test (dotnet/Fps.Core.Tests) でも同じファイルを読む。
    /// </summary>
    [SetUpFixture]
    public sealed class JapaneseGameTextSetUp
    {
        /// <summary>リポジトリの根からの、言語ごとの文字列表のフォルダー。</summary>
        public const string StringsFolder = "Assets/game/Localization/Resources/Strings";

        private Func<StringTable> _previous;

        [OneTimeSetUp]
        public void UseJapanese()
        {
            _previous = GameText.Source;
            var table = LoadTable(GameLanguage.Japanese);
            GameText.Use(() => table);
        }

        [OneTimeTearDown]
        public void Restore()
        {
            GameText.Use(_previous);
        }

        /// <summary>リポジトリの根からの、言語コードのフォルダーごとに重ねる表を置くフォルダー。</summary>
        public const string LayersFolder = "Assets/game/Resources/Localization";

        /// <summary>
        /// language の文字列表をリポジトリから読む。ゲーム (Fps.Runtime の Localizer) と同じく、<see cref="StringsFolder"/> の表に
        /// <see cref="LayersFolder"/>/&lt;言語コード&gt;/ の *.json をファイル名の順に重ねる。<see cref="StringsFolder"/> の表が見つからない・
        /// どれか 1 つでも読めなければテストを失敗にする。
        /// </summary>
        public static StringTable LoadTable(GameLanguage language)
        {
            var root = FindRepositoryRoot();
            var code = GameLanguages.Code(language);
            var path = Path.Combine(root, StringsFolder, code + ".json");
            Assert.That(File.Exists(path), Is.True, path);

            var paths = new List<string> { path };
            var layers = Path.Combine(root, LayersFolder, code);
            if (Directory.Exists(layers))
            {
                var found = new List<string>(Directory.GetFiles(layers, "*.json"));
                found.Sort((a, b) => string.CompareOrdinal(Path.GetFileNameWithoutExtension(a), Path.GetFileNameWithoutExtension(b)));
                paths.AddRange(found);
            }

            var tables = new List<StringTable>(paths.Count);
            foreach (var layer in paths)
            {
                Assert.That(StringTable.TryParse(File.ReadAllText(layer), null, out var table, out var error), Is.True, $"{layer}: {error}");
                tables.Add(table);
            }

            var warnings = new List<string>();
            return StringTable.Merge(tables, warnings.Add);
        }

        /// <summary>
        /// <see cref="StringsFolder"/> を含むリポジトリの根。dotnet test はビルドの出力先から、Unity はプロジェクトの根 (今のフォルダー) から上へ探す。
        /// </summary>
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
