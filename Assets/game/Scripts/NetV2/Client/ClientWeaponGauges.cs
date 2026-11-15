using System;
using Fps.Core;
using Fps.Protocol;

namespace Fps.NetV2
{
    // 出すかどうかを武器の値で決めるのは、溜めも熱も無い武器にも WeaponSync は 0 を運ぶので、値だけでは空のゲージが出てしまうため。
    public static class ClientWeaponGauges
    {
        public static void Show(HudModel model, in WeaponSync weapon, WeaponSpec spec)
        {
            if (model == null)
            {
                throw new ArgumentNullException(nameof(model));
            }

            if (spec == null || weapon.WeaponCount == 0)
            {
                Clear(model);
                return;
            }

            if (spec.Trigger.Charge != null)
            {
                model.SetCharge(Fraction(weapon.ChargeFraction));
            }
            else
            {
                model.ClearCharge();
            }

            if (spec.Ammo.Kind == AmmoKind.Heat)
            {
                model.SetHeat(Fraction(weapon.Heat));
            }
            else
            {
                model.ClearHeat();
            }

            var types = spec.AmmoTypes;
            if (weapon.AmmoTypeIndex < types.Count)
            {
                model.SetAmmoTypeName(GameText.Resolve(types[weapon.AmmoTypeIndex].Name));
            }
            else
            {
                model.ClearAmmoTypeName();
            }
        }

        public static void Clear(HudModel model)
        {
            if (model == null)
            {
                throw new ArgumentNullException(nameof(model));
            }

            model.ClearCharge();
            model.ClearHeat();
            model.ClearAmmoTypeName();
        }

        private static float Fraction(float value)
        {
            return Math.Min(1f, Math.Max(0f, value));
        }
    }
}
