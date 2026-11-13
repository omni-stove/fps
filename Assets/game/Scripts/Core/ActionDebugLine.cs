using System;

namespace Fps.Core
{
    /// <summary>アクションのデバッグ表示の 1 行。<see cref="ActionDebugFormat.Lines"/> が作る。</summary>
    public readonly struct ActionDebugLine : IEquatable<ActionDebugLine>
    {
        private readonly string _text;

        /// <summary>画面に出す文字列。null なら空文字。</summary>
        public string Text => _text ?? string.Empty;

        /// <summary>今その項目が有効か。有効な行は色を変えて目立たせる。</summary>
        public bool IsActive { get; }

        public ActionDebugLine(string text, bool isActive)
        {
            _text = text ?? string.Empty;
            IsActive = isActive;
        }

        public bool Equals(ActionDebugLine other)
        {
            return IsActive == other.IsActive && string.Equals(Text, other.Text, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return obj is ActionDebugLine other && Equals(other);
        }

        public static bool operator ==(ActionDebugLine left, ActionDebugLine right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(ActionDebugLine left, ActionDebugLine right)
        {
            return !left.Equals(right);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(StringComparer.Ordinal.GetHashCode(Text), IsActive);
        }

        /// <summary>例: <c>* ADS: ON</c> (有効)、<c>  ADS: OFF</c> (無効)。</summary>
        public override string ToString()
        {
            return (IsActive ? "* " : "  ") + Text;
        }
    }
}
