using System;
using Fps.Core;
using Unity.Netcode;

namespace Fps.Net
{
    /// <summary>
    /// クライアントが見た目と HUD に使うルールの値。試合の <see cref="GameRules"/> のうち、クライアントが読むものだけを 1 つの
    /// NetworkVariable で送る (別々に届いて食い違わないよう)。味方撃ち・回復・予備弾の倍率などサーバーの判定にしか使わない値は送らない。
    /// </summary>
    public struct NetMatchRules : INetworkSerializable, IEquatable<NetMatchRules>
    {
        public DeathBody DeathBody;
        public DeathCamera DeathCamera;
        public RespawnMode RespawnMode;
        public bool InfiniteReserve;

        public NetMatchRules(DeathBody deathBody, DeathCamera deathCamera, RespawnMode respawnMode, bool infiniteReserve)
        {
            DeathBody = deathBody;
            DeathCamera = deathCamera;
            RespawnMode = respawnMode;
            InfiniteReserve = infiniteReserve;
        }

        /// <summary><see cref="GameRules.Default"/> の値。サーバーから届く前と、試合を始められなかったときはこの値のまま。</summary>
        public static NetMatchRules Default => From(GameRules.Default);

        /// <summary>rules のうちクライアントへ送る値を写す。</summary>
        public static NetMatchRules From(GameRules rules)
        {
            if (rules == null)
            {
                throw new ArgumentNullException(nameof(rules));
            }

            return new NetMatchRules(rules.DeathBody, rules.DeathCamera, rules.RespawnMode, rules.InfiniteReserve);
        }

        public void NetworkSerialize<T>(BufferSerializer<T> serializer)
            where T : IReaderWriter
        {
            serializer.SerializeValue(ref DeathBody);
            serializer.SerializeValue(ref DeathCamera);
            serializer.SerializeValue(ref RespawnMode);
            serializer.SerializeValue(ref InfiniteReserve);
        }

        public bool Equals(NetMatchRules other)
        {
            return DeathBody == other.DeathBody
                && DeathCamera == other.DeathCamera
                && RespawnMode == other.RespawnMode
                && InfiniteReserve == other.InfiniteReserve;
        }

        public override bool Equals(object obj)
        {
            return obj is NetMatchRules other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine((int)DeathBody, (int)DeathCamera, (int)RespawnMode, InfiniteReserve);
        }

        public override string ToString()
        {
            return "deathBody=" + DeathBody + " deathCamera=" + DeathCamera + " respawnMode=" + RespawnMode
                + " infiniteReserve=" + (InfiniteReserve ? "true" : "false");
        }
    }
}
