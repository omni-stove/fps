using System;
using System.Globalization;

namespace Shooter.Runtime
{
    // どれも null を許すのは、通信路によって測れない値 (ロスなど) や測り始める前の値を、0 と取り違えずに「分からない」と出すため。
    public readonly struct NetStatsSignal : IEquatable<NetStatsSignal>
    {
        public const int MaxLossPercent = 100;

        public NetStatsSignal(int? pingMs, int? lossPercent, int? serverTick)
        {
            if (pingMs < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(pingMs), pingMs, "ping must not be negative");
            }

            if (lossPercent < 0 || lossPercent > MaxLossPercent)
            {
                throw new ArgumentOutOfRangeException(nameof(lossPercent), lossPercent, "loss must be between 0 and 100");
            }

            if (serverTick < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(serverTick), serverTick, "server tick must not be negative");
            }

            PingMs = pingMs;
            LossPercent = lossPercent;
            ServerTick = serverTick;
        }

        public int? PingMs { get; }

        public int? LossPercent { get; }

        public int? ServerTick { get; }

        public bool Equals(NetStatsSignal other)
        {
            return PingMs == other.PingMs && LossPercent == other.LossPercent && ServerTick == other.ServerTick;
        }

        public override bool Equals(object obj)
        {
            return obj is NetStatsSignal other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(PingMs, LossPercent, ServerTick);
        }

        public static bool operator ==(NetStatsSignal left, NetStatsSignal right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(NetStatsSignal left, NetStatsSignal right)
        {
            return !left.Equals(right);
        }

        public override string ToString()
        {
            return "ping=" + Describe(PingMs) + "ms loss=" + Describe(LossPercent) + "% tick=" + Describe(ServerTick);
        }

        private static string Describe(int? value)
        {
            return value.HasValue ? value.Value.ToString(CultureInfo.InvariantCulture) : "-";
        }
    }
}
