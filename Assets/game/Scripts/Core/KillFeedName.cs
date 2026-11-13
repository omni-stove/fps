using System;

namespace Fps.Core
{
    /// <summary>
    /// キルフィードの 1 行に出す、倒した側か倒された側の名前。チームが分かれば色を付け、この画面のプレイヤーなら行を強調する。
    /// </summary>
    public readonly struct KillFeedName : IEquatable<KillFeedName>
    {
        // default(KillFeedName) ではコンストラクタを通らず null になるので、読むときに空文字へ寄せる。
        private readonly string _text;

        /// <summary>出す名前。null にならない。</summary>
        public string Text => _text ?? string.Empty;

        /// <summary>チーム。チームの無い対戦 (オフライン) なら null。</summary>
        public Team? Team { get; }

        /// <summary>この画面で操作しているプレイヤーか。</summary>
        public bool IsLocal { get; }

        /// <param name="text">名前。null は空文字。</param>
        /// <param name="team">チーム。無ければ null。</param>
        /// <param name="isLocal">この画面のプレイヤーか。</param>
        public KillFeedName(string text, Team? team, bool isLocal)
        {
            _text = text ?? string.Empty;
            Team = team;
            IsLocal = isLocal;
        }

        public bool Equals(KillFeedName other)
        {
            return string.Equals(Text, other.Text, StringComparison.Ordinal) && Team == other.Team && IsLocal == other.IsLocal;
        }

        public override bool Equals(object obj)
        {
            return obj is KillFeedName other && Equals(other);
        }

        public static bool operator ==(KillFeedName left, KillFeedName right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(KillFeedName left, KillFeedName right)
        {
            return !left.Equals(right);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(StringComparer.Ordinal.GetHashCode(Text), Team, IsLocal);
        }

        public override string ToString()
        {
            return Text + (Team.HasValue ? "(" + Team.Value + ")" : string.Empty) + (IsLocal ? "*" : string.Empty);
        }
    }
}
