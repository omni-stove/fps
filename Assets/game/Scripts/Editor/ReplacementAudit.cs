using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using Shooter.Runtime;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Shooter.Editor
{
    // 名前のキーを文にする表は game 側が持つので、武器はキーがあるかだけを見て、表にキーがあるかは game 側の点検に任せる。
    public static class ReplacementAudit
    {
        public const string LogTag = "[ReplacementAudit]";
        public const string MenuPath = "Shooter/Audit Replacements";

        public const int SuccessExitCode = 0;
        public const int FindingsExitCode = 1;
        public const int ErrorExitCode = 2;

        // パッケージの中の同梱物は、ゲームが差し替える前の仮のものなので見ない。
        public const string SearchFolder = "Assets";

        private static readonly Dictionary<Type, FieldInfo[]> _requiredFields = new Dictionary<Type, FieldInfo[]>();

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

        public static IReadOnlyList<ReplacementAuditFinding> Collect()
        {
            var findings = new List<ReplacementAuditFinding>();

            var catalog = PresentationCatalog.Load();
            CheckPresentationCatalog(catalog, findings);

            CheckWeapons(LoadAssets<WeaponDefinition>(), catalog, findings);

            CheckBuildScenes(findings);
            CheckPrefabs(findings);
            CheckScriptableObjects(findings);

            foreach (var check in ExtensionPoints.CreateAll<IReplacementAuditCheck>(_ => 0))
            {
                check.Check(findings);
            }

            return findings;
        }

        public static int ExitCodeFor(IReadOnlyCollection<ReplacementAuditFinding> findings)
        {
            if (findings == null)
            {
                throw new ArgumentNullException(nameof(findings));
            }

            return findings.Count == 0 ? SuccessExitCode : FindingsExitCode;
        }

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

        // catalog が無いことは CheckPresentationCatalog が出すので、null なら音とエフェクトは見ない。
        public static void CheckWeapons(
            IEnumerable<WeaponDefinition> weapons,
            PresentationCatalog catalog,
            ICollection<ReplacementAuditFinding> findings)
        {
            if (weapons == null)
            {
                throw new ArgumentNullException(nameof(weapons));
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

                if (weapon.NameKey.Trim().Length == 0)
                {
                    findings.Add(new ReplacementAuditFinding(ReplacementAuditArea.Strings, where, "the weapon has no name key"));
                }
            }
        }

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
                var wasInHierarchy = scene.IsValid();
                var wasLoaded = wasInHierarchy && scene.isLoaded;
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
                        // Hierarchy に残っていた未読み込みのシーンは、取り除かず元の「読み込まれていない」状態へ戻す。
                        EditorSceneManager.CloseScene(scene, !wasInHierarchy);
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

        public static List<T> LoadAssets<T>()
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

        public static string WhereIs(Object asset)
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
