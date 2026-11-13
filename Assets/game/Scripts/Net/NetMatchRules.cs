using System;
using Fps.Core;
using Unity.Netcode;

namespace Fps.Net
{
    /// <summary>
    /// クライアントが見た目と HUD に使うルールの値。試合の <see cref="GameRules"/> のうち、クライアントが読むものだけを 1 つの
    /// NetworkVariable で送る (別々に届いて食い違わないよう)。味方撃ち・回復・予備弾の倍率などサーバーの判定にしか使わない値は送らない。
    /// カバーとブラインドファイアは、オーナーが入力を送るか・予測するかを決めるのに使う (判定はサーバーが同じ値で行う)。
    /// </summary>
    public struct NetMatchRules : INetworkSerializable, IEquatable<NetMatchRules>
    {
        public DeathBody DeathBody;
        public DeathCamera DeathCamera;
        public RespawnMode RespawnMode;
        public bool InfiniteReserve;
        public Cover Cover;
        public BlindFire BlindFire;

        public NetMatchRules(DeathBody deathBody, DeathCamera deathCamera, RespawnMode respawnMode, bool infiniteReserve)
            : this(deathBody, deathCamera, respawnMode, infiniteReserve, Cover.Off, BlindFire.Off)
        {
        }

        public NetMatchRules(
            DeathBody deathBody, DeathCamera deathCamera, RespawnMode respawnMode, bool infiniteReserve, Cover cover, BlindFire blindFire)
        {
            DeathBody = deathBody;
            DeathCamera = deathCamera;
            RespawnMode = respawnMode;
            InfiniteReserve = infiniteReserve;
            Cover = cover;
            BlindFire = blindFire;
        }

        /// <summary><see cref="GameRules.Default"/> の値。サーバーから届く前と、試合を始められなかったときはこの値のまま。</summary>
        public static NetMatchRules Default => From(GameRules.Default);

        /// <summary>カバーを使えるか。</summary>
        public bool CoverEnabled => Cover == Cover.On;

        /// <summary>カバーから隠れたまま撃てるか。カバーが使えなければ (届いた値が食い違っていても) false。</summary>
        public bool BlindFireEnabled => CoverEnabled && BlindFire == BlindFire.On;

        /// <summary>rules のうちクライアントへ送る値を写す。</summary>
        public static NetMatchRules From(GameRules rules)
        {
            if (rules == null)
            {
                throw new ArgumentNullException(nameof(rules));
            }

            return new NetMatchRules(
                rules.DeathBody, rules.DeathCamera, rules.RespawnMode, rules.InfiniteReserve, rules.Actions.Cover, rules.Actions.BlindFire);
        }

        public void NetworkSerialize<T>(BufferSerializer<T> serializer)
            where T : IReaderWriter
        {
            serializer.SerializeValue(ref DeathBody);
            serializer.SerializeValue(ref DeathCamera);
            serializer.SerializeValue(ref RespawnMode);
            serializer.SerializeValue(ref InfiniteReserve);
            serializer.SerializeValue(ref Cover);
            serializer.SerializeValue(ref BlindFire);
        }

        public bool Equals(NetMatchRules other)
        {
            return DeathBody == other.DeathBody
                && DeathCamera == other.DeathCamera
                && RespawnMode == other.RespawnMode
                && InfiniteReserve == other.InfiniteReserve
                && Cover == other.Cover
                && BlindFire == other.BlindFire;
        }

        public override bool Equals(object obj)
        {
            return obj is NetMatchRules other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine((int)DeathBody, (int)DeathCamera, (int)RespawnMode, InfiniteReserve, (int)Cover, (int)BlindFire);
        }

        public override string ToString()
        {
            return "deathBody=" + DeathBody + " deathCamera=" + DeathCamera + " respawnMode=" + RespawnMode
                + " infiniteReserve=" + (InfiniteReserve ? "true" : "false")
                + " cover=" + Cover + " blindFire=" + BlindFire;
        }
    }
}
