using System;
using System.Globalization;

namespace Fps.Core
{
    /// <summary>
    /// スコアボードの 1 行の名前。誰を指すか (<see cref="Kind"/>) と番号か、言葉の入らない名前だけを持ち、文にするのは UI。
    /// <c>default(ScoreboardName)</c> は空の <see cref="ScoreboardNameKind.Literal"/>。
    /// </summary>
    public readonly struct ScoreboardName : IEquatable<ScoreboardName>
    {
        // default では null になるので、読むときに空文字へ寄せる。
        private readonly string _text;

        private ScoreboardName(ScoreboardNameKind kind, int number, string text)
        {
            Kind = kind;
            Number = number;
            _text = text;
        }

        public ScoreboardNameKind Kind { get; }

        /// <summary><see cref="ScoreboardNameKind.Player"/> ならプレイヤー Id、<see cref="ScoreboardNameKind.Target"/>・<see cref="ScoreboardNameKind.Bot"/> なら通し番号。ほかは 0。</summary>
        public int Number { get; }

        /// <summary><see cref="ScoreboardNameKind.Literal"/> の名前。ほかは空文字。</summary>
        public string Text => _text ?? string.Empty;

        public bool IsEmpty => Kind == ScoreboardNameKind.Literal && Text.Length == 0;

        public static ScoreboardName LocalPlayer => new ScoreboardName(ScoreboardNameKind.LocalPlayer, 0, null);

        public static ScoreboardName Player(int playerId)
        {
            return new ScoreboardName(ScoreboardNameKind.Player, playerId, null);
        }

        /// <exception cref="ArgumentOutOfRangeException">number が 1 未満。</exception>
        public static ScoreboardName Target(int number)
        {
            if (number < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(number), number, "number must be 1 or more");
            }

            return new ScoreboardName(ScoreboardNameKind.Target, number, null);
        }

        /// <exception cref="ArgumentOutOfRangeException">number が 1 未満。</exception>
        public static ScoreboardName Bot(int number)
        {
            if (number < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(number), number, "number must be 1 or more");
            }

            return new ScoreboardName(ScoreboardNameKind.Bot, number, null);
        }

        /// <summary>text をそのまま出す名前。null は空。</summary>
        public static ScoreboardName Literal(string text)
        {
            return new ScoreboardName(ScoreboardNameKind.Literal, 0, text ?? string.Empty);
        }

        public bool Equals(ScoreboardName other)
        {
            return Kind == other.Kind && Number == other.Number && string.Equals(Text, other.Text, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return obj is ScoreboardName other && Equals(other);
        }

        public static bool operator ==(ScoreboardName left, ScoreboardName right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(ScoreboardName left, ScoreboardName right)
        {
            return !left.Equals(right);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine((int)Kind, Number, StringComparer.Ordinal.GetHashCode(Text));
        }

        public override string ToString()
        {
            return Kind == ScoreboardNameKind.Literal
                ? "'" + Text + "'"
                : Kind + (Kind == ScoreboardNameKind.LocalPlayer ? string.Empty : " " + Number.ToString(CultureInfo.InvariantCulture));
        }
    }
}
