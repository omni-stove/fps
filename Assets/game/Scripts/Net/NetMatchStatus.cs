using System;
using Fps.Core;
using Unity.Netcode;

namespace Fps.Net
{
    /// <summary>
    /// 試合の状態と勝ったチーム。終了と勝者が別々に届いて食い違わないよう、1 つの NetworkVariable で送る。
    /// </summary>
    public struct NetMatchStatus : INetworkSerializable, IEquatable<NetMatchStatus>
    {
        /// <summary>勝者がいない (終了前か引き分け) ことを表す <see cref="WinnerCode"/>。</summary>
        public const int NoWinner = -1;

        public GameModeState State;

        /// <summary>勝ったチームを int にしたもの。いなければ <see cref="NoWinner"/>。</summary>
        public int WinnerCode;

        public NetMatchStatus(GameModeState state, Team? winner)
        {
            State = state;
            WinnerCode = winner.HasValue ? (int)winner.Value : NoWinner;
        }

        public Team? Winner => WinnerCode == NoWinner ? (Team?)null : (Team)WinnerCode;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer)
            where T : IReaderWriter
        {
            serializer.SerializeValue(ref State);
            serializer.SerializeValue(ref WinnerCode);
        }

        public bool Equals(NetMatchStatus other)
        {
            return State == other.State && WinnerCode == other.WinnerCode;
        }

        public override bool Equals(object obj)
        {
            return obj is NetMatchStatus other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine((int)State, WinnerCode);
        }
    }
}
