using Fps.Core;
using UnityEngine;

namespace Fps.Runtime
{
    /// <summary>
    /// <see cref="PlayerController"/> のうち、ダッシュの処理。
    /// Sprint は <see cref="ControlSettings"/> のモード (押している間／押すたびに切り替え) で <see cref="SprintInput"/> が解釈し、
    /// 射撃・死亡・リスポーンでの解除は <see cref="BindSprintRelease"/> で購読する。
    /// </summary>
    public sealed partial class PlayerController
    {
        private InputDeviceKind _device = InputDeviceKind.KeyboardMouse;
        private SprintInput _sprint;
        private WeaponController _releaseWeapon;
        private Damageable _releaseDamageable;

        /// <summary>最後にダッシュか移動の入力をした機器。ダッシュのモードはこの機器の設定を使う。</summary>
        public InputDeviceKind ActiveDevice => _device;

        /// <summary>ダッシュ中か (ボタンをモードどおりに解釈し、自動解除した後の値)。</summary>
        public bool IsSprinting => Sprint.IsActive;

        /// <summary>自分の入力でダッシュ中かが常に分かるので、<see cref="IsSprinting"/> を入れて true を返す。</summary>
        bool ICharacterMotionSource.TryGetSprinting(out bool sprinting)
        {
            sprinting = IsSprinting;
            return true;
        }

        /// <summary>今使っているダッシュのモード。</summary>
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

        /// <summary>
        /// 1 フレーム分のダッシュボタンと移動入力を、device の <see cref="ControlSettings"/> のモードで解釈し、ダッシュ中かを返す。
        /// Toggle では移動入力がなくなると解除する (<see cref="SprintInput"/>)。変化は <c>[Player] sprint=..</c> でログに出す。
        /// </summary>
        public bool UpdateSprint(Vector2 move, bool isPressed, bool wasPressedThisFrame, InputDeviceKind device)
        {
            var mode = _controlSettings.Get(device, HoldAction.Sprint);
            _device = device;
            return Sprint.Update(mode, move.ToNumerics(), isPressed, wasPressedThisFrame);
        }

        /// <summary>
        /// ダッシュを解除する。Hold でもボタンを押し直すまでダッシュしない。ADS など外の処理から呼ぶ。
        /// ダッシュ中だったら true。
        /// </summary>
        /// <param name="reason">ログに出す理由 (<see cref="SprintInput.ReasonFire"/> など)。</param>
        public bool ReleaseSprint(string reason)
        {
            return Sprint.Release(reason);
        }

        /// <summary>
        /// weapon が撃ったとき、damageable が死んだとき・体力を作り直した (リスポーンした) ときにダッシュを解除するよう購読する。
        /// 死んだとき・体力を作り直したときは回避・乗り越え・飛び込みもやめる。
        /// 前に渡したものの購読は外す。どちらも null 可 (その理由では解除しない)。
        /// </summary>
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
