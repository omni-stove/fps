using System;
using Shooter.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Shooter.Runtime
{
    public sealed partial class PlayerController
    {
        public const string ProneActionPath = InputActionPaths.Prone;

        // シーンに置いたまま再コンパイルすると、Unity が private の値も保存し直し、null の InputAction を空のアクションとして戻してしまう (再生でシーンを読み直さないので、そのまま使われる)。
        [NonSerialized]
        private InputAction _proneAction;
        private bool _enabledProne;
        private Prone _proneRule = Prone.Off;
        private ProneSpec _proneSpec = ProneSpec.Default;
        // コードで渡した伏せを、後から TryInitializeFromHierarchy で MotorSettings の値に戻さないため。
        private bool _proneSpecAssigned;
        private ProneInput _proneInput;
        private ProneDive _dive;

        public event Action<Vector3> DiveStarted;

        public event Action DiveEnded;

        public InputAction ProneAction => _proneAction;

        public bool IsProneActive => ProneState.IsActive;

        public bool IsDiving => _dive != null && _dive.IsDiving;

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

        public ProneSpec ProneSpec
        {
            get => _proneSpec;
            set
            {
                _proneSpec = value ?? throw new ArgumentNullException(nameof(value));
                _proneSpecAssigned = true;
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

        public static string FormatDivingLog(bool diving)
        {
            return $"{LogTag} diving={(diving ? "true" : "false")}";
        }

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

        public bool UpdateProne(bool pronePressed, bool crouchPressed, bool jumpPressed, bool sprinting, Vector2 move)
        {
            var active = ProneState.Update(CanStartProne(), pronePressed, crouchPressed, jumpPressed, sprinting, out var dive);
            if (dive)
            {
                StartDive(move);
            }

            return active;
        }

        public bool ReleaseProne(string reason)
        {
            CancelDive();
            return ProneState.Release(reason);
        }

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
