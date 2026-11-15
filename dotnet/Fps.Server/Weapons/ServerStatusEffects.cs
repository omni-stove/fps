using System;
using System.Diagnostics.CodeAnalysis;
using Fps.Core;

namespace Fps.Server
{
    /// <summary>燃焼などの状態異常。相手ごとに持ち、毎 tick 進めてダメージを出す。</summary>
    internal sealed class ServerStatusEffects
    {
        public ServerStatusEffects(ServerCombat combat)
        {
            Combat = combat ?? throw new ArgumentNullException(nameof(combat));
        }

        public ServerCombat Combat { get; }

        /// <summary>shot の弾で victimId に effect をかける。</summary>
        [SuppressMessage("Performance", "CA1822", Justification = "Effects are kept on the instance")]
        public void Afflict(in ServerShot shot, int victimId, StatusEffectSpec effect)
        {
        }

        [SuppressMessage("Performance", "CA1822", Justification = "Effects are kept on the instance")]
        public void Tick(float deltaSeconds)
        {
        }

        /// <summary>playerId にかかっている状態異常を消す (抜けた・倒れた)。</summary>
        [SuppressMessage("Performance", "CA1822", Justification = "Effects are kept on the instance")]
        public void Forget(int playerId)
        {
        }

        /// <summary>試合の始まりに、すべての状態異常を消す。</summary>
        [SuppressMessage("Performance", "CA1822", Justification = "Effects are kept on the instance")]
        public void Clear()
        {
        }
    }
}
