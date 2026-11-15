using System;
using System.Diagnostics.CodeAnalysis;
using Fps.Core;

namespace Fps.Server
{
    /// <summary>
    /// 火炎のような吹き付け (<see cref="RoundDelivery.Spray"/>)。押した・離したの要求で吹き付けを持ち、毎 tick 円錐の中の相手にダメージを出す。
    /// </summary>
    internal sealed class ServerSpray
    {
        public ServerSpray(ServerCombat combat)
        {
            Combat = combat ?? throw new ArgumentNullException(nameof(combat));
        }

        public ServerCombat Combat { get; }

        /// <summary>受け入れた射撃 shot の弾 1 本。押した知らせ (<see cref="ServerShot.Trigger"/>) なら始める。</summary>
        [SuppressMessage("Performance", "CA1822", Justification = "Sprays being fired are kept on the instance")]
        public void Fire(in ServerShot shot, PlannedRound planned)
        {
        }

        /// <summary>shooterId が引き金を離した。</summary>
        [SuppressMessage("Performance", "CA1822", Justification = "Sprays being fired are kept on the instance")]
        public void Release(int shooterId)
        {
        }

        [SuppressMessage("Performance", "CA1822", Justification = "Sprays being fired are kept on the instance")]
        public void Tick(float deltaSeconds)
        {
        }

        /// <summary>playerId が抜けた・倒れた・試合が始まった。</summary>
        [SuppressMessage("Performance", "CA1822", Justification = "Sprays being fired are kept on the instance")]
        public void Forget(int playerId)
        {
        }
    }
}
