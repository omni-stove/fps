using System;
using System.Collections.Generic;

namespace Shooter.Core
{
    public static class WeaponAttachments
    {
        public static readonly IReadOnlyList<string> None = Array.AsReadOnly(Array.Empty<string>());

        public static WeaponSpec Apply(WeaponSpec spec, IReadOnlyList<string> attachmentItemIds, ItemCatalog catalog)
        {
            if (spec == null)
            {
                throw new ArgumentNullException(nameof(spec));
            }

            if (catalog == null)
            {
                throw new ArgumentNullException(nameof(catalog));
            }

            var attached = Resolve(catalog, spec.Id, attachmentItemIds);
            if (attached.Count == 0)
            {
                return spec;
            }

            var range = 1f;
            var hip = 1f;
            var ads = 1f;
            var move = 1f;
            var recoil = 1f;
            var reload = 1f;
            var magazine = 0;
            var noise = 1f;
            var speed = 1f;
            var weight = spec.Weight;
            IReadOnlyList<float> zoom = null;
            SightSpec sight = null;
            foreach (var item in attached)
            {
                var a = item.Attachment;
                range *= a.RangeMultiplier;
                hip *= a.HipSpreadMultiplier;
                ads *= a.AdsSpreadMultiplier;
                move *= a.MoveSpreadMultiplier;
                recoil *= a.RecoilMultiplier;
                reload *= a.ReloadSecondsMultiplier;
                magazine += a.MagazineSizeBonus;
                noise *= a.ShotNoiseRadiusMultiplier;
                speed *= a.MoveSpeedMultiplier;
                weight += item.Weight;
                zoom = a.ZoomLevels ?? zoom;
                if (a.IsOptic)
                {
                    // 倍率だけ決めた照準器でも武器のスコープの縁や照準を残さないよう、照準器ごと替える。
                    sight = a.Sight ?? SightSpec.Default;
                }
            }

            var aim = spec.Aim;
            var newAim = new AimSpec(
                Angle(aim.HipSpreadDegrees * hip),
                Angle(aim.AdsSpreadDegrees * ads),
                Angle(aim.MoveSpreadDegrees * move),
                Angle(aim.RecoilPitchDegrees * recoil),
                Angle(aim.RecoilYawDegrees * recoil),
                aim.RecoilRecoveryPerSecond,
                aim.RecoilRecoveryDelaySeconds,
                aim.BlindFireSpreadDegrees,
                zoom ?? aim.ZoomLevels,
                aim.BreathHold,
                aim.SwayDegrees,
                sight ?? aim.Sight,
                Angle(aim.AdsMoveSpreadDegrees * move));

            var ammo = spec.Ammo;
            var newAmmo = new AmmoSpec(
                ammo.Kind == AmmoKind.Magazine ? Math.Max(1, ammo.MagazineSize + magazine) : ammo.MagazineSize,
                ammo.ReserveAmmo,
                ammo.ReloadSeconds * reload,
                ammo.ActiveReloadWindowStart,
                ammo.ActiveReloadWindowLength,
                ammo.ActiveReloadFailSeconds,
                ammo.ReloadStyle,
                ammo.ReloadSecondsPerRound * reload,
                ammo.Kind,
                ammo.Heat);

            var ammoTypes = new List<AmmoTypeSpec>(spec.AmmoTypes.Count);
            foreach (var type in spec.AmmoTypes)
            {
                ammoTypes.Add(type.Round == null ? type : new AmmoTypeSpec(type.Id, type.NameKey, WithRange(type.Round, range), type.Impact));
            }

            return new WeaponSpec(
                spec.Id,
                spec.NameKey,
                spec.Category,
                spec.Trigger,
                newAmmo,
                WithRange(spec.Round, range),
                spec.Impact,
                newAim,
                ammoTypes,
                spec.ShotNoiseRadius * noise,
                weight,
                spec.MoveSpeedMultiplier * speed,
                spec.Description,
                spec.DrawSeconds,
                spec.HolsterSeconds,
                spec.Shield,
                spec.Melee);
        }

        public static IReadOnlyList<string> Keep(ItemCatalog catalog, string weaponId, IEnumerable<string> attachmentItemIds)
        {
            if (catalog == null)
            {
                throw new ArgumentNullException(nameof(catalog));
            }

            if (attachmentItemIds == null)
            {
                return None;
            }

            var bySlot = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var itemId in attachmentItemIds)
            {
                if (catalog.CanAttach(weaponId, itemId) && catalog.TryFindAttachment(itemId, out var item))
                {
                    bySlot[item.Attachment.Slot] = itemId;
                }
            }

            if (bySlot.Count == 0)
            {
                return None;
            }

            var kept = new List<string>(bySlot.Count);
            foreach (var slot in catalog.SlotsOf(weaponId))
            {
                if (bySlot.TryGetValue(slot, out var itemId))
                {
                    kept.Add(itemId);
                }
            }

            return kept.AsReadOnly();
        }

        public static IReadOnlyList<string> Attach(
            ItemCatalog catalog, string weaponId, IReadOnlyList<string> current, string attachmentItemId, out string replaced)
        {
            if (catalog == null)
            {
                throw new ArgumentNullException(nameof(catalog));
            }

            replaced = null;
            if (!catalog.CanAttach(weaponId, attachmentItemId) || !catalog.TryFindAttachment(attachmentItemId, out var item))
            {
                return null;
            }

            var next = new List<string>();
            foreach (var itemId in Keep(catalog, weaponId, current))
            {
                if (catalog.TryFindAttachment(itemId, out var on) && string.Equals(on.Attachment.Slot, item.Attachment.Slot, StringComparison.Ordinal))
                {
                    replaced = itemId;
                }
                else
                {
                    next.Add(itemId);
                }
            }

            next.Add(attachmentItemId);
            return Keep(catalog, weaponId, next);
        }

        public static IReadOnlyList<string> Detach(IReadOnlyList<string> current, string attachmentItemId)
        {
            if (current == null)
            {
                return null;
            }

            var at = -1;
            for (var i = 0; i < current.Count; i++)
            {
                if (string.Equals(current[i], attachmentItemId, StringComparison.Ordinal))
                {
                    at = i;
                    break;
                }
            }

            if (at < 0)
            {
                return null;
            }

            var next = new List<string>(current.Count - 1);
            for (var i = 0; i < current.Count; i++)
            {
                if (i != at)
                {
                    next.Add(current[i]);
                }
            }

            return next.Count == 0 ? None : next.AsReadOnly();
        }

        public static bool SameAs(IReadOnlyList<string> a, IReadOnlyList<string> b)
        {
            var left = a ?? None;
            var right = b ?? None;
            if (left.Count != right.Count)
            {
                return false;
            }

            for (var i = 0; i < left.Count; i++)
            {
                if (!string.Equals(left[i], right[i], StringComparison.Ordinal))
                {
                    return false;
                }
            }

            return true;
        }

        public static string Describe(IReadOnlyList<string> attachmentItemIds)
        {
            return attachmentItemIds == null || attachmentItemIds.Count == 0 ? "-" : string.Join(",", attachmentItemIds);
        }

        private static List<ItemSpec> Resolve(ItemCatalog catalog, string weaponId, IReadOnlyList<string> attachmentItemIds)
        {
            var list = new List<ItemSpec>();
            foreach (var itemId in Keep(catalog, weaponId, attachmentItemIds))
            {
                if (catalog.TryFindAttachment(itemId, out var item))
                {
                    list.Add(item);
                }
            }

            return list;
        }

        private static RoundSpec WithRange(RoundSpec round, float multiplier)
        {
            if (multiplier == 1f)
            {
                return round;
            }

            return new RoundSpec(
                round.Delivery,
                round.Range * multiplier,
                round.PelletCount,
                round.PelletSpreadDegrees,
                round.Radius,
                round.Penetration,
                round.Ricochet,
                round.Projectile,
                round.Homing,
                round.Beam,
                round.Spray);
        }

        // AimSpec が 90 度を超える角度を断るので、掛けた結果は 90 度で止める
        private static float Angle(float degrees)
        {
            return Math.Min(90f, degrees);
        }
    }
}
