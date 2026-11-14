using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;

namespace Fps.Core.Tests
{
    /// <summary>
    /// この名前空間のテストの間、Core の画面の文字 (<see cref="GameText"/>) をゲームと同じ日本語の文字列表
    /// (<see cref="StringsFolder"/> の ja.json に <see cref="LayerFolder"/> の ja/ を重ねたもの) から引かせ、終わったら前の引き先に戻す。
    /// 書式のテストは日本語の文を期待しているので、エディターや OS の言語で結果が変わらないようにする。
    /// Unity (Fps.Core.Tests) でも dotnet test (dotnet/Fps.Core.Tests) でも同じファイルを読む。
    /// </summary>
    [SetUpFixture]
    public sealed class JapaneseGameTextSetUp
    {
        /// <summary>リポジトリの根からの、言語ごとの文字列表のフォルダー。</summary>
        public const string StringsFolder = "Assets/game/Localization/Resources/Strings";

        /// <summary>リポジトリの根からの、言語コードのフォルダーごとに重ねる表を置くフォルダー。</summary>
        public const string LayerFolder = "Assets/game/Resources/Localization";

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

        /// <summary>
        /// language の文字列表をリポジトリから読む。ゲーム (Fps.Runtime の Localizer) と同じく、<see cref="StringsFolder"/> の表に
        /// <see cref="LayerFolder"/> の言語コードのフォルダーにある JSON をファイル名 (拡張子なし) の順に重ね、同じキーは後のものにする。
        /// 見つからない・読めなければテストを失敗にする。
        /// </summary>
        public static StringTable LoadTable(GameLanguage language)
        {
            var root = FindRepositoryRoot();
            var code = GameLanguages.Code(language);
            var path = Path.Combine(root, StringsFolder, code + ".json");
            Assert.That(File.Exists(path), Is.True, path);

            var paths = new List<string> { path };
            var layerDirectory = Path.Combine(root, LayerFolder, code);
            if (Directory.Exists(layerDirectory))
            {
                var layers = new List<string>(Directory.GetFiles(layerDirectory, "*.json"));
                layers.Sort((a, b) => string.CompareOrdinal(Path.GetFileNameWithoutExtension(a), Path.GetFileNameWithoutExtension(b)));
                paths.AddRange(layers);
            }

            var tables = new List<StringTable>();
            foreach (var file in paths)
            {
                Assert.That(StringTable.TryParse(File.ReadAllText(file), null, out var one, out var error), Is.True, $"{file}: {error}");
                tables.Add(one);
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
