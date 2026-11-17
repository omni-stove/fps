using Shooter.Core;
using UnityEngine;

namespace Shooter.Runtime
{
    public sealed partial class PlayerController
    {
        private InputDeviceKind _device = InputDeviceKind.KeyboardMouse;
        private SprintInput _sprint;
        private WeaponController _releaseWeapon;
        private Damageable _releaseDamageable;

        public InputDeviceKind ActiveDevice => _device;

        public bool IsSprinting => Sprint.IsActive;

        // 自分の入力でダッシュ中かが常に分かるので、いつも true を返す。
        bool ICharacterMotionSource.TryGetSprinting(out bool sprinting)
        {
            sprinting = IsSprinting;
            return true;
        }

        public HoldMode SprintMode => Sprint.Mode;

        private SprintInput Sprint
        {
            get
            {
                // Edit Mode では Awake が走らないので、初めて触れたときに作る。
                if (_sprint == null)
                {
                    _sprint = new SprintInput();
                    _sprint.Changed += OnSprintChanged;
                }

                return _sprint;
            }
        }

        public bool UpdateSprint(Vector2 move, bool isPressed, bool wasPressedThisFrame, InputDeviceKind device)
        {
            var mode = _controlSettings.Get(device, HoldAction.Sprint);
            _device = device;
            return Sprint.Update(mode, move.ToNumerics(), isPressed, wasPressedThisFrame);
        }

        public bool ReleaseSprint(string reason)
        {
            return Sprint.Release(reason);
        }

        public void BindSprintRelease(WeaponController weapon, Damageable damageable)
        {
            if (_releaseWeapon != null)
            {
                _releaseWeapon.Fired -= OnWeaponFired;
            }

            if (_releaseDamageable != null)
            {
                _releaseDamageable.Died -= OnDied;
                _releaseDamageable.HealthReset -= OnHealthReset;
            }

            _releaseWeapon = weapon;
            _releaseDamageable = damageable;

            if (_releaseWeapon != null)
            {
                _releaseWeapon.Fired += OnWeaponFired;
            }

            if (_releaseDamageable != null)
            {
                _releaseDamageable.Died += OnDied;
                _releaseDamageable.HealthReset += OnHealthReset;
            }
        }

        private void OnWeaponFired(Weapon _)
        {
            ReleaseSprint(SprintInput.ReasonFire);
        }

        private void OnDied(Damageable _)
        {
            ReleaseSprint(SprintInput.ReasonDeath);
            CancelDodge();
            CancelVault();
            CancelDive();
            CancelMantle();
        }

        private void OnHealthReset(Health _)
        {
            ReleaseSprint(SprintInput.ReasonRespawn);
            CancelDodge();
            CancelVault();
            CancelDive();
            CancelMantle();

            // 戻った位置で、倒れる前の待ち時間に縛られずすぐ回避できるようにする。
            _dodgeMotion?.Reset();
        }

        private void OnSprintChanged(SprintChange change)
        {
            Debug.Log(SprintInput.FormatLog(change), this);
        }
    }
}
