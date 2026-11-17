using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;
using Shooter.Core;

namespace Shooter.Runtime
{
    public sealed class StringTable
    {
        private readonly Dictionary<string, string> _entries;
        private readonly Action<string> _warn;
        // 無いキーや差し込めない所の警告をキーごとに一度だけにするのは、毎フレーム引く所で同じ警告を出し続けないため。
        private readonly HashSet<string> _warned = new HashSet<string>(StringComparer.Ordinal);

        // 呼び出し側が後から entries を変えても表が変わらないよう、写しを持つ。
        public StringTable(IEnumerable<KeyValuePair<string, string>> entries, Action<string> warn = null)
        {
            if (entries == null)
            {
                throw new ArgumentNullException(nameof(entries));
            }

            _entries = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var pair in entries)
            {
                if (pair.Key == null || pair.Value == null)
                {
                    throw new ArgumentNullException(nameof(entries), "keys and texts must not be null");
                }

                _entries[pair.Key] = pair.Value;
            }

            _warn = warn;
            Keys = new ReadOnlyCollection<string>(new List<string>(_entries.Keys));
        }

        public IReadOnlyList<string> Keys { get; }

        public int Count => _entries.Count;

        public static bool TryParse(string json, Action<string> warn, out StringTable table, out string error)
        {
            if (json == null)
            {
                throw new ArgumentNullException(nameof(json));
            }

            table = null;
            if (!FlatJsonObject.TryParse(json, out var values, out error))
            {
                return false;
            }

            foreach (var key in values.Keys)
            {
                if (key.Length == 0)
                {
                    error = "a key must not be empty";
                    return false;
                }
            }

            table = new StringTable(values, warn);
            return true;
        }

        public static StringTable Merge(IEnumerable<StringTable> tables, Action<string> warn = null)
        {
            if (tables == null)
            {
                throw new ArgumentNullException(nameof(tables));
            }

            var entries = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var table in tables)
            {
                if (table == null)
                {
                    throw new ArgumentNullException(nameof(tables), "tables must not contain null");
                }

                foreach (var pair in table._entries)
                {
                    entries[pair.Key] = pair.Value;
                }
            }

            return new StringTable(entries, warn);
        }

        public bool Contains(string key)
        {
            if (key == null)
            {
                throw new ArgumentNullException(nameof(key));
            }

            return _entries.ContainsKey(key);
        }

        // 無いキーでキーそのものを返すのは、どのキーが足りないか画面で分かるようにするため。
        public string Get(string key)
        {
            if (key == null)
            {
                throw new ArgumentNullException(nameof(key));
            }

            if (_entries.TryGetValue(key, out var text))
            {
                return text;
            }

            WarnOnce("missing:" + key, $"missing key '{key}'; showing the key itself");
            return key;
        }

        // 訳の誤りで画面が止まらないよう、渡されていない番号の所は例外にせずそのまま残して警告する。
        public string Format(string key, params object[] args)
        {
            if (args == null)
            {
                throw new ArgumentNullException(nameof(args));
            }

            var template = Get(key);
            var text = Substitute(template, args, out var unmatched);
            if (unmatched >= 0)
            {
                WarnOnce(
                    "format:" + key,
                    $"'{key}' refers to {{{unmatched.ToString(CultureInfo.InvariantCulture)}}} but only {args.Length.ToString(CultureInfo.InvariantCulture)} value(s) were given; leaving it as is");
            }

            return text;
        }

        public string Resolve(LocalizedText text)
        {
            if (text.IsEmpty)
            {
                return string.Empty;
            }

            if (text.IsLiteral || !_entries.ContainsKey(text.Key))
            {
                return text.IsLiteral ? text.Key : Get(text.Key);
            }

            var args = text.Args;
            var values = new object[args.Count];
            for (var i = 0; i < values.Length; i++)
            {
                values[i] = args[i] is LocalizedText nested ? Resolve(nested) : args[i];
            }

            return Format(text.Key, values);
        }

        public static IReadOnlyList<int> PlaceholdersOf(string template)
        {
            if (template == null)
            {
                throw new ArgumentNullException(nameof(template));
            }

            var found = new SortedSet<int>();
            Scan(template, null, null, found, out _);
            return new ReadOnlyCollection<int>(new List<int>(found));
        }

        private static string Substitute(string template, object[] args, out int unmatched)
        {
            var builder = new StringBuilder(template.Length);
            Scan(template, args, builder, null, out unmatched);
            return builder.ToString();
        }

        private static void Scan(string template, object[] args, StringBuilder builder, SortedSet<int> indices, out int unmatched)
        {
            unmatched = -1;
            var i = 0;
            while (i < template.Length)
            {
                var c = template[i];
                if ((c == '{' || c == '}') && i + 1 < template.Length && template[i + 1] == c)
                {
                    builder?.Append(c);
                    i += 2;
                    continue;
                }

                if (c == '{' && TryReadPlaceholder(template, i, out var index, out var end))
                {
                    indices?.Add(index);
                    if (builder != null)
                    {
                        if (index < args.Length)
                        {
                            builder.Append(Describe(args[index]));
                        }
                        else
                        {
                            builder.Append(template, i, end - i);
                            if (unmatched < 0)
                            {
                                unmatched = index;
                            }
                        }
                    }

                    i = end;
                    continue;
                }

                builder?.Append(c);
                i++;
            }
        }

        private static bool TryReadPlaceholder(string template, int start, out int index, out int end)
        {
            index = 0;
            end = start;
            var i = start + 1;
            var digits = 0;
            while (i < template.Length && template[i] >= '0' && template[i] <= '9')
            {
                if (digits == 6)
                {
                    return false;
                }

                index = (index * 10) + (template[i] - '0');
                digits++;
                i++;
            }

            if (digits == 0 || i >= template.Length || template[i] != '}')
            {
                return false;
            }

            end = i + 1;
            return true;
        }

        private static string Describe(object value)
        {
            switch (value)
            {
                case null:
                    return string.Empty;
                case IFormattable formattable:
                    return formattable.ToString(null, CultureInfo.InvariantCulture);
                default:
                    return value.ToString() ?? string.Empty;
            }
        }

        private void WarnOnce(string id, string message)
        {
            if (_warn != null && _warned.Add(id))
            {
                _warn(message);
            }
        }
    }
}
