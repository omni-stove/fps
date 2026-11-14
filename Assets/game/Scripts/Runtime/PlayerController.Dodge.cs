using System;
using Fps.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Fps.Runtime
{
    /// <summary>
    /// <see cref="PlayerController"/> のうち、回避の処理。
    /// <see cref="DodgeRule"/> が <see cref="Dodge.On"/> で接地しているときに Dodge を押すと、移動入力の向き (無ければ後ろ) へ
    /// <see cref="DodgeMotion"/> で転がる。転がっている間は <see cref="TryOverrideMove"/> で移動を差し替え、しゃがみ・ダッシュ・ジャンプを止める。
    /// 始めるときにしゃがみとダッシュを解き (押し直すまで戻らない)、<see cref="DodgeStarted"/> を呼ぶ (ADS を解くのは購読した側)。
    /// <see cref="DodgeBlocker"/> が true を返す間 (カバー中) は始めない。終わってから <see cref="DodgeMotion.DefaultCooldownSeconds"/> は次を始めない。
    /// </summary>
    public sealed partial class PlayerController
    {
        private InputAction _dodgeAction;
        private bool _enabledDodge;
        private Dodge _dodgeRule = Dodge.Off;
        private DodgeMotion _dodgeMotion;

        /// <summary>転がり始めたときに、転がる向き (ワールドの水平の単位ベクトル) を渡して呼ばれる。</summary>
        public event Action<Vector3> DodgeStarted;

        /// <summary>転がり終えたとき (途中でやめたときを含む) に呼ばれる。</summary>
        public event Action DodgeEnded;

        /// <summary>回避の最中か。</summary>
        public bool IsDodging => _dodgeMotion != null && _dodgeMotion.IsRolling;

        /// <summary>回避の動き。未初期化なら null。</summary>
        public DodgeMotion DodgeMotion => _dodgeMotion;

        /// <summary>読んでいる Dodge アクション。無ければ null (入力では回避しない)。</summary>
        public InputAction DodgeAction => _dodgeAction;

        /// <summary>
        /// 回避を使うか。既定は <see cref="Dodge.Off"/>。<see cref="PlayerActions.Attach"/> がルールの <see cref="ActionRules.Dodge"/> を入れる。
        /// Off にすると転がっている最中でもやめる。
        /// </summary>
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

        /// <summary>true を返す間は回避を始めない (カバー中など)。null なら止めない。</summary>
        public Func<bool> DodgeBlocker { get; set; }

        /// <summary>回避のログ。例: <c>[Player] dodging=true</c>。</summary>
        public static string FormatDodgingLog(bool dodging)
        {
            return $"{LogTag} dodging={(dodging ? "true" : "false")}";
        }

        /// <summary>
        /// 回避の入力にするアクションを差し替える。null なら入力では回避しない (<see cref="TryStartDodge"/> は呼べる)。
        /// 動作中なら、自分が有効にした前のアクションを戻してから新しいものを有効にする。
        /// </summary>
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

        /// <summary>
        /// move (移動入力、x が右・y が前) の向き (ほぼ 0 なら後ろ) へ転がり始める。
        /// <see cref="DodgeRule"/> が Off・未初期化・接地していない・乗り越えかよじ登りか移動の横取り (<see cref="IsMovementOverridden"/>) の最中・
        /// 転がり中か待ち時間中・<see cref="DodgeBlocker"/> が true なら何もせず false。
        /// 始めたらしゃがみとダッシュを解き、<c>[Player] dodging=true</c> をログに出して <see cref="DodgeStarted"/> を呼ぶ。
        /// </summary>
        public bool TryStartDodge(Vector2 move)
        {
            if (_dodgeRule != Dodge.On || !IsInitialized || !IsGrounded || IsVaulting || IsMantling || IsMovementOverridden)
            {
                return false;
            }

            if (!_dodgeMotion.IsReady || (DodgeBlocker != null && DodgeBlocker()))
            {
                return false;
            }

            var yawRadians = _look.transform.eulerAngles.y * Mathf.Deg2Rad;
            var direction = DodgeMotion.ResolveDirection(move.ToNumerics(), yawRadians);
            if (!_dodgeMotion.TryStart(direction))
            {
                return false;
            }

            ReleaseCrouch(CrouchInput.ReasonDodge);
            ReleaseSprint(SprintInput.ReasonDodge);
            Debug.Log(FormatDodgingLog(true), this);
            DodgeStarted?.Invoke(new Vector3(direction.X, 0f, direction.Y));
            return true;
        }

        /// <summary>転がっている最中ならやめる (待ち時間は始まる)。やめたら true。</summary>
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
