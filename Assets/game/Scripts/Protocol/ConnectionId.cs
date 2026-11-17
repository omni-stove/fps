using System;
using System.Globalization;

namespace Shooter.Protocol
{
    public readonly struct ConnectionId : IEquatable<ConnectionId>
    {
        public ConnectionId(ulong value)
        {
            Value = value;
        }

        public static ConnectionId None => default;

        public ulong Value { get; }

        public bool IsValid => Value != 0;

        public static bool operator ==(ConnectionId left, ConnectionId right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(ConnectionId left, ConnectionId right)
        {
            return !left.Equals(right);
        }

        public bool Equals(ConnectionId other)
        {
            return Value == other.Value;
        }

        public override bool Equals(object obj)
        {
            return obj is ConnectionId other && Equals(other);
        }

        public override int GetHashCode()
        {
            return Value.GetHashCode();
        }

        public override string ToString()
        {
            return IsValid ? "#" + Value.ToString(CultureInfo.InvariantCulture) : "#none";
        }
    }
}
