using System;
using System.Security.Cryptography;

namespace Shooter.Core
{
    // 設定で手書きしたコードには - _ や読み違えやすい文字も入りうるので、比べるときは Alphabet に絞らない。
    public static class JoinCode
    {
        // 見分けにくい 0 と O、1 と I と L は読み違えるので除く。
        public const string Alphabet = "23456789ABCDEFGHJKMNPQRSTUVWXYZ";

        public const int DefaultLength = 6;

        public const int MinLength = 1;

        public const int MaxLength = ConnectionExtras.MaxJoinCodeLength;

        public static string Generate(int length)
        {
            // コードを当てれば非公開の試合に入れるので、続きを読まれうる System.Random は使わない
            return Generate(length, RandomNumberGenerator.GetInt32);
        }

        public static string Generate(int length, Func<int, int> nextIndex)
        {
            if (nextIndex == null)
            {
                throw new ArgumentNullException(nameof(nextIndex));
            }

            if (length < MinLength || length > MaxLength)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(length), length, nameof(length) + " must be between " + MinLength + " and " + MaxLength);
            }

            var chars = new char[length];
            for (var i = 0; i < chars.Length; i++)
            {
                var index = nextIndex(Alphabet.Length);
                if (index < 0 || index >= Alphabet.Length)
                {
                    throw new ArgumentOutOfRangeException(
                        nameof(nextIndex), index, nameof(nextIndex) + " must return 0 to " + (Alphabet.Length - 1));
                }

                chars[i] = Alphabet[index];
            }

            return new string(chars);
        }

        public static bool TryNormalize(string text, out string code)
        {
            code = null;
            if (text == null)
            {
                return false;
            }

            var chars = new char[text.Length];
            var length = 0;
            foreach (var c in text)
            {
                if (char.IsWhiteSpace(c))
                {
                    continue;
                }

                chars[length++] = c >= 'a' && c <= 'z' ? (char)(c - ('a' - 'A')) : c;
            }

            var normalized = new string(chars, 0, length);
            if (!ConnectionExtras.IsValidField(normalized, MaxLength))
            {
                return false;
            }

            code = normalized;
            return true;
        }

        public static bool Matches(string expected, string entered)
        {
            return TryNormalize(expected, out var left)
                && TryNormalize(entered, out var right)
                && string.Equals(left, right, StringComparison.Ordinal);
        }
    }
}
