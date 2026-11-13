using System;

namespace Fps.Core
{
    /// <summary>
    /// 武器 1 種類の不変な設定。生成時に値を検査し、不正なら例外を投げる。
    /// </summary>
    public sealed class WeaponSpec
    {
        /// <summary>武器の識別子。小文字・ハイフン区切りの UUID (<c>Guid.ToString("D")</c> の形)。</summary>
        public string Id { get; }

        /// <summary>表示名。生成時に空なら <see cref="Id"/>。</summary>
        public string DisplayName { get; }

        /// <summary>武器の種別。ロードアウトの制約と表示にだけ使う。</summary>
        public WeaponCategory Category { get; }

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

        /// <summary>
        /// 覗き込み (ADS) し切ったときのカメラの縦の視野角 (度)。0 より大きく 180 未満。
        /// 生成時に指定しなければ <see cref="AdsZoom.DefaultFieldOfView"/>。クライアントの見た目にだけ使い、サーバーは見ない。
        /// </summary>
        public float AdsFieldOfView { get; }

        /// <param name="id">識別子。前後の空白は除く。除いた後が小文字・ハイフン区切りの UUID (例: 3f2b8c1e-0a4d-4c6e-9b1a-7d2e5f8c0b13)。</param>
        /// <param name="displayName">表示名。null・空白のみなら <paramref name="id"/> を使う。</param>
        /// <param name="category">武器の種別。定義済みの値のみ。</param>
        /// <param name="fireMode">射撃モード。</param>
        /// <param name="magazineSize">弾倉の容量。1 以上。</param>
        /// <param name="reserveAmmo">予備弾の初期値。0 以上。</param>
        /// <param name="fireInterval">連射間隔 (秒)。0 以上の有限値。</param>
        /// <param name="reloadSeconds">リロード時間 (秒)。0 以上の有限値。</param>
        /// <param name="damage">1 発あたりのダメージ。0 以上。</param>
        /// <param name="range">射程 (m)。0 より大きい有限値。</param>
        /// <param name="falloff">距離減衰。null 不可 (減衰しないなら <see cref="DamageFalloff.None"/>)。</param>
        /// <param name="adsFieldOfView">ADS し切ったときの視野角 (度)。0 より大きく 180 未満。null なら <see cref="AdsZoom.DefaultFieldOfView"/>。</param>
        public WeaponSpec(
            string id,
            string displayName,
            WeaponCategory category,
            FireMode fireMode,
            int magazineSize,
            int reserveAmmo,
            float fireInterval,
            float reloadSeconds,
            int damage,
            float range,
            DamageFalloff falloff,
            float? adsFieldOfView = null)
        {
            var normalizedId = NormalizeId(id);

            if (!Enum.IsDefined(typeof(WeaponCategory), category))
            {
                throw new ArgumentOutOfRangeException(nameof(category), category, "category is not a defined WeaponCategory");
            }

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

            if (adsFieldOfView.HasValue)
            {
                AdsZoom.ValidateFieldOfView(adsFieldOfView.Value, nameof(adsFieldOfView));
            }

            Id = normalizedId;
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? normalizedId : displayName.Trim();
            Category = category;
            FireMode = fireMode;
            MagazineSize = magazineSize;
            ReserveAmmo = reserveAmmo;
            FireInterval = fireInterval;
            ReloadSeconds = reloadSeconds;
            Damage = damage;
            Range = range;
            Falloff = falloff ?? throw new ArgumentNullException(nameof(falloff));
            AdsFieldOfView = adsFieldOfView ?? AdsZoom.DefaultFieldOfView;
        }

        private static string NormalizeId(string id)
        {
            if (id == null)
            {
                throw new ArgumentNullException(nameof(id));
            }

            var trimmed = id.Trim();
            if (!IsLowercaseUuid(trimmed))
            {
                throw new ArgumentException(
                    "id must be a lowercase hyphenated UUID (e.g. 3f2b8c1e-0a4d-4c6e-9b1a-7d2e5f8c0b13): '" + trimmed + "'", nameof(id));
            }

            return trimmed;
        }

        // Guid.TryParseExact(value, "D", ...) は大文字も通すので使わない
        private static bool IsLowercaseUuid(string value)
        {
            if (value.Length != 36)
            {
                return false;
            }

            for (var i = 0; i < value.Length; i++)
            {
                var c = value[i];
                if (i == 8 || i == 13 || i == 18 || i == 23)
                {
                    if (c != '-')
                    {
                        return false;
                    }
                }
                else if (!((c >= '0' && c <= '9') || (c >= 'a' && c <= 'f')))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool IsNonNegativeFinite(float value)
        {
            return value >= 0f && !float.IsInfinity(value);
        }
    }
}
