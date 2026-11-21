using System;
using System.Globalization;
using Shooter.Protocol;

namespace Shooter.Server
{
    internal readonly struct ServerLockTarget : IEquatable<ServerLockTarget>
    {
        public ServerLockTarget(LockTargetKind kind, int id)
        {
            Kind = kind;
            Id = id;
        }

        public LockTargetKind Kind { get; }

        // Player なら人の Id、Vehicle なら乗り物の番号。
        public int Id { get; }

        public bool IsVehicle => Kind == LockTargetKind.Vehicle;

        public static ServerLockTarget Player(int playerId) => new ServerLockTarget(LockTargetKind.Player, playerId);

        public static ServerLockTarget Vehicle(int vehicle) => new ServerLockTarget(LockTargetKind.Vehicle, vehicle);

        public bool Equals(ServerLockTarget other) => Kind == other.Kind && Id == other.Id;

        public override bool Equals(object obj) => obj is ServerLockTarget other && Equals(other);

        public override int GetHashCode() => HashCode.Combine(Kind, Id);

        public static bool operator ==(ServerLockTarget left, ServerLockTarget right) => left.Equals(right);

        public static bool operator !=(ServerLockTarget left, ServerLockTarget right) => !left.Equals(right);

        // ログの target= に書く形。人は今までどおり Id だけ、乗り物は v を付けて人の Id と見分ける。
        public override string ToString()
        {
            var id = Id.ToString(CultureInfo.InvariantCulture);
            return IsVehicle ? "v" + id : id;
        }
    }
}
