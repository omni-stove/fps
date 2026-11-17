using System;

namespace Shooter.Core
{
    public readonly struct Participant : IEquatable<Participant>
    {
        // default(Participant) ではコンストラクタを通らず null になるので、読むときに空文字へ寄せる。
        private readonly string _name;

        public int Id { get; }

        // 文言のキーにしないのは、P12 や [BOT] B1 のような言語によらない固有の名前だけを持たせ、Core から文言を出さないため。
        public string Name => _name ?? string.Empty;

        public Team? Team { get; }

        public bool IsLocal { get; }

        public Participant(int id, string name, Team? team, bool isLocal)
        {
            Id = id;
            _name = name ?? string.Empty;
            Team = team;
            IsLocal = isLocal;
        }

        public bool Equals(Participant other)
        {
            return Id == other.Id
                && string.Equals(Name, other.Name, StringComparison.Ordinal)
                && Team == other.Team
                && IsLocal == other.IsLocal;
        }

        public override bool Equals(object obj)
        {
            return obj is Participant other && Equals(other);
        }

        public static bool operator ==(Participant left, Participant right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(Participant left, Participant right)
        {
            return !left.Equals(right);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Id, StringComparer.Ordinal.GetHashCode(Name), Team, IsLocal);
        }

        public override string ToString()
        {
            return Id + ":" + Name + (Team.HasValue ? "(" + Team.Value + ")" : string.Empty) + (IsLocal ? "*" : string.Empty);
        }
    }
}
