using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Shooter.Core;
using Shooter.Runtime;
using UnityEditor;
using UnityEngine;

namespace Shooter.Editor
{
    // 既存のアセットは GUID を保って中身だけ更新するので、参照しているシーンやプレハブは壊れない。
    public static class GameRulesJsonImporter
    {
        public const string MenuPath = "Shooter/Import Game Rules From JSON";
        public const string LogTag = "[GameRulesJsonImporter]";

        public const string SourceArgument = "-rulesJson";

        public const string OutputArgument = "-rulesOut";

        public const string JsonExtension = ".json";

        private const string _assetExtension = ".asset";
        private const string _writablePrefix = "Assets/";

        public static void Import()
        {
            var (source, output) = ResolveArguments(Environment.GetCommandLineArgs(), () => GameRulesAssets.Folder);
            ImportFrom(source, output);
        }

        [MenuItem(MenuPath)]
        private static void ImportFromMenu()
        {
            var source = EditorUtility.OpenFolderPanel("Folder of game rules JSON", Directory.GetCurrentDirectory(), string.Empty);
            if (string.IsNullOrEmpty(source))
            {
                return;
            }

            try
            {
                var imported = ImportFrom(source, GameRulesAssets.Folder);
                EditorUtility.DisplayDialog(
                    "Import Game Rules From JSON",
                    $"Imported {imported.Length.ToString(System.Globalization.CultureInfo.InvariantCulture)} game rules into '{GameRulesAssets.Folder}'.",
                    "OK");
            }
            catch (Exception e)
            {
                // メニューからの失敗はダイアログにも出し、Console を開いていなくても気づけるようにする
                Debug.LogException(e);
                EditorUtility.DisplayDialog("Import Game Rules From JSON failed", e.Message, "OK");
            }
        }

        // 出力先ルートの引数の検査を使わないときに走らせないよう、defaultOutput は出力先が省かれたときだけ呼ぶ。
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

        // すべて読んで検査してから書くのは、どれか 1 つでも失敗したときに 1 つも保存しないため。
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
                Debug.Log(GameRulesLog.Read(file, rules));
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
                    entries[i].Path, d => GameRulesAssets.Configure(d, rules));
            }

            Debug.Log($"{LogTag} saved {string.Join(", ", entries.ConvertAll(e => "'" + e.Path + "'"))}");
            return built;
        }

        public static IReadOnlyList<string> FindJsonFiles(string source)
        {
            if (string.IsNullOrWhiteSpace(source))
            {
                throw new ArgumentException($"{LogTag} the game rules JSON path must not be empty", nameof(source));
            }

            var trimmed = ShooterKitRoot.PhysicalPath(source.Trim().Replace('\\', '/').TrimEnd('/'));
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
