using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;

namespace Fps.Core
{
    /// <summary>
    /// 1 つの言語の、キーから画面に出す文字列への表。JSON (<c>{"settings.title": "設定", ...}</c>) から <see cref="TryParse"/> で作る。
    /// <see cref="Get"/> は無いキーならキーそのものを返し、<see cref="Format"/> は <c>{0}</c> などの所に値を差し込む。
    /// 無いキーや差し込めない所は、キーごとに一度だけ警告の受け取り先に知らせる (毎フレーム引く所で同じ警告を出し続けないため)。
    /// 表の中身は変わらない。
    /// </summary>
    public sealed class StringTable
    {
        private readonly Dictionary<string, string> _entries;
        private readonly Action<string> _warn;
        private readonly HashSet<string> _warned = new HashSet<string>(StringComparer.Ordinal);

        /// <param name="entries">キーと文字列。写しを持つので、後で変えてもこの表は変わらない。</param>
        /// <param name="warn">無いキーや差し込めない所の知らせの受け取り先。null なら知らせない。</param>
        /// <exception cref="ArgumentNullException">entries が null か、キーか文字列に null がある。</exception>
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

        /// <summary>表にあるキー。</summary>
        public IReadOnlyList<string> Keys { get; }

        /// <summary>表にあるキーの数。</summary>
        public int Count => _entries.Count;

        /// <summary>
        /// JSON の文字列 (値がすべて文字列で入れ子の無いオブジェクト) から表を作る。
        /// 空のキー・同じキーの重なり・文字列でない値があれば読まない。
        /// </summary>
        /// <param name="json">読む文字列。先頭の BOM は読み飛ばす。</param>
        /// <param name="warn">作った表の、無いキーや差し込めない所の知らせの受け取り先。null なら知らせない。</param>
        /// <param name="table">読めたときの表。読めなければ null。</param>
        /// <param name="error">読めなかったときの理由 (何文字目か付き)。読めたら null。</param>
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

        /// <summary>key が表にあるか。</summary>
        public bool Contains(string key)
        {
            if (key == null)
            {
                throw new ArgumentNullException(nameof(key));
            }

            return _entries.ContainsKey(key);
        }

        /// <summary>key の文字列。無ければ、どのキーが足りないか画面で分かるようキーそのものを返し、警告する。</summary>
        /// <exception cref="ArgumentNullException">key が null。</exception>
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

        /// <summary>
        /// key の文字列の <c>{0}</c>・<c>{1}</c>… に args の値を差し込む。数は言語に依らない書き方 (InvariantCulture) にする。
        /// <c>{{</c> と <c>}}</c> は <c>{</c> と <c>}</c> になる。訳の誤りで画面が止まらないよう、渡されていない番号の所はそのまま残して警告する。
        /// 無いキーは <see cref="Get"/> と同じくキーそのものを返す。
        /// </summary>
        /// <exception cref="ArgumentNullException">key か args が null。</exception>
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

        /// <summary>
        /// <see cref="LocalizedText"/> を文にする。入れ子の文は先にこの表で文にして <see cref="Format"/> に渡す。
        /// <see cref="LocalizedText.Literal"/> はその文字列、<c>default</c> は空文字、表に無いキーは引数を使わずキーそのものを返す。
        /// </summary>
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

        /// <summary>
        /// template が差し込む番号を小さい順に重なり無く返す。言語ごとの表で同じキーが同じ値を使っているかを確かめるためのもの。
        /// </summary>
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

        // builder が null なら番号を集めるだけ。unmatched は args に無い番号のうち最初のもの (無ければ -1)。
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

        // template[start] の '{' から '{数字}' を読む。end は '}' の次。数字が無い・桁が多すぎる・閉じていなければ false。
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
