using System;
using System.Globalization;

namespace Shooter.Core
{
    // SDK の型を使わないのは、Core を EOS SDK に依存させないため。
#pragma warning disable CA1711 // ロビーの「属性」を表す名前で、System.Attribute の派生ではない
    public sealed class LobbyAttribute
#pragma warning restore CA1711
    {
        // EOS の属性キーの上限より十分短くしておく。
        public const int MaxKeyLength = 32;

        // EOS_LOBBYMODIFICATION_MAX_ATTRIBUTE_LENGTH に合わせてある。
        public const int MaxTextLength = 1000;

        private LobbyAttribute(string key, string text, long number)
        {
            Key = key;
            Text = text;
            Number = number;
        }

        public string Key { get; }

        public string Text { get; }

        public long Number { get; }

        public bool IsText => Text != null;

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

        public static LobbyAttribute OfNumber(string key, long value)
        {
            ValidateKey(key);
            return new LobbyAttribute(key, null, value);
        }

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
