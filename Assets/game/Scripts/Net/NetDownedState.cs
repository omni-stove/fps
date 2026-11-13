using System;
using Fps.Core;
using Unity.Netcode;

namespace Fps.Net
{
    /// <summary>
    /// ダウン中かどうかと、出血で倒れるまでの残り秒数・蘇生の進み。ダウンと起き上がりで一緒に変わるので、
    /// 食い違って届かないよう 1 つの NetworkVariable で送る (<see cref="NetPlayer"/>)。
    /// </summary>
    /// <remarks>
    /// 秒数と進みは HUD に出す粒度 (切り上げた秒・切り捨てた百分率) にそろえてから送る。サーバーは毎フレーム書き直すが、
    /// 値が変わったときだけ送られるので、出血は 1 秒に 1 度、蘇生は 1% ごとにしか届かない。
    /// </remarks>
    public struct NetDownedState : INetworkSerializable, IEquatable<NetDownedState>
    {
        /// <summary>ダウンしていない状態。NetworkVariable の初期値。</summary>
        public static readonly NetDownedState None = default;

        public bool IsDowned;

        /// <summary>出血で倒れるまでの残り秒数 (切り上げ)。ダウンしていなければ 0。</summary>
        public int BleedoutSeconds;

        /// <summary>味方に起こされている進み (0〜100 の百分率、切り捨て)。誰も起こしていなければ 0。</summary>
        public byte RevivePercent;

        public NetDownedState(bool isDowned, int bleedoutSeconds, byte revivePercent)
        {
            IsDowned = isDowned;
            BleedoutSeconds = bleedoutSeconds;
            RevivePercent = revivePercent;
        }

        /// <summary>
        /// Session の値 (<see cref="TeamDeathmatchSession.IsDowned"/>・<see cref="TeamDeathmatchSession.GetDownedRemaining"/>・
        /// <see cref="TeamDeathmatchSession.GetReviveProgress"/>) から作る。ダウンしていなければ <see cref="None"/>。
        /// </summary>
        public static NetDownedState From(bool isDowned, float? bleedoutRemaining, float reviveProgress)
        {
            if (!isDowned)
            {
                return None;
            }

            var seconds = bleedoutRemaining.HasValue ? HudFormat.DisplayedSeconds(bleedoutRemaining.Value) : 0;
            return new NetDownedState(true, seconds, (byte)HudFormat.RevivePercent(reviveProgress));
        }

        /// <summary>session の playerId の今の値から作る。</summary>
        public static NetDownedState From(TeamDeathmatchSession session, int playerId)
        {
            if (session == null)
            {
                throw new ArgumentNullException(nameof(session));
            }

            return From(session.IsDowned(playerId), session.GetDownedRemaining(playerId), session.GetReviveProgress(playerId));
        }

        public void NetworkSerialize<T>(BufferSerializer<T> serializer)
            where T : IReaderWriter
        {
            serializer.SerializeValue(ref IsDowned);
            serializer.SerializeValue(ref BleedoutSeconds);
            serializer.SerializeValue(ref RevivePercent);
        }

        public bool Equals(NetDownedState other)
        {
            return IsDowned == other.IsDowned && BleedoutSeconds == other.BleedoutSeconds && RevivePercent == other.RevivePercent;
        }

        public override bool Equals(object obj)
        {
            return obj is NetDownedState other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(IsDowned, BleedoutSeconds, RevivePercent);
        }

        public override string ToString()
        {
            return "downed=" + (IsDowned ? "true" : "false") + " bleedout=" + BleedoutSeconds + " revive=" + RevivePercent + "%";
        }
    }
}
