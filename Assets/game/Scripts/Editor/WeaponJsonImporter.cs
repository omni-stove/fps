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
    /// <summary>
    /// JSON に書いた武器の値から <see cref="WeaponDefinition"/> を作るか更新する。Editor を開かずに利用側へ武器を入れるためのもの。
    /// <para>
    /// JSON は 1 ファイルに武器 1 つのオブジェクトで、読み書きは <see cref="WeaponSpecJson"/> に任せる。<c>id</c>・<c>category</c> のほかは
    /// 部品 (<c>trigger</c>・<c>ammo</c>・<c>round</c>・<c>impact</c>・<c>aim</c>・<c>ammoTypes</c>) ごとの入れ子のオブジェクトに書き、
    /// 項目名は部品の Spec のコンストラクタ引数と同じで、書かない項目は部品の既定になる。部品に分ける前の平らな形
    /// (<c>damage</c>・<c>range</c>・<c>explosionRadius</c>・<c>adsFieldOfView</c> など) もそのまま読める。
    /// </para>
    /// <para>
    /// <c>nameKey</c> は画面に出す名前を引く表のキー (例: <c>weapon.rifle</c>) で、文そのものは Resources/Localization/&lt;言語&gt;/ の JSON に書く。
    /// </para>
    /// <para>
    /// アセットは出力先のフォルダに JSON のファイル名 (拡張子を除く) で置き、既存のアセットは GUID を保って中身だけ更新する。
    /// 不正な値・欠けた項目・知らない項目・重複した Id などは、ファイル名と項目名を添えた <see cref="WeaponJsonException"/> にし、
    /// どのアセットも保存しない (batchmode では Unity が終了コード 1 で終わる)。
    /// </para>
    /// Editor を閉じた状態で次のように呼べる:
    /// <code>unity -batchmode -quit -projectPath . -executeMethod Shooter.Editor.WeaponJsonImporter.Import -weaponJson &lt;file|dir&gt; [-weaponOut &lt;dir&gt;] [-weaponCatalog &lt;asset&gt;]</code>
    /// </summary>
    public static class WeaponJsonImporter
    {
        public const string MenuPath = "Shooter/Import Weapons From JSON";
        public const string LogTag = "[WeaponJsonImporter]";

        public const string JsonArgument = "-weaponJson";
        public const string OutputArgument = "-weaponOut";
        public const string CatalogArgument = "-weaponCatalog";

        public const string JsonExtension = ".json";

        /// <summary><c>falloff</c> にこの文字列を書くと <see cref="DamageFalloff.None"/> (減衰しない) になる。</summary>
        public const string NoFalloff = WeaponSpecJson.NoFalloff;

        public const string IdField = WeaponSpecJson.IdItem;
        public const string NameKeyField = WeaponSpecJson.NameKeyItem;

        /// <summary><see cref="NameKeyField"/> の前の項目名。書かれていたら、知らない項目とは別の文で書き換えを促す。</summary>
        public const string LegacyDisplayNameField = "displayName";
        public const string RecoilRecoveryDelaySecondsField = WeaponSpecJson.RecoilRecoveryDelaySecondsItem;

        private const string WritablePrefix = "Assets/";

        /// <summary><c>-weaponOut</c> を省いたときの出力先。<see cref="WeaponAssets.Folder"/> (出力先ルートの <c>Weapons/</c>)。</summary>
        public static string DefaultOutputFolder => WeaponAssets.Folder;

        /// <summary>
        /// コマンドライン引数 (<see cref="ParseArguments"/>) の JSON を取り込む。失敗したら例外を投げる
        /// (batchmode では Unity が終了コード 1 で終わる)。
        /// </summary>
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

        /// <summary>
        /// args から <see cref="JsonArgument"/> (必須)、<see cref="OutputArgument"/>・<see cref="CatalogArgument"/> (省略可) を読む。
        /// 省いた項目は null。出力先とカタログのパスは Assets/ の下であることを確かめ、区切りを / に揃える。
        /// </summary>
        /// <exception cref="ArgumentException">必須の引数が無い、値が無い・重なっている、またはパスが不正。</exception>
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

        /// <summary>
        /// source (JSON ファイル、またはその直下の *.json を読むフォルダ) の武器を outputFolder に <see cref="WeaponDefinition"/> として
        /// 作るか更新し、ファイル名の順に返す。catalogPath が null でなければ、その <see cref="WeaponCatalogAsset"/> に Id で重複なく足す
        /// (同じ Id の別アセットが既にあれば、その位置を取り込んだアセットに差し替える)。
        /// すべての JSON とカタログを検査してから保存するので、どこかが不正なら何も保存しない。
        /// </summary>
        /// <exception cref="WeaponJsonException">JSON の文法・項目・値が不正、Id が重複している、または既存のアセットと食い違う。</exception>
        /// <exception cref="FileNotFoundException">source が無い、またはフォルダに JSON が無い。</exception>
        /// <exception cref="ArgumentException">outputFolder か catalogPath が Assets/ の下でない。</exception>
        /// <exception cref="InvalidOperationException">catalogPath のカタログが無い。</exception>
        /// <exception cref="WeaponDefinitionException">catalogPath のカタログの既存の武器が不正。</exception>
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

        /// <summary>
        /// source がファイルならそれだけを、フォルダならその直下の *.json を名前の順に返す。区切りは / に揃える。
        /// <c>Packages/</c> で始まる source は <see cref="ShooterKitRoot.PhysicalPath"/> でディスク上のパスに直してから読む。
        /// </summary>
        /// <exception cref="ArgumentException">source が空。</exception>
        /// <exception cref="FileNotFoundException">source が無い、またはフォルダに JSON が無い。</exception>
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

        /// <summary>
        /// json を武器 1 つとして読み、<see cref="WeaponSpec"/> にして返す。file は例外の文に出す読み元の名前。
        /// </summary>
        /// <exception cref="WeaponJsonException">文法・項目・値が不正。<see cref="WeaponJsonException.Field"/> に項目名を持つ。</exception>
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

        /// <summary>
        /// current の後ろに imported を Id で重複なく足した並びを返す。同じ Id の武器が既にあれば、同じアセットならそのまま、
        /// 別のアセットならその位置を imported の側に差し替える。current も imported も null 要素を含まず、値が正しいこと。
        /// </summary>
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

        /// <summary>folder を出力先として正規化して返す。Assets/ の下のフォルダであること。</summary>
        /// <exception cref="ArgumentException">folder が空、Assets/ の下でない、または空・<c>.</c>・<c>..</c> の区切りを含む。</exception>
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

    /// <summary><see cref="WeaponJsonImporter.ParseArguments"/> が読んだ引数。</summary>
    public sealed class WeaponJsonImportOptions
    {
        public WeaponJsonImportOptions(string source, string outputFolder, string catalogPath)
        {
            Source = source ?? throw new ArgumentNullException(nameof(source));
            OutputFolder = outputFolder;
            CatalogPath = catalogPath;
        }

        /// <summary><c>-weaponJson</c> の JSON ファイルかフォルダ。</summary>
        public string Source { get; }

        /// <summary><c>-weaponOut</c> の出力先。省いたら null (<see cref="WeaponJsonImporter.DefaultOutputFolder"/> を使う)。</summary>
        public string OutputFolder { get; }

        /// <summary><c>-weaponCatalog</c> のカタログ。省いたら null (カタログには足さない)。</summary>
        public string CatalogPath { get; }
    }

    /// <summary>武器の JSON が読めない・不正。どのファイルのどの項目かを持つ。</summary>
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

        /// <summary>読み元のファイル。</summary>
        public string File { get; }

        /// <summary>問題の項目名 (入れ子は <c>falloff.startDistance</c> の形)。ファイル全体の問題なら null。</summary>
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
