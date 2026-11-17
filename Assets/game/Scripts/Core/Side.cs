using System;

namespace Shooter.Core
{
    public readonly struct Side : IEquatable<Side>, IComparable<Side>
    {
        public static readonly Side Alpha = new Side(0);

        public static readonly Side Bravo = new Side(1);

        public int Id { get; }

        public Side(int id)
        {
            if (id < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(id), id, "id must not be negative");
            }

            Id = id;
        }

        public bool IsTeam => Id == Alpha.Id || Id == Bravo.Id;

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

        public static Side? FromTeam(Team? team) => team.HasValue ? FromTeam(team.Value) : (Side?)null;

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

        public Team ToTeam()
        {
            if (!TryGetTeam(out var team))
            {
                throw new InvalidOperationException($"side {Id} is neither Alpha nor Bravo");
            }

            return team;
        }

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
