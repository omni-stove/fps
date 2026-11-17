using System;
using System.Collections.Generic;

namespace Fps.Core
{
    /// <summary>
    /// 武器 1 種類の不変な設定。生成時に値を検査し、不正なら例外を投げる。
    /// </summary>
    public sealed class WeaponSpec
    {
        private static readonly IReadOnlyList<AmmoTypeSpec> _noAmmoTypes = Array.AsReadOnly(Array.Empty<AmmoTypeSpec>());

        /// <summary>生成時に指定しなかったときの <see cref="ActiveReloadWindowStart"/>。</summary>
        public const float DefaultActiveReloadWindowStart = AmmoSpec.DefaultActiveReloadWindowStart;

        /// <summary>生成時に指定しなかったときの <see cref="ActiveReloadWindowLength"/>。</summary>
        public const float DefaultActiveReloadWindowLength = AmmoSpec.DefaultActiveReloadWindowLength;

        /// <summary>生成時に指定しなかったときの <see cref="ActiveReloadFailSeconds"/>。</summary>
        public const float DefaultActiveReloadFailSeconds = AmmoSpec.DefaultActiveReloadFailSeconds;

        /// <summary>生成時に指定しなかったときの <see cref="RecoilRecoveryDelaySeconds"/>。</summary>
        public const float DefaultRecoilRecoveryDelaySeconds = AimSpec.DefaultRecoilRecoveryDelaySeconds;

        // 消音していない銃声が、射撃場の端の敵まで届く距離。
        public const float DefaultShotNoiseRadius = 50f;

        public const float DefaultWeight = 0f;

        public const float DefaultMoveSpeedMultiplier = 1f;

        /// <summary>武器の識別子。小文字・ハイフン区切りの UUID (<c>Guid.ToString("D")</c> の形)。</summary>
        public string Id { get; }

        /// <summary>画面に出す名前を引く表のキー (例: <c>weapon.rifle</c>)。前後の空白を除いた値で、生成時に空なら空文字。</summary>
        public string NameKey { get; }

        /// <summary>
        /// 画面に出す名前。<see cref="NameKey"/> があればそのキーの文、無ければ <see cref="Id"/> をそのまま出す <see cref="LocalizedText.Literal"/>。
        /// </summary>
        public LocalizedText Name => NameOf(Id, NameKey);

        /// <summary>
        /// id と nameKey (前後の空白は除く) の武器の画面に出す名前 (<see cref="Name"/>)。<see cref="WeaponSpec"/> を作らずに名前だけ要るところで使う。null は空文字にする。
        /// </summary>
        public static LocalizedText NameOf(string id, string nameKey)
        {
            var key = SpecGuard.NameKey(nameKey);
            return key.Length > 0 ? new LocalizedText(key) : LocalizedText.Literal(id ?? string.Empty);
        }

        /// <summary>ログに書く名前。<see cref="NameKey"/> があればキー、無ければ <see cref="Id"/>。表は引かない。</summary>
        public string LogName => NameKey.Length > 0 ? NameKey : Id;

        /// <summary>武器の種別。ロードアウトの制約と表示にだけ使う。</summary>
        public WeaponCategory Category { get; }

        public TriggerSpec Trigger { get; }

        public AmmoSpec Ammo { get; }

        public RoundSpec Round { get; }

        public ImpactSpec Impact { get; }

        public AimSpec Aim { get; }

        public IReadOnlyList<AmmoTypeSpec> AmmoTypes { get; }

        /// <summary>射撃モード。</summary>
        public FireMode FireMode => Trigger.FireMode;

        /// <summary>弾倉の容量。1 以上。</summary>
        public int MagazineSize => Ammo.MagazineSize;

        /// <summary>予備弾の初期値。0 以上。</summary>
        public int ReserveAmmo => Ammo.ReserveAmmo;

        /// <summary>射撃から次に撃てるまでの秒数。0 以上の有限値。</summary>
        public float FireInterval => Trigger.FireInterval;

        /// <summary>リロード開始から補充までの秒数。0 以上の有限値。</summary>
        public float ReloadSeconds => Ammo.ReloadSeconds;

        /// <summary>1 発あたりのダメージ。0 以上。</summary>
        public int Damage => Impact.Damage;

        /// <summary>射程 (m)。0 より大きい有限値。</summary>
        public float Range => Round.Range;

        /// <summary>距離によるダメージ減衰。null にならない。</summary>
        public DamageFalloff Falloff => Impact.Falloff;

        /// <summary>建造物に当たったときに被害を広げる半径 (m)。0 なら当たった物だけ。0 以上の有限値。</summary>
        public float ExplosionRadius => Impact.StructureRadius;

        /// <summary>
        /// 覗き込み (ADS) し切ったときのカメラの縦の視野角 (度)。0 より大きく 180 未満。
        /// 生成時に指定しなければ <see cref="AdsZoom.DefaultFieldOfView"/>。クライアントの見た目にだけ使い、サーバーは見ない。
        /// </summary>
        public float AdsFieldOfView => Aim.AdsFieldOfView;

        /// <summary>隠れたまま撃ったときに、狙った向きから弾が散らばる最大の角度 (度)。0 以上 90 以下。</summary>
        public float BlindFireSpreadDegrees => Aim.BlindFireSpreadDegrees;

        /// <summary>アクティブリロードの判定の幅が始まる時点の、<see cref="ReloadSeconds"/> に対する割合。0 以上 1 未満。</summary>
        public float ActiveReloadWindowStart => Ammo.ActiveReloadWindowStart;

        /// <summary>アクティブリロードの判定の幅の長さの、<see cref="ReloadSeconds"/> に対する割合。0 より大きく、<see cref="ActiveReloadWindowStart"/> との和が 1 以下。</summary>
        public float ActiveReloadWindowLength => Ammo.ActiveReloadWindowLength;

        /// <summary>アクティブリロードで判定の幅を外したときに、リロードに足される秒数。0 以上の有限値。</summary>
        public float ActiveReloadFailSeconds => Ammo.ActiveReloadFailSeconds;

        /// <summary>1 発ごとに視線が上へ跳ねる角度 (度)。0 以上 90 以下。</summary>
        public float RecoilPitchDegrees => Aim.RecoilPitchDegrees;

        /// <summary>1 発ごとに視線が左右へぶれる最大の角度 (度)。0 以上 90 以下。</summary>
        public float RecoilYawDegrees => Aim.RecoilYawDegrees;

        /// <summary>撃ち止めてから跳ねた視線が戻る速さ (度/秒)。0 なら戻らない。0 以上の有限値。</summary>
        public float RecoilRecoveryPerSecond => Aim.RecoilRecoveryPerSecond;

        /// <summary>腰だめで撃ったときに、狙った向きから弾が散らばる最大の角度 (度)。0 以上 90 以下。</summary>
        public float HipSpreadDegrees => Aim.HipSpreadDegrees;

        /// <summary>覗き込み (ADS) し切って撃ったときに弾が散らばる最大の角度 (度)。0 以上 90 以下。</summary>
        public float AdsSpreadDegrees => Aim.AdsSpreadDegrees;

        /// <summary>動きながら撃ったときに散らばりへ足す最大の角度 (度)。0 以上 90 以下。</summary>
        public float MoveSpreadDegrees => Aim.MoveSpreadDegrees;

        /// <summary>最後に撃ってから跳ねた視線が戻り始めるまでの秒数。0 以上の有限値。</summary>
        public float RecoilRecoveryDelaySeconds => Aim.RecoilRecoveryDelaySeconds;

        public float ShotNoiseRadius { get; }

        /// <summary>重さ (kg)。0 以上の有限値。移動の速さへの効き方は <see cref="CarryLoadSpec"/> が決める。</summary>
        public float Weight { get; }

        /// <summary>持っている間に移動の速さへ掛ける倍率。0 以上の有限値。どの武器を数えるかは <see cref="CarryLoadSpec.MultiplierSource"/>。</summary>
        public float MoveSpeedMultiplier { get; }

        /// <summary>作る人向けのメモ (どの銃を模したか・どんな役回りか)。プレイヤーには見せない。null は空文字にする。</summary>
        public string Description { get; }

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
        /// <param name="recoilRecoveryDelaySeconds">撃ってから反動が戻り始めるまでの秒数。0 以上の有限値。</param>
        /// <param name="shotNoiseRadius">銃声が敵に聞こえる距離 (m)。消音した武器は小さくする。0 以上の有限値。</param>
        /// <param name="weight">重さ (kg)。0 以上の有限値。</param>
        /// <param name="moveSpeedMultiplier">持っている間に移動の速さへ掛ける倍率。0 以上の有限値。</param>
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
            float moveSpreadDegrees = 0f,
            float recoilRecoveryDelaySeconds = DefaultRecoilRecoveryDelaySeconds,
            float shotNoiseRadius = DefaultShotNoiseRadius,
            float weight = DefaultWeight,
            float moveSpeedMultiplier = DefaultMoveSpeedMultiplier)
            : this(
                id,
                nameKey,
                category,
                new TriggerSpec(fireMode, fireInterval),
                new AmmoSpec(
                    magazineSize,
                    reserveAmmo,
                    reloadSeconds,
                    activeReloadWindowStart,
                    activeReloadWindowLength,
                    activeReloadFailSeconds),
                new RoundSpec(range: range),
                new ImpactSpec(
                    damage,
                    falloff ?? throw new ArgumentNullException(nameof(falloff)),
                    // 部品では structureRadius と呼ぶが、この口の呼び手には explosionRadius の名前で不正を知らせる。
                    SpecGuard.NonNegativeFinite(explosionRadius, nameof(explosionRadius))),
                new AimSpec(
                    hipSpreadDegrees,
                    adsSpreadDegrees,
                    moveSpreadDegrees,
                    recoilPitchDegrees,
                    recoilYawDegrees,
                    recoilRecoveryPerSecond,
                    recoilRecoveryDelaySeconds,
                    blindFireSpreadDegrees,
                    adsFieldOfView.HasValue
                        ? new[] { AdsZoom.ValidateFieldOfView(adsFieldOfView.Value, nameof(adsFieldOfView)) }
                        : null),
                null,
                shotNoiseRadius,
                weight,
                moveSpeedMultiplier)
        {
        }

        public WeaponSpec(
            string id,
            string nameKey,
            WeaponCategory category,
            TriggerSpec trigger = null,
            AmmoSpec ammo = null,
            RoundSpec round = null,
            ImpactSpec impact = null,
            AimSpec aim = null,
            IEnumerable<AmmoTypeSpec> ammoTypes = null,
            float shotNoiseRadius = DefaultShotNoiseRadius,
            float weight = DefaultWeight,
            float moveSpeedMultiplier = DefaultMoveSpeedMultiplier,
            string description = null)
        {
            var normalizedId = SpecGuard.LowercaseUuid(id, nameof(id));

            if (!Enum.IsDefined(typeof(WeaponCategory), category))
            {
                throw new ArgumentOutOfRangeException(nameof(category), category, "category is not a defined WeaponCategory");
            }

            SpecGuard.NonNegativeFinite(shotNoiseRadius, nameof(shotNoiseRadius));
            SpecGuard.NonNegativeFinite(weight, nameof(weight));
            SpecGuard.NonNegativeFinite(moveSpeedMultiplier, nameof(moveSpeedMultiplier));

            var resolvedRound = round ?? RoundSpec.Default;
            var resolvedImpact = impact ?? ImpactSpec.Default;
            RequireDetonationReachable(resolvedRound, resolvedImpact, nameof(impact));
            var resolvedAmmoTypes = CopyAmmoTypes(ammoTypes, resolvedRound, resolvedImpact);

            Id = normalizedId;
            NameKey = SpecGuard.NameKey(nameKey);
            Category = category;
            Trigger = trigger ?? TriggerSpec.Default;
            Ammo = ammo ?? AmmoSpec.Default;
            Round = resolvedRound;
            Impact = resolvedImpact;
            Aim = aim ?? AimSpec.Default;
            AmmoTypes = resolvedAmmoTypes;
            ShotNoiseRadius = shotNoiseRadius;
            Weight = weight;
            MoveSpeedMultiplier = moveSpeedMultiplier;
            Description = description ?? string.Empty;
        }

        public WeaponSpec WithFireInterval(float fireInterval)
        {
            var trigger = Trigger.WithFireInterval(fireInterval);
            return ReferenceEquals(trigger, Trigger)
                ? this
                : new WeaponSpec(Id, NameKey, Category, trigger, Ammo, Round, Impact, Aim, AmmoTypes, ShotNoiseRadius, Weight, MoveSpeedMultiplier, Description);
        }

        // 起爆を止まったとき・寿命が尽きたときにすると、ヒットスキャンなどの止まりも尽きもしない弾では爆発しないまま設定だけが残る。
        private static void RequireDetonationReachable(RoundSpec round, ImpactSpec impact, string paramName)
        {
            var explosion = impact.Explosion;
            if (explosion != null && explosion.Detonation != DetonationTrigger.OnImpact && round.Delivery != RoundDelivery.Projectile)
            {
                throw new ArgumentException(
                    "explosion detonation " + explosion.Detonation + " needs round delivery Projectile, but it is " + round.Delivery,
                    paramName);
            }
        }

        private static IReadOnlyList<AmmoTypeSpec> CopyAmmoTypes(
            IEnumerable<AmmoTypeSpec> ammoTypes, RoundSpec weaponRound, ImpactSpec weaponImpact)
        {
            if (ammoTypes == null)
            {
                return _noAmmoTypes;
            }

            var copied = new List<AmmoTypeSpec>();
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var ammoType in ammoTypes)
            {
                if (ammoType == null)
                {
                    throw new ArgumentException("ammoTypes must not contain null", nameof(ammoTypes));
                }

                if (!ids.Add(ammoType.Id))
                {
                    throw new ArgumentException("ammoTypes has a duplicate id '" + ammoType.Id + "'", nameof(ammoTypes));
                }

                RequireDetonationReachable(ammoType.RoundOr(weaponRound), ammoType.ImpactOr(weaponImpact), nameof(ammoTypes));
                copied.Add(ammoType);
            }

            return copied.Count == 0 ? _noAmmoTypes : copied.AsReadOnly();
        }
    }
}
