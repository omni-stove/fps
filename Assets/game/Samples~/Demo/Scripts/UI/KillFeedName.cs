using System;
using Shooter.Core;

namespace Shooter.Runtime
{
    public readonly struct KillFeedName : IEquatable<KillFeedName>
    {
        // default(KillFeedName) ではコンストラクタを通らず null になるので、読むときに空文字へ寄せる。
        private readonly string _text;

        public string Text => _text ?? string.Empty;

        public Team? Team { get; }

        public bool IsLocal { get; }

        public KillFeedName(string text, Team? team, bool isLocal)
        {
            _text = text ?? string.Empty;
            Team = team;
            IsLocal = isLocal;
        }

        public KillFeedName(Participant participant)
            : this(participant.Name, participant.Team, participant.IsLocal)
        {
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
