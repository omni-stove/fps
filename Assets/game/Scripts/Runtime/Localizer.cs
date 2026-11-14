using System;
using System.Collections.Generic;
using System.Linq;
using Fps.Core;
using UnityEngine;

namespace Fps.Runtime
{
    /// <summary>
    /// 画面に出す文字を、今効いている設定の言語 (<see cref="AppliedControlSettings.Current"/> の <see cref="ControlSettings.Language"/>) の
    /// <see cref="StringTable"/> から引く。表は Resources の Strings/&lt;言語コード&gt;.json と、Localization/&lt;言語コード&gt;/ にあるすべての
    /// TextAsset をファイル名の順に重ねて作り (<see cref="ReadResources"/>)、同じキーは後のものが勝つ。
    /// 利用側のプロジェクトの Resources/Localization/&lt;言語コード&gt;/ も同じく読むので、キーの上書きや文の追加ができる。
    /// 表は初めて使うときと Play の開始時に読み、Play の間は持ち続ける。言語が変わると (<see cref="AppliedControlSettings.Apply"/>・<see cref="SwitchTo"/>)
    /// <see cref="Changed"/> で知らせる。
    /// 言語の表が見つからない・壊れているときは警告を出して <see cref="FallbackLanguage"/> の表で続け、それも読めなければエラーを出し、
    /// 読めた分だけ (無ければキーそのものを出す空の表) で続ける。無いキーは <c>[Localization]</c> 付きの警告で知らせる。
    /// Core の書式 (<see cref="HudFormat"/> など) が引く <see cref="GameText"/> にも、Play の開始時とエディターの読み込み時に同じ表を渡す。
    /// </summary>
    public static class Localizer
    {
        public const string LogTag = "[Localization]";

        /// <summary>Resources の中で言語ごとの表 (&lt;言語コード&gt;.json) を置くフォルダー。</summary>
        public const string ResourceFolder = "Strings";

        /// <summary>Resources の中で、言語コードのフォルダーごとに重ねる表を置くフォルダー。</summary>
        public const string LayerFolder = "Localization";

        /// <summary>言語の表が読めないときに代わりに使う言語。</summary>
        public const GameLanguage FallbackLanguage = GameLanguage.Japanese;

        /// <summary>言語の選択に出す、その言語自身での名前を置くキー (各言語の settings.json)。</summary>
        public const string NativeNameKey = "settings.language.name";

        // ドメインの再読み込みを切っているので、Play の開始時に ResetOnPlay で戻す。
        private static readonly Dictionary<GameLanguage, StringTable> _tables = new Dictionary<GameLanguage, StringTable>();

        /// <summary>
        /// 言語が変わったとき。購読した側は受け取った言語で文字を引き直す。1 つの購読先が投げても、ほかの購読先には届ける。
        /// </summary>
        public static event Action<GameLanguage> Changed;

        /// <summary>今の言語。</summary>
        public static GameLanguage Language => AppliedControlSettings.Current.Language;

        /// <summary>language の表を読む Resources のパス (拡張子なし)。</summary>
        public static string ResourcePathOf(GameLanguage language)
        {
            return ResourceFolder + "/" + GameLanguages.Code(language);
        }

        /// <summary>language の重ねる表を置く Resources のフォルダー。</summary>
        public static string LayerFolderOf(GameLanguage language)
        {
            return LayerFolder + "/" + GameLanguages.Code(language);
        }

        /// <summary>
        /// OS の言語 (<see cref="Application.systemLanguage"/>) から、初めて起動したときの言語を選ぶ。
        /// </summary>
        public static GameLanguage SystemDefault()
        {
            return FromSystemLanguage(Application.systemLanguage);
        }

        /// <summary>
        /// OS の言語 system に合う言語。日本語なら日本語、それ以外 (分からないときも含む) は、日本語を読めない人の方が多いので英語。
        /// </summary>
        public static GameLanguage FromSystemLanguage(SystemLanguage system)
        {
            return system == SystemLanguage.Japanese ? GameLanguage.Japanese : GameLanguage.English;
        }

        /// <summary>今の言語で key の文字列。無ければキーそのもの (警告を出す)。</summary>
        /// <exception cref="ArgumentNullException">key が null。</exception>
        public static string Get(string key)
        {
            return TableOf(Language).Get(key);
        }

        /// <summary>今の言語で key の文字列の <c>{0}</c> などに args を差し込む (<see cref="StringTable.Format"/>)。</summary>
        /// <exception cref="ArgumentNullException">key か args が null。</exception>
        public static string Format(string key, params object[] args)
        {
            return TableOf(Language).Format(key, args);
        }

        /// <summary>今の言語で <see cref="LocalizedText"/> を文にする (<see cref="StringTable.Resolve"/>)。</summary>
        public static string Resolve(LocalizedText text)
        {
            return TableOf(Language).Resolve(text);
        }

        /// <summary>今の言語の表に key があれば、その文字列を text に入れて true。無ければ警告を出さずに false。</summary>
        /// <exception cref="ArgumentNullException">key が null。</exception>
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

        /// <summary>language の表。まだ読んでいなければ Resources から読む。</summary>
        /// <exception cref="ArgumentOutOfRangeException">language が定義済みの値でない。</exception>
        public static StringTable TableOf(GameLanguage language)
        {
            if (!_tables.TryGetValue(language, out var table))
            {
                table = Build(language, ReadResources);
                _tables[language] = table;
            }

            return table;
        }

        /// <summary>
        /// language を今の言語にし、<paramref name="store"/> に保存する。表を Resources から読み直し、
        /// 前と同じ言語でも <see cref="Changed"/> を呼ぶ (読み直した文を画面に出し直させるため)。
        /// </summary>
        /// <param name="language">切り替える言語。</param>
        /// <param name="store">保存先。null なら既定のキーの <see cref="LanguagePreferenceStore"/>。</param>
        /// <exception cref="ArgumentOutOfRangeException">language が定義済みの値でない。</exception>
        public static void SwitchTo(GameLanguage language, LanguagePreferenceStore store = null)
        {
            _ = GameLanguages.Code(language);
            Reload();
            var current = AppliedControlSettings.Current;
            if (current.Language == language)
            {
                NotifyChanged(language);
            }
            else
            {
                AppliedControlSettings.Apply(current.WithLanguage(language));
            }

            (store ?? new LanguagePreferenceStore()).Save(language);
        }

        /// <summary>読んだ表をすべて捨て、次に引くときに Resources から読み直させる。</summary>
        public static void Reload()
        {
            _tables.Clear();
        }

        /// <summary>
        /// read が返す表 (前から順に重ねる。名前は警告に出す) から language の表を作る。
        /// 1 つも無いか 1 つでも壊れていれば警告を出して <see cref="FallbackLanguage"/> の表を作る。
        /// <see cref="FallbackLanguage"/> の表は、壊れたものだけエラーを出して飛ばし、1 つも読めなければエラーを出して空の表にする。
        /// </summary>
        /// <param name="language">作る表の言語。</param>
        /// <param name="read">言語ごとに、表の名前と JSON の文字列を重ねる順に返す。Resources を使わずに試すときは別のものを渡す。</param>
        /// <exception cref="ArgumentNullException">read が null。</exception>
        /// <exception cref="ArgumentOutOfRangeException">language が定義済みの値でない。</exception>
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

        /// <summary>
        /// Resources にある language の表を重ねる順に読む。先に <see cref="ResourcePathOf"/> の表、続けて
        /// <see cref="LayerFolderOf"/> のフォルダーにある TextAsset をファイル名 (拡張子なし) の順 (大文字小文字を区別する) に並べる。
        /// 同じ名前のものは Resources が返した順のまま並べる。
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">language が定義済みの値でない。</exception>
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

        /// <summary>
        /// Resources の <see cref="LayerFolderOf"/> のフォルダーにある language の TextAsset を、ファイル名 (拡張子なし) の順 (大文字小文字を区別する) に読む。
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">language が定義済みの値でない。</exception>
        public static IReadOnlyList<KeyValuePair<string, string>> ReadLayers(GameLanguage language)
        {
            var folder = LayerFolderOf(language);
            var assets = Resources.LoadAll<TextAsset>(folder);
            return assets
                .OrderBy(a => a.name, StringComparer.Ordinal)
                .Select(a => new KeyValuePair<string, string>($"Resources/{folder}/{a.name}", a.text))
                .ToList();
        }

        /// <summary>
        /// 設定画面で選べる言語を <see cref="GameLanguages.All"/> の並びで返す。Resources の Localization/&lt;言語コード&gt;/ に表が 1 つでもある言語。
        /// </summary>
        public static IReadOnlyList<GameLanguage> AvailableLanguages()
        {
            return AvailableLanguages(ReadLayers);
        }

        /// <summary>readLayers が表を 1 つでも返す言語を、<see cref="GameLanguages.All"/> の並びで返す。</summary>
        /// <param name="readLayers">言語ごとに、Localization/&lt;言語コード&gt;/ の表の名前と JSON の文字列を返す。Resources を使わずに試すときは別のものを渡す。</param>
        /// <exception cref="ArgumentNullException">readLayers が null。</exception>
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

        /// <summary>
        /// 言語の選択に出す、language 自身での名前。language の表の <see cref="NativeNameKey"/> を引く。
        /// どの表にも無ければ警告を出して <see cref="GameLanguages.NativeName"/> を返す。
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">language が定義済みの値でない。</exception>
        public static string NativeNameOf(GameLanguage language)
        {
            return NativeNameOf(language, ReadResources);
        }

        /// <summary>read が返す language の表を重ねる順に見て、最後に <see cref="NativeNameKey"/> を持つ表の名前。壊れた表は飛ばす。</summary>
        /// <param name="language">名前を引く言語。</param>
        /// <param name="read">言語ごとに、表の名前と JSON の文字列を重ねる順に返す。Resources を使わずに試すときは別のものを渡す。</param>
        /// <exception cref="ArgumentNullException">read が null。</exception>
        /// <exception cref="ArgumentOutOfRangeException">language が定義済みの値でない。</exception>
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

        // AppliedControlSettings.Apply が、効いている言語が前と変わったときに呼ぶ。
        internal static void NotifyChanged(GameLanguage language)
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

        // skipBroken が false なら壊れた表が 1 つでもあれば読まない。true なら壊れた表だけエラーを出して飛ばす。
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
            Changed = null;
            UseForCore();
        }

        // 表が見つからない・壊れているという知らせを、最初の画面を開く前の起動時に出す。
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void LoadOnPlay()
        {
            _ = TableOf(Language);
        }

        // Core の書式 (HudFormat など) は Unity に依らないので、今の言語の表を GameText 越しに渡す。
        // Edit Mode (テストやエディター拡張) では Play の初期化が走らないので、エディターの読み込みでも渡す。
#if UNITY_EDITOR
        [UnityEditor.InitializeOnLoadMethod]
#endif
        private static void UseForCore()
        {
            GameText.Use(CurrentTable);
        }

        private static StringTable CurrentTable()
        {
            return TableOf(Language);
        }
    }
}
