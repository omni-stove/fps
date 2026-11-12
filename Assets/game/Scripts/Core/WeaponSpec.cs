using System;

namespace Fps.Core
{
    /// <summary>
    /// 武器 1 種類の不変な設定。生成時に値を検査し、不正なら例外を投げる。
    /// </summary>
    public sealed class WeaponSpec
    {
        /// <summary>武器の識別子。小文字英数字・ハイフン・アンダースコアのみ (例: rifle, handgun)。</summary>
        public string Id { get; }

        /// <summary>表示名。生成時に空なら <see cref="Id"/>。</summary>
        public string DisplayName { get; }

        /// <summary>射撃モード。</summary>
        public FireMode FireMode { get; }

        /// <summary>弾倉の容量。1 以上。</summary>
        public int MagazineSize { get; }

        /// <summary>予備弾の初期値。0 以上。</summary>
        public int ReserveAmmo { get; }

        /// <summary>射撃から次に撃てるまでの秒数。0 以上の有限値。</summary>
        public float FireInterval { get; }

        /// <summary>リロード開始から補充までの秒数。0 以上の有限値。</summary>
        public float ReloadSeconds { get; }

        /// <summary>1 発あたりのダメージ。0 以上。</summary>
        public int Damage { get; }

        /// <summary>射程 (m)。0 より大きい有限値。</summary>
        public float Range { get; }

        /// <summary>距離によるダメージ減衰。null にならない。</summary>
        public DamageFalloff Falloff { get; }

        /// <param name="id">識別子。前後の空白は除く。除いた後が空でなく、小文字英数字・ハイフン・アンダースコアのみ。</param>
        /// <param name="displayName">表示名。null・空白のみなら <paramref name="id"/> を使う。</param>
        /// <param name="fireMode">射撃モード。</param>
        /// <param name="magazineSize">弾倉の容量。1 以上。</param>
        /// <param name="reserveAmmo">予備弾の初期値。0 以上。</param>
        /// <param name="fireInterval">連射間隔 (秒)。0 以上の有限値。</param>
        /// <param name="reloadSeconds">リロード時間 (秒)。0 以上の有限値。</param>
        /// <param name="damage">1 発あたりのダメージ。0 以上。</param>
        /// <param name="range">射程 (m)。0 より大きい有限値。</param>
        /// <param name="falloff">距離減衰。null 不可 (減衰しないなら <see cref="DamageFalloff.None"/>)。</param>
        public WeaponSpec(
            string id,
            string displayName,
            FireMode fireMode,
            int magazineSize,
            int reserveAmmo,
            float fireInterval,
            float reloadSeconds,
            int damage,
            float range,
            DamageFalloff falloff)
        {
            var normalizedId = NormalizeId(id);

            if (fireMode != FireMode.Auto && fireMode != FireMode.Semi)
            {
                throw new ArgumentOutOfRangeException(nameof(fireMode), fireMode, "fireMode must be Auto or Semi");
            }

            if (magazineSize <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(magazineSize), magazineSize, "magazineSize must be positive");
            }

            if (reserveAmmo < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(reserveAmmo), reserveAmmo, "reserveAmmo must not be negative");
            }

            if (!IsNonNegativeFinite(fireInterval))
            {
                throw new ArgumentOutOfRangeException(nameof(fireInterval), fireInterval, "fireInterval must be a non-negative finite number");
            }

            if (!IsNonNegativeFinite(reloadSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(reloadSeconds), reloadSeconds, "reloadSeconds must be a non-negative finite number");
            }

            if (damage < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(damage), damage, "damage must not be negative");
            }

            if (!(range > 0f) || float.IsInfinity(range))
            {
                throw new ArgumentOutOfRangeException(nameof(range), range, "range must be positive and finite");
            }

            Id = normalizedId;
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? normalizedId : displayName.Trim();
            FireMode = fireMode;
            MagazineSize = magazineSize;
            ReserveAmmo = reserveAmmo;
            FireInterval = fireInterval;
            ReloadSeconds = reloadSeconds;
            Damage = damage;
            Range = range;
            Falloff = falloff ?? throw new ArgumentNullException(nameof(falloff));
        }

        private static string NormalizeId(string id)
        {
            if (id == null)
            {
                throw new ArgumentNullException(nameof(id));
            }

            var trimmed = id.Trim();
            if (trimmed.Length == 0)
            {
                throw new ArgumentException("id must not be empty", nameof(id));
            }

            foreach (var c in trimmed)
            {
                if (!IsIdChar(c))
                {
                    throw new ArgumentException(
                        "id must contain only lowercase letters, digits, '-' and '_': '" + trimmed + "'", nameof(id));
                }
            }

            return trimmed;
        }

        private static bool IsIdChar(char c)
        {
            return (c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || c == '-' || c == '_';
        }

        private static bool IsNonNegativeFinite(float value)
        {
            return value >= 0f && !float.IsInfinity(value);
        }
    }
}
