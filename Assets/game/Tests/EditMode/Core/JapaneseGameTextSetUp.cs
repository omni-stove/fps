using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;

namespace Fps.Core.Tests
{
    /// <summary>
    /// この名前空間のテストの間、Core の画面の文字 (<see cref="GameText"/>) をゲームと同じ日本語の文字列表
    /// (<see cref="StringsFolder"/> の ja.json に <see cref="LayerFolder"/>/ja/ の表を重ねたもの) から引かせ、終わったら前の引き先に戻す。
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
        public const string LayerFolder = "Assets/game/Resources/Localization";

        /// <summary>
        /// language の文字列表をリポジトリから、ゲーム (Fps.Runtime の Localizer) と同じ順に重ねて読む。
        /// 見つからない・読めなければテストを失敗にする。
        /// </summary>
        public static StringTable LoadTable(GameLanguage language)
        {
            return StringTable.Merge(LoadLayers(language).Select(layer => layer.Value));
        }

        /// <summary>
        /// language の文字列表を、重ねる順にファイルのパスと組にして読む。先に <see cref="StringsFolder"/> の &lt;言語コード&gt;.json、
        /// 続けて <see cref="LayerFolder"/>/&lt;言語コード&gt;/ の *.json をファイル名 (拡張子なし) の順 (大文字小文字を区別する) に並べる。
        /// 見つからない・読めなければテストを失敗にする。
        /// </summary>
        public static IReadOnlyList<KeyValuePair<string, StringTable>> LoadLayers(GameLanguage language)
        {
            var root = FindRepositoryRoot();
            var code = GameLanguages.Code(language);
            var single = Path.Combine(root, StringsFolder, code + ".json");
            Assert.That(File.Exists(single), Is.True, single);

            var paths = new List<string> { single };
            var folder = Path.Combine(root, LayerFolder, code);
            if (Directory.Exists(folder))
            {
                paths.AddRange(Directory.GetFiles(folder, "*.json")
                    .OrderBy(Path.GetFileNameWithoutExtension, StringComparer.Ordinal));
            }

            var layers = new List<KeyValuePair<string, StringTable>>();
            foreach (var path in paths)
            {
                Assert.That(StringTable.TryParse(File.ReadAllText(path), null, out var table, out var error), Is.True, $"{path}: {error}");
                layers.Add(new KeyValuePair<string, StringTable>(path, table));
            }

            return layers;
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
