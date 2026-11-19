using System;
using System.Collections.Generic;

namespace Shooter.Core
{
    public sealed class WeaponSpec
    {
        private static readonly IReadOnlyList<AmmoTypeSpec> _noAmmoTypes = Array.AsReadOnly(Array.Empty<AmmoTypeSpec>());

        public const float DefaultActiveReloadWindowStart = AmmoSpec.DefaultActiveReloadWindowStart;

        public const float DefaultActiveReloadWindowLength = AmmoSpec.DefaultActiveReloadWindowLength;

        public const float DefaultActiveReloadFailSeconds = AmmoSpec.DefaultActiveReloadFailSeconds;

        public const float DefaultRecoilRecoveryDelaySeconds = AimSpec.DefaultRecoilRecoveryDelaySeconds;

        // 消音していない銃声が、射撃場の端の敵まで届く距離。
        public const float DefaultShotNoiseRadius = 50f;

        public const float DefaultWeight = 0f;

        public const float DefaultMoveSpeedMultiplier = 1f;

        // 0 なら持ち替えの時間は持ち替え先を構える秒数だけになる。
        public const float DefaultHolsterSeconds = 0f;

        public string Id { get; }

        public string NameKey { get; }

        // Core は表示の文言を持たないので、ログには表を引かずキーをそのまま書く。
        public string LogName => NameKey.Length > 0 ? NameKey : Id;

        public WeaponCategory Category { get; }

        public TriggerSpec Trigger { get; }

        public AmmoSpec Ammo { get; }

        public RoundSpec Round { get; }

        public ImpactSpec Impact { get; }

        public AimSpec Aim { get; }

        public IReadOnlyList<AmmoTypeSpec> AmmoTypes { get; }

        public FireMode FireMode => Trigger.FireMode;

        public int MagazineSize => Ammo.MagazineSize;

        public int ReserveAmmo => Ammo.ReserveAmmo;

        public float FireInterval => Trigger.FireInterval;

        public float ReloadSeconds => Ammo.ReloadSeconds;

        public int Damage => Impact.Damage;

        public float Range => Round.Range;

        public DamageFalloff Falloff => Impact.Falloff;

        public float ExplosionRadius => Impact.StructureRadius;

        public float AdsFieldOfView => Aim.AdsFieldOfView;

        public float BlindFireSpreadDegrees => Aim.BlindFireSpreadDegrees;

        public float ActiveReloadWindowStart => Ammo.ActiveReloadWindowStart;

        public float ActiveReloadWindowLength => Ammo.ActiveReloadWindowLength;

        public float ActiveReloadFailSeconds => Ammo.ActiveReloadFailSeconds;

        public float RecoilPitchDegrees => Aim.RecoilPitchDegrees;

        public float RecoilYawDegrees => Aim.RecoilYawDegrees;

        public float RecoilRecoveryPerSecond => Aim.RecoilRecoveryPerSecond;

        public float HipSpreadDegrees => Aim.HipSpreadDegrees;

        public float AdsSpreadDegrees => Aim.AdsSpreadDegrees;

        public float MoveSpreadDegrees => Aim.MoveSpreadDegrees;

        public float RecoilRecoveryDelaySeconds => Aim.RecoilRecoveryDelaySeconds;

        public float ShotNoiseRadius { get; }

        public float Weight { get; }

        public float MoveSpeedMultiplier { get; }

        public string Description { get; }

        // null ならロードアウトの持ち替え秒数を使う。
        public float? DrawSeconds { get; }

        // この武器から別の武器へ持ち替えるとき、構える前に待つ秒数。
        public float HolsterSeconds { get; }

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
            string description = null,
            float? drawSeconds = null,
            float holsterSeconds = DefaultHolsterSeconds)
        {
            var normalizedId = SpecGuard.LowercaseUuid(id, nameof(id));

            if (!Enum.IsDefined(typeof(WeaponCategory), category))
            {
                throw new ArgumentOutOfRangeException(nameof(category), category, "category is not a defined WeaponCategory");
            }

            SpecGuard.NonNegativeFinite(shotNoiseRadius, nameof(shotNoiseRadius));
            SpecGuard.NonNegativeFinite(weight, nameof(weight));
            SpecGuard.NonNegativeFinite(moveSpeedMultiplier, nameof(moveSpeedMultiplier));
            if (drawSeconds.HasValue)
            {
                SpecGuard.NonNegativeFinite(drawSeconds.Value, nameof(drawSeconds));
            }

            SpecGuard.NonNegativeFinite(holsterSeconds, nameof(holsterSeconds));

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
            DrawSeconds = drawSeconds;
            HolsterSeconds = holsterSeconds;
        }

        public WeaponSpec WithFireInterval(float fireInterval)
        {
            var trigger = Trigger.WithFireInterval(fireInterval);
            return ReferenceEquals(trigger, Trigger)
                ? this
                : new WeaponSpec(Id, NameKey, Category, trigger, Ammo, Round, Impact, Aim, AmmoTypes, ShotNoiseRadius, Weight, MoveSpeedMultiplier, Description, DrawSeconds, HolsterSeconds);
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
