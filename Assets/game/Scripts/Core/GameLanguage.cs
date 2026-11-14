using System;

namespace Fps.Core
{
    /// <summary>
    /// 画面に出す文字の言語。言語コード (<see cref="Code"/>) で区別する。設定 (<see cref="ControlSettings.Language"/>) で選び、文字は言語ごとの <see cref="StringTable"/> から引く。
    /// ゲームで選べる言語と並べる順・その言語自身での名前は <see cref="GameLanguages"/> の一覧 (データ) で決まる。
    /// <c>default</c> はどの言語でもなく、言語を受け取るメソッドは <see cref="ArgumentOutOfRangeException"/> を投げる。
    /// </summary>
    public readonly struct GameLanguage : IEquatable<GameLanguage>
    {
        /// <summary>言語コードの長さの上限。</summary>
        public const int MaxCodeLength = 35;

        /// <summary>キットの文字列表の元になっている言語。ほかの言語の表が読めないときの代わりにも使う。</summary>
        public static GameLanguage Japanese { get; } = new GameLanguage("ja");

        /// <summary>キットが表を持つもう 1 つの言語。</summary>
        public static GameLanguage English { get; } = new GameLanguage("en");

        private readonly string _code;

        /// <param name="code">言語コード (例: <c>ja</c>・<c>pt-BR</c>)。大文字小文字を区別する。</param>
        /// <exception cref="ArgumentNullException">code が null。</exception>
        /// <exception cref="ArgumentException">code が <see cref="IsValidCode"/> に合わない。</exception>
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

        /// <summary>言語コード。<c>default</c> なら null。</summary>
        public string Code => _code;

        /// <summary><c>default</c> でなく、言語コードを持つか。</summary>
        public bool IsDefined => _code != null;

        /// <summary>
        /// code が言語コードに使えるか。保存した設定と Resources のフォルダー名 (Localization/&lt;言語コード&gt;/) に使うので、
        /// ASCII の英字で始まり、英数字・<c>-</c>・<c>_</c> だけで <see cref="MaxCodeLength"/> 文字までに限る。
        /// </summary>
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

        /// <summary>言語コード。<c>default</c> なら <c>(none)</c>。</summary>
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
