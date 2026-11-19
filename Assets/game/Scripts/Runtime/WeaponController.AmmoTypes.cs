using System;
using Shooter.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Shooter.Runtime
{
    public sealed partial class WeaponController
    {
        public const string SwitchAmmoTypeActionPath = InputActionPaths.SwitchAmmoType;

        public const string AmmoTypeSwitchedLogPrefix = "[Combat] ammo-type";

        // シーンに置いたまま再コンパイルすると、Unity が private の値も保存し直し、null の InputAction を空のアクションとして戻してしまう (再生でシーンを読み直さないので、そのまま使われる)。
        [NonSerialized]
        private InputAction _switchAmmoTypeAction;
        private bool _enabledSwitchAmmoTypeAction;

        public event Action<Weapon> AmmoTypeSwitched;

        public InputAction SwitchAmmoTypeAction => _switchAmmoTypeAction;

        public void SetSwitchAmmoTypeAction(InputAction action)
        {
            ReplaceAction(ref _switchAmmoTypeAction, ref _enabledSwitchAmmoTypeAction, action);

            // Edit Mode では OnDisable が呼ばれず無効に戻せないので、Play 中だけ有効にする。
            if (Application.isPlaying && isActiveAndEnabled)
            {
                _enabledSwitchAmmoTypeAction |= EnableIfNeeded(_switchAmmoTypeAction);
            }
        }

        public bool SwitchAmmoType()
        {
            EnsureInitialized();
            var weapon = Weapon;
            if (weapon == null || IsSwitching || !weapon.TrySwitchAmmoType())
            {
                return false;
            }

            Debug.Log(AmmoTypeSwitchedLog(weapon), this);
            AmmoTypeSwitched?.Invoke(weapon);
            return true;
        }

        public static string AmmoTypeSwitchedLog(Weapon weapon)
        {
            if (weapon == null)
            {
                throw new ArgumentNullException(nameof(weapon));
            }

            var type = weapon.AmmoTypes.Current;
            return $"{AmmoTypeSwitchedLogPrefix} weapon={weapon.Spec.LogName} index={weapon.AmmoTypes.Index} id={type?.Id} damage={weapon.AmmoTypes.Impact.Damage}";
        }

        private void ReadSwitchAmmoType()
        {
            if (_switchAmmoTypeAction != null && _switchAmmoTypeAction.WasPressedThisFrame())
            {
                SwitchAmmoType();
            }
        }
    }
}
