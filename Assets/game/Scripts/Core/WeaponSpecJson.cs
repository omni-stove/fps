using System;
using System.Collections.Generic;

namespace Fps.Core
{
    // 項目名はサンプルの武器の JSON (Samples~/Demo/Weapons/Json) と同じにし、手で書いた武器の JSON をそのまま貼れるようにする。
    internal static class WeaponSpecJson
    {
        public const string IdItem = "id";
        public const string NameKeyItem = "nameKey";
        public const string CategoryItem = "category";
        public const string FireModeItem = "fireMode";
        public const string MagazineSizeItem = "magazineSize";
        public const string ReserveAmmoItem = "reserveAmmo";
        public const string FireIntervalItem = "fireInterval";
        public const string ReloadSecondsItem = "reloadSeconds";
        public const string DamageItem = "damage";
        public const string RangeItem = "range";
        public const string FalloffItem = "falloff";
        public const string ExplosionRadiusItem = "explosionRadius";
        public const string AdsFieldOfViewItem = "adsFieldOfView";
        public const string BlindFireSpreadDegreesItem = "blindFireSpreadDegrees";
        public const string ActiveReloadWindowStartItem = "activeReloadWindowStart";
        public const string ActiveReloadWindowLengthItem = "activeReloadWindowLength";
        public const string ActiveReloadFailSecondsItem = "activeReloadFailSeconds";
        public const string RecoilPitchDegreesItem = "recoilPitchDegrees";
        public const string RecoilYawDegreesItem = "recoilYawDegrees";
        public const string RecoilRecoveryPerSecondItem = "recoilRecoveryPerSecond";
        public const string HipSpreadDegreesItem = "hipSpreadDegrees";
        public const string AdsSpreadDegreesItem = "adsSpreadDegrees";
        public const string MoveSpreadDegreesItem = "moveSpreadDegrees";
        public const string RecoilRecoveryDelaySecondsItem = "recoilRecoveryDelaySeconds";

        public const string StartDistanceItem = "startDistance";
        public const string EndDistanceItem = "endDistance";
        public const string MinMultiplierItem = "minMultiplier";

        public static IReadOnlyList<string> ItemNames { get; } = new[]
        {
            IdItem, NameKeyItem, CategoryItem, FireModeItem, MagazineSizeItem, ReserveAmmoItem, FireIntervalItem, ReloadSecondsItem,
            DamageItem, RangeItem, FalloffItem, ExplosionRadiusItem, AdsFieldOfViewItem, BlindFireSpreadDegreesItem,
            ActiveReloadWindowStartItem, ActiveReloadWindowLengthItem, ActiveReloadFailSecondsItem, RecoilPitchDegreesItem,
            RecoilYawDegreesItem, RecoilRecoveryPerSecondItem, HipSpreadDegreesItem, AdsSpreadDegreesItem, MoveSpreadDegreesItem,
            RecoilRecoveryDelaySecondsItem,
        };

        public static IReadOnlyList<string> FalloffItemNames { get; } = new[] { StartDistanceItem, EndDistanceItem, MinMultiplierItem };

        public static WeaponSpec Read(ServerDataJson.Fields fields)
        {
            var falloff = DamageFalloff.None;
            var falloffFields = fields.Object(FalloffItem, FalloffItemNames);
            if (falloffFields != null)
            {
                try
                {
                    falloff = new DamageFalloff(
                        falloffFields.ReadFloat(StartDistanceItem, DamageFalloff.None.StartDistance),
                        falloffFields.ReadFloat(EndDistanceItem, DamageFalloff.None.EndDistance),
                        falloffFields.ReadFloat(MinMultiplierItem, DamageFalloff.None.MinMultiplier));
                }
                catch (ArgumentException e)
                {
                    throw falloffFields.Rejected(e.ParamName == null ? falloffFields.Path : falloffFields.ItemPath(e.ParamName), e.Message, e);
                }
            }

            var adsFieldOfView = fields.Has(AdsFieldOfViewItem) ? fields.ReadFloat(AdsFieldOfViewItem, 0f) : (float?)null;
            try
            {
                return new WeaponSpec(
                    fields.ToText(fields.ItemPath(IdItem), fields.Require(IdItem)),
                    fields.ReadString(NameKeyItem, string.Empty),
                    fields.ToEnum<WeaponCategory>(fields.ItemPath(CategoryItem), fields.Require(CategoryItem)),
                    fields.ToEnum<FireMode>(fields.ItemPath(FireModeItem), fields.Require(FireModeItem)),
                    fields.ToInt(fields.ItemPath(MagazineSizeItem), fields.Require(MagazineSizeItem)),
                    fields.ReadInt(ReserveAmmoItem, 0),
                    fields.ToFloat(fields.ItemPath(FireIntervalItem), fields.Require(FireIntervalItem)),
                    fields.ToFloat(fields.ItemPath(ReloadSecondsItem), fields.Require(ReloadSecondsItem)),
                    fields.ToInt(fields.ItemPath(DamageItem), fields.Require(DamageItem)),
                    fields.ToFloat(fields.ItemPath(RangeItem), fields.Require(RangeItem)),
                    falloff,
                    fields.ReadFloat(ExplosionRadiusItem, 0f),
                    adsFieldOfView,
                    fields.ReadFloat(BlindFireSpreadDegreesItem, 0f),
                    fields.ReadFloat(ActiveReloadWindowStartItem, WeaponSpec.DefaultActiveReloadWindowStart),
                    fields.ReadFloat(ActiveReloadWindowLengthItem, WeaponSpec.DefaultActiveReloadWindowLength),
                    fields.ReadFloat(ActiveReloadFailSecondsItem, WeaponSpec.DefaultActiveReloadFailSeconds),
                    fields.ReadFloat(RecoilPitchDegreesItem, 0f),
                    fields.ReadFloat(RecoilYawDegreesItem, 0f),
                    fields.ReadFloat(RecoilRecoveryPerSecondItem, 0f),
                    fields.ReadFloat(HipSpreadDegreesItem, 0f),
                    fields.ReadFloat(AdsSpreadDegreesItem, 0f),
                    fields.ReadFloat(MoveSpreadDegreesItem, 0f),
                    fields.ReadFloat(RecoilRecoveryDelaySecondsItem, WeaponSpec.DefaultRecoilRecoveryDelaySeconds));
            }
            catch (ArgumentException e)
            {
                // WeaponSpec は引数名を ParamName に入れて投げ、引数名は項目名と同じなので、そのまま項目に結び付けられる。
                throw fields.Rejected(e.ParamName == null ? fields.Path : fields.ItemPath(e.ParamName), e.Message, e);
            }
        }

        public static void Write(ServerDataJson.Writer writer, WeaponSpec spec)
        {
            writer.String(IdItem, spec.Id);
            writer.String(NameKeyItem, spec.NameKey);
            writer.Enum(CategoryItem, spec.Category);
            writer.Enum(FireModeItem, spec.FireMode);
            writer.Int(MagazineSizeItem, spec.MagazineSize);
            writer.Int(ReserveAmmoItem, spec.ReserveAmmo);
            writer.Float(FireIntervalItem, spec.FireInterval);
            writer.Float(ReloadSecondsItem, spec.ReloadSeconds);
            writer.Int(DamageItem, spec.Damage);
            writer.Float(RangeItem, spec.Range);
            writer.BeginObject(FalloffItem);
            writer.Float(StartDistanceItem, spec.Falloff.StartDistance);
            writer.Float(EndDistanceItem, spec.Falloff.EndDistance);
            writer.Float(MinMultiplierItem, spec.Falloff.MinMultiplier);
            writer.EndObject();
            writer.Float(ExplosionRadiusItem, spec.ExplosionRadius);
            writer.Float(AdsFieldOfViewItem, spec.AdsFieldOfView);
            writer.Float(BlindFireSpreadDegreesItem, spec.BlindFireSpreadDegrees);
            writer.Float(ActiveReloadWindowStartItem, spec.ActiveReloadWindowStart);
            writer.Float(ActiveReloadWindowLengthItem, spec.ActiveReloadWindowLength);
            writer.Float(ActiveReloadFailSecondsItem, spec.ActiveReloadFailSeconds);
            writer.Float(RecoilPitchDegreesItem, spec.RecoilPitchDegrees);
            writer.Float(RecoilYawDegreesItem, spec.RecoilYawDegrees);
            writer.Float(RecoilRecoveryPerSecondItem, spec.RecoilRecoveryPerSecond);
            writer.Float(HipSpreadDegreesItem, spec.HipSpreadDegrees);
            writer.Float(AdsSpreadDegreesItem, spec.AdsSpreadDegrees);
            writer.Float(MoveSpreadDegreesItem, spec.MoveSpreadDegrees);
            writer.Float(RecoilRecoveryDelaySecondsItem, spec.RecoilRecoveryDelaySeconds);
        }
    }
}
