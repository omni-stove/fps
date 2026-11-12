using System;
using Unity.Netcode;

namespace Fps.Net
{
    /// <summary>
    /// 武器の弾倉の残り・予備弾・リロード中かどうか。射撃とリロードで一緒に変わるので、食い違って届かないよう
    /// 1 つの NetworkVariable で送る (<see cref="NetPlayerWeapon"/>)。
    /// </summary>
    public struct NetWeaponState : INetworkSerializable, IEquatable<NetWeaponState>
    {
        public int AmmoInMagazine;
        public int ReserveAmmo;
        public bool IsReloading;

        public NetWeaponState(int ammoInMagazine, int reserveAmmo, bool isReloading)
        {
            AmmoInMagazine = ammoInMagazine;
            ReserveAmmo = reserveAmmo;
            IsReloading = isReloading;
        }

        public void NetworkSerialize<T>(BufferSerializer<T> serializer)
            where T : IReaderWriter
        {
            serializer.SerializeValue(ref AmmoInMagazine);
            serializer.SerializeValue(ref ReserveAmmo);
            serializer.SerializeValue(ref IsReloading);
        }

        public bool Equals(NetWeaponState other)
        {
            return AmmoInMagazine == other.AmmoInMagazine && ReserveAmmo == other.ReserveAmmo && IsReloading == other.IsReloading;
        }

        public override bool Equals(object obj)
        {
            return obj is NetWeaponState other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(AmmoInMagazine, ReserveAmmo, IsReloading);
        }

        public override string ToString()
        {
            return "ammo=" + AmmoInMagazine + " reserve=" + ReserveAmmo + " reloading=" + IsReloading;
        }
    }
}
