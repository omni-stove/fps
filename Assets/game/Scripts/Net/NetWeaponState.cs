using System;
using Unity.Netcode;

namespace Fps.Net
{
    /// <summary>
    /// 今持っている武器の弾倉の残り・予備弾・リロード中かどうかと、持っている武器の番号・数・持ち替え中かどうか、
    /// アクティブリロードの進みを描くための値 (リロードの開始時刻・長さ・成功になる幅)。
    /// 射撃・リロード・持ち替えで一緒に変わるので、食い違って届かないよう 1 つの NetworkVariable で送る (<see cref="NetPlayerWeapon"/>)。
    /// </summary>
    /// <remarks>
    /// 進みそのものは時間で変わるので送らず、開始時刻 (サーバーの時刻) と長さから受け取った側が <see cref="ReloadProgressAt"/> で求める。
    /// 判定はサーバーの <see cref="Fps.Core.ServerWeapon"/> が自分の進みで行い、これらの値は表示と「判定を頼んでよいか」にだけ使う。
    /// </remarks>
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

        /// <summary>
        /// 今のリロードを始めたサーバーの時刻 (NetworkManager.ServerTime の秒)。進みを出さないとき (<see cref="ReloadSeconds"/> が 0) は 0。
        /// </summary>
        public double ReloadStartedAt;

        /// <summary>
        /// 今のリロードにかかる秒数 (アクティブリロードに失敗していれば遅れた分を含む)。
        /// アクティブリロードが Off のルール・リロード中でないときは 0 で、進みのバーを出さない。
        /// </summary>
        public float ReloadSeconds;

        /// <summary>アクティブリロードで成功になる幅の始め (進みと同じ 0〜1)。判定を済ませた後・進みを出さないときは 0。</summary>
        public float ActiveReloadWindowStart;

        /// <summary>
        /// アクティブリロードで成功になる幅の終わり (進みと同じ 0〜1)。判定を済ませた後・進みを出さないときは 0。
        /// <see cref="ActiveReloadWindowStart"/> より大きいときだけ、まだ判定を頼める。
        /// </summary>
        public float ActiveReloadWindowEnd;

        public NetWeaponState(int ammoInMagazine, int reserveAmmo, bool isReloading, byte equippedIndex, byte weaponCount, bool isSwitching)
            : this(ammoInMagazine, reserveAmmo, isReloading, equippedIndex, weaponCount, isSwitching, 0d, 0f, 0f, 0f)
        {
        }

        public NetWeaponState(
            int ammoInMagazine,
            int reserveAmmo,
            bool isReloading,
            byte equippedIndex,
            byte weaponCount,
            bool isSwitching,
            double reloadStartedAt,
            float reloadSeconds,
            float activeReloadWindowStart,
            float activeReloadWindowEnd)
        {
            AmmoInMagazine = ammoInMagazine;
            ReserveAmmo = reserveAmmo;
            IsReloading = isReloading;
            EquippedIndex = equippedIndex;
            WeaponCount = weaponCount;
            IsSwitching = isSwitching;
            ReloadStartedAt = reloadStartedAt;
            ReloadSeconds = reloadSeconds;
            ActiveReloadWindowStart = activeReloadWindowStart;
            ActiveReloadWindowEnd = activeReloadWindowEnd;
        }

        /// <summary>
        /// リロードの進みのバーを出せる値か。リロード中で、<see cref="ReloadSeconds"/> が正の有限値、<see cref="ReloadStartedAt"/> が有限値のとき true。
        /// </summary>
        public bool HasReloadProgress =>
            IsReloading
            && ReloadSeconds > 0f && !float.IsInfinity(ReloadSeconds)
            && !double.IsNaN(ReloadStartedAt) && !double.IsInfinity(ReloadStartedAt);

        /// <summary>
        /// 今のリロードでまだアクティブリロードの判定を頼めるか。進みを出せて (<see cref="HasReloadProgress"/>)、
        /// 成功になる幅が 0〜1 の中で長さを持つとき true。
        /// </summary>
        public bool HasActiveReloadWindow =>
            HasReloadProgress
            && ActiveReloadWindowStart >= 0f
            && ActiveReloadWindowEnd <= 1f
            && ActiveReloadWindowEnd > ActiveReloadWindowStart;

        /// <summary>
        /// サーバーの時刻 serverTime でのリロードの進み (経過 / <see cref="ReloadSeconds"/>) を 0〜1 に収めて返す。
        /// <see cref="HasReloadProgress"/> が false か serverTime が有限値でなければ 0。
        /// </summary>
        public float ReloadProgressAt(double serverTime)
        {
            if (!HasReloadProgress || double.IsNaN(serverTime) || double.IsInfinity(serverTime))
            {
                return 0f;
            }

            var progress = (serverTime - ReloadStartedAt) / ReloadSeconds;
            return (float)Math.Min(1d, Math.Max(0d, progress));
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
            serializer.SerializeValue(ref ReloadStartedAt);
            serializer.SerializeValue(ref ReloadSeconds);
            serializer.SerializeValue(ref ActiveReloadWindowStart);
            serializer.SerializeValue(ref ActiveReloadWindowEnd);
        }

        public bool Equals(NetWeaponState other)
        {
            return AmmoInMagazine == other.AmmoInMagazine
                && ReserveAmmo == other.ReserveAmmo
                && IsReloading == other.IsReloading
                && EquippedIndex == other.EquippedIndex
                && WeaponCount == other.WeaponCount
                && IsSwitching == other.IsSwitching
                && ReloadStartedAt.Equals(other.ReloadStartedAt)
                && ReloadSeconds.Equals(other.ReloadSeconds)
                && ActiveReloadWindowStart.Equals(other.ActiveReloadWindowStart)
                && ActiveReloadWindowEnd.Equals(other.ActiveReloadWindowEnd);
        }

        public override bool Equals(object obj)
        {
            return obj is NetWeaponState other && Equals(other);
        }

        public override int GetHashCode()
        {
            var hash = new HashCode();
            hash.Add(AmmoInMagazine);
            hash.Add(ReserveAmmo);
            hash.Add(IsReloading);
            hash.Add(EquippedIndex);
            hash.Add(WeaponCount);
            hash.Add(IsSwitching);
            hash.Add(ReloadStartedAt);
            hash.Add(ReloadSeconds);
            hash.Add(ActiveReloadWindowStart);
            hash.Add(ActiveReloadWindowEnd);
            return hash.ToHashCode();
        }

        public override string ToString()
        {
            return "ammo=" + AmmoInMagazine + " reserve=" + ReserveAmmo + " reloading=" + IsReloading
                + " index=" + EquippedIndex + " count=" + WeaponCount + " switching=" + IsSwitching
                + " reloadStartedAt=" + ReloadStartedAt + " reloadSeconds=" + ReloadSeconds
                + " window=" + ActiveReloadWindowStart + "-" + ActiveReloadWindowEnd;
        }
    }
}
