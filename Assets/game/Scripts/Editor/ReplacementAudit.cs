using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using Fps.Core;
using Fps.Runtime;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Fps.Editor
{
    /// <summary>
    /// ゲームが差し替えるもののうち、抜けていると動かない・出ないものをまとめて洗い出し、一覧をログに出す。見るのは次のもの:
    /// <list type="bullet">
    /// <item>Assets/ の下の武器 (<see cref="WeaponDefinition"/>) の Id に、<see cref="PresentationCatalog"/> の音とエフェクトがあるか。
    /// カタログ自体が無い・<see cref="PresentationCatalog.Validate"/> に通らないときも出す。</item>
    /// <item>文言の表 (<see cref="Localizer.ReadResources"/>) が言語ごとに読めるか、どの言語にも同じキーがあるか、武器の名前のキーがどの言語にもあるか。</item>
    /// <item><see cref="RequiredReferenceAttribute"/> を付けた参照が、ビルドに入るシーン・Assets/ の下のプレハブ・アセットで空でないか。</item>
    /// </list>
    /// 漏れがあれば終了コード <see cref="FindingsExitCode"/>、点検そのものが失敗したら <see cref="ErrorExitCode"/>、無ければ
    /// <see cref="SuccessExitCode"/> で Editor を閉じる (batchmode のときだけ)。Editor を閉じた状態で次のように呼べる:
    /// <code>unity -batchmode -quit -projectPath . -executeMethod Fps.Editor.ReplacementAudit.Run -logFile -</code>
    /// </summary>
    public static class ReplacementAudit
    {
        public const string LogTag = "[ReplacementAudit]";
        public const string MenuPath = "Fps/Audit Replacements";

        public const int SuccessExitCode = 0;
        public const int FindingsExitCode = 1;
        public const int ErrorExitCode = 2;

        /// <summary>武器・プレハブ・アセットを探すフォルダ。パッケージの中の同梱物は、ゲームが差し替える前の仮のものなので見ない。</summary>
        public const string SearchFolder = "Assets";

        private static readonly Dictionary<Type, FieldInfo[]> _requiredFields = new Dictionary<Type, FieldInfo[]>();

        /// <summary>
        /// 点検して一覧をログに出す。batchmode なら終了コード付きで Editor を閉じる (<c>-executeMethod</c> から呼ぶ)。
        /// </summary>
        [MenuItem(MenuPath)]
        public static void Run()
        {
            int exitCode;
            try
            {
                var findings = Collect();
                exitCode = ExitCodeFor(findings);
                var summary = Format(findings);
                if (exitCode == SuccessExitCode)
                {
                    Debug.Log(summary);
                }
                else
                {
                    Debug.LogError(summary);
                }
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                Debug.LogError($"{LogTag} the audit could not finish: {e.Message} (exit code {ErrorExitCode})");
                exitCode = ErrorExitCode;
            }

            if (Application.isBatchMode)
            {
                EditorApplication.Exit(exitCode);
            }
        }

        /// <summary>プロジェクトを点検し、見つけた漏れを返す。何もログに出さず、Editor も閉じない。</summary>
        public static IReadOnlyList<ReplacementAuditFinding> Collect()
        {
            var findings = new List<ReplacementAuditFinding>();

            var catalog = PresentationCatalog.Load();
            CheckPresentationCatalog(catalog, findings);

            var strings = AuditedStrings.Read(GameLanguages.All, Localizer.ReadResources, findings);
            CheckStringKeys(strings, findings);

            CheckWeapons(LoadAssets<WeaponDefinition>(), catalog, strings, findings);

            CheckBuildScenes(findings);
            CheckPrefabs(findings);
            CheckScriptableObjects(findings);
            return findings;
        }

        /// <summary>findings が空なら <see cref="SuccessExitCode"/>、そうでなければ <see cref="FindingsExitCode"/>。</summary>
        /// <exception cref="ArgumentNullException">findings が null。</exception>
        public static int ExitCodeFor(IReadOnlyCollection<ReplacementAuditFinding> findings)
        {
            if (findings == null)
            {
                throw new ArgumentNullException(nameof(findings));
            }

            return findings.Count == 0 ? SuccessExitCode : FindingsExitCode;
        }

        /// <summary>ログに出す一覧。1 行目に件数と終了コード、続けて 1 件 1 行。</summary>
        /// <exception cref="ArgumentNullException">findings が null。</exception>
        public static string Format(IReadOnlyCollection<ReplacementAuditFinding> findings)
        {
            if (findings == null)
            {
                throw new ArgumentNullException(nameof(findings));
            }

            var exitCode = ExitCodeFor(findings);
            if (exitCode == SuccessExitCode)
            {
                return $"{LogTag} passed: nothing is missing (exit code {exitCode})";
            }

            var text = new StringBuilder();
            text.Append(LogTag).Append(" failed: ").Append(findings.Count).Append(" missing replacement(s) (exit code ")
                .Append(exitCode).Append(')');
            foreach (var finding in findings)
            {
                text.Append("\n  ").Append(finding);
            }

            return text.ToString();
        }

        /// <summary>catalog が無い、または <see cref="PresentationCatalog.Validate"/> に通らなければ findings に足す。</summary>
        /// <exception cref="ArgumentNullException">findings が null。</exception>
        public static void CheckPresentationCatalog(PresentationCatalog catalog, ICollection<ReplacementAuditFinding> findings)
        {
            if (findings == null)
            {
                throw new ArgumentNullException(nameof(findings));
            }

            if (catalog == null)
            {
                findings.Add(new ReplacementAuditFinding(
                    ReplacementAuditArea.Presentation,
                    $"Resources/{PresentationCatalog.ResourcePath}",
                    "the presentation catalog was not found; no weapon has sounds or effects"));
                return;
            }

            try
            {
                catalog.Validate();
            }
            catch (PresentationCatalogException e)
            {
                findings.Add(new ReplacementAuditFinding(ReplacementAuditArea.Presentation, WhereIs(catalog), e.Message));
            }
        }

        /// <summary>
        /// strings の読めた言語どうしでキーを比べ、ほかの言語にあって自分に無いキーを 1 つずつ findings に足す。
        /// キーは序数の順、言語は <see cref="AuditedStrings.ReadableLanguages"/> の順に並べる。
        /// </summary>
        /// <exception cref="ArgumentNullException">どちらかが null。</exception>
        public static void CheckStringKeys(AuditedStrings strings, ICollection<ReplacementAuditFinding> findings)
        {
            if (strings == null)
            {
                throw new ArgumentNullException(nameof(strings));
            }

            if (findings == null)
            {
                throw new ArgumentNullException(nameof(findings));
            }

            var languages = strings.ReadableLanguages;
            var allKeys = new SortedSet<string>(StringComparer.Ordinal);
            foreach (var language in languages)
            {
                allKeys.UnionWith(strings.KeysOf(language));
            }

            foreach (var key in allKeys)
            {
                foreach (var language in languages)
                {
                    if (strings.TryGetTable(language, key, out _))
                    {
                        continue;
                    }

                    var holder = languages.First(other => strings.TryGetTable(other, key, out _));
                    strings.TryGetTable(holder, key, out var table);
                    findings.Add(new ReplacementAuditFinding(
                        ReplacementAuditArea.Strings,
                        GameLanguages.Code(language),
                        $"key '{key}' is missing ({GameLanguages.Code(holder)} has it in '{table}')"));
                }
            }
        }

        /// <summary>
        /// 武器ごとに、Id が空でないか、catalog に Id の音とエフェクトがあるか、名前のキーが strings の読めた言語のすべてにあるかを見て、
        /// 足りなければ findings に足す。catalog が null なら音とエフェクトは見ない (<see cref="CheckPresentationCatalog"/> が出す)。null の武器は飛ばす。
        /// </summary>
        /// <exception cref="ArgumentNullException">weapons・strings・findings のどれかが null。</exception>
        public static void CheckWeapons(
            IEnumerable<WeaponDefinition> weapons,
            PresentationCatalog catalog,
            AuditedStrings strings,
            ICollection<ReplacementAuditFinding> findings)
        {
            if (weapons == null)
            {
                throw new ArgumentNullException(nameof(weapons));
            }

            if (strings == null)
            {
                throw new ArgumentNullException(nameof(strings));
            }

            if (findings == null)
            {
                throw new ArgumentNullException(nameof(findings));
            }

            foreach (var weapon in weapons)
            {
                if (weapon == null)
                {
                    continue;
                }

                var where = WhereIs(weapon);
                var id = weapon.Id.Trim();
                if (id.Length == 0)
                {
                    findings.Add(new ReplacementAuditFinding(ReplacementAuditArea.Presentation, where, "the weapon has no id"));
                }
                else if (catalog != null && !catalog.TryGetWeapon(id, out _))
                {
                    findings.Add(new ReplacementAuditFinding(
                        ReplacementAuditArea.Presentation,
                        where,
                        $"weapon id '{id}' has no sounds or effects in presentation catalog '{catalog.name}'"));
                }

                var nameKey = weapon.NameKey.Trim();
                if (nameKey.Length == 0)
                {
                    findings.Add(new ReplacementAuditFinding(ReplacementAuditArea.Strings, where, "the weapon has no name key"));
                    continue;
                }

                foreach (var language in strings.ReadableLanguages)
                {
                    if (!strings.TryGetTable(language, nameKey, out _))
                    {
                        findings.Add(new ReplacementAuditFinding(
                            ReplacementAuditArea.Strings,
                            where,
                            $"name key '{nameKey}' is missing from the {GameLanguages.Code(language)} strings"));
                    }
                }
            }
        }

        /// <summary>
        /// target の <see cref="RequiredReferenceAttribute"/> を付けた参照のうち、空のもの (配列なら空の並びと空の要素) を findings に足す。
        /// 参照先のアセットが消えているものも空として出す。
        /// </summary>
        /// <param name="target">見るコンポーネントかアセット。</param>
        /// <param name="where">findings に書く場所 (シーンのパスと階層など)。</param>
        /// <param name="findings">見つけた漏れを足す先。</param>
        /// <exception cref="ArgumentNullException">target か findings が null。</exception>
        /// <exception cref="ArgumentException">where が空。</exception>
        public static void CheckRequiredReferences(Object target, string where, ICollection<ReplacementAuditFinding> findings)
        {
            if (target == null)
            {
                throw new ArgumentNullException(nameof(target));
            }

            if (string.IsNullOrWhiteSpace(where))
            {
                throw new ArgumentException("where must not be empty", nameof(where));
            }

            if (findings == null)
            {
                throw new ArgumentNullException(nameof(findings));
            }

            var fields = RequiredFieldsOf(target.GetType());
            if (fields.Length == 0)
            {
                return;
            }

            using (var serialized = new SerializedObject(target))
            {
                foreach (var field in fields)
                {
                    var property = serialized.FindProperty(field.Name);
                    if (property == null)
                    {
                        continue;
                    }

                    var label = $"{target.GetType().Name} '{property.displayName}'";
                    if (property.propertyType == SerializedPropertyType.ObjectReference)
                    {
                        AddIfEmpty(property, label, where, findings);
                    }
                    else if (property.isArray && property.propertyType == SerializedPropertyType.Generic)
                    {
                        if (property.arraySize == 0)
                        {
                            findings.Add(new ReplacementAuditFinding(ReplacementAuditArea.Reference, where, $"{label} has no entries"));
                            continue;
                        }

                        for (var i = 0; i < property.arraySize; i++)
                        {
                            var element = property.GetArrayElementAtIndex(i);
                            if (element.propertyType == SerializedPropertyType.ObjectReference)
                            {
                                AddIfEmpty(element, $"{label} #{i}", where, findings);
                            }
                        }
                    }
                }
            }
        }

        /// <summary>type (基底の型も含む) のフィールドのうち <see cref="RequiredReferenceAttribute"/> を付けたもの。</summary>
        public static FieldInfo[] RequiredFieldsOf(Type type)
        {
            if (type == null)
            {
                throw new ArgumentNullException(nameof(type));
            }

            if (_requiredFields.TryGetValue(type, out var cached))
            {
                return cached;
            }

            var fields = new List<FieldInfo>();
            for (var current = type; current != null && current != typeof(Object); current = current.BaseType)
            {
                foreach (var field in current.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                {
                    if (field.IsDefined(typeof(RequiredReferenceAttribute), false))
                    {
                        fields.Add(field);
                    }
                }
            }

            var result = fields.ToArray();
            _requiredFields[type] = result;
            return result;
        }

        /// <summary>root と子孫のコンポーネント (無効のものも) の必須の参照を <see cref="CheckRequiredReferences"/> で見る。</summary>
        /// <param name="root">見る GameObject。</param>
        /// <param name="where">場所の頭 (シーンかプレハブのパス)。後ろに階層を付ける。</param>
        /// <param name="findings">見つけた漏れを足す先。</param>
        public static void CheckGameObject(GameObject root, string where, ICollection<ReplacementAuditFinding> findings)
        {
            if (root == null)
            {
                throw new ArgumentNullException(nameof(root));
            }

            foreach (var component in root.GetComponentsInChildren<Component>(true))
            {
                // スクリプトが見つからないコンポーネントは null で返る。
                if (component == null || RequiredFieldsOf(component.GetType()).Length == 0)
                {
                    continue;
                }

                CheckRequiredReferences(component, $"{where} {HierarchyPath(component.transform)}", findings);
            }
        }

        private static void CheckBuildScenes(ICollection<ReplacementAuditFinding> findings)
        {
            foreach (var entry in EditorBuildSettings.scenes)
            {
                if (!entry.enabled)
                {
                    continue;
                }

                var path = entry.path;
                if (AssetDatabase.LoadAssetAtPath<SceneAsset>(path) == null)
                {
                    findings.Add(new ReplacementAuditFinding(
                        ReplacementAuditArea.Reference, string.IsNullOrEmpty(path) ? "(build settings)" : path, "the scene in the build list was not found"));
                    continue;
                }

                var scene = SceneManager.GetSceneByPath(path);
                var wasLoaded = scene.IsValid() && scene.isLoaded;
                if (!wasLoaded)
                {
                    scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
                }

                try
                {
                    foreach (var root in scene.GetRootGameObjects())
                    {
                        CheckGameObject(root, path, findings);
                    }
                }
                finally
                {
                    if (!wasLoaded)
                    {
                        EditorSceneManager.CloseScene(scene, true);
                    }
                }
            }
        }

        private static void CheckPrefabs(ICollection<ReplacementAuditFinding> findings)
        {
            foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { SearchFolder }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab != null)
                {
                    CheckGameObject(prefab, path, findings);
                }
            }
        }

        private static void CheckScriptableObjects(ICollection<ReplacementAuditFinding> findings)
        {
            var types = TypeCache.GetFieldsWithAttribute<RequiredReferenceAttribute>()
                .Select(field => field.DeclaringType)
                .Where(type => type != null && typeof(ScriptableObject).IsAssignableFrom(type))
                .Distinct();

            var seen = new HashSet<Object>();
            foreach (var type in types)
            {
                foreach (var guid in AssetDatabase.FindAssets("t:" + type.Name, new[] { SearchFolder }))
                {
                    var path = AssetDatabase.GUIDToAssetPath(guid);
                    foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(path))
                    {
                        if (asset is ScriptableObject && type.IsInstanceOfType(asset) && seen.Add(asset))
                        {
                            CheckRequiredReferences(asset, path, findings);
                        }
                    }
                }
            }
        }

        private static List<T> LoadAssets<T>()
            where T : Object
        {
            var assets = new List<T>();
            foreach (var guid in AssetDatabase.FindAssets("t:" + typeof(T).Name, new[] { SearchFolder }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(path))
                {
                    if (asset is T typed)
                    {
                        assets.Add(typed);
                    }
                }
            }

            return assets;
        }

        private static void AddIfEmpty(
            SerializedProperty property,
            string label,
            string where,
            ICollection<ReplacementAuditFinding> findings)
        {
            // 消えたアセットを指す参照も objectReferenceValue は null になるので、空と同じく出す。
            if (property.objectReferenceValue == null)
            {
                findings.Add(new ReplacementAuditFinding(
                    ReplacementAuditArea.Reference, where, $"{label} is empty or points at an asset that no longer exists"));
            }
        }

        private static string WhereIs(Object asset)
        {
            var path = AssetDatabase.GetAssetPath(asset);
            if (!string.IsNullOrEmpty(path))
            {
                return path;
            }

            return string.IsNullOrEmpty(asset.name) ? $"(unnamed {asset.GetType().Name})" : asset.name;
        }

        private static string HierarchyPath(Transform transform)
        {
            var names = new List<string>();
            for (var current = transform; current != null; current = current.parent)
            {
                names.Add(current.name);
            }

            names.Reverse();
            return string.Join("/", names);
        }
    }
}
