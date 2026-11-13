using System;
using System.Collections.Generic;
using System.Globalization;

namespace Fps.Core
{
    /// <summary>
    /// 1 つの言語の、キーから書式 (例: <c>{0} の勝ち</c>) への表。<see cref="Resolve"/> で <see cref="LocalizedText"/> を文にする。
    /// 作った後は変わらない。表を重ねるときは <see cref="Merge"/> が新しい表を返す。
    /// </summary>
    public sealed class TextTable
    {
        /// <summary>何も入っていない表。どのキーもキーそのものになる。</summary>
        public static readonly TextTable Empty = new TextTable();

        private readonly Dictionary<string, string> _formats;

        public TextTable()
        {
            _formats = new Dictionary<string, string>(StringComparer.Ordinal);
        }

        /// <exception cref="ArgumentNullException">entries が null。</exception>
        /// <exception cref="ArgumentException">キーが null か空、書式が null、または同じキーが 2 度ある。</exception>
        public TextTable(IEnumerable<KeyValuePair<string, string>> entries)
            : this()
        {
            if (entries == null)
            {
                throw new ArgumentNullException(nameof(entries));
            }

            foreach (var entry in entries)
            {
                if (string.IsNullOrEmpty(entry.Key))
                {
                    throw new ArgumentException("key must not be null or empty", nameof(entries));
                }

                if (entry.Value == null)
                {
                    throw new ArgumentException("format for key '" + entry.Key + "' must not be null", nameof(entries));
                }

                if (_formats.ContainsKey(entry.Key))
                {
                    throw new ArgumentException("key '" + entry.Key + "' appears more than once", nameof(entries));
                }

                _formats.Add(entry.Key, entry.Value);
            }
        }

        private TextTable(Dictionary<string, string> formats)
        {
            _formats = formats;
        }

        /// <summary>入っているキーの数。</summary>
        public int Count => _formats.Count;

        /// <summary>入っているキー。順は決まっていない。</summary>
        public IEnumerable<string> Keys => _formats.Keys;

        /// <summary>
        /// <c>{"key": "書式", ...}</c> の平たい JSON オブジェクトから表を作る。値は文字列だけを受け付ける。
        /// 先頭の BOM は読み飛ばす。
        /// </summary>
        /// <exception cref="ArgumentNullException">json が null。</exception>
        /// <exception cref="FormatException">JSON として壊れているか、平たい文字列のオブジェクトでないか、同じキーが 2 度ある。</exception>
        public static TextTable FromJson(string json)
        {
            if (json == null)
            {
                throw new ArgumentNullException(nameof(json));
            }

            if (!FlatJsonObject.TryParse(json, out var values, out var error))
            {
                throw new FormatException(error);
            }

            try
            {
                return new TextTable(values);
            }
            catch (ArgumentException e)
            {
                // 空キーは FlatJsonObject が通すので、表を作るときの検査で弾かれた理由を FormatException に直す
                throw new FormatException(e.Message, e);
            }
        }

        public bool Contains(string key)
        {
            return key != null && _formats.ContainsKey(key);
        }

        public bool TryGetFormat(string key, out string format)
        {
            if (key == null)
            {
                format = null;
                return false;
            }

            return _formats.TryGetValue(key, out format);
        }

        /// <summary>
        /// この表に <paramref name="other"/> を重ねた新しい表。両方にあるキーは <paramref name="other"/> の書式になる。
        /// この表も <paramref name="other"/> も変わらない。
        /// </summary>
        /// <exception cref="ArgumentNullException">other が null。</exception>
        public TextTable Merge(TextTable other)
        {
            if (other == null)
            {
                throw new ArgumentNullException(nameof(other));
            }

            var merged = new Dictionary<string, string>(_formats, StringComparer.Ordinal);
            foreach (var entry in other._formats)
            {
                merged[entry.Key] = entry.Value;
            }

            return new TextTable(merged);
        }

        /// <summary>
        /// 書式の <c>{0}</c>, <c>{1}</c>… に引数を埋めた文。引数の <see cref="LocalizedText"/> はこの表で先に文にする。
        /// 数値はどの言語でも <see cref="CultureInfo.InvariantCulture"/> で書く (<c>{0:0.0}</c> のような書式指定も効く)。
        /// 表に無いキーはキーそのもの、<see cref="LocalizedText.Literal"/> はその文字列、<c>default</c> は空文字になる。
        /// </summary>
        /// <exception cref="FormatException">表の書式が壊れているか、書式が引数より多くの番号を使っている。</exception>
        public string Resolve(LocalizedText text)
        {
            if (text.IsEmpty)
            {
                return string.Empty;
            }

            if (text.IsLiteral || !_formats.TryGetValue(text.Key, out var format))
            {
                return text.Key;
            }

            var args = text.Args;
            var values = new object[args.Count];
            for (var i = 0; i < values.Length; i++)
            {
                values[i] = args[i] is LocalizedText nested ? Resolve(nested) : args[i];
            }

            try
            {
                return string.Format(CultureInfo.InvariantCulture, format, values);
            }
            catch (FormatException e)
            {
                // string.Format の例外はどのキーの書式かを言わないので、表を直す人が探せるようキーを添える
                throw new FormatException(
                    "format for key '" + text.Key + "' (\"" + format + "\") does not fit " + values.Length + " argument(s): " + e.Message,
                    e);
            }
        }
    }
}
