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
            Show(model, weapon, spec, null);
        }

        // 手元の溜めを優先するのは、サーバーの溜めは撃った弾のぶんが後から届くだけで、押している間のゲージが動かないため。
        public static void Show(HudModel model, in WeaponSync weapon, WeaponSpec spec, float? localCharge)
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
                model.SetCharge(Fraction(localCharge ?? weapon.ChargeFraction));
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
                model.SetAmmoType(types[weapon.AmmoTypeIndex]);
            }
            else
            {
                model.ClearAmmoType();
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
            model.ClearAmmoType();
        }

        private static float Fraction(float value)
        {
            return Math.Min(1f, Math.Max(0f, value));
        }
    }
}
