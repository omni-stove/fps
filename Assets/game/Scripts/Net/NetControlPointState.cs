using System;
using Fps.Core;
using Unity.Netcode;

namespace Fps.Net
{
    /// <summary>
    /// 拠点 1 つの、占領しているチーム・占領の進み・押し合いの有無。持ち主と進みが別々に届いて食い違わないよう、1 つの NetworkVariable で送る。
    /// 進みは -100 (Bravo) から +100 (Alpha) の百分率に切り詰めて送る (毎フレーム動く値なので、1% 未満の動きでは書き換えない)。
    /// </summary>
    public struct NetControlPointState : INetworkSerializable, IEquatable<NetControlPointState>
    {
        /// <summary>中立を表す <see cref="OwnerCode"/>。</summary>
        public const int NoOwner = -1;

        /// <summary><see cref="ControlPercent"/> の端。</summary>
        public const int FullPercent = 100;

        /// <summary>占領しているチームを int にしたもの。中立なら <see cref="NoOwner"/>。</summary>
        public int OwnerCode;

        /// <summary>占領の進み (百分率)。-100 (Bravo) から +100 (Alpha) で、0 が中立。</summary>
        public sbyte ControlPercent;

        /// <summary>両チームがいて進みが止まっていれば true。</summary>
        public bool Contested;

        /// <summary>中立で誰もいない状態。</summary>
        public static NetControlPointState Neutral => new NetControlPointState { OwnerCode = NoOwner };

        /// <param name="owner">占領しているチーム。中立なら null。</param>
        /// <param name="control">
        /// 占領の進み。-1 (Bravo) から +1 (Alpha)。0 に向けて百分率に切り捨てるので、端の ±100 は ±1 に届いたときだけになる。
        /// 範囲の外は端に寄せ、NaN は 0 にする。
        /// </param>
        /// <param name="contested">両チームがいて進みが止まっていれば true。</param>
        public NetControlPointState(Team? owner, float control, bool contested)
        {
            OwnerCode = owner.HasValue ? (int)owner.Value : NoOwner;
            ControlPercent = ToPercent(control);
            Contested = contested;
        }

        /// <summary>占領しているチーム。中立か、知らない値が届いたなら null。</summary>
        public Team? Owner => OwnerCode == (int)Team.Alpha || OwnerCode == (int)Team.Bravo ? (Team)OwnerCode : (Team?)null;

        /// <summary>占領の進み。-1 (Bravo) から +1 (Alpha)。</summary>
        public float Control => Math.Max(-FullPercent, Math.Min(FullPercent, (int)ControlPercent)) / (float)FullPercent;

        /// <summary>進みが寄っている側のチーム。0 なら null。</summary>
        public Team? LeaningTeam => ControlPercent > 0 ? Team.Alpha : ControlPercent < 0 ? Team.Bravo : (Team?)null;

        /// <summary>寄っている側への進みの大きさ。0 (中立) から 1 (占領し終えた)。</summary>
        public float Fill => Math.Abs(Control);

        public static sbyte ToPercent(float control)
        {
            if (float.IsNaN(control))
            {
                return 0;
            }

            var clamped = Math.Max(-1f, Math.Min(1f, control));
            return (sbyte)(int)Math.Truncate(clamped * FullPercent);
        }

        public void NetworkSerialize<T>(BufferSerializer<T> serializer)
            where T : IReaderWriter
        {
            serializer.SerializeValue(ref OwnerCode);
            serializer.SerializeValue(ref ControlPercent);
            serializer.SerializeValue(ref Contested);
        }

        public bool Equals(NetControlPointState other)
        {
            return OwnerCode == other.OwnerCode && ControlPercent == other.ControlPercent && Contested == other.Contested;
        }

        public override bool Equals(object obj)
        {
            return obj is NetControlPointState other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(OwnerCode, ControlPercent, Contested);
        }

        public override string ToString()
        {
            var owner = Owner;
            return "owner=" + (owner.HasValue ? owner.Value.ToString() : "none") + " control=" + ControlPercent + "% contested=" + Contested;
        }
    }
}
