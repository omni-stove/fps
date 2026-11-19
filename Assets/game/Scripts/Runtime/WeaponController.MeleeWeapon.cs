using System;
using Shooter.Core;

namespace Shooter.Runtime
{
    public sealed partial class WeaponController
    {
        private Weapon _chargingWeapon;

        public event Action<MeleeSpec, float> MeleeWeaponSwingRequested;

        public bool IsMeleeWeapon
        {
            get
            {
                var weapon = Weapon;
                return weapon != null && weapon.Spec.IsMelee;
            }
        }

        public MeleeCharge MeleeCharge => Weapon?.MeleeCharge;

        // 持ち替え中・しまっている間に振れると、構えていない武器で振れてしまう。
        public bool IsMeleeWeaponBlocked => IsShieldBashBlocked;

        // 押した瞬間ではなく離した瞬間に振る。押した瞬間に振ると、長押しで溜めた振りと区別できない。
        private bool PullMeleeTrigger(Weapon weapon, bool isPressed, bool wasPressedThisFrame)
        {
            _trigger.Drop();
            FollowChargingWeapon(weapon);
            var charge = weapon.MeleeCharge;
            if (IsMeleeWeaponBlocked)
            {
                charge.Drop();
                return false;
            }

            if (wasPressedThisFrame)
            {
                charge.Press();
            }

            if (!isPressed && charge.TryRelease(out var fraction))
            {
                MeleeWeaponSwingRequested?.Invoke(weapon.Spec.Melee.SwingFor(fraction), fraction);
            }

            return false;
        }

        private void TickMeleeCharge(float deltaSeconds)
        {
            var weapon = Weapon;
            FollowChargingWeapon(weapon);
            weapon?.MeleeCharge?.Advance(deltaSeconds);
        }

        private void DropMeleeCharge()
        {
            Weapon?.MeleeCharge?.Drop();
        }

        // 持ち替えたら前の武器の溜めを捨てる。持ち替えて戻ったときに、前に押していた分で振らないように。
        private void FollowChargingWeapon(Weapon weapon)
        {
            if (ReferenceEquals(weapon, _chargingWeapon))
            {
                return;
            }

            _chargingWeapon?.MeleeCharge?.Drop();
            _chargingWeapon = weapon;
        }
    }
}
