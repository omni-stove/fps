using System;
using System.Collections.Generic;

namespace Shooter.Core
{
    // 項目名はサンプルの武器の JSON (Samples~/Demo/Weapons/Json) と同じにし、手で書いた武器の JSON をそのまま貼れるようにする。
    // 部品の項目名は部品の Spec のコンストラクター引数と同じにし、Spec が ParamName に入れて投げた不正をそのまま項目に結び付ける。
    public static class WeaponSpecJson
    {
        public const string Kind = "weapon JSON";

        public const string IdItem = "id";
        public const string NameKeyItem = "nameKey";
        public const string DescriptionItem = "description";
        public const string CategoryItem = "category";
        public const string ShotNoiseRadiusItem = "shotNoiseRadius";
        public const string WeightItem = "weight";
        public const string MoveSpeedMultiplierItem = "moveSpeedMultiplier";

        public const string DrawSecondsItem = "drawSeconds";
        public const string HolsterSecondsItem = "holsterSeconds";
        public const string TriggerItem = "trigger";
        public const string AmmoItem = "ammo";
        public const string RoundItem = "round";
        public const string ImpactItem = "impact";
        public const string AimItem = "aim";
        public const string AmmoTypesItem = "ammoTypes";
        public const string ShieldItem = RiotShieldSpecJson.ShieldItem;
        public const string MeleeItem = MeleeWeaponSpecJson.MeleeItem;

        public const string FireModeItem = "fireMode";
        public const string FireIntervalItem = "fireInterval";
        public const string BurstItem = "burst";
        public const string ChargeItem = "charge";
        public const string SpinUpItem = "spinUp";
        public const string CycleItem = "cycle";

        public const string RoundsPerBurstItem = "roundsPerBurst";
        public const string RoundIntervalSecondsItem = "roundIntervalSeconds";
        public const string ChargeSecondsItem = "chargeSeconds";
        public const string MinChargeFractionItem = "minChargeFraction";
        public const string FullChargeDamageMultiplierItem = "fullChargeDamageMultiplier";
        public const string FiresWhenFullItem = "firesWhenFull";
        public const string FiresBelowMinItem = "firesBelowMin";
        public const string FullChargeSpeedMultiplierItem = "fullChargeSpeedMultiplier";
        public const string SpinUpSecondsItem = "spinUpSeconds";
        public const string StartIntervalMultiplierItem = "startIntervalMultiplier";
        public const string SpinDownSecondsItem = "spinDownSeconds";
        public const string CycleSecondsItem = "cycleSeconds";
        public const string LeavesAdsItem = "leavesAds";

        public const string AmmoKindItem = "kind";
        public const string MagazineSizeItem = "magazineSize";
        public const string ReserveAmmoItem = "reserveAmmo";
        public const string ReloadSecondsItem = "reloadSeconds";
        public const string ActiveReloadWindowStartItem = "activeReloadWindowStart";
        public const string ActiveReloadWindowLengthItem = "activeReloadWindowLength";
        public const string ActiveReloadFailSecondsItem = "activeReloadFailSeconds";
        public const string ReloadStyleItem = "reloadStyle";
        public const string ReloadSecondsPerRoundItem = "reloadSecondsPerRound";
        public const string HeatItem = "heat";

        public const string HeatPerShotItem = "heatPerShot";
        public const string CoolPerSecondItem = "coolPerSecond";
        public const string CoolDelaySecondsItem = "coolDelaySeconds";
        public const string OverheatSecondsItem = "overheatSeconds";

        public const string DeliveryItem = "delivery";
        public const string RangeItem = "range";
        public const string PelletCountItem = "pelletCount";
        public const string PelletSpreadDegreesItem = "pelletSpreadDegrees";
        public const string RadiusItem = "radius";
        public const string PenetrationItem = "penetration";
        public const string RicochetItem = "ricochet";
        public const string ProjectileItem = "projectile";
        public const string HomingItem = "homing";
        public const string BeamItem = "beam";
        public const string SprayItem = "spray";

        public const string MaxPenetrationsItem = "maxPenetrations";
        public const string DamageMultiplierPerPenetrationItem = "damageMultiplierPerPenetration";
        public const string MaxThicknessItem = "maxThickness";
        public const string MaterialsItem = "materials";
        public const string MaterialIdItem = "materialId";
        public const string PenetrabilityItem = "penetrability";
        public const string MaxBouncesItem = "maxBounces";
        public const string MaxGrazingAngleDegreesItem = "maxGrazingAngleDegrees";
        public const string DamageMultiplierPerBounceItem = "damageMultiplierPerBounce";
        public const string SpeedItem = "speed";
        public const string GravityMultiplierItem = "gravityMultiplier";
        public const string LifetimeSecondsItem = "lifetimeSeconds";
        public const string OnWorldHitItem = "onWorldHit";
        public const string VisualIdItem = "visualId";
        public const string LockOnSecondsItem = "lockOnSeconds";
        public const string LockOnConeDegreesItem = "lockOnConeDegrees";
        public const string TurnDegreesPerSecondItem = "turnDegreesPerSecond";
        public const string TickSecondsItem = "tickSeconds";
        public const string ConeDegreesItem = "coneDegrees";

        public const string DamageItem = "damage";
        public const string FalloffItem = "falloff";
        public const string StructureRadiusItem = "structureRadius";
        public const string ExplosionItem = "explosion";
        public const string StatusEffectItem = "statusEffect";
        public const string AreaEffectItem = "areaEffect";
        public const string BlindItem = "blind";

        public const string MaxDamageItem = "maxDamage";
        public const string FullDamageRadiusItem = "fullDamageRadius";
        public const string StructureDamageAtCenterItem = "structureDamageAtCenter";
        public const string DetonationItem = "detonation";
        public const string DurationSecondsItem = "durationSeconds";
        public const string DamagePerTickItem = "damagePerTick";
        public const string StatusKindItem = "kind";
        public const string BackFractionItem = "backFraction";

        public const string HipSpreadDegreesItem = "hipSpreadDegrees";
        public const string AdsSpreadDegreesItem = "adsSpreadDegrees";
        public const string MoveSpreadDegreesItem = "moveSpreadDegrees";
        public const string AdsMoveSpreadDegreesItem = "adsMoveSpreadDegrees";
        public const string RecoilPitchDegreesItem = "recoilPitchDegrees";
        public const string RecoilYawDegreesItem = "recoilYawDegrees";
        public const string RecoilRecoveryPerSecondItem = "recoilRecoveryPerSecond";
        public const string RecoilRecoveryDelaySecondsItem = "recoilRecoveryDelaySeconds";
        public const string BlindFireSpreadDegreesItem = "blindFireSpreadDegrees";
        public const string ZoomLevelsItem = "zoomLevels";
        public const string BreathHoldItem = "breathHold";
        public const string SwayDegreesItem = "swayDegrees";

        public const string MaxHoldSecondsItem = "maxHoldSeconds";
        public const string RecoverSecondsItem = "recoverSeconds";
        public const string SwayMultiplierItem = "swayMultiplier";

        public const string StartDistanceItem = "startDistance";
        public const string EndDistanceItem = "endDistance";
        public const string MinMultiplierItem = "minMultiplier";

        // 部品に分ける前の、武器の直下に平らに書いた項目。部品の同じ名前の項目として読む。
        public const string ExplosionRadiusItem = "explosionRadius";
        public const string AdsFieldOfViewItem = "adsFieldOfView";

        public const string NoFalloff = "None";

        private static readonly string[] _triggerFlatItems = { FireModeItem, FireIntervalItem };

        private static readonly string[] _ammoFlatItems =
        {
            MagazineSizeItem, ReserveAmmoItem, ReloadSecondsItem, ActiveReloadWindowStartItem, ActiveReloadWindowLengthItem,
            ActiveReloadFailSecondsItem,
        };

        private static readonly string[] _roundFlatItems = { RangeItem };

        private static readonly string[] _impactFlatItems = { DamageItem, FalloffItem, ExplosionRadiusItem };

        private static readonly string[] _aimFlatItems =
        {
            AdsFieldOfViewItem, BlindFireSpreadDegreesItem, RecoilPitchDegreesItem, RecoilYawDegreesItem, RecoilRecoveryPerSecondItem,
            HipSpreadDegreesItem, AdsSpreadDegreesItem, MoveSpreadDegreesItem, RecoilRecoveryDelaySecondsItem,
        };

        public static IReadOnlyList<string> ItemNames { get; } = Concat(
            new[]
            {
                IdItem, NameKeyItem, DescriptionItem, CategoryItem, ShotNoiseRadiusItem, WeightItem, MoveSpeedMultiplierItem, DrawSecondsItem, HolsterSecondsItem, TriggerItem, AmmoItem, RoundItem, ImpactItem, AimItem, AmmoTypesItem, ShieldItem, MeleeItem,
            },
            _triggerFlatItems,
            _ammoFlatItems,
            _roundFlatItems,
            _impactFlatItems,
            _aimFlatItems);

        public static IReadOnlyList<string> TriggerItemNames { get; } = new[] { FireModeItem, FireIntervalItem, BurstItem, ChargeItem, SpinUpItem, CycleItem };

        public static IReadOnlyList<string> AmmoItemNames { get; } = new[]
        {
            AmmoKindItem, MagazineSizeItem, ReserveAmmoItem, ReloadSecondsItem, ActiveReloadWindowStartItem, ActiveReloadWindowLengthItem,
            ActiveReloadFailSecondsItem, ReloadStyleItem, ReloadSecondsPerRoundItem, HeatItem,
        };

        public static IReadOnlyList<string> RoundItemNames { get; } = new[]
        {
            DeliveryItem, RangeItem, PelletCountItem, PelletSpreadDegreesItem, RadiusItem, PenetrationItem, RicochetItem, ProjectileItem,
            HomingItem, BeamItem, SprayItem,
        };

        public static IReadOnlyList<string> ImpactItemNames { get; } = new[]
        {
            DamageItem, FalloffItem, StructureRadiusItem, ExplosionItem, StatusEffectItem, AreaEffectItem,
        };

        // 目くらましは弾けた所から見えている人にかけ、当たった相手には効かないので、武器の着弾には書かせない。
        public static IReadOnlyList<string> GrenadeImpactItemNames { get; } = new[]
        {
            DamageItem, FalloffItem, StructureRadiusItem, ExplosionItem, StatusEffectItem, AreaEffectItem, BlindItem,
        };

        public static IReadOnlyList<string> AimItemNames { get; } = new[]
        {
            HipSpreadDegreesItem, AdsSpreadDegreesItem, MoveSpreadDegreesItem, AdsMoveSpreadDegreesItem, RecoilPitchDegreesItem,
            RecoilYawDegreesItem, RecoilRecoveryPerSecondItem, RecoilRecoveryDelaySecondsItem, BlindFireSpreadDegreesItem, ZoomLevelsItem,
            BreathHoldItem, SwayDegreesItem, SightSpecJson.SightItem,
        };

        public static IReadOnlyList<string> AmmoTypeItemNames { get; } = new[] { IdItem, NameKeyItem, RoundItem, ImpactItem };

        public static IReadOnlyList<string> FalloffItemNames { get; } = new[] { StartDistanceItem, EndDistanceItem, MinMultiplierItem };

        private static readonly string[] _burstItems = { RoundsPerBurstItem, RoundIntervalSecondsItem };
        private static readonly string[] _chargeItems =
        {
            ChargeSecondsItem, MinChargeFractionItem, FullChargeDamageMultiplierItem, FiresWhenFullItem, FiresBelowMinItem, FullChargeSpeedMultiplierItem,
        };
        private static readonly string[] _spinUpItems = { SpinUpSecondsItem, StartIntervalMultiplierItem, SpinDownSecondsItem };
        private static readonly string[] _cycleItems = { CycleSecondsItem, LeavesAdsItem };
        private static readonly string[] _heatItems = { HeatPerShotItem, CoolPerSecondItem, CoolDelaySecondsItem, OverheatSecondsItem };
        private static readonly string[] _penetrationItems = { MaxPenetrationsItem, DamageMultiplierPerPenetrationItem, MaxThicknessItem, MaterialsItem };
        private static readonly string[] _penetrationMaterialItems = { MaterialIdItem, PenetrabilityItem };
        private static readonly string[] _ricochetItems = { MaxBouncesItem, MaxGrazingAngleDegreesItem, DamageMultiplierPerBounceItem };
        private static readonly string[] _projectileItems = { SpeedItem, GravityMultiplierItem, LifetimeSecondsItem, OnWorldHitItem, VisualIdItem };
        private static readonly string[] _homingItems = { LockOnSecondsItem, LockOnConeDegreesItem, TurnDegreesPerSecondItem };
        private static readonly string[] _beamItems = { TickSecondsItem, VisualIdItem };
        private static readonly string[] _sprayItems = { TickSecondsItem, ConeDegreesItem, VisualIdItem };
        private static readonly string[] _explosionItems = { MaxDamageItem, RadiusItem, FullDamageRadiusItem, StructureDamageAtCenterItem, DetonationItem };
        private static readonly string[] _statusEffectItems = { StatusKindItem, DurationSecondsItem, TickSecondsItem, DamagePerTickItem, VisualIdItem };

        private static readonly string[] _areaEffectItems =
        {
            RadiusItem, DurationSecondsItem, TickSecondsItem, DamagePerTickItem, StatusEffectItem, VisualIdItem,
        };

        private static readonly string[] _blindItems = { RadiusItem, DurationSecondsItem, BackFractionItem };

        private static readonly string[] _breathHoldItems = { MaxHoldSecondsItem, RecoverSecondsItem, SwayMultiplierItem };

        public static WeaponSpec Parse(string text, string sourceName)
        {
            var source = ServerDataJson.SourceOf(sourceName, Kind);
            var root = ServerDataJson.ParseDocument(text, source, Kind);
            return Read(new ServerDataJson.Fields(source, Kind, root, string.Empty, ItemNames));
        }

        public static string Write(WeaponSpec spec)
        {
            if (spec == null)
            {
                throw new ArgumentNullException(nameof(spec));
            }

            var writer = new JsonWriter();
            Write(writer, spec);
            return writer.Finish();
        }

        internal static WeaponSpec Read(ServerDataJson.Fields fields)
        {
            var trigger = ReadTrigger(Part.Of(fields, TriggerItem, TriggerItemNames, _triggerFlatItems));
            var ammo = ReadAmmo(Part.Of(fields, AmmoItem, AmmoItemNames, _ammoFlatItems));
            var round = ReadRound(Part.Of(fields, RoundItem, RoundItemNames, _roundFlatItems));
            var impact = ReadImpact(Part.Of(fields, ImpactItem, ImpactItemNames, _impactFlatItems));
            var aim = ReadAim(Part.Of(fields, AimItem, AimItemNames, _aimFlatItems));
            var ammoTypes = ReadAmmoTypes(fields);
            var shieldFields = fields.Object(ShieldItem, RiotShieldSpecJson.ItemNames);
            var shield = shieldFields == null ? null : RiotShieldSpecJson.Read(shieldFields);
            var meleeFields = fields.Object(MeleeItem, MeleeWeaponSpecJson.ItemNames);
            var melee = meleeFields == null ? null : MeleeWeaponSpecJson.Read(meleeFields);

            // 名前を出さない武器を null で書いていた頃の JSON も、書かないのと同じに読む。
            var nameKeyNode = fields.FindRaw(NameKeyItem);
            var nameKey = nameKeyNode == null || nameKeyNode.Kind == JsonNodeKind.Null
                ? string.Empty
                : fields.ToText(fields.ItemPath(NameKeyItem), nameKeyNode);

            var drawNode = fields.FindRaw(DrawSecondsItem);
            float? drawSeconds = drawNode == null || drawNode.Kind == JsonNodeKind.Null
                ? (float?)null
                : fields.ToFloat(fields.ItemPath(DrawSecondsItem), drawNode);

            try
            {
                return new WeaponSpec(
                    fields.ToText(fields.ItemPath(IdItem), fields.Require(IdItem)),
                    nameKey,
                    fields.ToEnum<WeaponCategory>(fields.ItemPath(CategoryItem), fields.Require(CategoryItem)),
                    trigger,
                    ammo,
                    round,
                    impact,
                    aim,
                    ammoTypes,
                    fields.ReadFloat(ShotNoiseRadiusItem, WeaponSpec.DefaultShotNoiseRadius),
                    fields.ReadFloat(WeightItem, WeaponSpec.DefaultWeight),
                    fields.ReadFloat(MoveSpeedMultiplierItem, WeaponSpec.DefaultMoveSpeedMultiplier),
                    fields.ReadString(DescriptionItem, string.Empty),
                    drawSeconds,
                    fields.ReadFloat(HolsterSecondsItem, WeaponSpec.DefaultHolsterSeconds),
                    shield,
                    melee);
            }
            catch (ArgumentException e)
            {
                throw fields.Rejected(e.ParamName == null ? fields.Path : fields.ItemPath(e.ParamName), e.Message, e);
            }
        }

        internal static void Write(JsonWriter writer, WeaponSpec spec)
        {
            writer.String(IdItem, spec.Id);
            writer.String(NameKeyItem, spec.NameKey);
            // メモの無い武器の JSON を、メモを足す前と同じ文字列のまま書き戻す。
            if (spec.Description.Length > 0)
            {
                writer.String(DescriptionItem, spec.Description);
            }

            writer.Enum(CategoryItem, spec.Category);
            writer.Float(ShotNoiseRadiusItem, spec.ShotNoiseRadius);
            writer.Float(WeightItem, spec.Weight);
            writer.Float(MoveSpeedMultiplierItem, spec.MoveSpeedMultiplier);

            // 構える秒数を持たない武器の JSON を、この項目を足す前と同じ文字列のまま書き戻す。
            if (spec.DrawSeconds.HasValue)
            {
                writer.Float(DrawSecondsItem, spec.DrawSeconds.Value);
            }

            // しまう秒数を持たない武器の JSON も、この項目を足す前と同じ文字列のまま書き戻す。
            if (spec.HolsterSeconds != WeaponSpec.DefaultHolsterSeconds)
            {
                writer.Float(HolsterSecondsItem, spec.HolsterSeconds);
            }

            writer.BeginObject(TriggerItem);
            WriteTrigger(writer, spec.Trigger);
            writer.EndObject();

            writer.BeginObject(AmmoItem);
            WriteAmmo(writer, spec.Ammo);
            writer.EndObject();

            writer.BeginObject(RoundItem);
            WriteRound(writer, spec.Round);
            writer.EndObject();

            writer.BeginObject(ImpactItem);
            WriteImpact(writer, spec.Impact);
            writer.EndObject();

            writer.BeginObject(AimItem);
            WriteAim(writer, spec.Aim);
            writer.EndObject();

            if (spec.AmmoTypes.Count > 0)
            {
                writer.BeginArray(AmmoTypesItem);
                foreach (var ammoType in spec.AmmoTypes)
                {
                    writer.BeginElement();
                    writer.String(IdItem, ammoType.Id);
                    writer.String(NameKeyItem, ammoType.NameKey);
                    if (ammoType.Round != null)
                    {
                        writer.BeginObject(RoundItem);
                        WriteRound(writer, ammoType.Round);
                        writer.EndObject();
                    }

                    if (ammoType.Impact != null)
                    {
                        writer.BeginObject(ImpactItem);
                        WriteImpact(writer, ammoType.Impact);
                        writer.EndObject();
                    }

                    writer.EndObject();
                }

                writer.EndArray();
            }

            RiotShieldSpecJson.Write(writer, spec.Shield);
            MeleeWeaponSpecJson.Write(writer, spec.Melee);
        }

        private static TriggerSpec ReadTrigger(Part part)
        {
            var burst = ReadNested(part, BurstItem, _burstItems, p => new BurstSpec(p.RequireInt(RoundsPerBurstItem), p.RequireFloat(RoundIntervalSecondsItem)));
            var charge = ReadNested(part, ChargeItem, _chargeItems, p => new ChargeSpec(
                p.RequireFloat(ChargeSecondsItem),
                p.RequireFloat(MinChargeFractionItem),
                p.RequireFloat(FullChargeDamageMultiplierItem),
                p.Bool(FiresWhenFullItem, false),
                p.Bool(FiresBelowMinItem, false),
                p.Float(FullChargeSpeedMultiplierItem, ChargeSpec.DefaultFullChargeSpeedMultiplier)));
            var spinUp = ReadNested(part, SpinUpItem, _spinUpItems, p => new SpinUpSpec(
                p.RequireFloat(SpinUpSecondsItem), p.RequireFloat(StartIntervalMultiplierItem), p.RequireFloat(SpinDownSecondsItem)));
            var cycle = ReadNested(part, CycleItem, _cycleItems, p => new CycleSpec(p.RequireFloat(CycleSecondsItem), p.Bool(LeavesAdsItem, false)));

            return part.Build(() => new TriggerSpec(
                part.Enum(FireModeItem, TriggerSpec.DefaultFireMode),
                part.Float(FireIntervalItem, TriggerSpec.DefaultFireInterval),
                burst,
                charge,
                spinUp,
                cycle));
        }

        private static void WriteTrigger(JsonWriter writer, TriggerSpec trigger)
        {
            writer.Enum(FireModeItem, trigger.FireMode);
            writer.Float(FireIntervalItem, trigger.FireInterval);
            if (trigger.Burst != null)
            {
                writer.BeginObject(BurstItem);
                writer.Int(RoundsPerBurstItem, trigger.Burst.RoundsPerBurst);
                writer.Float(RoundIntervalSecondsItem, trigger.Burst.RoundIntervalSeconds);
                writer.EndObject();
            }

            if (trigger.Charge != null)
            {
                writer.BeginObject(ChargeItem);
                writer.Float(ChargeSecondsItem, trigger.Charge.ChargeSeconds);
                writer.Float(MinChargeFractionItem, trigger.Charge.MinChargeFraction);
                writer.Float(FullChargeDamageMultiplierItem, trigger.Charge.FullChargeDamageMultiplier);
                writer.Bool(FiresWhenFullItem, trigger.Charge.FiresWhenFull);
                writer.Bool(FiresBelowMinItem, trigger.Charge.FiresBelowMin);
                writer.Float(FullChargeSpeedMultiplierItem, trigger.Charge.FullChargeSpeedMultiplier);
                writer.EndObject();
            }

            if (trigger.SpinUp != null)
            {
                writer.BeginObject(SpinUpItem);
                writer.Float(SpinUpSecondsItem, trigger.SpinUp.SpinUpSeconds);
                writer.Float(StartIntervalMultiplierItem, trigger.SpinUp.StartIntervalMultiplier);
                writer.Float(SpinDownSecondsItem, trigger.SpinUp.SpinDownSeconds);
                writer.EndObject();
            }

            if (trigger.Cycle != null)
            {
                writer.BeginObject(CycleItem);
                writer.Float(CycleSecondsItem, trigger.Cycle.CycleSeconds);
                writer.Bool(LeavesAdsItem, trigger.Cycle.LeavesAds);
                writer.EndObject();
            }
        }

        private static AmmoSpec ReadAmmo(Part part)
        {
            var heat = ReadNested(part, HeatItem, _heatItems, p => new HeatSpec(
                p.RequireFloat(HeatPerShotItem), p.RequireFloat(CoolPerSecondItem), p.RequireFloat(CoolDelaySecondsItem), p.RequireFloat(OverheatSecondsItem)));

            return part.Build(() => new AmmoSpec(
                part.Int(MagazineSizeItem, AmmoSpec.DefaultMagazineSize),
                part.Int(ReserveAmmoItem, AmmoSpec.DefaultReserveAmmo),
                part.Float(ReloadSecondsItem, AmmoSpec.DefaultReloadSeconds),
                part.Float(ActiveReloadWindowStartItem, AmmoSpec.DefaultActiveReloadWindowStart),
                part.Float(ActiveReloadWindowLengthItem, AmmoSpec.DefaultActiveReloadWindowLength),
                part.Float(ActiveReloadFailSecondsItem, AmmoSpec.DefaultActiveReloadFailSeconds),
                part.Enum(ReloadStyleItem, ReloadStyle.Whole),
                part.Float(ReloadSecondsPerRoundItem, 0f),
                part.Enum(AmmoKindItem, AmmoKind.Magazine),
                heat));
        }

        private static void WriteAmmo(JsonWriter writer, AmmoSpec ammo)
        {
            writer.Enum(AmmoKindItem, ammo.Kind);
            writer.Int(MagazineSizeItem, ammo.MagazineSize);
            writer.Int(ReserveAmmoItem, ammo.ReserveAmmo);
            writer.Float(ReloadSecondsItem, ammo.ReloadSeconds);
            writer.Float(ActiveReloadWindowStartItem, ammo.ActiveReloadWindowStart);
            writer.Float(ActiveReloadWindowLengthItem, ammo.ActiveReloadWindowLength);
            writer.Float(ActiveReloadFailSecondsItem, ammo.ActiveReloadFailSeconds);
            writer.Enum(ReloadStyleItem, ammo.ReloadStyle);
            writer.Float(ReloadSecondsPerRoundItem, ammo.ReloadSecondsPerRound);
            if (ammo.Heat != null)
            {
                writer.BeginObject(HeatItem);
                writer.Float(HeatPerShotItem, ammo.Heat.HeatPerShot);
                writer.Float(CoolPerSecondItem, ammo.Heat.CoolPerSecond);
                writer.Float(CoolDelaySecondsItem, ammo.Heat.CoolDelaySeconds);
                writer.Float(OverheatSecondsItem, ammo.Heat.OverheatSeconds);
                writer.EndObject();
            }
        }

        private static RoundSpec ReadRound(Part part)
        {
            var penetration = ReadNested(part, PenetrationItem, _penetrationItems, p => new PenetrationSpec(
                p.RequireInt(MaxPenetrationsItem),
                p.RequireFloat(DamageMultiplierPerPenetrationItem),
                p.RequireFloat(MaxThicknessItem),
                ReadPenetrationMaterials(p.Fields)));
            var ricochet = ReadNested(part, RicochetItem, _ricochetItems, p => new RicochetSpec(
                p.RequireInt(MaxBouncesItem), p.RequireFloat(MaxGrazingAngleDegreesItem), p.RequireFloat(DamageMultiplierPerBounceItem)));
            var projectile = ReadNested(part, ProjectileItem, _projectileItems, p => new ProjectileSpec(
                p.RequireFloat(SpeedItem),
                p.RequireFloat(GravityMultiplierItem),
                p.RequireFloat(LifetimeSecondsItem),
                p.Enum(OnWorldHitItem, WorldHitResponse.Stop),
                p.String(VisualIdItem)));
            var homing = ReadNested(part, HomingItem, _homingItems, p => new HomingSpec(
                p.RequireFloat(LockOnSecondsItem), p.RequireFloat(LockOnConeDegreesItem), p.RequireFloat(TurnDegreesPerSecondItem)));
            var beam = ReadNested(part, BeamItem, _beamItems, p => new BeamSpec(p.RequireFloat(TickSecondsItem), p.String(VisualIdItem)));
            var spray = ReadNested(part, SprayItem, _sprayItems, p => new SpraySpec(
                p.RequireFloat(TickSecondsItem), p.RequireFloat(ConeDegreesItem), p.String(VisualIdItem)));

            return part.Build(() => new RoundSpec(
                part.Enum(DeliveryItem, RoundDelivery.Hitscan),
                part.Float(RangeItem, RoundSpec.DefaultRange),
                part.Int(PelletCountItem, 1),
                part.Float(PelletSpreadDegreesItem, 0f),
                part.Float(RadiusItem, 0f),
                penetration,
                ricochet,
                projectile,
                homing,
                beam,
                spray));
        }

        private static List<PenetrationMaterialSpec> ReadPenetrationMaterials(ServerDataJson.Fields fields)
        {
            var nodes = fields.Array(MaterialsItem);
            var materials = new List<PenetrationMaterialSpec>(nodes.Count);
            for (var i = 0; i < nodes.Count; i++)
            {
                var element = fields.Element(nodes[i], MaterialsItem, i, _penetrationMaterialItems);
                var whole = new Part(element, null, null, null);
                materials.Add(whole.Build(() => new PenetrationMaterialSpec(
                    element.ToText(element.ItemPath(MaterialIdItem), element.Require(MaterialIdItem)),
                    element.ToFloat(element.ItemPath(PenetrabilityItem), element.Require(PenetrabilityItem)))));
            }

            return materials;
        }

        private static void WriteRound(JsonWriter writer, RoundSpec round)
        {
            writer.Enum(DeliveryItem, round.Delivery);
            writer.Float(RangeItem, round.Range);
            writer.Int(PelletCountItem, round.PelletCount);
            writer.Float(PelletSpreadDegreesItem, round.PelletSpreadDegrees);
            writer.Float(RadiusItem, round.Radius);
            if (round.Penetration != null)
            {
                writer.BeginObject(PenetrationItem);
                writer.Int(MaxPenetrationsItem, round.Penetration.MaxPenetrations);
                writer.Float(DamageMultiplierPerPenetrationItem, round.Penetration.DamageMultiplierPerPenetration);
                writer.Float(MaxThicknessItem, round.Penetration.MaxThickness);
                if (round.Penetration.Materials.Count > 0)
                {
                    writer.BeginArray(MaterialsItem);
                    foreach (var material in round.Penetration.Materials)
                    {
                        writer.BeginElement();
                        writer.String(MaterialIdItem, material.MaterialId);
                        writer.Float(PenetrabilityItem, material.Penetrability);
                        writer.EndObject();
                    }

                    writer.EndArray();
                }

                writer.EndObject();
            }

            if (round.Ricochet != null)
            {
                writer.BeginObject(RicochetItem);
                writer.Int(MaxBouncesItem, round.Ricochet.MaxBounces);
                writer.Float(MaxGrazingAngleDegreesItem, round.Ricochet.MaxGrazingAngleDegrees);
                writer.Float(DamageMultiplierPerBounceItem, round.Ricochet.DamageMultiplierPerBounce);
                writer.EndObject();
            }

            if (round.Projectile != null)
            {
                writer.BeginObject(ProjectileItem);
                writer.Float(SpeedItem, round.Projectile.Speed);
                writer.Float(GravityMultiplierItem, round.Projectile.GravityMultiplier);
                writer.Float(LifetimeSecondsItem, round.Projectile.LifetimeSeconds);
                writer.Enum(OnWorldHitItem, round.Projectile.OnWorldHit);
                WriteVisualId(writer, round.Projectile.VisualId);
                writer.EndObject();
            }

            if (round.Homing != null)
            {
                writer.BeginObject(HomingItem);
                writer.Float(LockOnSecondsItem, round.Homing.LockOnSeconds);
                writer.Float(LockOnConeDegreesItem, round.Homing.LockOnConeDegrees);
                writer.Float(TurnDegreesPerSecondItem, round.Homing.TurnDegreesPerSecond);
                writer.EndObject();
            }

            if (round.Beam != null)
            {
                writer.BeginObject(BeamItem);
                writer.Float(TickSecondsItem, round.Beam.TickSeconds);
                WriteVisualId(writer, round.Beam.VisualId);
                writer.EndObject();
            }

            if (round.Spray != null)
            {
                writer.BeginObject(SprayItem);
                writer.Float(TickSecondsItem, round.Spray.TickSeconds);
                writer.Float(ConeDegreesItem, round.Spray.ConeDegrees);
                WriteVisualId(writer, round.Spray.VisualId);
                writer.EndObject();
            }
        }

        // 投げ物の調整値 (TuningJson) も、武器と同じ書き方で効果を書けるようにする
        internal static ImpactSpec ReadImpact(ServerDataJson.Fields fields)
        {
            return ReadImpact(new Part(fields, null, null, null));
        }

        private static ImpactSpec ReadImpact(Part part)
        {
            var falloff = ReadFalloff(part);
            var explosion = ReadNested(part, ExplosionItem, _explosionItems, p => new ExplosionSpec(
                p.Build(() => new ExplosionDamage(p.RequireInt(MaxDamageItem), p.RequireFloat(RadiusItem), p.Float(FullDamageRadiusItem, 0f))),
                p.Int(StructureDamageAtCenterItem, 0),
                p.Enum(DetonationItem, DetonationTrigger.OnImpact)));
            var statusEffect = ReadNested(part, StatusEffectItem, _statusEffectItems, ReadStatusEffect);
            var areaEffect = ReadNested(part, AreaEffectItem, _areaEffectItems, p => new AreaEffectSpec(
                p.RequireFloat(RadiusItem),
                p.RequireFloat(DurationSecondsItem),
                p.RequireFloat(TickSecondsItem),
                p.RequireInt(DamagePerTickItem),
                ReadNested(p, StatusEffectItem, _statusEffectItems, ReadStatusEffect),
                p.String(VisualIdItem)));
            var blind = ReadNested(part, BlindItem, _blindItems, p => new BlindSpec(
                p.RequireFloat(RadiusItem), p.RequireFloat(DurationSecondsItem), p.RequireFloat(BackFractionItem)));

            return part.Build(() => new ImpactSpec(
                part.Int(DamageItem, ImpactSpec.DefaultDamage),
                falloff,
                part.Float(StructureRadiusItem, 0f),
                explosion,
                statusEffect,
                areaEffect,
                blind));
        }

        private static StatusEffectSpec ReadStatusEffect(Part part)
        {
            return new StatusEffectSpec(
                part.RequireFloat(DurationSecondsItem),
                part.RequireFloat(TickSecondsItem),
                part.RequireInt(DamagePerTickItem),
                part.String(VisualIdItem),
                part.String(StatusKindItem));
        }

        private static DamageFalloff ReadFalloff(Part part)
        {
            var owner = part.Owner(FalloffItem, out var name);
            if (owner == null)
            {
                return DamageFalloff.None;
            }

            var item = owner.ItemPath(name);
            var node = owner.Find(name);
            if (node.Kind == JsonNodeKind.String)
            {
                if (string.Equals(node.Text, NoFalloff, StringComparison.Ordinal))
                {
                    return DamageFalloff.None;
                }

                throw owner.Invalid(item, node, $"must be an object with {string.Join(", ", FalloffItemNames)}, or \"{NoFalloff}\", but is \"{node.Text}\"");
            }

            var fields = owner.Object(name, FalloffItemNames);
            var falloff = new Part(fields, null, null, null);
            return falloff.Build(() => new DamageFalloff(
                falloff.RequireFloat(StartDistanceItem), falloff.RequireFloat(EndDistanceItem), falloff.RequireFloat(MinMultiplierItem)));
        }

        private static void WriteImpact(JsonWriter writer, ImpactSpec impact)
        {
            writer.Int(DamageItem, impact.Damage);
            WriteFalloff(writer, impact.Falloff);
            writer.Float(StructureRadiusItem, impact.StructureRadius);
            if (impact.Explosion != null)
            {
                writer.BeginObject(ExplosionItem);
                writer.Int(MaxDamageItem, impact.Explosion.Damage.MaxDamage);
                writer.Float(RadiusItem, impact.Explosion.Damage.Radius);
                writer.Float(FullDamageRadiusItem, impact.Explosion.Damage.FullDamageRadius);
                writer.Int(StructureDamageAtCenterItem, impact.Explosion.StructureDamageAtCenter);
                writer.Enum(DetonationItem, impact.Explosion.Detonation);
                writer.EndObject();
            }

            if (impact.StatusEffect != null)
            {
                writer.BeginObject(StatusEffectItem);
                WriteStatusEffect(writer, impact.StatusEffect);
                writer.EndObject();
            }

            if (impact.AreaEffect != null)
            {
                writer.BeginObject(AreaEffectItem);
                writer.Float(RadiusItem, impact.AreaEffect.Radius);
                writer.Float(DurationSecondsItem, impact.AreaEffect.DurationSeconds);
                writer.Float(TickSecondsItem, impact.AreaEffect.TickSeconds);
                writer.Int(DamagePerTickItem, impact.AreaEffect.DamagePerTick);
                if (impact.AreaEffect.StatusEffect != null)
                {
                    writer.BeginObject(StatusEffectItem);
                    WriteStatusEffect(writer, impact.AreaEffect.StatusEffect);
                    writer.EndObject();
                }

                WriteVisualId(writer, impact.AreaEffect.VisualId);
                writer.EndObject();
            }

            if (impact.Blind != null)
            {
                writer.BeginObject(BlindItem);
                writer.Float(RadiusItem, impact.Blind.Radius);
                writer.Float(DurationSecondsItem, impact.Blind.DurationSeconds);
                writer.Float(BackFractionItem, impact.Blind.BackFraction);
                writer.EndObject();
            }
        }

        private static void WriteFalloff(JsonWriter writer, DamageFalloff falloff)
        {
            var none = DamageFalloff.None;
            if (falloff.StartDistance == none.StartDistance && falloff.EndDistance == none.EndDistance && falloff.MinMultiplier == none.MinMultiplier)
            {
                writer.String(FalloffItem, NoFalloff);
                return;
            }

            writer.BeginObject(FalloffItem);
            writer.Float(StartDistanceItem, falloff.StartDistance);
            writer.Float(EndDistanceItem, falloff.EndDistance);
            writer.Float(MinMultiplierItem, falloff.MinMultiplier);
            writer.EndObject();
        }

        private static void WriteStatusEffect(JsonWriter writer, StatusEffectSpec statusEffect)
        {
            writer.String(StatusKindItem, statusEffect.KindId);
            writer.Float(DurationSecondsItem, statusEffect.DurationSeconds);
            writer.Float(TickSecondsItem, statusEffect.TickSeconds);
            writer.Int(DamagePerTickItem, statusEffect.DamagePerTick);
            WriteVisualId(writer, statusEffect.VisualId);
        }

        private static void WriteVisualId(JsonWriter writer, string visualId)
        {
            if (visualId.Length > 0)
            {
                writer.String(VisualIdItem, visualId);
            }
        }

        private static AimSpec ReadAim(Part part)
        {
            var zoomLevels = ReadZoomLevels(part);
            var breathHold = ReadNested(part, BreathHoldItem, _breathHoldItems, p => new BreathHoldSpec(
                p.RequireFloat(MaxHoldSecondsItem), p.RequireFloat(RecoverSecondsItem), p.RequireFloat(SwayMultiplierItem)));
            var sight = ReadNested(part, SightSpecJson.SightItem, SightSpecJson.ItemNames, p => SightSpecJson.Read(p.Fields));

            return part.Build(() => new AimSpec(
                part.Float(HipSpreadDegreesItem, 0f),
                part.Float(AdsSpreadDegreesItem, 0f),
                part.Float(MoveSpreadDegreesItem, 0f),
                part.Float(RecoilPitchDegreesItem, 0f),
                part.Float(RecoilYawDegreesItem, 0f),
                part.Float(RecoilRecoveryPerSecondItem, 0f),
                part.Float(RecoilRecoveryDelaySecondsItem, AimSpec.DefaultRecoilRecoveryDelaySeconds),
                part.Float(BlindFireSpreadDegreesItem, 0f),
                zoomLevels,
                breathHold,
                part.Float(SwayDegreesItem, 0f),
                sight,
                part.Owner(AdsMoveSpreadDegreesItem, out _) != null ? part.Float(AdsMoveSpreadDegreesItem, 0f) : (float?)null));
        }

        private static float[] ReadZoomLevels(Part part)
        {
            var owner = part.Owner(ZoomLevelsItem, out var name);
            if (owner == null)
            {
                return null;
            }

            if (name == AdsFieldOfViewItem)
            {
                // 覗き込みの視野角を 1 つだけ持っていた頃の JSON。null は「共通の初期値」の意味で書けた。
                var raw = owner.FindRaw(name);
                if (raw.Kind == JsonNodeKind.Null)
                {
                    return null;
                }

                var fieldOfView = owner.ToFloat(owner.ItemPath(name), raw);
                if (!AdsZoom.IsValidFieldOfView(fieldOfView))
                {
                    throw owner.Rejected(owner.ItemPath(name), "field of view must be greater than 0 and less than 180 degrees");
                }

                return new[] { fieldOfView };
            }

            var nodes = owner.Array(name);
            var levels = new float[nodes.Count];
            for (var i = 0; i < nodes.Count; i++)
            {
                levels[i] = owner.ToFloat(owner.ElementPath(name, i), nodes[i]);
            }

            return levels;
        }

        private static void WriteAim(JsonWriter writer, AimSpec aim)
        {
            writer.Float(HipSpreadDegreesItem, aim.HipSpreadDegrees);
            writer.Float(AdsSpreadDegreesItem, aim.AdsSpreadDegrees);
            writer.Float(MoveSpreadDegreesItem, aim.MoveSpreadDegrees);
            // 移動と同じ値は書かなくても同じに読めるので、項目が無かった頃に書き出したデータを変えない。
            if (aim.AdsMoveSpreadDegrees != aim.MoveSpreadDegrees)
            {
                writer.Float(AdsMoveSpreadDegreesItem, aim.AdsMoveSpreadDegrees);
            }
            writer.Float(RecoilPitchDegreesItem, aim.RecoilPitchDegrees);
            writer.Float(RecoilYawDegreesItem, aim.RecoilYawDegrees);
            writer.Float(RecoilRecoveryPerSecondItem, aim.RecoilRecoveryPerSecond);
            writer.Float(RecoilRecoveryDelaySecondsItem, aim.RecoilRecoveryDelaySeconds);
            writer.Float(BlindFireSpreadDegreesItem, aim.BlindFireSpreadDegrees);
            writer.FloatArray(ZoomLevelsItem, aim.ZoomLevels);
            if (aim.BreathHold != null)
            {
                writer.BeginObject(BreathHoldItem);
                writer.Float(MaxHoldSecondsItem, aim.BreathHold.MaxHoldSeconds);
                writer.Float(RecoverSecondsItem, aim.BreathHold.RecoverSeconds);
                writer.Float(SwayMultiplierItem, aim.BreathHold.SwayMultiplier);
                writer.EndObject();
            }

            writer.Float(SwayDegreesItem, aim.SwayDegrees);
            SightSpecJson.Write(writer, aim.Sight);
        }

        private static List<AmmoTypeSpec> ReadAmmoTypes(ServerDataJson.Fields fields)
        {
            var nodes = fields.Array(AmmoTypesItem);
            if (nodes.Count == 0)
            {
                return null;
            }

            var ammoTypes = new List<AmmoTypeSpec>(nodes.Count);
            for (var i = 0; i < nodes.Count; i++)
            {
                var element = fields.Element(nodes[i], AmmoTypesItem, i, AmmoTypeItemNames);
                var roundFields = element.Object(RoundItem, RoundItemNames);
                var impactFields = element.Object(ImpactItem, ImpactItemNames);
                var round = roundFields == null ? null : ReadRound(new Part(roundFields, null, null, null));
                var impact = impactFields == null ? null : ReadImpact(new Part(impactFields, null, null, null));
                var whole = new Part(element, null, null, null);
                ammoTypes.Add(whole.Build(() => new AmmoTypeSpec(
                    element.ToText(element.ItemPath(IdItem), element.Require(IdItem)),
                    element.ReadString(NameKeyItem, string.Empty),
                    round,
                    impact)));
            }

            return ammoTypes;
        }

        private static T ReadNested<T>(Part owner, string name, IReadOnlyList<string> known, Func<Part, T> read)
            where T : class
        {
            var fields = owner.Fields?.Object(name, known);
            if (fields == null)
            {
                return null;
            }

            var part = new Part(fields, null, null, null);
            return part.Build(() => read(part));
        }

        private static string[] Concat(params string[][] lists)
        {
            var all = new List<string>();
            foreach (var list in lists)
            {
                all.AddRange(list);
            }

            return all.ToArray();
        }

        // 部品のオブジェクトに無い項目は、部品に分ける前の形で武器の直下に平らに書いた項目から読む。
        // 同じ値を両方に書いたら、どちらを使うか読み手が迷うので断る。
        private sealed class Part
        {
            private readonly ServerDataJson.Fields _top;
            private readonly string _name;
            private readonly IReadOnlyList<string> _flatItems;

            public Part(ServerDataJson.Fields fields, ServerDataJson.Fields top, string name, IReadOnlyList<string> flatItems)
            {
                Fields = fields;
                _top = top;
                _name = name;
                _flatItems = flatItems ?? Array.Empty<string>();
            }

            public ServerDataJson.Fields Fields { get; }

            private string Path => Fields != null ? Fields.Path : _top.ItemPath(_name);

            public static Part Of(ServerDataJson.Fields top, string name, IReadOnlyList<string> known, IReadOnlyList<string> flatItems)
            {
                return new Part(top.Object(name, known), top, name, flatItems);
            }

            public ServerDataJson.Fields Owner(string item, out string name)
            {
                var flat = FlatName(item);
                var inPart = Fields != null && Fields.Has(item);
                var inTop = flat != null && _top.Has(flat);
                if (inPart && inTop)
                {
                    throw _top.Rejected(_top.ItemPath(flat), $"is also written as '{Fields.ItemPath(item)}'; write it in one place");
                }

                if (inPart)
                {
                    name = item;
                    return Fields;
                }

                if (inTop)
                {
                    name = flat;
                    return _top;
                }

                name = item;
                return null;
            }

            public float Float(string item, float fallback)
            {
                var owner = Owner(item, out var name);
                return owner == null ? fallback : owner.ReadFloat(name, fallback);
            }

            public int Int(string item, int fallback)
            {
                var owner = Owner(item, out var name);
                return owner == null ? fallback : owner.ReadInt(name, fallback);
            }

            public bool Bool(string item, bool fallback)
            {
                var owner = Owner(item, out var name);
                return owner == null ? fallback : owner.ReadBool(name, fallback);
            }

            public T Enum<T>(string item, T fallback)
                where T : struct, System.Enum
            {
                var owner = Owner(item, out var name);
                return owner == null ? fallback : owner.ReadEnum(name, fallback);
            }

            public string String(string item)
            {
                var owner = Owner(item, out var name);
                return owner == null ? null : owner.ReadString(name, null);
            }

            public float RequireFloat(string item)
            {
                return Fields.ToFloat(Fields.ItemPath(item), Fields.Require(item));
            }

            public int RequireInt(string item)
            {
                return Fields.ToInt(Fields.ItemPath(item), Fields.Require(item));
            }

            public T Build<T>(Func<T> create)
            {
                try
                {
                    return create();
                }
                catch (ArgumentException e)
                {
                    var owner = Fields ?? _top;
                    throw owner.Rejected(PathOf(e.ParamName), e.Message, e);
                }
            }

            private string PathOf(string param)
            {
                if (param == null)
                {
                    return Path;
                }

                // WeaponSpec の古いコンストラクターと同じく、平らに書いた値の不正はその項目名で知らせる。
                var owner = Owner(param, out var name);
                if (owner != null)
                {
                    return owner.ItemPath(name);
                }

                return Path.Length == 0 ? param : Path + "." + param;
            }

            private string FlatName(string item)
            {
                if (_top == null)
                {
                    return null;
                }

                var flat = item == StructureRadiusItem ? ExplosionRadiusItem : item == ZoomLevelsItem ? AdsFieldOfViewItem : item;
                foreach (var candidate in _flatItems)
                {
                    if (string.Equals(candidate, flat, StringComparison.Ordinal))
                    {
                        return flat;
                    }
                }

                return null;
            }
        }
    }
}
