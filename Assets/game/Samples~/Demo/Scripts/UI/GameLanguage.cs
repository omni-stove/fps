using System;

namespace Shooter.Runtime
{
    public readonly struct GameLanguage : IEquatable<GameLanguage>
    {
        public const int MaxCodeLength = 35;

        public static GameLanguage Japanese { get; } = new GameLanguage("ja");

        public static GameLanguage English { get; } = new GameLanguage("en");

        private readonly string _code;

        public GameLanguage(string code)
        {
            if (code == null)
            {
                throw new ArgumentNullException(nameof(code));
            }

            if (!IsValidCode(code))
            {
                throw new ArgumentException($"'{code}' is not a language code (ASCII letters first, then letters, digits, '-' or '_', at most {MaxCodeLength})", nameof(code));
            }

            _code = code;
        }

        public string Code => _code;

        public bool IsDefined => _code != null;

        // 保存した設定と Resources のフォルダー名 (Localization/<言語コード>/) に使うので、
        // ASCII の英字で始まり、英数字・-・_ だけで MaxCodeLength 文字までに限る。
        public static bool IsValidCode(string code)
        {
            if (string.IsNullOrEmpty(code) || code.Length > MaxCodeLength || !IsAsciiLetter(code[0]))
            {
                return false;
            }

            foreach (var c in code)
            {
                if (!IsAsciiLetter(c) && !(c >= '0' && c <= '9') && c != '-' && c != '_')
                {
                    return false;
                }
            }

            return true;
        }

        public bool Equals(GameLanguage other)
        {
            return string.Equals(_code, other._code, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return obj is GameLanguage other && Equals(other);
        }

        public override int GetHashCode()
        {
            return _code == null ? 0 : StringComparer.Ordinal.GetHashCode(_code);
        }

        public override string ToString()
        {
            return _code ?? "(none)";
        }

        public static bool operator ==(GameLanguage left, GameLanguage right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(GameLanguage left, GameLanguage right)
        {
            return !left.Equals(right);
        }

        private static bool IsAsciiLetter(char c)
        {
            return (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z');
        }
    }
}
