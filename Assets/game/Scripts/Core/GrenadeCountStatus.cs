using System;

namespace Shooter.Core
{
    public readonly struct GrenadeCountStatus : IEquatable<GrenadeCountStatus>
    {
        // default(GrenadeCountStatus) ではコンストラクタを通らず null になるので、読むときに空文字へ寄せる。
        private readonly string _typeId;

        public GrenadeCountStatus(bool usesGrenades, int remaining, string typeId)
        {
            UsesGrenades = usesGrenades;
            Remaining = Math.Max(0, remaining);
            _typeId = typeId ?? string.Empty;
        }

        public bool UsesGrenades { get; }

        public int Remaining { get; }

        public string TypeId => _typeId ?? string.Empty;

        public bool HasType => TypeId.Length > 0;

        public bool Equals(GrenadeCountStatus other)
        {
            return UsesGrenades == other.UsesGrenades && Remaining == other.Remaining && string.Equals(TypeId, other.TypeId, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return obj is GrenadeCountStatus other && Equals(other);
        }

        public static bool operator ==(GrenadeCountStatus left, GrenadeCountStatus right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(GrenadeCountStatus left, GrenadeCountStatus right)
        {
            return !left.Equals(right);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(UsesGrenades, Remaining, StringComparer.Ordinal.GetHashCode(TypeId));
        }

        public override string ToString()
        {
            return (UsesGrenades ? Remaining.ToString(System.Globalization.CultureInfo.InvariantCulture) : "off") + (HasType ? " " + TypeId : string.Empty);
        }
    }
}
