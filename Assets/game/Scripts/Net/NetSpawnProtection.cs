using System;
using System.Globalization;
using Fps.Core;
using Unity.Netcode;

namespace Fps.Net
{
    /// <summary>
    /// <see cref="NetTeamDeathmatch.SpawnProtections"/> の 1 行。出現直後の無敵の最中のプレイヤー 1 人と、無敵が切れるまでの秒数。
    /// 無敵でない人の行は置かない。
    /// </summary>
    /// <remarks>
    /// 秒数は HUD に出す粒度 (<see cref="HudFormat.DisplayedSeconds"/> で切り上げた秒) にそろえてから送る。サーバーは毎フレーム書き直すが、
    /// 値が変わったときだけ送られるので 1 秒に 1 度しか届かない。切り上げるので、無敵の間は 1 以上になる。
    /// </remarks>
    public struct NetSpawnProtection : INetworkSerializable, IEquatable<NetSpawnProtection>
    {
        public ulong ClientId;

        /// <summary>無敵が切れるまでの残り秒数 (切り上げ)。1 以上。</summary>
        public int Seconds;

        public NetSpawnProtection(ulong clientId, int seconds)
        {
            ClientId = clientId;
            Seconds = seconds;
        }

        /// <summary>Session の残り秒数 (<see cref="TeamDeathmatchSession.GetSpawnProtectionRemaining"/>) を送る秒数にする。無敵でなければ 0。</summary>
        public static int SecondsFrom(float remaining)
        {
            return HudFormat.DisplayedSeconds(remaining);
        }

        public void NetworkSerialize<T>(BufferSerializer<T> serializer)
            where T : IReaderWriter
        {
            serializer.SerializeValue(ref ClientId);
            serializer.SerializeValue(ref Seconds);
        }

        public bool Equals(NetSpawnProtection other)
        {
            return ClientId == other.ClientId && Seconds == other.Seconds;
        }

        public override bool Equals(object obj)
        {
            return obj is NetSpawnProtection other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(ClientId, Seconds);
        }

        public override string ToString()
        {
            return "client=" + ClientId.ToString(CultureInfo.InvariantCulture)
                + " protected=" + Seconds.ToString(CultureInfo.InvariantCulture) + "s";
        }
    }
}
