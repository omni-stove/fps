using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Fps.Core;
using Fps.Runtime;
using UnityEditor;
using UnityEngine;

namespace Fps.Editor
{
    /// <summary>
    /// JSON に書いた武器の値から <see cref="WeaponDefinition"/> を作るか更新する。Editor を開かずに利用側へ武器を入れるためのもの。
    /// <para>
    /// JSON は 1 ファイルに武器 1 つのオブジェクトで、項目名は <see cref="WeaponSpec"/> のコンストラクタ引数と同じ
    /// (既定値の無い引数は必須、既定値のある引数は省略可)。<c>category</c>・<c>fireMode</c> は列挙子の名前の文字列、
    /// <c>falloff</c> は <see cref="DamageFalloff"/> のコンストラクタ引数を項目に持つオブジェクトか、減衰しないなら文字列 <c>"None"</c>、
    /// <c>adsFieldOfView</c> と <c>displayName</c> は null も書ける。値の検証は <see cref="WeaponSpec"/> に任せる。
    /// </para>
    /// <para>
    /// アセットは出力先のフォルダに JSON のファイル名 (拡張子を除く) で置き、既存のアセットは GUID を保って中身だけ更新する。
    /// 不正な値・欠けた項目・知らない項目・重複した Id などは、ファイル名と項目名を添えた <see cref="WeaponJsonException"/> にし、
    /// どのアセットも保存しない (batchmode では Unity が終了コード 1 で終わる)。
    /// </para>
    /// Editor を閉じた状態で次のように呼べる:
    /// <code>unity -batchmode -quit -projectPath . -executeMethod Fps.Editor.WeaponJsonImporter.Import -weaponJson &lt;file|dir&gt; [-weaponOut &lt;dir&gt;] [-weaponCatalog &lt;asset&gt;]</code>
    /// </summary>
    public static class WeaponJsonImporter
    {
        public const string MenuPath = "Fps/Import Weapons From JSON";
        public const string LogTag = "[WeaponJsonImporter]";

        public const string JsonArgument = "-weaponJson";
        public const string OutputArgument = "-weaponOut";
        public const string CatalogArgument = "-weaponCatalog";

        public const string JsonExtension = ".json";

        /// <summary><c>falloff</c> にこの文字列を書くと <see cref="DamageFalloff.None"/> (減衰しない) になる。</summary>
        public const string NoFalloff = "None";

        public const string IdField = "id";
        public const string DisplayNameField = "displayName";
        public const string CategoryField = "category";
        public const string FireModeField = "fireMode";
        public const string MagazineSizeField = "magazineSize";
        public const string ReserveAmmoField = "reserveAmmo";
        public const string FireIntervalField = "fireInterval";
        public const string ReloadSecondsField = "reloadSeconds";
        public const string DamageField = "damage";
        public const string RangeField = "range";
        public const string FalloffField = "falloff";
        public const string ExplosionRadiusField = "explosionRadius";
        public const string AdsFieldOfViewField = "adsFieldOfView";
        public const string BlindFireSpreadDegreesField = "blindFireSpreadDegrees";
        public const string ActiveReloadWindowStartField = "activeReloadWindowStart";
        public const string ActiveReloadWindowLengthField = "activeReloadWindowLength";
        public const string ActiveReloadFailSecondsField = "activeReloadFailSeconds";
        public const string RecoilPitchDegreesField = "recoilPitchDegrees";
        public const string RecoilYawDegreesField = "recoilYawDegrees";
        public const string RecoilRecoveryPerSecondField = "recoilRecoveryPerSecond";
        public const string HipSpreadDegreesField = "hipSpreadDegrees";
        public const string AdsSpreadDegreesField = "adsSpreadDegrees";
        public const string MoveSpreadDegreesField = "moveSpreadDegrees";

        public const string FalloffStartDistanceField = "startDistance";
        public const string FalloffEndDistanceField = "endDistance";
        public const string FalloffMinMultiplierField = "minMultiplier";

        private const string WritablePrefix = "Assets/";

        /// <summary>武器の JSON の項目名。<see cref="WeaponSpec"/> のコンストラクタ引数と同じ順。</summary>
        public static readonly IReadOnlyList<string> Fields = new[]
        {
            IdField,
            DisplayNameField,
            CategoryField,
            FireModeField,
            MagazineSizeField,
            ReserveAmmoField,
            FireIntervalField,
            ReloadSecondsField,
            DamageField,
            RangeField,
            FalloffField,
            ExplosionRadiusField,
            AdsFieldOfViewField,
            BlindFireSpreadDegreesField,
            ActiveReloadWindowStartField,
            ActiveReloadWindowLengthField,
            ActiveReloadFailSecondsField,
            RecoilPitchDegreesField,
            RecoilYawDegreesField,
            RecoilRecoveryPerSecondField,
            HipSpreadDegreesField,
            AdsSpreadDegreesField,
            MoveSpreadDegreesField,
        };

        private static readonly IReadOnlyList<string> _falloffFields = new[]
        {
            FalloffStartDistanceField,
            FalloffEndDistanceField,
            FalloffMinMultiplierField,
        };

        /// <summary><c>-weaponOut</c> を省いたときの出力先。<see cref="WeaponAssetBuilder.Folder"/> (出力先ルートの <c>Weapons/</c>)。</summary>
        public static string DefaultOutputFolder => WeaponAssetBuilder.Folder;

        /// <summary>同梱のサンプル JSON (rifle・handgun・launcher) のフォルダ。</summary>
        public static string SampleFolder => FpsKitRoot.BundledPath("Weapons/Json");

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
            var start = Directory.Exists(SampleFolder) ? SampleFolder : "Assets";
            var source = EditorUtility.OpenFolderPanel("Import weapons from JSON", start, string.Empty);
            if (string.IsNullOrEmpty(source))
            {
                return;
            }

            try
            {
                var outputFolder = DefaultOutputFolder;
                string catalogPath = null;
                var defaultCatalog = WeaponAssetBuilder.CatalogPath;
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
            var catalog = FindArgument(args, CatalogArgument, "a WeaponCatalogAsset path such as " + WeaponAssetBuilder.CatalogPath);
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
                    entry.AssetPath, d => WeaponAssetBuilder.Configure(d, spec)));
                Debug.Log($"{LogTag} saved '{entry.AssetPath}' (id '{spec.Id}') from '{entry.File}'");
            }

            if (catalog != null)
            {
                var merged = MergeById(catalog.Weapons, definitions);
                SceneBuildHelpers.CreateOrUpdateAsset<WeaponCatalogAsset>(
                    catalogPath, c => WeaponAssetBuilder.Configure(c, merged));
                Debug.Log($"{LogTag} saved '{catalogPath}' with {merged.Count.ToString(CultureInfo.InvariantCulture)} weapon(s)");
            }

            return definitions;
        }

        /// <summary>
        /// source がファイルならそれだけを、フォルダならその直下の *.json を名前の順に返す。区切りは / に揃える。
        /// </summary>
        /// <exception cref="ArgumentException">source が空。</exception>
        /// <exception cref="FileNotFoundException">source が無い、またはフォルダに JSON が無い。</exception>
        public static IReadOnlyList<string> FindJsonFiles(string source)
        {
            if (string.IsNullOrWhiteSpace(source))
            {
                throw new ArgumentException($"{LogTag} the JSON source must not be empty", nameof(source));
            }

            var trimmed = source.Trim();
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

            object root;
            try
            {
                root = WeaponJsonSyntax.Parse(json);
            }
            catch (FormatException e)
            {
                throw new WeaponJsonException(file, null, "is not valid JSON: " + e.Message, e);
            }

            if (!(root is Dictionary<string, object> fields))
            {
                throw new WeaponJsonException(file, null, "must be a JSON object whose fields are the WeaponSpec constructor arguments");
            }

            RequireKnownFields(file, null, fields, Fields);
            var reader = new FieldReader(file, fields);

            var id = reader.RequiredString(IdField, allowNull: false);
            var displayName = reader.RequiredString(DisplayNameField, allowNull: true);
            var category = reader.RequiredEnum<WeaponCategory>(CategoryField);
            var fireMode = reader.RequiredEnum<FireMode>(FireModeField);
            var magazineSize = reader.RequiredInt(MagazineSizeField);
            var reserveAmmo = reader.RequiredInt(ReserveAmmoField);
            var fireInterval = reader.RequiredFloat(FireIntervalField);
            var reloadSeconds = reader.RequiredFloat(ReloadSecondsField);
            var damage = reader.RequiredInt(DamageField);
            var range = reader.RequiredFloat(RangeField);
            var falloff = ReadFalloff(file, reader);
            var explosionRadius = reader.OptionalFloat(ExplosionRadiusField, 0f);
            var adsFieldOfView = reader.OptionalNullableFloat(AdsFieldOfViewField);
            var blindFireSpreadDegrees = reader.OptionalFloat(BlindFireSpreadDegreesField, 0f);
            var activeReloadWindowStart = reader.OptionalFloat(ActiveReloadWindowStartField, WeaponSpec.DefaultActiveReloadWindowStart);
            var activeReloadWindowLength = reader.OptionalFloat(ActiveReloadWindowLengthField, WeaponSpec.DefaultActiveReloadWindowLength);
            var activeReloadFailSeconds = reader.OptionalFloat(ActiveReloadFailSecondsField, WeaponSpec.DefaultActiveReloadFailSeconds);
            var recoilPitchDegrees = reader.OptionalFloat(RecoilPitchDegreesField, 0f);
            var recoilYawDegrees = reader.OptionalFloat(RecoilYawDegreesField, 0f);
            var recoilRecoveryPerSecond = reader.OptionalFloat(RecoilRecoveryPerSecondField, 0f);
            var hipSpreadDegrees = reader.OptionalFloat(HipSpreadDegreesField, 0f);
            var adsSpreadDegrees = reader.OptionalFloat(AdsSpreadDegreesField, 0f);
            var moveSpreadDegrees = reader.OptionalFloat(MoveSpreadDegreesField, 0f);

            try
            {
                return new WeaponSpec(
                    id,
                    displayName,
                    category,
                    fireMode,
                    magazineSize,
                    reserveAmmo,
                    fireInterval,
                    reloadSeconds,
                    damage,
                    range,
                    falloff,
                    explosionRadius,
                    adsFieldOfView,
                    blindFireSpreadDegrees,
                    activeReloadWindowStart,
                    activeReloadWindowLength,
                    activeReloadFailSeconds,
                    recoilPitchDegrees,
                    recoilYawDegrees,
                    recoilRecoveryPerSecond,
                    hipSpreadDegrees,
                    adsSpreadDegrees,
                    moveSpreadDegrees);
            }
            catch (ArgumentException e)
            {
                // WeaponSpec は引数名を ParamName に入れるので、それがそのまま JSON の項目名になる
                throw new WeaponJsonException(file, e.ParamName, "is invalid: " + e.Message, e);
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
                    $"{LogTag} weapon catalog '{catalogPath}' was not found; create it first (e.g. {WeaponAssetBuilder.MenuPath})");
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

            return catalog;
        }

        private static DamageFalloff ReadFalloff(string file, FieldReader reader)
        {
            var value = reader.Required(FalloffField);
            if (value is string text)
            {
                if (string.Equals(text, NoFalloff, StringComparison.Ordinal))
                {
                    return DamageFalloff.None;
                }

                throw new WeaponJsonException(
                    file, FalloffField, $"must be an object with {string.Join(", ", _falloffFields)}, or \"{NoFalloff}\" (got \"{text}\")");
            }

            if (!(value is Dictionary<string, object> fields))
            {
                throw new WeaponJsonException(
                    file, FalloffField, $"must be an object with {string.Join(", ", _falloffFields)}, or \"{NoFalloff}\"");
            }

            RequireKnownFields(file, FalloffField, fields, _falloffFields);
            var falloffReader = new FieldReader(file, fields, FalloffField + ".");
            var start = falloffReader.RequiredFloat(FalloffStartDistanceField);
            var end = falloffReader.RequiredFloat(FalloffEndDistanceField);
            var minMultiplier = falloffReader.RequiredFloat(FalloffMinMultiplierField);
            try
            {
                return new DamageFalloff(start, end, minMultiplier);
            }
            catch (ArgumentException e)
            {
                throw new WeaponJsonException(
                    file, e.ParamName == null ? FalloffField : FalloffField + "." + e.ParamName, "is invalid: " + e.Message, e);
            }
        }

        private static void RequireKnownFields(
            string file, string parent, Dictionary<string, object> fields, IReadOnlyList<string> known)
        {
            foreach (var key in fields.Keys)
            {
                if (!Contains(known, key))
                {
                    throw new WeaponJsonException(
                        file,
                        parent == null ? key : parent + "." + key,
                        $"is not a known field; expected {string.Join(", ", known)}");
                }
            }
        }

        private static bool Contains(IReadOnlyList<string> values, string value)
        {
            for (var i = 0; i < values.Count; i++)
            {
                if (string.Equals(values[i], value, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
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

        /// <summary>JSON オブジェクトの項目を型ごとに読む。型が違う・欠けているときは項目名を添えて例外にする。</summary>
        private sealed class FieldReader
        {
            private readonly string _file;
            private readonly Dictionary<string, object> _fields;
            private readonly string _prefix;

            public FieldReader(string file, Dictionary<string, object> fields, string prefix = "")
            {
                _file = file;
                _fields = fields;
                _prefix = prefix;
            }

            public object Required(string name)
            {
                if (!_fields.TryGetValue(name, out var value))
                {
                    throw Fail(name, "is missing");
                }

                return value;
            }

            public string RequiredString(string name, bool allowNull)
            {
                var value = Required(name);
                if (value == null && allowNull)
                {
                    return null;
                }

                if (!(value is string text))
                {
                    throw Fail(name, allowNull ? "must be a string or null" : "must be a string");
                }

                return text;
            }

            public T RequiredEnum<T>(string name)
                where T : struct, Enum
            {
                var value = Required(name);
                var names = string.Join(", ", Enum.GetNames(typeof(T)));
                // Enum.TryParse は "3" のような数字や大文字小文字の違いも通すので、名前の完全一致だけを受け付ける
                if (!(value is string text) || !Enum.IsDefined(typeof(T), text))
                {
                    throw Fail(name, $"must be one of {names} (got {Describe(value)})");
                }

                return (T)Enum.Parse(typeof(T), text);
            }

            public int RequiredInt(string name)
            {
                var value = Required(name);
                if (!(value is WeaponJsonNumber number) || !number.IsInteger)
                {
                    throw Fail(name, $"must be an integer (got {Describe(value)})");
                }

                if (!int.TryParse(number.Text, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var result))
                {
                    throw Fail(name, $"is out of the integer range (got {number.Text})");
                }

                return result;
            }

            public float RequiredFloat(string name)
            {
                return ToFloat(name, Required(name));
            }

            public float OptionalFloat(string name, float fallback)
            {
                return _fields.TryGetValue(name, out var value) ? ToFloat(name, value) : fallback;
            }

            public float? OptionalNullableFloat(string name)
            {
                if (!_fields.TryGetValue(name, out var value) || value == null)
                {
                    return null;
                }

                return ToFloat(name, value);
            }

            private float ToFloat(string name, object value)
            {
                // double を経ずに float として読み、0.1 が JSON の 0.1 と C# の 0.1f で同じ値になるようにする
                if (!(value is WeaponJsonNumber number)
                    || !float.TryParse(number.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var result))
                {
                    throw Fail(name, $"must be a number (got {Describe(value)})");
                }

                if (float.IsInfinity(result))
                {
                    throw Fail(name, $"is out of the float range (got {number.Text})");
                }

                return result;
            }

            private WeaponJsonException Fail(string name, string message)
            {
                return new WeaponJsonException(_file, _prefix + name, message);
            }

            private static string Describe(object value)
            {
                switch (value)
                {
                    case null:
                        return "null";
                    case string text:
                        return "\"" + text + "\"";
                    case WeaponJsonNumber number:
                        return number.Text;
                    case bool flag:
                        return flag ? "true" : "false";
                    case Dictionary<string, object> _:
                        return "an object";
                    case List<object> _:
                        return "an array";
                    default:
                        return value.ToString();
                }
            }
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
