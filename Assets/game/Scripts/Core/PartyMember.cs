using System;

namespace Shooter.Core
{
    // パーティのメンバーは試合の参加者でないので IParticipants に引けない。そのため Id でなく名前の文字を持つ。
    public readonly struct PartyMember : IEquatable<PartyMember>
    {
        // default ではコンストラクタを通らず null になるので、読むときに空文字へ寄せる。
        private readonly string _name;

        public PartyMember(string name, bool isLeader)
        {
            if (name == null)
            {
                throw new ArgumentNullException(nameof(name));
            }

            if (name.Trim().Length == 0)
            {
                throw new ArgumentException("name must not be blank", nameof(name));
            }

            _name = name;
            IsLeader = isLeader;
        }

        public string Name => _name ?? string.Empty;

        public bool IsLeader { get; }

        public bool Equals(PartyMember other)
        {
            return string.Equals(Name, other.Name, StringComparison.Ordinal) && IsLeader == other.IsLeader;
        }

        public override bool Equals(object obj)
        {
            return obj is PartyMember other && Equals(other);
        }

        public static bool operator ==(PartyMember left, PartyMember right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(PartyMember left, PartyMember right)
        {
            return !left.Equals(right);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(StringComparer.Ordinal.GetHashCode(Name), IsLeader);
        }
    }
}
