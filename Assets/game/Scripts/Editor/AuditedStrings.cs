using System;
using System.Collections.Generic;
using Fps.Core;
using Fps.Runtime;

namespace Fps.Editor
{
    /// <summary>
    /// <see cref="ReplacementAudit"/> が比べる、言語ごとの文言のキーと、それを持つ表の名前。
    /// 表が 1 つも無い・1 つでも読めない言語は、ゲームでは丸ごと <see cref="Localizer.FallbackLanguage"/> の表に代わるので、
    /// キーを比べる言語 (<see cref="ReadableLanguages"/>) から外す。
    /// </summary>
    public sealed class AuditedStrings
    {
        private readonly List<GameLanguage> _readable = new List<GameLanguage>();
        private readonly Dictionary<GameLanguage, Dictionary<string, string>> _sources =
            new Dictionary<GameLanguage, Dictionary<string, string>>();

        private AuditedStrings()
        {
        }

        /// <summary>表がすべて読めた言語。<see cref="Read"/> に渡した順。</summary>
        public IReadOnlyList<GameLanguage> ReadableLanguages => _readable;

        /// <summary>language の表に key があれば、それを持つ表 (重ねた順で最後のもの) の名前を table に入れて true。</summary>
        /// <exception cref="ArgumentNullException">key が null。</exception>
        public bool TryGetTable(GameLanguage language, string key, out string table)
        {
            if (key == null)
            {
                throw new ArgumentNullException(nameof(key));
            }

            table = null;
            return _sources.TryGetValue(language, out var keys) && keys.TryGetValue(key, out table);
        }

        /// <summary>language の表にあるキー。読めなかった言語なら空。</summary>
        public IEnumerable<string> KeysOf(GameLanguage language)
        {
            return _sources.TryGetValue(language, out var keys) ? keys.Keys : (IEnumerable<string>)Array.Empty<string>();
        }

        /// <summary>
        /// languages の表を read で読む。表が無い・読めない言語は findings に足し、<see cref="ReadableLanguages"/> に入れない。
        /// </summary>
        /// <param name="languages">読む言語。</param>
        /// <param name="read">言語ごとに、表の名前と JSON の文字列を重ねる順に返す (<see cref="Localizer.ReadResources"/> と同じ形)。</param>
        /// <param name="findings">見つけた漏れを足す先。</param>
        /// <exception cref="ArgumentNullException">どれかが null。</exception>
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
