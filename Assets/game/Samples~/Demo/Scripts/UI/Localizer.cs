using System;
using System.Collections.Generic;
using System.Linq;
using Shooter.Core;
using UnityEngine;

namespace Shooter.Runtime
{
    public static class Localizer
    {
        public const string LogTag = "[Localization]";

        public const string ResourceFolder = "Strings";

        public const string LayerFolder = "Localization";

        // 一覧を 1 つのファイルでなくフォルダーにして中の TextAsset を重ねるのは、利用側が自分の Resources に別の名前のファイルを置いて言語を足せるようにするため。
        public const string LanguageListFolder = LayerFolder + "/Languages";

        public static readonly GameLanguage FallbackLanguage = GameLanguage.Japanese;

        public const string NativeNameKey = "settings.language.name";

        // ドメインの再読み込みを切っているので、Play の開始時に ResetOnPlay で戻す。
        private static readonly Dictionary<GameLanguage, StringTable> _tables = new Dictionary<GameLanguage, StringTable>();
        private static GameLanguageList _languageList;
        private static GameLanguage? _language;

        public static event Action<GameLanguage> Changed;

        public static GameLanguage Language => _language ??= new LanguagePreferenceStore().Load();

        public static string ResourcePathOf(GameLanguage language)
        {
            return ResourceFolder + "/" + GameLanguages.Code(language);
        }

        public static string LayerFolderOf(GameLanguage language)
        {
            return LayerFolder + "/" + GameLanguages.Code(language);
        }

        public static GameLanguage SystemDefault()
        {
            return FromSystemLanguage(Application.systemLanguage);
        }

        public static GameLanguage FromSystemLanguage(SystemLanguage system)
        {
            EnsureLanguageList();
            return GameLanguages.Current.ForSystemLanguage(system.ToString());
        }

        public static GameLanguageList LanguageList => _languageList ??= LoadLanguageList(ReadLanguageLists);

        public static IReadOnlyList<KeyValuePair<string, string>> ReadLanguageLists()
        {
            var assets = Resources.LoadAll<TextAsset>(LanguageListFolder);
            return assets
                .OrderBy(a => a.name, StringComparer.Ordinal)
                .Select(a => new KeyValuePair<string, string>($"Resources/{LanguageListFolder}/{a.name}", a.text))
                .ToList();
        }

        // Unity の SystemLanguage に無い OS の言語の名前はどの OS にも合わないので、警告を出す。
        public static GameLanguageList LoadLanguageList(Func<IReadOnlyList<KeyValuePair<string, string>>> read)
        {
            if (read == null)
            {
                throw new ArgumentNullException(nameof(read));
            }

            Action<string> warn = message => Debug.LogWarning($"{LogTag} {message}");
            if (!GameLanguageList.TryRead(read() ?? Array.Empty<KeyValuePair<string, string>>(), warn, out var list, out var error))
            {
                var builtIn = GameLanguageList.BuiltIn;
                Debug.LogError(
                    $"{LogTag} {error} in Resources/{LanguageListFolder}/; using {string.Join(", ", builtIn.Languages.Select(l => l.Code))} instead");
                return builtIn;
            }

            foreach (var entry in list.Entries)
            {
                foreach (var system in entry.SystemLanguages)
                {
                    if (!Enum.TryParse<SystemLanguage>(system, false, out var parsed) || parsed.ToString() != system)
                    {
                        warn($"{entry.Language.Code}: '{system}' is not a UnityEngine.SystemLanguage name, so no system matches it");
                    }
                }
            }

            return list;
        }

        // GameLanguages を引く前に呼ぶ。InitializeOnLoad の順は決まらないので、ほかのクラスの読み込み時に保存した設定を読んでも
        // Resources の一覧で言語コードを確かめられるようにする。テストが差し替えた引き先は外さない。
        internal static void EnsureLanguageList()
        {
            if (GameLanguages.Source == null)
            {
                GameLanguages.Use(CurrentLanguageList);
            }
        }

        public static string Get(string key)
        {
            return TableOf(Language).Get(key);
        }

        public static string Format(string key, params object[] args)
        {
            return TableOf(Language).Format(key, args);
        }

        public static string Resolve(LocalizedText text)
        {
            return TableOf(Language).Resolve(text);
        }

        public static bool TryGet(string key, out string text)
        {
            var table = TableOf(Language);
            if (table.Contains(key))
            {
                text = table.Get(key);
                return true;
            }

            text = null;
            return false;
        }

        public static StringTable TableOf(GameLanguage language)
        {
            if (!_tables.TryGetValue(language, out var table))
            {
                table = Build(language, ReadResources);
                _tables[language] = table;
            }

            return table;
        }

        // 前と同じ言語でも Changed を呼ぶのは、Resources から読み直した文を画面に出し直させるため。
        public static void SwitchTo(GameLanguage language, LanguagePreferenceStore store = null)
        {
            _ = GameLanguages.Code(language);
            Reload();
            _language = language;
            NotifyChanged(language);
            (store ?? new LanguagePreferenceStore()).Save(language);
        }

        public static void Apply(GameLanguage language)
        {
            _ = GameLanguages.Code(language);
            var previous = _language;
            _language = language;

            // 初めて決めたときは、まだ文字を引いた画面が無いので知らせない。
            if (previous.HasValue && previous.Value != language)
            {
                NotifyChanged(language);
            }
        }

        public static void Reload()
        {
            _tables.Clear();
            _languageList = null;
        }

        public static StringTable Build(GameLanguage language, Func<GameLanguage, IReadOnlyList<KeyValuePair<string, string>>> read)
        {
            if (read == null)
            {
                throw new ArgumentNullException(nameof(read));
            }

            var code = GameLanguages.Code(language);
            if (language != FallbackLanguage)
            {
                if (TryCompose(code, read(language), false, out var table, out var problem))
                {
                    return table;
                }

                Debug.LogWarning($"{LogTag} {code}: {problem}; using {GameLanguages.Code(FallbackLanguage)} instead");
            }

            var fallbackCode = GameLanguages.Code(FallbackLanguage);
            if (TryCompose(fallbackCode, read(FallbackLanguage), true, out var fallback, out var fallbackProblem))
            {
                return fallback;
            }

            Debug.LogError($"{LogTag} {fallbackCode}: {fallbackProblem}; showing the keys instead");
            return new StringTable(Array.Empty<KeyValuePair<string, string>>(), WarnFor(fallbackCode));
        }

        public static IReadOnlyList<KeyValuePair<string, string>> ReadResources(GameLanguage language)
        {
            var layers = new List<KeyValuePair<string, string>>();
            var path = ResourcePathOf(language);
            var single = Resources.Load<TextAsset>(path);
            if (single != null)
            {
                layers.Add(new KeyValuePair<string, string>($"Resources/{path}.json", single.text));
            }

            layers.AddRange(ReadLayers(language));
            return layers;
        }

        public static IReadOnlyList<KeyValuePair<string, string>> ReadLayers(GameLanguage language)
        {
            var folder = LayerFolderOf(language);
            var assets = Resources.LoadAll<TextAsset>(folder);
            var layers = assets
                .OrderBy(a => a.name, StringComparer.Ordinal)
                .Select(a => new KeyValuePair<string, string>($"Resources/{folder}/{a.name}", a.text))
                .ToList();
            layers.AddRange(ReadContentLayers(LocalizationContent.Current, language));
            return layers;
        }

        public static IReadOnlyList<KeyValuePair<string, string>> ReadContentLayers(LocalizationContent content, GameLanguage language)
        {
            var code = GameLanguages.Code(language);
            if (content == null)
            {
                return Array.Empty<KeyValuePair<string, string>>();
            }

            return content.TextsFor(code)
                .Select(t => new KeyValuePair<string, string>($"LocalizationContent '{content.name}' {code}/{t.name}", t.text))
                .ToList();
        }

        public static IReadOnlyList<GameLanguage> AvailableLanguages()
        {
            EnsureLanguageList();
            return AvailableLanguages(ReadLayers);
        }

        public static IReadOnlyList<GameLanguage> AvailableLanguages(Func<GameLanguage, IReadOnlyList<KeyValuePair<string, string>>> readLayers)
        {
            if (readLayers == null)
            {
                throw new ArgumentNullException(nameof(readLayers));
            }

            // Resources はフォルダーの一覧を返さないので、知っている言語コードのフォルダーごとに中身があるかを見る。
            var available = new List<GameLanguage>();
            foreach (var language in GameLanguages.All)
            {
                var layers = readLayers(language);
                if (layers != null && layers.Count > 0)
                {
                    available.Add(language);
                }
            }

            return available;
        }

        public static string NativeNameOf(GameLanguage language)
        {
            return NativeNameOf(language, ReadResources);
        }

        public static string NativeNameOf(GameLanguage language, Func<GameLanguage, IReadOnlyList<KeyValuePair<string, string>>> read)
        {
            if (read == null)
            {
                throw new ArgumentNullException(nameof(read));
            }

            var code = GameLanguages.Code(language);

            // TableOf は読めない言語を日本語の表で代えるので、それを引くと別の言語に「日本語」と出てしまう。
            string name = null;
            foreach (var layer in read(language) ?? Array.Empty<KeyValuePair<string, string>>())
            {
                if (layer.Value != null
                    && StringTable.TryParse(layer.Value, null, out var table, out _)
                    && table.Contains(NativeNameKey))
                {
                    name = table.Get(NativeNameKey);
                }
            }

            if (!string.IsNullOrWhiteSpace(name))
            {
                return name;
            }

            var fallback = GameLanguages.NativeName(language);
            Debug.LogWarning($"{LogTag} {code}: '{NativeNameKey}' was not found in the strings; showing '{fallback}' instead");
            return fallback;
        }

        private static void NotifyChanged(GameLanguage language)
        {
            Debug.Log($"{LogTag} language is now {GameLanguages.Code(language)}");
            var handlers = Changed;
            if (handlers == null)
            {
                return;
            }

            foreach (var handler in handlers.GetInvocationList())
            {
                try
                {
                    ((Action<GameLanguage>)handler)(language);
                }
                catch (Exception e)
                {
                    Debug.LogError($"{LogTag} a language change handler threw: {e.Message}");
                    Debug.LogException(e);
                }
            }
        }

        private static bool TryCompose(
            string code,
            IReadOnlyList<KeyValuePair<string, string>> layers,
            bool skipBroken,
            out StringTable table,
            out string problem)
        {
            table = null;
            problem = null;
            var parsed = new List<StringTable>();
            var anyBroken = false;
            foreach (var layer in layers ?? Array.Empty<KeyValuePair<string, string>>())
            {
                string error;
                if (layer.Value == null)
                {
                    error = "the text is missing";
                }
                else if (StringTable.TryParse(layer.Value, null, out var one, out error))
                {
                    parsed.Add(one);
                    continue;
                }

                problem = $"strings '{layer.Key}' could not be read: {error}";
                if (!skipBroken)
                {
                    return false;
                }

                anyBroken = true;
                Debug.LogError($"{LogTag} {code}: {problem}; skipping it");
            }

            if (parsed.Count == 0)
            {
                problem = anyBroken
                    ? "none of the strings could be read"
                    : $"no strings were found at Resources/{ResourceFolder}/{code}.json or in Resources/{LayerFolder}/{code}/";
                return false;
            }

            table = StringTable.Merge(parsed, WarnFor(code));
            return true;
        }

        private static Action<string> WarnFor(string code)
        {
            return message => Debug.LogWarning($"{LogTag} {code}: {message}");
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnPlay()
        {
            _tables.Clear();
            _languageList = null;
            _language = null;
            Changed = null;
            UseForCore();
        }

        // 表が見つからない・壊れているという知らせを、最初の画面を開く前の起動時に出す。
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void LoadOnPlay()
        {
            _ = TableOf(Language);
        }

        // 書式 (LoadoutFormat など) は Unity に依らないので、今の言語の表を GameText 越しに渡す。
        // Edit Mode (テストやエディター拡張) では Play の初期化が走らないので、エディターの読み込みでも渡す。
#if UNITY_EDITOR
        [UnityEditor.InitializeOnLoadMethod]
#endif
        private static void UseForCore()
        {
            GameText.Use(CurrentTable);
            GameLanguages.Use(CurrentLanguageList);
        }

        private static StringTable CurrentTable()
        {
            return TableOf(Language);
        }

        private static GameLanguageList CurrentLanguageList()
        {
            return LanguageList;
        }
    }
}
