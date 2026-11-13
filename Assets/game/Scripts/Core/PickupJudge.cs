using System;

namespace Fps.Core
{
    /// <summary>
    /// 拾える物を拾おうとしたときに何が起きるかを決める。状態は変えず、反映は呼ぶ側が <see cref="PickupOutcome"/> に従って行う。
    /// </summary>
    /// <remarks>
    /// 判定の順: 倒れていれば <see cref="PickupRejection.CollectorDown"/>、陣営の条件に合わなければ <see cref="PickupRejection.SideNotAllowed"/>、
    /// その後に種類ごとの判定をする。
    /// <list type="bullet">
    /// <item>武器: 持っていなければ持ち武器に加える。持っていればその武器の予備弾を <see cref="PickupSpec.Amount"/> まで (上限で切り詰めて) 足し、
    /// 足せなければ <see cref="PickupRejection.AmmoFull"/>。</item>
    /// <item>予備弾: 武器の Id が決まっていればその武器、無ければ今の武器、今の武器が満タンなら持ち武器の並びの順に、足せる最初の武器に足す。</item>
    /// <item>回復・防具: 上限までの足りない分だけ足し、満タンなら拾わない。</item>
    /// <item>タグ: 生きていて陣営の条件に合えば必ず拾える。得点などはモードが <see cref="PickupOutcome.IsOwnSideTag"/> を見て決める。</item>
    /// </list>
    /// </remarks>
    public static class PickupJudge
    {
        public static PickupOutcome Judge(PickupSpec spec, PickupCollector collector)
        {
            if (spec == null)
            {
                throw new ArgumentNullException(nameof(spec));
            }

            if (collector == null)
            {
                throw new ArgumentNullException(nameof(collector));
            }

            if (!collector.IsAlive)
            {
                return PickupOutcome.Rejected(spec.Kind, PickupRejection.CollectorDown);
            }

            if (!spec.Eligibility.Allows(collector.Side))
            {
                return PickupOutcome.Rejected(spec.Kind, PickupRejection.SideNotAllowed);
            }

            switch (spec.Kind)
            {
                case PickupKind.Weapon:
                    return JudgeWeapon(spec, collector);
                case PickupKind.ReserveAmmo:
                    return JudgeAmmo(spec, collector);
                case PickupKind.Health:
                    var missingHealth = collector.MaxHealth - collector.Health;
                    return missingHealth > 0
                        ? PickupOutcome.Healed(Math.Min(spec.Amount, missingHealth))
                        : PickupOutcome.Rejected(spec.Kind, PickupRejection.HealthFull);
                case PickupKind.Armor:
                    if (collector.MaxArmor <= 0)
                    {
                        return PickupOutcome.Rejected(spec.Kind, PickupRejection.NoArmor);
                    }

                    var missingArmor = collector.MaxArmor - collector.Armor;
                    return missingArmor > 0
                        ? PickupOutcome.ArmorAdded(Math.Min(spec.Amount, missingArmor))
                        : PickupOutcome.Rejected(spec.Kind, PickupRejection.ArmorFull);
                case PickupKind.Tag:
                    var ownSide = spec.TagOwnerSide.HasValue && collector.Side.HasValue && spec.TagOwnerSide.Value == collector.Side.Value;
                    return PickupOutcome.TagTaken(spec.TagOwnerId, spec.TagOwnerSide, ownSide);
                default:
                    throw new ArgumentOutOfRangeException(nameof(spec), spec.Kind, "spec has an unknown PickupKind");
            }
        }

        private static PickupOutcome JudgeWeapon(PickupSpec spec, PickupCollector collector)
        {
            if (collector.Weapons == null)
            {
                return PickupOutcome.Rejected(spec.Kind, PickupRejection.CannotHoldWeapons);
            }

            var index = collector.IndexOfWeapon(spec.WeaponId);
            if (index < 0)
            {
                return PickupOutcome.WeaponAdded(spec.WeaponId);
            }

            var added = Math.Min(spec.Amount, collector.Weapons[index].Room);
            return added > 0
                ? PickupOutcome.AmmoAdded(spec.Kind, spec.WeaponId, added)
                : PickupOutcome.Rejected(spec.Kind, PickupRejection.AmmoFull);
        }

        private static PickupOutcome JudgeAmmo(PickupSpec spec, PickupCollector collector)
        {
            var weapons = collector.Weapons;
            if (weapons == null)
            {
                return PickupOutcome.Rejected(spec.Kind, PickupRejection.CannotHoldWeapons);
            }

            if (spec.WeaponId != null)
            {
                var index = collector.IndexOfWeapon(spec.WeaponId);
                return index < 0 ? PickupOutcome.Rejected(spec.Kind, PickupRejection.NoWeapon) : AddAmmo(spec, weapons[index]);
            }

            if (weapons.Count == 0)
            {
                return PickupOutcome.Rejected(spec.Kind, PickupRejection.NoWeapon);
            }

            var current = collector.IndexOfWeapon(collector.CurrentWeaponId);
            if (current >= 0 && weapons[current].Room > 0)
            {
                return AddAmmo(spec, weapons[current]);
            }

            for (var i = 0; i < weapons.Count; i++)
            {
                if (weapons[i].Room > 0)
                {
                    return AddAmmo(spec, weapons[i]);
                }
            }

            return PickupOutcome.Rejected(spec.Kind, PickupRejection.AmmoFull);
        }

        private static PickupOutcome AddAmmo(PickupSpec spec, PickupWeaponState weapon)
        {
            var added = Math.Min(spec.Amount, weapon.Room);
            return added > 0
                ? PickupOutcome.AmmoAdded(spec.Kind, weapon.WeaponId, added)
                : PickupOutcome.Rejected(spec.Kind, PickupRejection.AmmoFull);
        }
    }
}
