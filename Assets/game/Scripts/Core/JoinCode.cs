using System;
using System.Security.Cryptography;

namespace Shooter.Core
{
    /// <summary>
    /// 非公開の試合 (<see cref="NetMatchSettings.PrivateMatch"/>) に入る参加コード。作るときは読み違えにくい文字 (<see cref="Alphabet"/>) だけを使い、
    /// 比べるときは大文字・小文字と空白の違いを揃えてから比べる。
    /// </summary>
    /// <remarks>
    /// 揃えた後のコードは承認データの欄 (<see cref="ConnectionExtras.JoinCode"/>) とロビーの属性 (<see cref="ServerLobby.JoinCodeKey"/>) にそのまま載る。
    /// 設定で手書きしたコードには <c>-</c> <c>_</c> や読み違えやすい文字も入りうるので、比べるときは <see cref="Alphabet"/> に絞らない。
    /// </remarks>
    public static class JoinCode
    {
        /// <summary>作るコードの文字。見分けにくい 0 と O、1 と I と L を除いた数字と大文字。</summary>
        public const string Alphabet = "23456789ABCDEFGHJKMNPQRSTUVWXYZ";

        /// <summary>作るコードの長さの既定。</summary>
        public const int DefaultLength = 6;

        /// <summary>作れるコードの最短の長さ。</summary>
        public const int MinLength = 1;

        /// <summary>作れるコードの最長の長さ。承認データの欄に収まる長さ。</summary>
        public const int MaxLength = ConnectionExtras.MaxJoinCodeLength;

        /// <summary>
        /// <paramref name="length"/> 文字のコードを、暗号論的な乱数で <see cref="Alphabet"/> から選んで作る。
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">length が <see cref="MinLength"/>〜<see cref="MaxLength"/> の外。</exception>
        public static string Generate(int length)
        {
            // コードを当てれば非公開の試合に入れるので、続きを読まれうる System.Random は使わない
            return Generate(length, RandomNumberGenerator.GetInt32);
        }

        /// <summary>
        /// <paramref name="length"/> 文字のコードを、<paramref name="nextIndex"/> が返す <see cref="Alphabet"/> の添字で作る。
        /// </summary>
        /// <param name="length">コードの長さ。</param>
        /// <param name="nextIndex">0 以上・引数未満の添字を返す。</param>
        /// <exception cref="ArgumentNullException">nextIndex が null。</exception>
        /// <exception cref="ArgumentOutOfRangeException">length が範囲の外、または nextIndex が範囲の外の添字を返した。</exception>
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

        /// <summary>
        /// 入力 <paramref name="text"/> から空白を除き、ASCII の小文字を大文字にする。
        /// 残りが空、<see cref="MaxLength"/> より長い、または英数字と <c>-</c> <c>_</c> 以外を含むなら false (code は null)。
        /// </summary>
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

        /// <summary>
        /// 入力 <paramref name="entered"/> が試合のコード <paramref name="expected"/> と、揃えた (<see cref="TryNormalize"/>) 上で同じなら true。
        /// どちらかが揃えられないなら false。
        /// </summary>
        public static bool Matches(string expected, string entered)
        {
            return TryNormalize(expected, out var left)
                && TryNormalize(entered, out var right)
                && string.Equals(left, right, StringComparison.Ordinal);
        }
    }
}
