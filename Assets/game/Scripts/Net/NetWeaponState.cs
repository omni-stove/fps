using System;
using Unity.Netcode;

namespace Fps.Net
{
    /// <summary>
    /// 今持っている武器の弾倉の残り・予備弾・リロード中かどうかと、持っている武器の番号・数・持ち替え中かどうか。
    /// 射撃・リロード・持ち替えで一緒に変わるので、食い違って届かないよう 1 つの NetworkVariable で送る (<see cref="NetPlayerWeapon"/>)。
    /// </summary>
    public struct NetWeaponState : INetworkSerializable, IEquatable<NetWeaponState>
    {
        public int AmmoInMagazine;
        public int ReserveAmmo;
        public bool IsReloading;

        /// <summary>今持っている武器の番号 (ロードアウトの並び順)。</summary>
        public byte EquippedIndex;

        /// <summary>
        /// 持っている武器の数。装備前は 0。持ち主が次・前の番号を決めるのに使う
        /// (番号だけでは最後の次が 0 番かどうかが分からない)。
        /// </summary>
        public byte WeaponCount;

        /// <summary>持ち替えの途中なら true。この間は撃てずリロードもできない。</summary>
        public bool IsSwitching;

        public NetWeaponState(int ammoInMagazine, int reserveAmmo, bool isReloading, byte equippedIndex, byte weaponCount, bool isSwitching)
        {
            AmmoInMagazine = ammoInMagazine;
            ReserveAmmo = reserveAmmo;
            IsReloading = isReloading;
            EquippedIndex = equippedIndex;
            WeaponCount = weaponCount;
            IsSwitching = isSwitching;
        }

        public void NetworkSerialize<T>(BufferSerializer<T> serializer)
            where T : IReaderWriter
        {
            serializer.SerializeValue(ref AmmoInMagazine);
            serializer.SerializeValue(ref ReserveAmmo);
            serializer.SerializeValue(ref IsReloading);
            serializer.SerializeValue(ref EquippedIndex);
            serializer.SerializeValue(ref WeaponCount);
            serializer.SerializeValue(ref IsSwitching);
        }

        public bool Equals(NetWeaponState other)
        {
            return AmmoInMagazine == other.AmmoInMagazine
                && ReserveAmmo == other.ReserveAmmo
                && IsReloading == other.IsReloading
                && EquippedIndex == other.EquippedIndex
                && WeaponCount == other.WeaponCount
                && IsSwitching == other.IsSwitching;
        }

        public override bool Equals(object obj)
        {
            return obj is NetWeaponState other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(AmmoInMagazine, ReserveAmmo, IsReloading, EquippedIndex, WeaponCount, IsSwitching);
        }

        public override string ToString()
        {
            return "ammo=" + AmmoInMagazine + " reserve=" + ReserveAmmo + " reloading=" + IsReloading
                + " index=" + EquippedIndex + " count=" + WeaponCount + " switching=" + IsSwitching;
        }
    }
}
