using System;
using System.Numerics;

namespace Fps.Core
{
    /// <summary>
    /// 付けた印 1 つ。値は変わらず、追う・見失うなどは <see cref="PingBoard"/> が新しい値に置き換える。
    /// </summary>
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

        /// <summary>板 (<see cref="PingBoard"/>) の中で一意の番号。1 から振る。</summary>
        public int Id { get; }

        public PingKind Kind { get; }

        /// <summary>敵の印なら、その敵を表す鍵 (呼び出し側が決める)。場所の印は null。</summary>
        public int? TargetKey { get; }

        /// <summary>印を出す位置。敵を追っている間は敵の位置、見失った後は最後に見た位置。</summary>
        public Vector3 Position { get; }

        /// <summary>敵を追っているか。場所の印と、敵を見失った後は false。</summary>
        public bool IsTracking { get; }

        /// <summary>付けた (付け直した) 時刻 (秒)。</summary>
        public float PlacedAt { get; }

        /// <summary>この時刻 (秒) になったら消える。</summary>
        public float ExpiresAt { get; }

        /// <summary>now での残りの秒数。消える時刻を過ぎていれば 0。</summary>
        public float RemainingSeconds(float now) => Math.Max(0f, ExpiresAt - now);

        /// <summary>now で消える時刻に達しているか。</summary>
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
