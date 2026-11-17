using System;
using System.Collections.Generic;
using Shooter.Runtime;

namespace Shooter.Editor
{
    // 表が 1 つも無い・1 つでも読めない言語は、ゲームでは丸ごと Localizer.FallbackLanguage の表に代わるので、
    // キーを比べる言語 (ReadableLanguages) から外す。
    public sealed class AuditedStrings
    {
        private readonly List<GameLanguage> _readable = new List<GameLanguage>();
        private readonly Dictionary<GameLanguage, Dictionary<string, string>> _sources =
            new Dictionary<GameLanguage, Dictionary<string, string>>();

        private AuditedStrings()
        {
        }

        public IReadOnlyList<GameLanguage> ReadableLanguages => _readable;

        public bool TryGetTable(GameLanguage language, string key, out string table)
        {
            if (key == null)
            {
                throw new ArgumentNullException(nameof(key));
            }

            table = null;
            return _sources.TryGetValue(language, out var keys) && keys.TryGetValue(key, out table);
        }

        public IEnumerable<string> KeysOf(GameLanguage language)
        {
            return _sources.TryGetValue(language, out var keys) ? keys.Keys : (IEnumerable<string>)Array.Empty<string>();
        }

        public static AuditedStrings Read(
            IReadOnlyList<GameLanguage> languages,
            Func<GameLanguage, IReadOnlyList<KeyValuePair<string, string>>> read,
            ICollection<ReplacementAuditFinding> findings)
        {
            if (languages == null)
            {
                throw new ArgumentNullException(nameof(languages));
            }

            if (read == null)
            {
                throw new ArgumentNullException(nameof(read));
            }

            if (findings == null)
            {
                throw new ArgumentNullException(nameof(findings));
            }

            var strings = new AuditedStrings();
            foreach (var language in languages)
            {
                var code = GameLanguages.Code(language);
                var layers = read(language) ?? Array.Empty<KeyValuePair<string, string>>();
                if (layers.Count == 0)
                {
                    findings.Add(new ReplacementAuditFinding(
                        ReplacementAuditArea.Strings,
                        code,
                        $"no strings were found at Resources/{Localizer.ResourceFolder}/{code}.json or in Resources/{Localizer.LayerFolder}/{code}/"));
                    continue;
                }

                var keys = new Dictionary<string, string>(StringComparer.Ordinal);
                var broken = false;
                foreach (var layer in layers)
                {
                    string error;
                    if (layer.Value == null)
                    {
                        error = "the text is missing";
                    }
                    else if (StringTable.TryParse(layer.Value, null, out var table, out error))
                    {
                        foreach (var key in table.Keys)
                        {
                            keys[key] = layer.Key;
                        }

                        continue;
                    }

                    broken = true;
                    findings.Add(new ReplacementAuditFinding(
                        ReplacementAuditArea.Strings, code, $"strings '{layer.Key}' could not be read: {error}"));
                }

                if (!broken)
                {
                    strings._readable.Add(language);
                    strings._sources[language] = keys;
                }
            }

            return strings;
        }
    }
}
