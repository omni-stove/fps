using System;
using Shooter.Core;
using UnityEngine;

namespace Shooter.Runtime
{
    public readonly struct SquadOrderStatus : IEquatable<SquadOrderStatus>
    {
        public SquadOrderStatus(BotErrandKind order, Vector3 point, Damageable target)
        {
            Order = order;
            Point = point;
            Target = target;
        }

        public BotErrandKind Order { get; }

        public Vector3 Point { get; }

        public Damageable Target { get; }

        public Vector3 CurrentPoint => Target != null ? Target.AimPoint : Point;

        public bool Equals(SquadOrderStatus other) =>
            Order == other.Order && Point.Equals(other.Point) && ReferenceEquals(Target, other.Target);

        public override bool Equals(object obj) => obj is SquadOrderStatus other && Equals(other);

        public override int GetHashCode() => ((int)Order * 397) ^ Point.GetHashCode();

        public override string ToString() => $"{Order} at {Point}" + (Target != null ? $" target={Target.Id}" : string.Empty);
    }
}
