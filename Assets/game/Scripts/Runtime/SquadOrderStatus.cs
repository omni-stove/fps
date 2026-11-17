using System;
using Fps.Core;
using UnityEngine;

namespace Fps.Runtime
{
    /// <summary>
    /// プレイヤーが味方の bot に出している指示。<see cref="GameSignals.SquadOrder"/> で UI に渡す。指示を出していない間は信号の値が null。
    /// </summary>
    public readonly struct SquadOrderStatus : IEquatable<SquadOrderStatus>
    {
        public SquadOrderStatus(BotErrandKind order, Vector3 point, Damageable target)
        {
            Order = order;
            Point = point;
            Target = target;
        }

        /// <summary>指示の種類。</summary>
        public BotErrandKind Order { get; }

        /// <summary>守る場所か、狙う敵の狙う点 (ワールド座標)。狙う指示では指示を出したときの点。</summary>
        public Vector3 Point { get; }

        /// <summary>狙う敵。狙う指示でなければ null。</summary>
        public Damageable Target { get; }

        /// <summary>
        /// 今の印の位置。狙う敵がいればその今の狙う点 (敵が動けば動く)、いなければ <see cref="Point"/>。
        /// </summary>
        public Vector3 CurrentPoint => Target != null ? Target.AimPoint : Point;

        public bool Equals(SquadOrderStatus other) =>
            Order == other.Order && Point.Equals(other.Point) && ReferenceEquals(Target, other.Target);

        public override bool Equals(object obj) => obj is SquadOrderStatus other && Equals(other);

        public override int GetHashCode() => ((int)Order * 397) ^ Point.GetHashCode();

        public override string ToString() => $"{Order} at {Point}" + (Target != null ? $" target={Target.Id}" : string.Empty);
    }
}
