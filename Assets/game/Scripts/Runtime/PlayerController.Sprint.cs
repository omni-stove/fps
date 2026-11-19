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
        private IStaminaGate _staminaGate;

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

        // 消された部品 (MonoBehaviour) が残した参照を使わないよう、Unity の null 比較で見る。
        public IStaminaGate StaminaGate
        {
            get => _staminaGate != null && !(_staminaGate is UnityEngine.Object unityObject && unityObject == null) ? _staminaGate : null;
            set => _staminaGate = value;
        }

        public bool CanSprintOnStamina
        {
            get
            {
                var gate = StaminaGate;
                return gate == null || gate.CanSprint;
            }
        }

        public bool UpdateSprint(Vector2 move, bool isPressed, bool wasPressedThisFrame, InputDeviceKind device)
        {
            var mode = _controlSettings.Get(device, HoldAction.Sprint);
            _device = device;

            // 尽きている間は押下を無かったことにする。Hold は押したままでも、戻った後に押し直すまで走らない (ほかの自動解除と同じ)。
            if (!CanSprintOnStamina)
            {
                Sprint.Release(SprintInput.ReasonStamina);
                isPressed = false;
                wasPressedThisFrame = false;
            }

            return Sprint.Update(mode, move.ToNumerics(), isPressed, wasPressedThisFrame);
        }

        // 試すときだけ呼ぶ (断ったことをログに出すので、状態を見るだけの用には使わない)。
        public bool HasStaminaFor(StaminaUse use)
        {
            var gate = StaminaGate;
            if (gate == null || gate.CanAfford(use))
            {
                return true;
            }

            Debug.Log(StaminaLog.FormatRefused(use, gate.CostOf(use), gate.Status), this);
            return false;
        }

        public void SpendStamina(StaminaUse use)
        {
            StaminaGate?.Spend(use);
        }

        // Tick を直に呼ぶ側 (テスト・ボット) は SprintInput を通さないので、ダッシュの入力もここで止める。
        private void FilterInputForStamina(ref bool sprint)
        {
            if (!sprint || CanSprintOnStamina)
            {
                return;
            }

            sprint = false;
            ReleaseSprint(SprintInput.ReasonStamina);
        }

        private void StepStamina(bool sprinted, float deltaSeconds)
        {
            var gate = StaminaGate;
            if (gate == null)
            {
                return;
            }

            gate.Step(sprinted, deltaSeconds);

            // 次の入力を待たずに解く。尽きたことと走りやめたことが同じ tick のログに並ぶように。
            if (!gate.CanSprint)
            {
                ReleaseSprint(SprintInput.ReasonStamina);
            }
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
