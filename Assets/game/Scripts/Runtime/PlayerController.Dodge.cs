using System;
using Shooter.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Shooter.Runtime
{
    public sealed partial class PlayerController
    {
        // シーンに置いたまま再コンパイルすると、Unity が private の値も保存し直し、null の InputAction を空のアクションとして戻してしまう (再生でシーンを読み直さないので、そのまま使われる)。
        [NonSerialized]
        private InputAction _dodgeAction;
        private bool _enabledDodge;
        private Dodge _dodgeRule = Dodge.Off;
        private DodgeMotion _dodgeMotion;

        public event Action<Vector3> DodgeStarted;

        public event Action DodgeEnded;

        public bool IsDodging => _dodgeMotion != null && _dodgeMotion.IsRolling;

        public DodgeMotion DodgeMotion => _dodgeMotion;

        public InputAction DodgeAction => _dodgeAction;

        public Dodge DodgeRule
        {
            get => _dodgeRule;
            set
            {
                if (!Enum.IsDefined(typeof(Dodge), value))
                {
                    throw new ArgumentOutOfRangeException(nameof(value), value, "value is not a defined Dodge");
                }

                _dodgeRule = value;
                if (value != Dodge.On)
                {
                    CancelDodge();
                }
            }
        }

        public Func<bool> DodgeBlocker { get; set; }

        public static string FormatDodgingLog(bool dodging)
        {
            return $"{LogTag} dodging={(dodging ? "true" : "false")}";
        }

        public void SetDodgeAction(InputAction action)
        {
            if (_isLive)
            {
                ReleaseDodgeAction();
            }

            _dodgeAction = action;
            if (_isLive)
            {
                AcquireDodgeAction();
            }
        }

        public bool TryStartDodge(Vector2 move)
        {
            if (_dodgeRule != Dodge.On || !IsInitialized || !IsGrounded || IsVaulting || IsMantling || IsMovementOverridden)
            {
                return false;
            }

            if (IsProne || IsProneActive || IsDiving)
            {
                return false;
            }

            if (!_dodgeMotion.IsReady || (DodgeBlocker != null && DodgeBlocker()))
            {
                return false;
            }

            // ほかの条件より後に聞く。断ったログを、ほかの理由でどのみち転がれないときにまで出さないため。
            if (!HasStaminaFor(StaminaUse.Dodge))
            {
                return false;
            }

            var yawRadians = _look.transform.eulerAngles.y * Mathf.Deg2Rad;
            var direction = DodgeMotion.ResolveDirection(move.ToNumerics(), yawRadians);
            if (!_dodgeMotion.TryStart(direction))
            {
                return false;
            }

            SpendStamina(StaminaUse.Dodge);

            ReleaseCrouch(CrouchInput.ReasonDodge);
            ReleaseSprint(SprintInput.ReasonDodge);
            Debug.Log(FormatDodgingLog(true), this);
            DodgeStarted?.Invoke(new Vector3(direction.X, 0f, direction.Y));
            return true;
        }

        public bool CancelDodge()
        {
            if (_dodgeMotion == null || !_dodgeMotion.Cancel())
            {
                return false;
            }

            ReportDodgeEnded();
            return true;
        }

        // 転がっている間は姿勢とジャンプを止める。カバーの差し込み (MoveFilter) の後に呼ぶので、こちらが優先する。
        private void FilterInputForDodge(ref bool sprint, ref bool crouch, ref bool jumpPressed)
        {
            if (!IsDodging)
            {
                return;
            }

            sprint = false;
            crouch = false;
            jumpPressed = false;
        }

        private void TryOverrideMoveForDodge(
            Vector2 move, bool jumpPressed, float deltaSeconds, ref bool overridden, ref Vector3 displacement)
        {
            if (_dodgeMotion == null)
            {
                return;
            }

            var wasRolling = _dodgeMotion.IsRolling;
            var velocity = _dodgeMotion.Velocity;
            var horizontal = _dodgeMotion.Step(deltaSeconds);
            if (!wasRolling)
            {
                return;
            }

            // 縦は PlayerMotor と同じ扱い (接地中は地面へ吸着、空中は重力の台形則) にし、段差から転がり落ちても浮かない。
            var grounded = _controller.isGrounded;
            var verticalStart = grounded ? PlayerMotor.GroundedVerticalSpeed : _motor.Velocity.Y;
            var verticalEnd = grounded ? verticalStart : verticalStart - (_motor.Spec.Gravity * deltaSeconds);

            // 転がり終えて空中にいたら、その勢いのまま PlayerMotor の空中制御に引き継がせる。
            _motor.SetVelocity(new System.Numerics.Vector3(velocity.X, verticalEnd, velocity.Y));

            displacement = new Vector3(horizontal.X, (verticalStart + verticalEnd) * 0.5f * deltaSeconds, horizontal.Y);
            overridden = true;

            if (!_dodgeMotion.IsRolling)
            {
                ReportDodgeEnded();
            }
        }

        private void ReportDodgeEnded()
        {
            Debug.Log(FormatDodgingLog(false), this);
            DodgeEnded?.Invoke();
        }

        private void AcquireDodgeAction()
        {
            if (_dodgeAction == null || _dodgeAction.enabled)
            {
                _enabledDodge = false;
                return;
            }

            _dodgeAction.Enable();
            _enabledDodge = true;
        }

        private void ReleaseDodgeAction()
        {
            DisableIfOwned(_dodgeAction, ref _enabledDodge);
        }

        // Dodge が無い入力アセットでも移動はできるので、警告だけ出して回避の入力を無効にする。
        private void ResolveDodgeAction(InputActionAsset actions)
        {
            var dodge = actions.FindAction(DodgeActionPath);
            if (dodge == null)
            {
                Debug.LogWarning(
                    $"{LogTag} Input Actions '{actions.name}' に '{DodgeActionPath}' がないため、回避は入力からはできません。", this);
            }

            SetDodgeAction(dodge);
        }
    }
}
