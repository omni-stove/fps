using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Fps.Core;
using Fps.Runtime;
using UnityEditor;
using UnityEngine;

namespace Fps.Editor
{
    /// <summary>
    /// JSON (<see cref="GameRulesJson"/> の形) からモードのルールのアセット (<see cref="GameRulesDefinition"/>) を作るか上書きする。
    /// 1 ファイルが 1 アセットで、アセットの名前はファイル名から拡張子を除いたもの (<c>TeamDeathmatch.json</c> なら <c>TeamDeathmatch.asset</c>)。
    /// 既存のアセットは GUID を保って中身だけ更新するので、参照しているシーンやプレハブは壊れない。
    /// どれか 1 つでも読めない・値が不正・書けない場所なら、1 つも保存せずに、ファイル名と項目名を添えた例外を投げる
    /// (batchmode では Unity が終了コード 1 で終わる)。
    /// Editor を閉じた状態で次のように呼べる (<see cref="OutputArgument"/> を省くと <see cref="GameRulesAssetBuilder.Folder"/>):
    /// <code>unity -batchmode -quit -projectPath . -executeMethod Fps.Editor.GameRulesJsonImporter.Import -rulesJson Assets/game/Rules/Json -rulesOut Assets/Scratch/Rules</code>
    /// </summary>
    public static class GameRulesJsonImporter
    {
        public const string MenuPath = "Fps/Import Game Rules From JSON";
        public const string LogTag = "[GameRulesJsonImporter]";

        /// <summary>読む JSON のファイルか、JSON を並べたフォルダ (直下の <c>*.json</c> だけを読む)。プロジェクトのフォルダからの相対か絶対パス。</summary>
        public const string SourceArgument = "-rulesJson";

        /// <summary>アセットを書き出す Assets/ の下のフォルダ。無ければ作る。</summary>
        public const string OutputArgument = "-rulesOut";

        public const string JsonExtension = ".json";

        private const string _assetExtension = ".asset";
        private const string _writablePrefix = "Assets/";

        /// <summary>
        /// 同梱のサンプル (<see cref="GameRulesAssetBuilder"/> の 7 つのルールの今の値) を置くフォルダ。
        /// 取り込むと <see cref="GameRulesAssetBuilder.Build"/> と同じ値のアセットになる。
        /// </summary>
        public static string SamplesFolder => FpsKitRoot.BundledPath("Rules/Json");

        /// <summary>batchmode の入口。引数は <see cref="ResolveArguments"/> のとおり。</summary>
        public static void Import()
        {
            var (source, output) = ResolveArguments(Environment.GetCommandLineArgs(), () => GameRulesAssetBuilder.Folder);
            ImportFrom(source, output);
        }

        [MenuItem(MenuPath)]
        private static void ImportFromMenu()
        {
            var start = Directory.Exists(SamplesFolder) ? SamplesFolder : Directory.GetCurrentDirectory();
            var source = EditorUtility.OpenFolderPanel("Folder of game rules JSON", start, string.Empty);
            if (string.IsNullOrEmpty(source))
            {
                return;
            }

            ImportFrom(source, GameRulesAssetBuilder.Folder);
        }

        /// <summary>
        /// args から読み込み元 (<see cref="SourceArgument"/>、必須) と出力先 (<see cref="OutputArgument"/>、無ければ defaultOutput の値) を取り出す。
        /// defaultOutput は出力先が省かれたときだけ呼ぶ (出力先ルートの引数の検査を、使わないときに走らせないため)。
        /// </summary>
        /// <exception cref="ArgumentException">読み込み元が無い、どちらかの値が無い、または同じ引数が 2 度ある。</exception>
        public static (string Source, string Output) ResolveArguments(IReadOnlyList<string> args, Func<string> defaultOutput)
        {
            if (defaultOutput == null)
            {
                throw new ArgumentNullException(nameof(defaultOutput));
            }

            var source = FindArgument(args, SourceArgument, "a JSON file or a folder of JSON files");
            if (source == null)
            {
                throw new ArgumentException(
                    $"{LogTag} {SourceArgument} <file|dir> is required (e.g. {SourceArgument} {KitPaths.DefaultRoot}/Rules/Json)", nameof(args));
            }

            var output = FindArgument(args, OutputArgument, "a folder under " + _writablePrefix);
            return (source, output ?? defaultOutput());
        }

        /// <summary>
        /// source (JSON のファイルか、JSON を並べたフォルダ) のルールを outputFolder に <c>&lt;ファイル名&gt;.asset</c> として作るか上書きして保存し、
        /// 読んだ順 (フォルダならファイル名の順) に返す。すべて読んで検査してから書くので、失敗したときは 1 つも保存しない。
        /// </summary>
        /// <exception cref="GameRulesJsonException">JSON が読めない、知らない項目がある、または値が不正。メッセージにファイル名と項目名を含む。</exception>
        /// <exception cref="ArgumentException">source が無い・JSON が 1 つも無い、outputFolder が Assets/ の下でない、またはアセットの名前が重なる。</exception>
        /// <exception cref="InvalidOperationException">出力先に <see cref="GameRulesDefinition"/> でないアセットがある。</exception>
        public static GameRulesDefinition[] ImportFrom(string source, string outputFolder)
        {
            var files = FindJsonFiles(source);
            var folder = ValidateOutputFolder(outputFolder);

            var entries = new List<(string Path, GameRules Rules)>(files.Count);
            var names = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var file in files)
            {
                var name = Path.GetFileNameWithoutExtension(file);
                if (string.IsNullOrWhiteSpace(name))
                {
                    throw new ArgumentException($"{LogTag} '{file}' has no file name to use as the asset name", nameof(source));
                }

                // 大文字小文字だけが違うファイルは、大文字小文字を区別しない環境で同じアセットに上書きし合う。
                if (names.TryGetValue(name, out var other))
                {
                    throw new ArgumentException($"{LogTag} '{file}' and '{other}' would both be saved as '{name}{_assetExtension}'", nameof(source));
                }

                names.Add(name, file);

                var rules = GameRulesJson.Parse(File.ReadAllText(file, Encoding.UTF8), file);
                var assetPath = SceneBuildHelpers.ValidateAssetPath(folder + "/" + name + _assetExtension);
                entries.Add((assetPath, rules));
            }

            // CreateOrUpdateAsset も型の違うアセットを断るが、途中で断ると先のアセットが保存済みになるので書く前に確かめる。
            foreach (var entry in entries)
            {
                var existing = AssetDatabase.LoadMainAssetAtPath(entry.Path);
                if (existing != null && !(existing is GameRulesDefinition))
                {
                    throw new InvalidOperationException(
                        $"{LogTag} '{entry.Path}' is a {existing.GetType().Name}, not a {nameof(GameRulesDefinition)}; move it away first");
                }
            }

            var built = new GameRulesDefinition[entries.Count];
            for (var i = 0; i < entries.Count; i++)
            {
                var rules = entries[i].Rules;
                built[i] = SceneBuildHelpers.CreateOrUpdateAsset<GameRulesDefinition>(
                    entries[i].Path, d => GameRulesAssetBuilder.Configure(d, rules));
            }

            Debug.Log($"{LogTag} saved {string.Join(", ", entries.ConvertAll(e => "'" + e.Path + "'"))}");
            return built;
        }

        /// <summary>
        /// source がファイルならそれだけを、フォルダなら直下の <c>*.json</c> をファイル名の順 (序数比較) に返す。
        /// 区切りは / に揃える。
        /// </summary>
        /// <exception cref="ArgumentException">source が空・存在しない・.json でない、またはフォルダに .json が 1 つも無い。</exception>
        public static IReadOnlyList<string> FindJsonFiles(string source)
        {
            if (string.IsNullOrWhiteSpace(source))
            {
                throw new ArgumentException($"{LogTag} the game rules JSON path must not be empty", nameof(source));
            }

            var trimmed = source.Trim().Replace('\\', '/').TrimEnd('/');
            if (File.Exists(trimmed))
            {
                if (!trimmed.EndsWith(JsonExtension, StringComparison.OrdinalIgnoreCase))
                {
                    throw new ArgumentException($"{LogTag} '{source}' is not a {JsonExtension} file", nameof(source));
                }

                return new[] { trimmed };
            }

            if (!Directory.Exists(trimmed))
            {
                throw new ArgumentException($"{LogTag} '{source}' is neither a file nor a folder", nameof(source));
            }

            var files = new List<string>();
            foreach (var path in Directory.GetFiles(trimmed))
            {
                if (path.EndsWith(JsonExtension, StringComparison.OrdinalIgnoreCase))
                {
                    files.Add(path.Replace('\\', '/'));
                }
            }

            if (files.Count == 0)
            {
                throw new ArgumentException($"{LogTag} folder '{source}' has no {JsonExtension} files", nameof(source));
            }

            files.Sort(StringComparer.Ordinal);
            return files;
        }

        /// <summary>folder を Assets/ の下のフォルダとして正規化して返す。</summary>
        /// <exception cref="ArgumentException">folder が空、Assets/ の下でない、または空・<c>.</c>・<c>..</c> の区切りを含む。</exception>
        public static string ValidateOutputFolder(string folder)
        {
            var normalized = KitPaths.NormalizeRoot(folder);
            if (!normalized.StartsWith(_writablePrefix, StringComparison.Ordinal))
            {
                throw new ArgumentException(
                    $"{LogTag} output folder '{folder}' must be a folder under {_writablePrefix} (packages are read-only)", nameof(folder));
            }

            return normalized;
        }

        private static string FindArgument(IReadOnlyList<string> args, string name, string expected)
        {
            if (args == null)
            {
                return null;
            }

            string found = null;
            for (var i = 0; i < args.Count; i++)
            {
                if (!string.Equals(args[i], name, StringComparison.Ordinal))
                {
                    continue;
                }

                if (i + 1 >= args.Count || string.IsNullOrWhiteSpace(args[i + 1]) || args[i + 1].StartsWith("-", StringComparison.Ordinal))
                {
                    throw new ArgumentException($"{LogTag} {name} needs {expected}", nameof(args));
                }

                if (found != null)
                {
                    throw new ArgumentException($"{LogTag} {name} is given more than once", nameof(args));
                }

                found = args[i + 1];
                i++;
            }

            return found;
        }
    }
}
