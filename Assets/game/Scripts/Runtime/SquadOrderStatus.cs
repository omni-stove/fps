using Fps.Core;
using UnityEngine;

namespace Fps.Runtime
{
    /// <summary>
    /// プレイヤーが味方の bot に出している指示。<see cref="GameSignals.SquadOrder"/> で UI に渡す。指示を出していない間は信号の値が null。
    /// </summary>
    public readonly struct SquadOrderStatus
    {
        public SquadOrderStatus(BotErrandKind order, Vector3 point, Damageable target)
        {
            Order = order;
            Point = point;
            Target = target;
        }

        /// <summary>指示の種類。</summary>
        public BotErrandKind Order { get; }

        /// <summary>守る場所か、狙う敵の狙う点 (ワールド座標)。</summary>
        public Vector3 Point { get; }

        /// <summary>狙う敵。狙う指示でなければ null。</summary>
        public Damageable Target { get; }
    }
}
