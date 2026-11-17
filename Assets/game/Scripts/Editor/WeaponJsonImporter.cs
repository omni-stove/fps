using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Shooter.Core;
using Shooter.Runtime;
using UnityEditor;
using UnityEngine;

namespace Shooter.Editor
{
    public static class WeaponJsonImporter
    {
        public const string MenuPath = "Shooter/Import Weapons From JSON";
        public const string LogTag = "[WeaponJsonImporter]";

        public const string JsonArgument = "-weaponJson";
        public const string OutputArgument = "-weaponOut";
        public const string CatalogArgument = "-weaponCatalog";

        public const string JsonExtension = ".json";

        public const string NoFalloff = WeaponSpecJson.NoFalloff;

        public const string IdField = WeaponSpecJson.IdItem;
        public const string NameKeyField = WeaponSpecJson.NameKeyItem;

        // NameKeyField の前の項目名。書かれていたら、知らない項目とは別の文で書き換えを促すために持つ。
        public const string LegacyDisplayNameField = "displayName";
        public const string RecoilRecoveryDelaySecondsField = WeaponSpecJson.RecoilRecoveryDelaySecondsItem;

        private const string WritablePrefix = "Assets/";

        public static string DefaultOutputFolder => WeaponAssets.Folder;

        public static void Import()
        {
            var options = ParseArguments(Environment.GetCommandLineArgs());
            ImportFrom(options.Source, options.OutputFolder ?? DefaultOutputFolder, options.CatalogPath);
        }

        [MenuItem(MenuPath)]
        private static void ImportFromMenu()
        {
            var source = EditorUtility.OpenFolderPanel("Import weapons from JSON", "Assets", string.Empty);
            if (string.IsNullOrEmpty(source))
            {
                return;
            }

            try
            {
                var outputFolder = DefaultOutputFolder;
                string catalogPath = null;
                var defaultCatalog = WeaponAssets.CatalogPath;
                if (AssetDatabase.LoadAssetAtPath<WeaponCatalogAsset>(defaultCatalog) != null
                    && EditorUtility.DisplayDialog(
                        "Import Weapons From JSON",
                        $"Also add the imported weapons to '{defaultCatalog}'?",
                        "Add",
                        "Skip"))
                {
                    catalogPath = defaultCatalog;
                }

                var imported = ImportFrom(source, outputFolder, catalogPath);
                EditorUtility.DisplayDialog(
                    "Import Weapons From JSON",
                    $"Imported {imported.Count.ToString(CultureInfo.InvariantCulture)} weapon(s) into '{outputFolder}'.",
                    "OK");
            }
            catch (Exception e)
            {
                // メニューからの失敗はダイアログにも出し、Console を開いていなくても気づけるようにする
                Debug.LogException(e);
                EditorUtility.DisplayDialog("Import Weapons From JSON failed", e.Message, "OK");
            }
        }

        public static WeaponJsonImportOptions ParseArguments(IReadOnlyList<string> args)
        {
            var source = FindArgument(args, JsonArgument, "a JSON file or a folder of JSON files");
            if (source == null)
            {
                throw new ArgumentException($"{LogTag} {JsonArgument} <file|dir> is required", nameof(args));
            }

            var output = FindArgument(args, OutputArgument, "a folder under Assets/");
            var catalog = FindArgument(args, CatalogArgument, "a WeaponCatalogAsset path such as " + WeaponAssets.CatalogPath);
            return new WeaponJsonImportOptions(
                source,
                output == null ? null : ValidateOutputFolder(output),
                catalog == null ? null : SceneBuildHelpers.ValidateAssetPath(catalog));
        }

        // すべての JSON とカタログを検査してから保存するのは、どこかが不正なときに何も保存しないため。
        public static IReadOnlyList<WeaponDefinition> ImportFrom(string source, string outputFolder, string catalogPath)
        {
            outputFolder = ValidateOutputFolder(outputFolder);
            if (catalogPath != null)
            {
                catalogPath = SceneBuildHelpers.ValidateAssetPath(catalogPath);
            }

            var files = FindJsonFiles(source);
            var entries = ReadEntries(files, outputFolder);
            RequireConsistentWithExistingAssets(entries, outputFolder);
            var catalog = catalogPath == null ? null : LoadCatalogForMerge(catalogPath);

            var definitions = new List<WeaponDefinition>(entries.Count);
            foreach (var entry in entries)
            {
                var spec = entry.Spec;
                definitions.Add(SceneBuildHelpers.CreateOrUpdateAsset<WeaponDefinition>(
                    entry.AssetPath, d => WeaponAssets.Configure(d, spec)));
                Debug.Log($"{LogTag} saved '{entry.AssetPath}' (id '{spec.Id}') from '{entry.File}'");
            }

            if (catalog != null)
            {
                var merged = MergeById(catalog.Weapons, definitions);
                SceneBuildHelpers.CreateOrUpdateAsset<WeaponCatalogAsset>(
                    catalogPath, c => WeaponAssets.Configure(c, merged));
                Debug.Log($"{LogTag} saved '{catalogPath}' with {merged.Count.ToString(CultureInfo.InvariantCulture)} weapon(s)");
            }

            return definitions;
        }

        public static IReadOnlyList<string> FindJsonFiles(string source)
        {
            if (string.IsNullOrWhiteSpace(source))
            {
                throw new ArgumentException($"{LogTag} the JSON source must not be empty", nameof(source));
            }

            var trimmed = ShooterKitRoot.PhysicalPath(source.Trim());
            if (File.Exists(trimmed))
            {
                return new[] { trimmed.Replace('\\', '/') };
            }

            if (!Directory.Exists(trimmed))
            {
                throw new FileNotFoundException($"{LogTag} '{trimmed}' is neither a JSON file nor a folder", trimmed);
            }

            var files = new List<string>();
            foreach (var file in Directory.GetFiles(trimmed, "*" + JsonExtension, SearchOption.TopDirectoryOnly))
            {
                // Windows のワイルドカードは短い名前にも当たるので、拡張子を改めて確かめる
                if (string.Equals(Path.GetExtension(file), JsonExtension, StringComparison.OrdinalIgnoreCase))
                {
                    files.Add(file.Replace('\\', '/'));
                }
            }

            if (files.Count == 0)
            {
                throw new FileNotFoundException($"{LogTag} folder '{trimmed}' has no {JsonExtension} files", trimmed);
            }

            files.Sort(StringComparer.Ordinal);
            return files;
        }

        public static WeaponSpec ParseSpec(string json, string file)
        {
            if (json == null)
            {
                throw new ArgumentNullException(nameof(json));
            }

            try
            {
                return WeaponSpecJson.Parse(json, file);
            }
            catch (ServerDataJsonException e) when (e.Item == LegacyDisplayNameField)
            {
                throw new WeaponJsonException(
                    file,
                    LegacyDisplayNameField,
                    $"is no longer read; write the key of the name (e.g. \"weapon.rifle\") in '{NameKeyField}' "
                    + "and the text itself in Resources/Localization/<language>/*.json",
                    e);
            }
            catch (ServerDataJsonException e)
            {
                throw new WeaponJsonException(file, e.Item, "is invalid: " + e.Message, e);
            }
        }

        public static List<WeaponDefinition> MergeById(IReadOnlyList<WeaponDefinition> current, IReadOnlyList<WeaponDefinition> imported)
        {
            if (current == null)
            {
                throw new ArgumentNullException(nameof(current));
            }

            if (imported == null)
            {
                throw new ArgumentNullException(nameof(imported));
            }

            var merged = new List<WeaponDefinition>(current);
            var ids = new List<string>(merged.Count);
            foreach (var definition in merged)
            {
                ids.Add(definition.ToSpec().Id);
            }

            foreach (var definition in imported)
            {
                var id = definition.ToSpec().Id;
                var index = ids.IndexOf(id);
                if (index < 0)
                {
                    merged.Add(definition);
                    ids.Add(id);
                }
                else if (merged[index] != definition)
                {
                    Debug.LogWarning(
                        $"{LogTag} the catalog entry #{index.ToString(CultureInfo.InvariantCulture)} ('{merged[index].name}') "
                        + $"has the id '{id}'; replacing it with '{AssetDatabase.GetAssetPath(definition)}'");
                    merged[index] = definition;
                }
            }

            return merged;
        }

        public static string ValidateOutputFolder(string folder)
        {
            string normalized;
            try
            {
                normalized = KitPaths.NormalizeRoot(folder);
            }
            catch (ArgumentException e)
            {
                throw new ArgumentException($"{LogTag} weapon output folder '{folder}' is invalid: {e.Message}", nameof(folder), e);
            }

            if (!normalized.StartsWith(WritablePrefix, StringComparison.Ordinal))
            {
                throw new ArgumentException(
                    $"{LogTag} weapon output folder '{folder}' must be a folder under {WritablePrefix}", nameof(folder));
            }

            return normalized;
        }

        private static List<Entry> ReadEntries(IReadOnlyList<string> files, string outputFolder)
        {
            var entries = new List<Entry>(files.Count);
            var byId = new Dictionary<string, string>(StringComparer.Ordinal);
            var byPath = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var file in files)
            {
                string text;
                try
                {
                    text = File.ReadAllText(file, Encoding.UTF8);
                }
                catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
                {
                    throw new WeaponJsonException(file, null, "could not be read: " + e.Message, e);
                }

                var spec = ParseSpec(text, file);
                if (byId.TryGetValue(spec.Id, out var other))
                {
                    throw new WeaponJsonException(file, IdField, $"'{spec.Id}' is already used by '{other}'");
                }

                byId.Add(spec.Id, file);

                var name = Path.GetFileNameWithoutExtension(file);
                string assetPath;
                try
                {
                    assetPath = SceneBuildHelpers.ValidateAssetPath(outputFolder + "/" + name + ".asset");
                }
                catch (ArgumentException e)
                {
                    throw new WeaponJsonException(file, null, "cannot be turned into an asset name: " + e.Message, e);
                }

                // 大文字小文字だけが違う名前は、大文字小文字を区別しないファイルシステムで同じアセットになる
                if (byPath.TryGetValue(assetPath, out var samePath))
                {
                    throw new WeaponJsonException(file, null, $"would be saved to '{assetPath}', the same asset as '{samePath}'");
                }

                byPath.Add(assetPath, file);
                entries.Add(new Entry(file, spec, assetPath));
            }

            return entries;
        }

        // Id は作った後に変えない約束なので、既存のアセットと Id が食い違う取り込みは上書きせずに止める
        private static void RequireConsistentWithExistingAssets(IReadOnlyList<Entry> entries, string outputFolder)
        {
            foreach (var entry in entries)
            {
                var existing = AssetDatabase.LoadMainAssetAtPath(entry.AssetPath);
                if (existing == null)
                {
                    continue;
                }

                if (!(existing is WeaponDefinition definition))
                {
                    throw new WeaponJsonException(
                        entry.File, null, $"would overwrite '{entry.AssetPath}', which is a {existing.GetType().Name}, not a {nameof(WeaponDefinition)}");
                }

                var existingId = definition.Id?.Trim();
                if (!string.IsNullOrEmpty(existingId) && !string.Equals(existingId, entry.Spec.Id, StringComparison.Ordinal))
                {
                    throw new WeaponJsonException(
                        entry.File,
                        IdField,
                        $"is '{entry.Spec.Id}' but '{entry.AssetPath}' already holds the weapon '{existingId}'; weapon ids must not change once created");
                }
            }

            if (!AssetDatabase.IsValidFolder(outputFolder))
            {
                return;
            }

            foreach (var guid in AssetDatabase.FindAssets("t:" + nameof(WeaponDefinition), new[] { outputFolder }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var definition = AssetDatabase.LoadAssetAtPath<WeaponDefinition>(path);
                var existingId = definition == null ? null : definition.Id?.Trim();
                if (string.IsNullOrEmpty(existingId))
                {
                    continue;
                }

                foreach (var entry in entries)
                {
                    if (string.Equals(entry.Spec.Id, existingId, StringComparison.Ordinal)
                        && !string.Equals(entry.AssetPath, path, StringComparison.OrdinalIgnoreCase))
                    {
                        throw new WeaponJsonException(
                            entry.File,
                            IdField,
                            $"'{existingId}' is already used by '{path}'; name the JSON file '{Path.GetFileNameWithoutExtension(path)}{JsonExtension}' to update that asset");
                    }
                }
            }
        }

        private static WeaponCatalogAsset LoadCatalogForMerge(string catalogPath)
        {
            var catalog = AssetDatabase.LoadAssetAtPath<WeaponCatalogAsset>(catalogPath);
            if (catalog == null)
            {
                throw new InvalidOperationException(
                    $"{LogTag} weapon catalog '{catalogPath}' was not found; create it first");
            }

            // 武器を保存した後でカタログが壊れていると分かっても戻せないので、保存の前に既存の中身を確かめる
            var weapons = catalog.Weapons;
            for (var i = 0; i < weapons.Count; i++)
            {
                var definition = weapons[i];
                if (definition == null)
                {
                    throw new WeaponDefinitionException(
                        $"weapon catalog '{catalogPath}' weapon #{i.ToString(CultureInfo.InvariantCulture)} is empty; fix the catalog before importing");
                }

                try
                {
                    definition.ToSpec();
                }
                catch (WeaponDefinitionException e)
                {
                    throw new WeaponDefinitionException(
                        $"weapon catalog '{catalogPath}' weapon #{i.ToString(CultureInfo.InvariantCulture)}: {e.Message}", e);
                }
            }

            // Id の重なりは ToCatalog でしか拾えず、これは保存の後の Configure で初めて呼ばれるので、ここで先に通す
            try
            {
                catalog.ToCatalog();
            }
            catch (WeaponDefinitionException e)
            {
                throw new WeaponDefinitionException($"weapon catalog '{catalogPath}': {e.Message}", e);
            }

            return catalog;
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

        private sealed class Entry
        {
            public Entry(string file, WeaponSpec spec, string assetPath)
            {
                File = file;
                Spec = spec;
                AssetPath = assetPath;
            }

            public string File { get; }
            public WeaponSpec Spec { get; }
            public string AssetPath { get; }
        }
    }

    public sealed class WeaponJsonImportOptions
    {
        public WeaponJsonImportOptions(string source, string outputFolder, string catalogPath)
        {
            Source = source ?? throw new ArgumentNullException(nameof(source));
            OutputFolder = outputFolder;
            CatalogPath = catalogPath;
        }

        public string Source { get; }

        public string OutputFolder { get; }

        public string CatalogPath { get; }
    }

    public sealed class WeaponJsonException : Exception
    {
        public WeaponJsonException(string file, string field, string detail)
            : this(file, field, detail, null)
        {
        }

        public WeaponJsonException(string file, string field, string detail, Exception innerException)
            : base(Format(file, field, detail), innerException)
        {
            File = file;
            Field = field;
        }

        public string File { get; }

        public string Field { get; }

        private static string Format(string file, string field, string detail)
        {
            var source = string.IsNullOrEmpty(file) ? "(unnamed JSON)" : file;
            return field == null
                ? $"{WeaponJsonImporter.LogTag} weapon JSON '{source}' {detail}"
                : $"{WeaponJsonImporter.LogTag} weapon JSON '{source}' field \"{field}\" {detail}";
        }
    }
}
