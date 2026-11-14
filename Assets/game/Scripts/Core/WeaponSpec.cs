using System;

namespace Fps.Core
{
    /// <summary>
    /// 武器 1 種類の不変な設定。生成時に値を検査し、不正なら例外を投げる。
    /// </summary>
    public sealed class WeaponSpec
    {
        /// <summary>生成時に指定しなかったときの <see cref="ActiveReloadWindowStart"/>。</summary>
        public const float DefaultActiveReloadWindowStart = 0.5f;

        /// <summary>生成時に指定しなかったときの <see cref="ActiveReloadWindowLength"/>。</summary>
        public const float DefaultActiveReloadWindowLength = 0.15f;

        /// <summary>生成時に指定しなかったときの <see cref="ActiveReloadFailSeconds"/>。</summary>
        public const float DefaultActiveReloadFailSeconds = 0.5f;

        /// <summary>武器の識別子。小文字・ハイフン区切りの UUID (<c>Guid.ToString("D")</c> の形)。</summary>
        public string Id { get; }

        /// <summary>画面に出す名前を引く表のキー (例: <c>weapon.rifle</c>)。前後の空白を除いた値で、生成時に空なら空文字。</summary>
        public string NameKey { get; }

        /// <summary>
        /// 画面に出す名前。<see cref="NameKey"/> があればそのキーの文、無ければ <see cref="Id"/> をそのまま出す <see cref="LocalizedText.Literal"/>。
        /// </summary>
        public LocalizedText Name => NameKey.Length > 0 ? new LocalizedText(NameKey) : LocalizedText.Literal(Id);

        /// <summary>ログに書く名前。<see cref="NameKey"/> があればキー、無ければ <see cref="Id"/>。表は引かない。</summary>
        public string LogName => NameKey.Length > 0 ? NameKey : Id;

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

        /// <summary>建造物に当たったときに被害を広げる半径 (m)。0 なら当たった物だけ。0 以上の有限値。</summary>
        public float ExplosionRadius { get; }

        /// <summary>
        /// 覗き込み (ADS) し切ったときのカメラの縦の視野角 (度)。0 より大きく 180 未満。
        /// 生成時に指定しなければ <see cref="AdsZoom.DefaultFieldOfView"/>。クライアントの見た目にだけ使い、サーバーは見ない。
        /// </summary>
        public float AdsFieldOfView { get; }

        /// <summary>隠れたまま撃ったときに、狙った向きから弾が散らばる最大の角度 (度)。0 以上 90 以下。</summary>
        public float BlindFireSpreadDegrees { get; }

        /// <summary>アクティブリロードの判定の幅が始まる時点の、<see cref="ReloadSeconds"/> に対する割合。0 以上 1 未満。</summary>
        public float ActiveReloadWindowStart { get; }

        /// <summary>アクティブリロードの判定の幅の長さの、<see cref="ReloadSeconds"/> に対する割合。0 より大きく、<see cref="ActiveReloadWindowStart"/> との和が 1 以下。</summary>
        public float ActiveReloadWindowLength { get; }

        /// <summary>アクティブリロードで判定の幅を外したときに、リロードに足される秒数。0 以上の有限値。</summary>
        public float ActiveReloadFailSeconds { get; }

        /// <summary>1 発ごとに視線が上へ跳ねる角度 (度)。0 以上 90 以下。</summary>
        public float RecoilPitchDegrees { get; }

        /// <summary>1 発ごとに視線が左右へぶれる最大の角度 (度)。0 以上 90 以下。</summary>
        public float RecoilYawDegrees { get; }

        /// <summary>撃ち止めてから跳ねた視線が戻る速さ (度/秒)。0 なら戻らない。0 以上の有限値。</summary>
        public float RecoilRecoveryPerSecond { get; }

        /// <summary>腰だめで撃ったときに、狙った向きから弾が散らばる最大の角度 (度)。0 以上 90 以下。</summary>
        public float HipSpreadDegrees { get; }

        /// <summary>覗き込み (ADS) し切って撃ったときに弾が散らばる最大の角度 (度)。0 以上 90 以下。</summary>
        public float AdsSpreadDegrees { get; }

        /// <summary>動きながら撃ったときに散らばりへ足す最大の角度 (度)。0 以上 90 以下。</summary>
        public float MoveSpreadDegrees { get; }

        /// <param name="id">識別子。前後の空白は除く。除いた後が小文字・ハイフン区切りの UUID (例: 3f2b8c1e-0a4d-4c6e-9b1a-7d2e5f8c0b13)。</param>
        /// <param name="nameKey">画面に出す名前を引く表のキー。null・空白のみなら名前は <paramref name="id"/> になる。</param>
        /// <param name="category">武器の種別。定義済みの値のみ。</param>
        /// <param name="fireMode">射撃モード。</param>
        /// <param name="magazineSize">弾倉の容量。1 以上。</param>
        /// <param name="reserveAmmo">予備弾の初期値。0 以上。</param>
        /// <param name="fireInterval">連射間隔 (秒)。0 以上の有限値。</param>
        /// <param name="reloadSeconds">リロード時間 (秒)。0 以上の有限値。</param>
        /// <param name="damage">1 発あたりのダメージ。0 以上。</param>
        /// <param name="range">射程 (m)。0 より大きい有限値。</param>
        /// <param name="falloff">距離減衰。null 不可 (減衰しないなら <see cref="DamageFalloff.None"/>)。</param>
        /// <param name="explosionRadius">建造物への被害を広げる半径 (m)。0 以上の有限値。</param>
        /// <param name="adsFieldOfView">ADS し切ったときの視野角 (度)。0 より大きく 180 未満。null なら <see cref="AdsZoom.DefaultFieldOfView"/>。</param>
        /// <param name="blindFireSpreadDegrees">ブラインドファイアの散らばりの最大角度 (度)。0 以上 90 以下。</param>
        /// <param name="activeReloadWindowStart">アクティブリロードの判定の幅の始まり (リロード時間に対する割合)。0 以上 1 未満。</param>
        /// <param name="activeReloadWindowLength">アクティブリロードの判定の幅の長さ (リロード時間に対する割合)。0 より大きく、activeReloadWindowStart との和が 1 以下。</param>
        /// <param name="activeReloadFailSeconds">アクティブリロードに失敗したときに足される秒数。0 以上の有限値。</param>
        /// <param name="recoilPitchDegrees">1 発ごとの縦の反動 (度)。0 以上 90 以下。</param>
        /// <param name="recoilYawDegrees">1 発ごとの横の反動の最大 (度)。0 以上 90 以下。</param>
        /// <param name="recoilRecoveryPerSecond">反動が戻る速さ (度/秒)。0 以上の有限値。</param>
        /// <param name="hipSpreadDegrees">腰だめの散らばりの最大角度 (度)。0 以上 90 以下。</param>
        /// <param name="adsSpreadDegrees">ADS の散らばりの最大角度 (度)。0 以上 90 以下。</param>
        /// <param name="moveSpreadDegrees">移動中に足す散らばりの最大角度 (度)。0 以上 90 以下。</param>
        public WeaponSpec(
            string id,
            string nameKey,
            WeaponCategory category,
            FireMode fireMode,
            int magazineSize,
            int reserveAmmo,
            float fireInterval,
            float reloadSeconds,
            int damage,
            float range,
            DamageFalloff falloff,
            float explosionRadius = 0f,
            float? adsFieldOfView = null,
            float blindFireSpreadDegrees = 0f,
            float activeReloadWindowStart = DefaultActiveReloadWindowStart,
            float activeReloadWindowLength = DefaultActiveReloadWindowLength,
            float activeReloadFailSeconds = DefaultActiveReloadFailSeconds,
            float recoilPitchDegrees = 0f,
            float recoilYawDegrees = 0f,
            float recoilRecoveryPerSecond = 0f,
            float hipSpreadDegrees = 0f,
            float adsSpreadDegrees = 0f,
            float moveSpreadDegrees = 0f)
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

            if (!IsNonNegativeFinite(explosionRadius))
            {
                throw new ArgumentOutOfRangeException(nameof(explosionRadius), explosionRadius, "explosionRadius must be a non-negative finite number");
            }

            if (adsFieldOfView.HasValue)
            {
                AdsZoom.ValidateFieldOfView(adsFieldOfView.Value, nameof(adsFieldOfView));
            }

            if (!(blindFireSpreadDegrees >= 0f && blindFireSpreadDegrees <= 90f))
            {
                throw new ArgumentOutOfRangeException(nameof(blindFireSpreadDegrees), blindFireSpreadDegrees, "blindFireSpreadDegrees must be between 0 and 90");
            }

            if (!(activeReloadWindowStart >= 0f && activeReloadWindowStart < 1f))
            {
                throw new ArgumentOutOfRangeException(nameof(activeReloadWindowStart), activeReloadWindowStart, "activeReloadWindowStart must be at least 0 and less than 1");
            }

            if (!(activeReloadWindowLength > 0f && activeReloadWindowStart + activeReloadWindowLength <= 1f))
            {
                throw new ArgumentOutOfRangeException(nameof(activeReloadWindowLength), activeReloadWindowLength, "activeReloadWindowLength must be positive and end the window at or before the end of the reload");
            }

            if (!IsNonNegativeFinite(activeReloadFailSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(activeReloadFailSeconds), activeReloadFailSeconds, "activeReloadFailSeconds must be a non-negative finite number");
            }

            ValidateAngle(recoilPitchDegrees, nameof(recoilPitchDegrees));
            ValidateAngle(recoilYawDegrees, nameof(recoilYawDegrees));

            if (!IsNonNegativeFinite(recoilRecoveryPerSecond))
            {
                throw new ArgumentOutOfRangeException(nameof(recoilRecoveryPerSecond), recoilRecoveryPerSecond, "recoilRecoveryPerSecond must be a non-negative finite number");
            }

            ValidateAngle(hipSpreadDegrees, nameof(hipSpreadDegrees));
            ValidateAngle(adsSpreadDegrees, nameof(adsSpreadDegrees));
            ValidateAngle(moveSpreadDegrees, nameof(moveSpreadDegrees));

            Id = normalizedId;
            NameKey = string.IsNullOrWhiteSpace(nameKey) ? string.Empty : nameKey.Trim();
            Category = category;
            FireMode = fireMode;
            MagazineSize = magazineSize;
            ReserveAmmo = reserveAmmo;
            FireInterval = fireInterval;
            ReloadSeconds = reloadSeconds;
            Damage = damage;
            Range = range;
            Falloff = falloff ?? throw new ArgumentNullException(nameof(falloff));
            ExplosionRadius = explosionRadius;
            AdsFieldOfView = adsFieldOfView ?? AdsZoom.DefaultFieldOfView;
            BlindFireSpreadDegrees = blindFireSpreadDegrees;
            ActiveReloadWindowStart = activeReloadWindowStart;
            ActiveReloadWindowLength = activeReloadWindowLength;
            ActiveReloadFailSeconds = activeReloadFailSeconds;
            RecoilPitchDegrees = recoilPitchDegrees;
            RecoilYawDegrees = recoilYawDegrees;
            RecoilRecoveryPerSecond = recoilRecoveryPerSecond;
            HipSpreadDegrees = hipSpreadDegrees;
            AdsSpreadDegrees = adsSpreadDegrees;
            MoveSpreadDegrees = moveSpreadDegrees;
        }

        private static void ValidateAngle(float degrees, string paramName)
        {
            if (!(degrees >= 0f && degrees <= 90f))
            {
                throw new ArgumentOutOfRangeException(paramName, degrees, paramName + " must be between 0 and 90");
            }
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
