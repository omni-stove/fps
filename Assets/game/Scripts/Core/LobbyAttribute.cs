using System;
using System.Globalization;

namespace Fps.Core
{
    /// <summary>
    /// ロビーに付ける公開の属性 1 つ。値は文字列か整数で、検索で比べられる。SDK の型には依存しない。
    /// </summary>
    public sealed class LobbyAttribute
    {
        /// <summary>キーの最大の長さ。EOS の属性キーの上限より十分短くしておく。</summary>
        public const int MaxKeyLength = 32;

        /// <summary>文字列の値の最大の長さ (EOS_LOBBYMODIFICATION_MAX_ATTRIBUTE_LENGTH)。</summary>
        public const int MaxTextLength = 1000;

        private LobbyAttribute(string key, string text, long number)
        {
            Key = key;
            Text = text;
            Number = number;
        }

        public string Key { get; }

        /// <summary>文字列の値。整数の属性では null。</summary>
        public string Text { get; }

        /// <summary>整数の値。文字列の属性では 0。</summary>
        public long Number { get; }

        public bool IsText => Text != null;

        /// <summary>文字列の属性を作る。</summary>
        /// <param name="key">英小文字・数字・<c>_</c> だけの <see cref="MaxKeyLength"/> 文字以内のキー。</param>
        /// <param name="value">空でない <see cref="MaxTextLength"/> 文字以内の値。</param>
        public static LobbyAttribute OfText(string key, string value)
        {
            ValidateKey(key);
            if (string.IsNullOrEmpty(value))
            {
                throw new ArgumentException("must not be null or empty", nameof(value));
            }

            if (value.Length > MaxTextLength)
            {
                throw new ArgumentException("must be at most " + MaxTextLength + " characters", nameof(value));
            }

            return new LobbyAttribute(key, value, 0);
        }

        /// <summary>整数の属性を作る。</summary>
        /// <param name="key">英小文字・数字・<c>_</c> だけの <see cref="MaxKeyLength"/> 文字以内のキー。</param>
        /// <param name="value">値。</param>
        public static LobbyAttribute OfNumber(string key, long value)
        {
            ValidateKey(key);
            return new LobbyAttribute(key, null, value);
        }

        /// <summary>ログ向けの <c>key=value</c>。</summary>
        public override string ToString()
        {
            return Key + "=" + (IsText ? Text : Number.ToString(CultureInfo.InvariantCulture));
        }

        private static void ValidateKey(string key)
        {
            if (string.IsNullOrEmpty(key))
            {
                throw new ArgumentException("must not be null or empty", nameof(key));
            }

            if (key.Length > MaxKeyLength)
            {
                throw new ArgumentException("must be at most " + MaxKeyLength + " characters", nameof(key));
            }

            foreach (var c in key)
            {
                // 作る側と検索する側でキーの表記がずれると一致しないので、書き方を 1 つに絞る
                var allowed = (c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || c == '_';
                if (!allowed)
                {
                    throw new ArgumentException("must contain only a-z, 0-9 and '_' (got '" + key + "')", nameof(key));
                }
            }
        }
    }
}
