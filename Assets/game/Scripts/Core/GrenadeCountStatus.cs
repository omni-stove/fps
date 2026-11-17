using System;

namespace Fps.Core
{
    /// <summary>
    /// グレネードの残り。プレイヤーの部品にグレネードを投げる役 (GrenadeThrower) が無い画面 (ネット対戦) が、届いた数をそのまま出す。
    /// 種類を切り替えて持っているときは、投げる種類の Id (<see cref="TypeId"/>) も持つ。名前にするのは UI。
    /// </summary>
    public readonly struct GrenadeCountStatus : IEquatable<GrenadeCountStatus>
    {
        // default(GrenadeCountStatus) ではコンストラクタを通らず null になるので、読むときに空文字へ寄せる。
        private readonly string _typeId;

        /// <param name="usesGrenades">ルールでグレネードを使うか。</param>
        /// <param name="remaining">投げる種類の残り。負の数は 0 にする。</param>
        /// <param name="typeId">投げる種類の Id。1 種類しか持たないときは null か空文字。</param>
        public GrenadeCountStatus(bool usesGrenades, int remaining, string typeId)
        {
            UsesGrenades = usesGrenades;
            Remaining = Math.Max(0, remaining);
            _typeId = typeId ?? string.Empty;
        }

        public bool UsesGrenades { get; }

        public int Remaining { get; }

        /// <summary>投げる種類の Id。1 種類しか持たないときは空文字。</summary>
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
