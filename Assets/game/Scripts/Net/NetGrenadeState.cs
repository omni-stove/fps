using System;
using Unity.Netcode;

namespace Fps.Net
{
    /// <summary>
    /// 持ち主に同期するグレネードの状態。試合のルールでグレネードを使うか (<see cref="Enabled"/>) と残りの数。
    /// サーバーが <see cref="ServerGrenadeSupply"/> から作り、<see cref="NetPlayer"/> の NetworkVariable で持ち主にだけ送る。
    /// 使うかどうかを数と一緒に送るのは、ルールが届く前に数だけが届いて「残り 0」と出さないため。
    /// </summary>
    public struct NetGrenadeState : INetworkSerializable, IEquatable<NetGrenadeState>
    {
        /// <summary>この試合のルールでグレネードを使うか。false なら HUD に数を出さない。</summary>
        public bool Enabled;

        /// <summary>残りの数。0 以上。</summary>
        public int Remaining;

        public NetGrenadeState(bool enabled, int remaining)
        {
            Enabled = enabled;
            Remaining = remaining;
        }

        /// <summary>グレネードを使わない状態。参加前と、サーバーが配る前はこの値。</summary>
        public static NetGrenadeState None => new NetGrenadeState(false, 0);

        /// <summary>今投げてよいか (使う・残りがある)。持ち主が要求を送る前の見込みにだけ使い、判断はサーバーが行う。</summary>
        public bool CanThrow => Enabled && Remaining > 0;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer)
            where T : IReaderWriter
        {
            serializer.SerializeValue(ref Enabled);
            serializer.SerializeValue(ref Remaining);
        }

        public bool Equals(NetGrenadeState other)
        {
            return Enabled == other.Enabled && Remaining == other.Remaining;
        }

        public override bool Equals(object obj)
        {
            return obj is NetGrenadeState other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Enabled, Remaining);
        }

        public override string ToString()
        {
            return "enabled=" + (Enabled ? "true" : "false") + " remaining=" + Remaining;
        }
    }
}
