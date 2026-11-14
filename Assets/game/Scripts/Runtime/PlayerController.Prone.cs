using System;
using Fps.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Fps.Runtime
{
    /// <summary>
    /// <see cref="PlayerController"/> のうち、伏せの入力と飛び込みの処理。姿勢そのものは .Stance.cs が当てる。
    /// <see cref="ProneRule"/> が <see cref="Prone.On"/> で接地しているときに Prone を押すと伏せ、もう一度押すかしゃがみ・ジャンプ・ダッシュの開始で起き上がる
    /// (<see cref="ProneInput"/>)。ダッシュ中に押すと、その勢いの向きへ <see cref="ProneDive"/> で飛び込んで伏せる。
    /// 伏せている間は <see cref="ProneSpec.Speed"/> で動き、ダッシュもジャンプもせず、上下を向ける範囲を <see cref="ProneSpec.MaxPitchDegrees"/> に絞り
    /// (<see cref="FirstPersonLook.PitchLimit"/>)、射撃のばらつきに <see cref="SpreadScale"/> を掛けさせる (配線は <see cref="GameBootstrap"/>)。
    /// 伏せると、しゃがみとダッシュは解く (押し直すまで戻らない)。
    /// </summary>
    public sealed partial class PlayerController
    {
        /// <summary>Assets/InputSystem_Actions.inputactions の Prone アクション。</summary>
        public const string ProneActionPath = InputActionPaths.Prone;

        private InputAction _proneAction;
        private bool _enabledProne;
        private Prone _proneRule = Prone.Off;
        private ProneSpec _proneSpec = ProneSpec.Default;
        private ProneInput _proneInput;
        private ProneDive _dive;

        /// <summary>飛び込み始めたときに、飛び込む向き (ワールドの水平の単位ベクトル) を渡して呼ばれる。</summary>
        public event Action<Vector3> DiveStarted;

        /// <summary>飛び込み終えたとき (途中でやめたときを含む) に呼ばれる。</summary>
        public event Action DiveEnded;

        /// <summary>読んでいる Prone アクション。無ければ null (入力では伏せない)。</summary>
        public InputAction ProneAction => _proneAction;

        /// <summary>伏せたいか (ボタンを解釈し、自動解除した後の値)。頭上が塞がっていると、false でも <see cref="IsProne"/> は true のことがある。</summary>
        public bool IsProneActive => ProneState.IsActive;

        /// <summary>飛び込みの最中か。</summary>
        public bool IsDiving => _dive != null && _dive.IsDiving;

        /// <summary>
        /// 伏せを使うか。既定は <see cref="Prone.Off"/>。<see cref="PlayerActions.Attach"/> がルールの <see cref="ActionRules.Prone"/> を入れる。
        /// Off にすると伏せたい状態を解き、飛び込みもやめる (実際に起き上がるのは次の <see cref="Tick"/> で頭上が空いているとき)。
        /// </summary>
        public Prone ProneRule
        {
            get => _proneRule;
            set
            {
                if (!Enum.IsDefined(typeof(Prone), value))
                {
                    throw new ArgumentOutOfRangeException(nameof(value), value, "value is not a defined Prone");
                }

                _proneRule = value;
                if (value != Prone.On)
                {
                    ReleaseProne(ProneInput.ReasonRule);
                }
            }
        }

        /// <summary>
        /// 伏せの調整値。既定は <see cref="ProneSpec.Default"/>。初期化済みで変えると、伏せの寸法を作り直し (伏せていれば今の姿勢に当て直す)、飛び込みはやめる。
        /// 伏せの高さが CharacterController に合わないとき (半径の 2 倍未満・しゃがみより高い) は、合う範囲に収めて警告を出す。
        /// </summary>
        public ProneSpec ProneSpec
        {
            get => _proneSpec;
            set
            {
                _proneSpec = value ?? throw new ArgumentNullException(nameof(value));
                if (!IsInitialized)
                {
                    return;
                }

                CancelDive();
                _dive = new ProneDive(value);
                var standing = _stance.Standing;
                _stance = new StanceGeometry(
                    standing.Height,
                    standing.CenterY,
                    standing.EyeY,
                    _stance.Crouched.Height,
                    FitProneHeight(value.Height, _stance.Crouched.Height, _stance.Radius),
                    _stance.Radius);
                ApplyStance(_currentStance);
                ApplyProneLook();
            }
        }

        /// <summary>今撃ったときに弾の散らばりと照準の広がりに掛ける倍率。伏せていれば <see cref="ProneSpec.SpreadScale"/>、それ以外は 1。</summary>
        public float SpreadScale => IsProne ? _proneSpec.SpreadScale : 1f;

        private ProneInput ProneState
        {
            get
            {
                // Edit Mode では Awake が走らないので、初めて触れたときに作る。
                if (_proneInput == null)
                {
                    _proneInput = new ProneInput();
                    _proneInput.Changed += OnProneInputChanged;
                }

                return _proneInput;
            }
        }

        /// <summary>飛び込みのログ。例: <c>[Player] diving=true</c>。</summary>
        public static string FormatDivingLog(bool diving)
        {
            return $"{LogTag} diving={(diving ? "true" : "false")}";
        }

        /// <summary>
        /// 伏せの入力にするアクションを差し替える。null なら入力では伏せない (<see cref="UpdateProne"/> は呼べる)。
        /// 動作中なら、自分が有効にした前のアクションを戻してから新しいものを有効にする。
        /// </summary>
        public void SetProneAction(InputAction action)
        {
            if (_isLive)
            {
                ReleaseProneAction();
            }

            _proneAction = action;
            if (_isLive)
            {
                AcquireProneAction();
            }
        }

        /// <summary>
        /// 1 フレーム分の伏せボタンを解釈し、伏せたいかを返す (<see cref="ProneInput"/>)。
        /// 伏せ始められるのは、<see cref="ProneRule"/> が On・初期化済み・接地している・回避と乗り越えと移動の横取りと飛び込みの最中でないときだけ。
        /// ダッシュ中に伏せたら、move (移動入力) と今の勢いの向きへ飛び込みも始める。変化は <c>[Player] prone=..</c> でログに出す。
        /// </summary>
        public bool UpdateProne(bool pronePressed, bool crouchPressed, bool jumpPressed, bool sprinting, Vector2 move)
        {
            var active = ProneState.Update(CanStartProne(), pronePressed, crouchPressed, jumpPressed, sprinting, out var dive);
            if (dive)
            {
                StartDive(move);
            }

            return active;
        }

        /// <summary>
        /// 伏せたい状態を解き、飛び込みもやめる (死亡・リスポーンなど)。頭上は確かめないが、実際に起き上がるのは次の <see cref="Tick"/> で頭上が空いているときだけ。
        /// 伏せたい状態だったら true。
        /// </summary>
        /// <param name="reason">ログに出す理由 (<see cref="ProneInput.ReasonDeath"/> など)。</param>
        public bool ReleaseProne(string reason)
        {
            CancelDive();
            return ProneState.Release(reason);
        }

        /// <summary>飛び込んでいる最中ならやめる (伏せたままその場で止まる)。やめたら true。</summary>
        public bool CancelDive()
        {
            if (_dive == null || !_dive.Cancel())
            {
                return false;
            }

            ReportDiveEnded();
            return true;
        }

        private bool CanStartProne()
        {
            return _proneRule == Prone.On
                && IsInitialized
                && IsGrounded
                && !IsDodging
                && !IsVaulting
                && !IsDiving
                && !IsMovementOverridden;
        }

        private void OnProneInputChanged(ProneInput input, string reason)
        {
            Debug.Log(ProneInput.FormatLog(input.IsActive, reason), this);
            if (input.IsActive)
            {
                ReleaseSprint(SprintInput.ReasonProne);
                ReleaseCrouch(CrouchInput.ReasonProne);
            }
            else
            {
                CancelDive();
            }
        }

        // 勢いがあればその向き、無ければ移動入力、それも無ければ向いている前へ飛び込む。
        private void StartDive(Vector2 move)
        {
            if (_dive == null || _dive.Distance <= 0f)
            {
                return;
            }

            var velocity = _motor.Velocity;
            var direction = new System.Numerics.Vector2(velocity.X, velocity.Z);
            if (direction.LengthSquared() < 1e-4f)
            {
                var yawRadians = _look.transform.eulerAngles.y * Mathf.Deg2Rad;
                var input = move.sqrMagnitude > 1e-4f ? move.ToNumerics() : new System.Numerics.Vector2(0f, 1f);
                direction = DodgeMotion.ResolveDirection(input, yawRadians);
            }

            if (!_dive.TryStart(direction))
            {
                return;
            }

            Debug.Log(FormatDivingLog(true), this);
            var normalized = _dive.Direction;
            DiveStarted?.Invoke(new Vector3(normalized.X, 0f, normalized.Y));
        }

        // 伏せている (伏せたい) 間はダッシュもジャンプもしない。飛び込みや回避などの差し込みの後に呼ぶ。
        private void FilterInputForProne(ref bool sprint, ref bool jumpPressed)
        {
            if (!IsProne && !IsProneActive)
            {
                return;
            }

            sprint = false;
            jumpPressed = false;
        }

        private void TryOverrideMoveForDive(float deltaSeconds, ref bool overridden, ref Vector3 displacement)
        {
            if (!IsDiving)
            {
                return;
            }

            var velocity = _dive.Velocity;
            var horizontal = _dive.Step(deltaSeconds);

            // 縦は回避と同じく PlayerMotor の扱い (接地中は地面へ吸着、空中は重力の台形則) にする。
            var grounded = _controller.isGrounded;
            var verticalStart = grounded ? PlayerMotor.GroundedVerticalSpeed : _motor.Velocity.Y;
            var verticalEnd = grounded ? verticalStart : verticalStart - (_motor.Spec.Gravity * deltaSeconds);
            _motor.SetVelocity(new System.Numerics.Vector3(velocity.X, verticalEnd, velocity.Y));

            displacement = new Vector3(horizontal.X, (verticalStart + verticalEnd) * 0.5f * deltaSeconds, horizontal.Y);
            overridden = true;

            if (!_dive.IsDiving)
            {
                ReportDiveEnded();
            }
        }

        private void ReportDiveEnded()
        {
            Debug.Log(FormatDivingLog(false), this);
            DiveEnded?.Invoke();
        }

        // 伏せている間だけ上下を絞る。視点の上限より広い値は視点の上限のまま。
        private void ApplyProneLook()
        {
            if (_look == null)
            {
                return;
            }

            _look.PitchLimit = IsProne ? Mathf.Min(_proneSpec.MaxPitchDegrees, _look.MaxPitch) : _look.MaxPitch;
        }

        // CharacterController は半径の 2 倍より低くできず、伏せはしゃがみより高くできないので、その間に収める。
        private float FitProneHeight(float height, float crouchHeight, float radius)
        {
            var fitted = Mathf.Clamp(height, Mathf.Min(radius * 2f, crouchHeight), crouchHeight);
            if (!Mathf.Approximately(fitted, height))
            {
                Debug.LogWarning(
                    $"{LogTag} 伏せの高さ {height:0.##}m はカプセル (半径 {radius:0.##}m、しゃがみ {crouchHeight:0.##}m) に合わないため {fitted:0.##}m にします。",
                    this);
            }

            return fitted;
        }

        private void AcquireProneAction()
        {
            if (_proneAction == null || _proneAction.enabled)
            {
                _enabledProne = false;
                return;
            }

            _proneAction.Enable();
            _enabledProne = true;
        }

        private void ReleaseProneAction()
        {
            DisableIfOwned(_proneAction, ref _enabledProne);
        }

        // Prone が無い入力アセットでも移動はできるので、警告だけ出して伏せの入力を無効にする。
        private void ResolveProneAction(InputActionAsset actions)
        {
            var prone = actions.FindAction(ProneActionPath);
            if (prone == null)
            {
                Debug.LogWarning(
                    $"{LogTag} Input Actions '{actions.name}' に '{ProneActionPath}' がないため、伏せは入力からはできません。", this);
            }

            SetProneAction(prone);
        }
    }
}
