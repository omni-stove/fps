using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Fps.Core
{
    /// <summary>
    /// 画面に出す文を、言語の表 (<see cref="TextTable"/>) のキーと書式に埋める引数の並びで表す値。
    /// 引数には int・float・string・別の <see cref="LocalizedText"/> を入れられ、入れ子の文は表を引くときに一緒に引く。
    /// 表を通さず決まった文字列をそのまま運ぶときは <see cref="Literal"/> で作る。
    /// </summary>
    public readonly struct LocalizedText : IEquatable<LocalizedText>
    {
        private static readonly object[] _noArgs =Array.Empty<object>();

        private readonly string _key;
        private readonly object[] _args;

        /// <summary>表を引くキー。<see cref="IsLiteral"/> なら運ぶ文字列そのもの。<c>default</c> なら空文字。</summary>
        public string Key => _key ?? string.Empty;

        /// <summary>書式の <c>{0}</c>, <c>{1}</c>… に埋める引数。要素は int・float・string・<see cref="LocalizedText"/> のどれか。</summary>
        public IReadOnlyList<object> Args => _args ?? _noArgs;

        /// <summary>表を引かずに <see cref="Key"/> をそのまま出すか。</summary>
        public bool IsLiteral { get; }

        /// <summary><c>default</c> のときに真。表を引くと空文字になる。</summary>
        public bool IsEmpty => _key == null;

        /// <exception cref="ArgumentNullException">key が null。</exception>
        /// <exception cref="ArgumentException">key が空か、引数に int・float・string・<see cref="LocalizedText"/> 以外 (null を含む) がある。</exception>
        public LocalizedText(string key, params object[] args)
        {
            if (key == null)
            {
                throw new ArgumentNullException(nameof(key));
            }

            if (key.Length == 0)
            {
                throw new ArgumentException("key must not be empty", nameof(key));
            }

            _key = key;
            _args = CopyArgs(args);
            IsLiteral = false;
        }

        private LocalizedText(string text, bool isLiteral)
        {
            _key = text;
            _args = _noArgs;
            IsLiteral = isLiteral;
        }

        /// <summary>表を引かず、どの言語でも <paramref name="text"/> をそのまま出す文。</summary>
        /// <exception cref="ArgumentNullException">text が null。</exception>
        public static LocalizedText Literal(string text)
        {
            if (text == null)
            {
                throw new ArgumentNullException(nameof(text));
            }

            return new LocalizedText(text, true);
        }

        public bool Equals(LocalizedText other)
        {
            if (IsLiteral != other.IsLiteral || IsEmpty != other.IsEmpty || !string.Equals(Key, other.Key, StringComparison.Ordinal))
            {
                return false;
            }

            var args = Args;
            var otherArgs = other.Args;
            if (args.Count != otherArgs.Count)
            {
                return false;
            }

            for (var i = 0; i < args.Count; i++)
            {
                if (!args[i].Equals(otherArgs[i]))
                {
                    return false;
                }
            }

            return true;
        }

        public override bool Equals(object obj)
        {
            return obj is LocalizedText other && Equals(other);
        }

        public static bool operator ==(LocalizedText left, LocalizedText right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(LocalizedText left, LocalizedText right)
        {
            return !left.Equals(right);
        }

        public override int GetHashCode()
        {
            var hash = new HashCode();
            hash.Add(Key, StringComparer.Ordinal);
            hash.Add(IsLiteral);
            hash.Add(IsEmpty);
            foreach (var arg in Args)
            {
                hash.Add(arg);
            }

            return hash.ToHashCode();
        }

        /// <summary>ログやデバッグ用の形。例: <c>match.win(Alpha, 3)</c>、Literal なら <c>"GG"</c>。表は引かない。</summary>
        public override string ToString()
        {
            if (IsLiteral)
            {
                return "\"" + Key + "\"";
            }

            var builder = new StringBuilder(Key);
            if (Args.Count > 0)
            {
                builder.Append('(');
                for (var i = 0; i < Args.Count; i++)
                {
                    if (i > 0)
                    {
                        builder.Append(", ");
                    }

                    builder.Append(Convert.ToString(Args[i], CultureInfo.InvariantCulture));
                }

                builder.Append(')');
            }

            return builder.ToString();
        }

        // 呼び出し側が後から配列を書き換えても値が変わらないよう写し取る。型をここで絞るので、表を引く側は 4 種類だけを扱えばよい
        private static object[] CopyArgs(object[] args)
        {
            if (args == null || args.Length == 0)
            {
                return _noArgs;
            }

            var copy = new object[args.Length];
            for (var i = 0; i < args.Length; i++)
            {
                var arg = args[i];
                if (!(arg is int || arg is float || arg is string || arg is LocalizedText))
                {
                    var type = arg == null ? "null" : arg.GetType().FullName;
                    throw new ArgumentException(
                        "argument " + i + " is " + type + "; only int, float, string and LocalizedText are allowed",
                        nameof(args));
                }

                copy[i] = arg;
            }

            return copy;
        }
    }
}
