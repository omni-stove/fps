using System;
using System.Collections.Generic;
using Shooter.Core;
using Shooter.Runtime;
using UnityEditor;

namespace Shooter.Editor
{
    public static class WeaponAssets
    {
        public const string CatalogFileName = "WeaponCatalog.asset";

        public static string Folder => ShooterKitRoot.OutputPath("Weapons");

        public static string CatalogPath => Folder + "/" + CatalogFileName;

        public static WeaponDefinition LoadDefinition(string path)
        {
            path = SceneBuildHelpers.ValidateAssetPath(path);

            var definition = AssetDatabase.LoadAssetAtPath<WeaponDefinition>(path);
            if (definition == null)
            {
                throw new InvalidOperationException($"weapon definition '{path}' was not found; build or import the weapons first");
            }

            definition.ToSpec();
            return definition;
        }

        public static WeaponCatalogAsset LoadCatalog(string path)
        {
            path = SceneBuildHelpers.ValidateAssetPath(path);

            var catalog = AssetDatabase.LoadAssetAtPath<WeaponCatalogAsset>(path);
            if (catalog == null)
            {
                throw new InvalidOperationException($"weapon catalog '{path}' was not found; build or import the weapons first");
            }

            catalog.ToCatalog();
            return catalog;
        }

        public static LoadoutDefinition LoadLoadout(string path)
        {
            path = SceneBuildHelpers.ValidateAssetPath(path);

            var loadout = AssetDatabase.LoadAssetAtPath<LoadoutDefinition>(path);
            if (loadout == null)
            {
                throw new InvalidOperationException($"loadout '{path}' was not found; build or import the weapons first");
            }

            loadout.ToSpecs();
            return loadout;
        }

        public static WeaponDefinition FindInCatalog(WeaponCatalogAsset catalog, string placementId, string weaponId)
        {
            if (catalog == null)
            {
                throw new ArgumentNullException(nameof(catalog));
            }

            if (!catalog.TryFind(weaponId, out var definition))
            {
                throw new KeyNotFoundException(
                    $"placement '{placementId}' uses weapon id '{weaponId}', which is not in weapon catalog '{catalog.name}'");
            }

            return definition;
        }

        // 飛ぶ弾・ビーム・火炎・爆発の見た目と音の参照は spec に無いので、definition に入っているものを残す。
        public static void Configure(WeaponDefinition definition, WeaponSpec spec)
        {
            if (definition == null)
            {
                throw new ArgumentNullException(nameof(definition));
            }

            if (spec == null)
            {
                throw new ArgumentNullException(nameof(spec));
            }

            definition.Id = spec.Id;
            definition.NameKey = spec.NameKey;
            definition.Category = spec.Category;
            definition.FireMode = spec.FireMode;
            definition.MagazineSize = spec.MagazineSize;
            definition.ReserveAmmo = spec.ReserveAmmo;
            definition.FireInterval = spec.FireInterval;
            definition.ReloadSeconds = spec.ReloadSeconds;
            definition.Damage = spec.Damage;
            definition.Range = spec.Range;
            definition.FalloffStart = spec.Falloff.StartDistance;
            definition.FalloffEnd = spec.Falloff.EndDistance;
            definition.FalloffMinMultiplier = spec.Falloff.MinMultiplier;
            definition.ExplosionRadius = spec.ExplosionRadius;
            definition.AdsFieldOfView = spec.AdsFieldOfView;
            definition.BlindFireSpreadDegrees = spec.BlindFireSpreadDegrees;
            definition.ActiveReloadWindowStart = spec.ActiveReloadWindowStart;
            definition.ActiveReloadWindowLength = spec.ActiveReloadWindowLength;
            definition.ActiveReloadFailSeconds = spec.ActiveReloadFailSeconds;
            definition.RecoilPitchDegrees = spec.RecoilPitchDegrees;
            definition.RecoilYawDegrees = spec.RecoilYawDegrees;
            definition.RecoilRecoveryPerSecond = spec.RecoilRecoveryPerSecond;
            definition.HipSpreadDegrees = spec.HipSpreadDegrees;
            definition.AdsSpreadDegrees = spec.AdsSpreadDegrees;
            definition.MoveSpreadDegrees = spec.MoveSpreadDegrees;
            definition.RecoilRecoveryDelaySeconds = spec.RecoilRecoveryDelaySeconds;
            definition.ShotNoiseRadius = spec.ShotNoiseRadius;
            definition.Weight = spec.Weight;
            definition.MoveSpeedMultiplier = spec.MoveSpeedMultiplier;
            definition.DrawSeconds = spec.DrawSeconds;
            definition.HolsterSeconds = spec.HolsterSeconds;
            definition.Description = spec.Description;
            definition.Shield.Set(spec.Shield);

            var trigger = spec.Trigger;
            definition.Burst.Set(trigger.Burst);
            definition.Charge.Set(trigger.Charge);
            definition.SpinUp.Set(trigger.SpinUp);
            definition.Cycle.Set(trigger.Cycle);

            var ammo = spec.Ammo;
            definition.AmmoKind = ammo.Kind;
            definition.ReloadStyle = ammo.ReloadStyle;
            definition.ReloadSecondsPerRound = ammo.ReloadSecondsPerRound;
            definition.Heat.Set(ammo.Heat);

            var round = spec.Round;
            definition.Delivery = round.Delivery;
            definition.PelletCount = round.PelletCount;
            definition.PelletSpreadDegrees = round.PelletSpreadDegrees;
            definition.RoundRadius = round.Radius;
            definition.Penetration.Set(round.Penetration);
            definition.Ricochet.Set(round.Ricochet);
            definition.Projectile.Set(round.Projectile);
            definition.Homing.Set(round.Homing);
            definition.Beam.Set(round.Beam);
            definition.Spray.Set(round.Spray);

            var impact = spec.Impact;
            definition.Explosion.Set(impact.Explosion);
            definition.StatusEffect.Set(impact.StatusEffect);
            definition.AreaEffect.Set(impact.AreaEffect);

            var aim = spec.Aim;
            var extraZoomLevels = new float[aim.ZoomLevels.Count - 1];
            for (var i = 0; i < extraZoomLevels.Length; i++)
            {
                extraZoomLevels[i] = aim.ZoomLevels[i + 1];
            }

            definition.ExtraZoomLevels = extraZoomLevels;
            definition.BreathHold.Set(aim.BreathHold);
            definition.SwayDegrees = aim.SwayDegrees;
            definition.Sight.Set(aim.Sight);

            // 同じ Id の弾薬の種類は前の要素に写し、そちらにだけある見た目と音の参照を残す。
            var previous = definition.AmmoTypes;
            var ammoTypes = new AmmoTypeSettings[spec.AmmoTypes.Count];
            for (var i = 0; i < ammoTypes.Length; i++)
            {
                var ammoType = spec.AmmoTypes[i];
                ammoTypes[i] = Array.Find(previous, p => p != null && string.Equals(p.Id.Trim(), ammoType.Id, StringComparison.Ordinal))
                    ?? new AmmoTypeSettings();
                ammoTypes[i].Set(ammoType);
            }

            definition.AmmoTypes = ammoTypes;

            // spec は検査済みだが、アセットの形で読み直せることも確かめる。不正なら保存しない。
            definition.ToSpec();
        }

        public static void Configure(WeaponCatalogAsset catalog, IEnumerable<WeaponDefinition> weapons)
        {
            if (catalog == null)
            {
                throw new ArgumentNullException(nameof(catalog));
            }

            if (weapons == null)
            {
                throw new ArgumentNullException(nameof(weapons));
            }

            catalog.SetWeapons(weapons);

            // 不正ならここで WeaponDefinitionException になり、壊れたアセットを保存しない。
            catalog.ToCatalog();
        }

        public static void Configure(LoadoutDefinition loadout, IEnumerable<LoadoutSlotDefinition> slots, float switchSeconds)
        {
            if (loadout == null)
            {
                throw new ArgumentNullException(nameof(loadout));
            }

            if (slots == null)
            {
                throw new ArgumentNullException(nameof(slots));
            }

            loadout.SetSlots(slots);
            loadout.SwitchSeconds = switchSeconds;

            // 不正ならここで WeaponDefinitionException になり、壊れたアセットを保存しない。
            loadout.ToSpecs();
        }
    }
}
