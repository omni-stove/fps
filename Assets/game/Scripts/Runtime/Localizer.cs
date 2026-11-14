using System;
using System.Collections.Generic;
using Fps.Core;
using UnityEngine;

namespace Fps.Runtime
{
    /// <summary>
    /// 画面に出す文字を、今効いている設定の言語 (<see cref="AppliedControlSettings.Current"/> の <see cref="ControlSettings.Language"/>) の
    /// <see cref="StringTable"/> から引く。表は Assets/game/Localization/Resources/Strings/ja.json と en.json を Resources から
    /// 初めて使うときに読み、Play の間は持ち続ける。言語が変わると (<see cref="AppliedControlSettings.Apply"/>) <see cref="Changed"/> で知らせる。
    /// 表が見つからない・壊れているときはエラーを出し、空の表 (キーそのものを出す) で続ける。無いキーは <c>[Localization]</c> 付きの警告で知らせる。
    /// Core の書式 (<see cref="HudFormat"/> など) が引く <see cref="GameText"/> にも、Play の開始時とエディターの読み込み時に同じ表を渡す。
    /// </summary>
    public static class Localizer
    {
        public const string LogTag = "[Localization]";

        /// <summary>Resources の中で言語ごとの表を置くフォルダー。</summary>
        public const string ResourceFolder = "Strings";

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
                table = Load(language);
                _tables[language] = table;
            }

            return table;
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

        private static StringTable Load(GameLanguage language)
        {
            var code = GameLanguages.Code(language);
            var path = ResourcePathOf(language);
            void Warn(string message) => Debug.LogWarning($"{LogTag} {code}: {message}");

            var asset = Resources.Load<TextAsset>(path);
            if (asset == null)
            {
                Debug.LogError(
                    $"{LogTag} strings 'Resources/{path}.json' were not found (expected under Assets/game/Localization/Resources); showing the keys instead");
                return new StringTable(Array.Empty<KeyValuePair<string, string>>(), Warn);
            }

            if (!StringTable.TryParse(asset.text, Warn, out var table, out var error))
            {
                Debug.LogError($"{LogTag} strings 'Resources/{path}.json' could not be read: {error}; showing the keys instead");
                return new StringTable(Array.Empty<KeyValuePair<string, string>>(), Warn);
            }

            return table;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnPlay()
        {
            _tables.Clear();
            Changed = null;
            UseForCore();
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
