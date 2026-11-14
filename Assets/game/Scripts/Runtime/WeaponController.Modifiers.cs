using System;
using System.Collections.Generic;
using UnityEngine;

namespace Fps.Runtime
{
    public sealed partial class WeaponController
    {
        public const string ReloadCancelledLogPrefix = "[Combat] reload-cancelled";

        private readonly List<IWeaponModifier> _modifiers = new List<IWeaponModifier>();

        public IReadOnlyList<IWeaponModifier> Modifiers => _modifiers;

        public bool IsFireBlockedByModifier
        {
            get
            {
                foreach (var modifier in _modifiers)
                {
                    if (modifier.BlocksFire)
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        public bool IsReloadBlockedByModifier => FindReloadBlocker() != null;

        public bool AddModifier(IWeaponModifier modifier)
        {
            if (modifier == null)
            {
                throw new ArgumentNullException(nameof(modifier));
            }

            if (_modifiers.Contains(modifier))
            {
                return false;
            }

            _modifiers.Add(modifier);
            return true;
        }

        public bool RemoveModifier(IWeaponModifier modifier)
        {
            if (modifier == null)
            {
                throw new ArgumentNullException(nameof(modifier));
            }

            return _modifiers.Remove(modifier);
        }

        public static string FormatReloadCancelledLog(IWeaponModifier modifier)
        {
            if (modifier == null)
            {
                throw new ArgumentNullException(nameof(modifier));
            }

            return $"{ReloadCancelledLogPrefix} by={modifier.GetType().Name}";
        }

        // 負や NaN の倍率は、広がりを 0 (常に真ん中) にする抜け道にも無限の広がりにもならないよう、その modifier だけ無視して警告する。
        private float ModifierSpreadMultiplier()
        {
            var multiplier = 1f;
            foreach (var modifier in _modifiers)
            {
                var value = modifier.SpreadMultiplier;
                if (!(value >= 0f) || float.IsInfinity(value))
                {
                    Debug.LogWarning($"[WeaponController] {modifier.GetType().Name} returned an invalid spread multiplier {value}; ignoring it", this);
                    continue;
                }

                multiplier *= value;
            }

            return multiplier;
        }

        private IWeaponModifier FindReloadBlocker()
        {
            foreach (var modifier in _modifiers)
            {
                if (modifier.BlocksReload)
                {
                    return modifier;
                }
            }

            return null;
        }

        private void CancelReloadBlockedByModifier()
        {
            var weapon = Weapon;
            if (weapon == null || !weapon.IsReloading)
            {
                return;
            }

            var blocker = FindReloadBlocker();
            if (blocker == null)
            {
                return;
            }

            weapon.CancelReload();
            Debug.Log(FormatReloadCancelledLog(blocker), this);
        }
    }
}
