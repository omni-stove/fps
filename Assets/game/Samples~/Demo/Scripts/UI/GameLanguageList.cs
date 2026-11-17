using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Shooter.Core;

namespace Shooter.Runtime
{
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

        public static GameLanguageList BuiltIn { get; } = new GameLanguageList(
            new[]
            {
                // その言語自身での名前で訳す文ではないが、UI の日本語の直書きの検査に掛からないよう escape で書く
                new GameLanguageEntry(GameLanguage.Japanese, "\u65E5\u672C\u8A9E", new[] { "Japanese" }),
                new GameLanguageEntry(GameLanguage.English, "English", new[] { "English" }),
            },
            GameLanguage.English);

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

        public IReadOnlyList<GameLanguageEntry> Entries { get; }

        public IReadOnlyList<GameLanguage> Languages { get; }

        public GameLanguage OtherSystemLanguages { get; }

        public bool Contains(GameLanguage language)
        {
            return language.IsDefined && _byCode.ContainsKey(language.Code);
        }

        public bool TryGet(GameLanguage language, out GameLanguageEntry entry)
        {
            entry = null;
            return language.IsDefined && _byCode.TryGetValue(language.Code, out entry);
        }

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
                if (positions.TryGetValue(otherCode, out var otherPosition))
                {
                    other = entries[otherPosition].Language;
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
