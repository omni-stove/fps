using System;
using System.Globalization;

namespace Fps.Protocol
{
    /// <summary>
    /// <see cref="IMatchTransport"/> の 1 つの接続を表す Id。実装が接続ごとに 1 以上の値を振り、切れるまで変えない。
    /// クライアントではつないだサーバーへの接続にも Id が振られる。
    /// </summary>
    public readonly struct ConnectionId : IEquatable<ConnectionId>
    {
        public ConnectionId(ulong value)
        {
            Value = value;
        }

        /// <summary>どの接続でもない Id。</summary>
        public static ConnectionId None => default;

        public ulong Value { get; }

        /// <summary><see cref="None"/> でないか。</summary>
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

        /// <summary>ログ向けの <c>#&lt;値&gt;</c>。<see cref="None"/> なら <c>#none</c>。</summary>
        public override string ToString()
        {
            return IsValid ? "#" + Value.ToString(CultureInfo.InvariantCulture) : "#none";
        }
    }
}
