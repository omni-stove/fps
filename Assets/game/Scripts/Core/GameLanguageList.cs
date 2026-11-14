using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Fps.Core
{
    /// <summary>
    /// ゲームで選べる言語の一覧。設定画面に並べる順と、言語ごとの名前・初めて起動したときに選ぶ OS の言語を持つ。
    /// データ (<see cref="TryRead"/>) から作り、<see cref="GameLanguages.Use"/> で使わせる。
    /// <code>
    /// {
    ///   "otherSystemLanguages": "en",
    ///   "languages": [
    ///     { "code": "ja", "name": "日本語", "systemLanguages": ["Japanese"] },
    ///     { "code": "en", "name": "English", "systemLanguages": ["English"] }
    ///   ]
    /// }
    /// </code>
    /// </summary>
    public sealed class GameLanguageList
    {
        public const string LanguagesKey = "languages";
        public const string OtherSystemLanguagesKey = "otherSystemLanguages";
        public const string CodeKey = "code";
        public const string NameKey = "name";
        public const string SystemLanguagesKey = "systemLanguages";

        private const char _byteOrderMark = (char)0xFEFF;

        private readonly GameLanguageEntry[] _entries;
        private readonly Dictionary<string, GameLanguageEntry> _byCode;

        /// <summary>データが読めないときに使う一覧。キットが表を持つ日本語と英語で、OS が日本語なら日本語、それ以外は英語。</summary>
        public static GameLanguageList BuiltIn { get; } = new GameLanguageList(
            new[]
            {
                new GameLanguageEntry(GameLanguage.Japanese, "日本語", new[] { "Japanese" }),
                new GameLanguageEntry(GameLanguage.English, "English", new[] { "English" }),
            },
            GameLanguage.English);

        /// <param name="entries">並べる順の言語。</param>
        /// <param name="otherSystemLanguages">OS の言語がどの言語の <see cref="GameLanguageEntry.SystemLanguages"/> にも無いときの言語。<c>default</c> なら最初の言語。</param>
        /// <exception cref="ArgumentNullException">entries が null か、null を含む。</exception>
        /// <exception cref="ArgumentException">entries が空か、同じ言語が 2 つある。または otherSystemLanguages が entries に無い。</exception>
        public GameLanguageList(IEnumerable<GameLanguageEntry> entries, GameLanguage otherSystemLanguages = default)
        {
            if (entries == null)
            {
                throw new ArgumentNullException(nameof(entries));
            }

            var list = new List<GameLanguageEntry>();
            _byCode = new Dictionary<string, GameLanguageEntry>(StringComparer.Ordinal);
            foreach (var entry in entries)
            {
                if (entry == null)
                {
                    throw new ArgumentNullException(nameof(entries), "entries must not contain null");
                }

                if (_byCode.ContainsKey(entry.Language.Code))
                {
                    throw new ArgumentException($"'{entry.Language.Code}' is listed twice", nameof(entries));
                }

                _byCode.Add(entry.Language.Code, entry);
                list.Add(entry);
            }

            if (list.Count == 0)
            {
                throw new ArgumentException("at least one language is needed", nameof(entries));
            }

            if (otherSystemLanguages.IsDefined && !_byCode.ContainsKey(otherSystemLanguages.Code))
            {
                throw new ArgumentException($"'{otherSystemLanguages.Code}' is not in the list", nameof(otherSystemLanguages));
            }

            _entries = list.ToArray();
            Entries = new ReadOnlyCollection<GameLanguageEntry>(_entries);
            var languages = new GameLanguage[_entries.Length];
            for (var i = 0; i < _entries.Length; i++)
            {
                languages[i] = _entries[i].Language;
            }

            Languages = new ReadOnlyCollection<GameLanguage>(languages);
            OtherSystemLanguages = otherSystemLanguages.IsDefined ? otherSystemLanguages : _entries[0].Language;
        }

        /// <summary>並べる順の行。</summary>
        public IReadOnlyList<GameLanguageEntry> Entries { get; }

        /// <summary>並べる順の言語。</summary>
        public IReadOnlyList<GameLanguage> Languages { get; }

        /// <summary>OS の言語に合う言語が無いときの言語。</summary>
        public GameLanguage OtherSystemLanguages { get; }

        /// <summary>language が一覧にあるか。</summary>
        public bool Contains(GameLanguage language)
        {
            return language.IsDefined && _byCode.ContainsKey(language.Code);
        }

        /// <summary>language の行。一覧に無い (<c>default</c> を含む) なら false。</summary>
        public bool TryGet(GameLanguage language, out GameLanguageEntry entry)
        {
            entry = null;
            return language.IsDefined && _byCode.TryGetValue(language.Code, out entry);
        }

        /// <summary>言語コード code の言語。大文字小文字は区別し、一覧に無ければ false。</summary>
        public bool TryFind(string code, out GameLanguage language)
        {
            if (code != null && _byCode.TryGetValue(code, out var entry))
            {
                language = entry.Language;
                return true;
            }

            language = default;
            return false;
        }

        /// <summary>
        /// OS の言語の名前 systemLanguage (Unity の <c>SystemLanguage</c> の名前) を <see cref="GameLanguageEntry.SystemLanguages"/> に持つ最初の言語。
        /// どれにも無い (null を含む) なら <see cref="OtherSystemLanguages"/>。
        /// </summary>
        public GameLanguage ForSystemLanguage(string systemLanguage)
        {
            if (systemLanguage != null)
            {
                foreach (var entry in _entries)
                {
                    foreach (var name in entry.SystemLanguages)
                    {
                        if (string.Equals(name, systemLanguage, StringComparison.Ordinal))
                        {
                            return entry.Language;
                        }
                    }
                }
            }

            return OtherSystemLanguages;
        }

        /// <summary>
        /// files (一覧の名前と JSON の文字列) を前から順に重ねて一覧を作る。後のファイルにある言語は前の行を同じ位置のまま置き換え、
        /// 新しい言語は後ろに足す。<see cref="OtherSystemLanguagesKey"/> は最後に書いたファイルのものを使う。
        /// 読めないファイル・読めない行・一覧に無い <see cref="OtherSystemLanguagesKey"/> は、warn で知らせて飛ばす。
        /// </summary>
        /// <param name="files">重ねる順の、名前 (知らせに出す) と JSON の文字列。</param>
        /// <param name="warn">飛ばしたものの知らせの受け取り先。null なら知らせない。</param>
        /// <param name="list">作った一覧。作れなければ null。</param>
        /// <param name="error">作れなかった理由。作れたら null。</param>
        /// <returns>言語が 1 つでも読めたか。</returns>
        /// <exception cref="ArgumentNullException">files が null。</exception>
        public static bool TryRead(
            IEnumerable<KeyValuePair<string, string>> files,
            Action<string> warn,
            out GameLanguageList list,
            out string error)
        {
            if (files == null)
            {
                throw new ArgumentNullException(nameof(files));
            }

            var entries = new List<GameLanguageEntry>();
            var positions = new Dictionary<string, int>(StringComparer.Ordinal);
            string otherCode = null;
            string otherFile = null;
            var fileCount = 0;
            foreach (var file in files)
            {
                fileCount++;
                if (!TryReadFile(file.Key, file.Value, warn, out var fileEntries, out var fileOther))
                {
                    continue;
                }

                foreach (var entry in fileEntries)
                {
                    if (positions.TryGetValue(entry.Language.Code, out var position))
                    {
                        entries[position] = entry;
                    }
                    else
                    {
                        positions.Add(entry.Language.Code, entries.Count);
                        entries.Add(entry);
                    }
                }

                if (fileOther != null)
                {
                    otherCode = fileOther;
                    otherFile = file.Key;
                }
            }

            if (entries.Count == 0)
            {
                list = null;
                error = fileCount == 0 ? "no language list was found" : "no language could be read from the lists";
                return false;
            }

            var other = default(GameLanguage);
            if (otherCode != null)
            {
                if (positions.ContainsKey(otherCode))
                {
                    other = entries[positions[otherCode]].Language;
                }
                else
                {
                    warn?.Invoke($"{otherFile}: '{OtherSystemLanguagesKey}' is '{otherCode}' but it is not in the list; using '{entries[0].Language.Code}'");
                }
            }

            list = new GameLanguageList(entries, other);
            error = null;
            return true;
        }

        private static bool TryReadFile(
            string name,
            string json,
            Action<string> warn,
            out List<GameLanguageEntry> entries,
            out string otherCode)
        {
            entries = new List<GameLanguageEntry>();
            otherCode = null;
            if (json == null)
            {
                warn?.Invoke($"{name}: the text is missing; skipping it");
                return false;
            }

            if (json.Length > 0 && json[0] == _byteOrderMark)
            {
                json = json.Substring(1);
            }

            object parsed;
            try
            {
                parsed = SaveJson.Parse(json);
            }
            catch (FormatException e)
            {
                warn?.Invoke($"{name}: {e.Message}; skipping it");
                return false;
            }

            if (!(parsed is Dictionary<string, object> root))
            {
                warn?.Invoke($"{name}: the top level must be an object; skipping it");
                return false;
            }

            if (!root.TryGetValue(LanguagesKey, out var rawLanguages) || !(rawLanguages is List<object> rows))
            {
                warn?.Invoke($"{name}: '{LanguagesKey}' must be an array; skipping it");
                return false;
            }

            foreach (var key in root.Keys)
            {
                if (key != LanguagesKey && key != OtherSystemLanguagesKey)
                {
                    warn?.Invoke($"{name}: unknown key '{key}' is ignored");
                }
            }

            if (root.TryGetValue(OtherSystemLanguagesKey, out var rawOther))
            {
                if (rawOther is string code && GameLanguage.IsValidCode(code))
                {
                    otherCode = code;
                }
                else
                {
                    warn?.Invoke($"{name}: '{OtherSystemLanguagesKey}' must be a language code; ignoring it");
                }
            }

            var seen = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < rows.Count; i++)
            {
                var where = $"{name}: {LanguagesKey}[{i}]";
                if (!TryReadEntry(rows[i], where, warn, out var entry, out var problem))
                {
                    warn?.Invoke($"{where} {problem}; skipping it");
                    continue;
                }

                if (!seen.Add(entry.Language.Code))
                {
                    warn?.Invoke($"{where} '{entry.Language.Code}' is listed twice; skipping it");
                    continue;
                }

                entries.Add(entry);
            }

            return true;
        }

        private static bool TryReadEntry(object raw, string where, Action<string> warn, out GameLanguageEntry entry, out string problem)
        {
            entry = null;
            if (!(raw is Dictionary<string, object> row))
            {
                problem = "must be an object";
                return false;
            }

            if (!row.TryGetValue(CodeKey, out var rawCode) || !(rawCode is string code) || !GameLanguage.IsValidCode(code))
            {
                problem = $"'{CodeKey}' must be a language code (ASCII letters first, then letters, digits, '-' or '_')";
                return false;
            }

            if (!row.TryGetValue(NameKey, out var rawName) || !(rawName is string name) || string.IsNullOrWhiteSpace(name))
            {
                problem = $"'{NameKey}' of '{code}' must be a non-empty string";
                return false;
            }

            var systems = new List<string>();
            if (row.TryGetValue(SystemLanguagesKey, out var rawSystems))
            {
                if (!(rawSystems is List<object> systemRows))
                {
                    problem = $"'{SystemLanguagesKey}' of '{code}' must be an array of strings";
                    return false;
                }

                foreach (var system in systemRows)
                {
                    if (!(system is string systemName) || string.IsNullOrWhiteSpace(systemName))
                    {
                        problem = $"'{SystemLanguagesKey}' of '{code}' must be an array of strings";
                        return false;
                    }

                    systems.Add(systemName);
                }
            }

            foreach (var key in row.Keys)
            {
                if (key != CodeKey && key != NameKey && key != SystemLanguagesKey)
                {
                    warn?.Invoke($"{where} '{code}': unknown key '{key}' is ignored");
                }
            }

            entry = new GameLanguageEntry(new GameLanguage(code), name, systems);
            problem = null;
            return true;
        }
    }
}
