using System;
using System.Numerics;

namespace Shooter.Core
{
    public readonly struct PingMarker : IEquatable<PingMarker>
    {
        public PingMarker(int id, PingKind kind, int? targetKey, Vector3 position, bool isTracking, float placedAt, float expiresAt)
        {
            Id = id;
            Kind = kind;
            TargetKey = targetKey;
            Position = position;
            IsTracking = isTracking;
            PlacedAt = placedAt;
            ExpiresAt = expiresAt;
        }

        public int Id { get; }

        public PingKind Kind { get; }

        public int? TargetKey { get; }

        public Vector3 Position { get; }

        public bool IsTracking { get; }

        public float PlacedAt { get; }

        public float ExpiresAt { get; }

        public float RemainingSeconds(float now) => Math.Max(0f, ExpiresAt - now);

        public bool IsExpiredAt(float now) => now >= ExpiresAt;

        internal PingMarker WithPosition(Vector3 position) =>
            new PingMarker(Id, Kind, TargetKey, position, IsTracking, PlacedAt, ExpiresAt);

        internal PingMarker Lost() => new PingMarker(Id, Kind, TargetKey, Position, false, PlacedAt, ExpiresAt);

        public bool Equals(PingMarker other) =>
            Id == other.Id
            && Kind == other.Kind
            && TargetKey == other.TargetKey
            && Position.Equals(other.Position)
            && IsTracking == other.IsTracking
            && PlacedAt.Equals(other.PlacedAt)
            && ExpiresAt.Equals(other.ExpiresAt);

        public override bool Equals(object obj) => obj is PingMarker other && Equals(other);

        public override int GetHashCode() => Id;

        public static bool operator ==(PingMarker left, PingMarker right) => left.Equals(right);

        public static bool operator !=(PingMarker left, PingMarker right) => !left.Equals(right);

        public override string ToString() => PingLog.Describe(this);
    }
}
