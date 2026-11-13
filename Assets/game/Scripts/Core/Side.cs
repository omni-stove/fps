using System;

namespace Fps.Core
{
    /// <summary>
    /// 試合の陣営。0 以上の整数の ID で区別する。2 チームのモードでは 0 が <see cref="Team.Alpha"/>、1 が <see cref="Team.Bravo"/> にあたる。
    /// FFA では参加者ごとに、分隊制では分隊ごとに 1 つの陣営になる。
    /// </summary>
    public readonly struct Side : IEquatable<Side>, IComparable<Side>
    {
        /// <summary><see cref="Team.Alpha"/> にあたる陣営。</summary>
        public static readonly Side Alpha = new Side(0);

        /// <summary><see cref="Team.Bravo"/> にあたる陣営。</summary>
        public static readonly Side Bravo = new Side(1);

        /// <summary>陣営の ID。0 以上。</summary>
        public int Id { get; }

        /// <param name="id">陣営の ID。0 以上。</param>
        public Side(int id)
        {
            if (id < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(id), id, "id must not be negative");
            }

            Id = id;
        }

        /// <summary>Alpha と Bravo のどちらかにあたるなら true。</summary>
        public bool IsTeam => Id == Alpha.Id || Id == Bravo.Id;

        /// <summary>team にあたる陣営。定義されていない値は <see cref="ArgumentOutOfRangeException"/>。</summary>
        public static Side FromTeam(Team team)
        {
            switch (team)
            {
                case Team.Alpha:
                    return Alpha;
                case Team.Bravo:
                    return Bravo;
                default:
                    throw new ArgumentOutOfRangeException(nameof(team), team, "team is not a valid Team");
            }
        }

        /// <summary>team にあたる陣営。null は null のまま。</summary>
        public static Side? FromTeam(Team? team) => team.HasValue ? FromTeam(team.Value) : (Side?)null;

        /// <summary>この陣営にあたる <see cref="Team"/>。Alpha と Bravo のどちらでもなければ false。</summary>
        public bool TryGetTeam(out Team team)
        {
            if (Id == Alpha.Id)
            {
                team = Team.Alpha;
                return true;
            }

            if (Id == Bravo.Id)
            {
                team = Team.Bravo;
                return true;
            }

            team = default;
            return false;
        }

        /// <summary>この陣営にあたる <see cref="Team"/>。Alpha と Bravo のどちらでもなければ <see cref="InvalidOperationException"/>。</summary>
        public Team ToTeam()
        {
            if (!TryGetTeam(out var team))
            {
                throw new InvalidOperationException($"side {Id} is neither Alpha nor Bravo");
            }

            return team;
        }

        /// <summary>side にあたる <see cref="Team"/>。null は null のまま。Alpha と Bravo のどちらでもなければ <see cref="InvalidOperationException"/>。</summary>
        public static Team? ToTeam(Side? side) => side.HasValue ? side.Value.ToTeam() : (Team?)null;

        public bool Equals(Side other) => Id == other.Id;

        public override bool Equals(object obj) => obj is Side other && Equals(other);

        public override int GetHashCode() => Id;

        public int CompareTo(Side other) => Id.CompareTo(other.Id);

        public override string ToString() => "Side " + Id;

        public static bool operator ==(Side left, Side right) => left.Equals(right);

        public static bool operator !=(Side left, Side right) => !left.Equals(right);

        public static bool operator <(Side left, Side right) => left.CompareTo(right) < 0;

        public static bool operator <=(Side left, Side right) => left.CompareTo(right) <= 0;

        public static bool operator >(Side left, Side right) => left.CompareTo(right) > 0;

        public static bool operator >=(Side left, Side right) => left.CompareTo(right) >= 0;
    }
}
