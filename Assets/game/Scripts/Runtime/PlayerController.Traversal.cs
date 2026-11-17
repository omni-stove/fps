using System;
using System.Globalization;
using Shooter.Core;
using UnityEngine;
using NVector2 = System.Numerics.Vector2;
using NVector3 = System.Numerics.Vector3;

namespace Shooter.Runtime
{
    // 予測・サーバーの CharacterMotorSimulation と同じ Core の段 (SwimStep・LeanStep・AirJumpStep) を、CharacterController で動くオフラインのプレイヤーから呼ぶ。
    // シミュレーションそのものを通さないのは、オフラインの移動が CharacterController の当たりと、はしご・ジップラインなどの横取りで組まれているため。
    public sealed partial class PlayerController
    {
        private IWaterVolumes _water = Traversal.WaterVolumes.Scene;
        private SwimTuning _swimTuning;
        private CharacterSwimState _swim;
        private CharacterLeanState _lean;
        private int _airJumpsUsed;
        private LeanController _leanSource;
        private CoverController _cover;
        private CharacterBody _leanBody;

        // Tick の引数に足さないのは、縁へ上がる崖のぼりは押した瞬間だけで試し、押したままの間は毎 tick 縁を調べ直させないため。
        private bool _ascendHeld;

        public event Action<bool> SwimmingChanged;

        // 見た目と音の UnderwaterPresenter と同じく、その場で開いている水を引く。予測とサーバーに渡す写しと違い、オフラインには食い違う相手がいないため。
        public IWaterVolumes Water
        {
            get => _water;
            set => _water = value ?? throw new ArgumentNullException(nameof(value));
        }

        // 既定の値でなく traversal の調整値を読み、ゲームが泳ぐ速さと浮く力をデータで変えられるようにする。
        public SwimTuning SwimTuning
        {
            get => _swimTuning ??= TraversalTuning.Current.Swim;
            set => _swimTuning = value ?? throw new ArgumentNullException(nameof(value));
        }

        public CharacterSwimState Swim => _swim;

        public bool IsSwimming => _swim.IsSwimming;

        public CharacterLeanState Lean => _lean;

        public int AirJumpsUsed => _airJumpsUsed;

        public LeanController LeanSource
        {
            get => _leanSource;
            set => _leanSource = value;
        }

        // 足音と着地の音は接地で決まるので、水底に触れながら泳いでいる間を歩いていると数えさせない。
        bool ICharacterMotionSource.IsGrounded => IsGrounded && !IsSwimming;

        public static string FormatSwimmingLog(bool swimming, float surfaceY)
        {
            return swimming
                ? $"{LogTag} swimming=true surface={surfaceY.ToString("F2", CultureInfo.InvariantCulture)}"
                : $"{LogTag} swimming=false";
        }

        public bool TryGetMotorState(out CharacterMotorState state)
        {
            if (!IsInitialized)
            {
                state = default;
                return false;
            }

            state = CaptureMotorState();
            return true;
        }

        // LeanController は PlayerActions.Attach で PlayerController より後に付くので、見つかるまで探し直す。
        private int ReadLean()
        {
            if (_leanSource == null)
            {
                TryGetComponent(out _leanSource);
            }

            return _leanSource != null ? _leanSource.Lean : 0;
        }

        private void UpdateSwim(float deltaSeconds)
        {
            if (!(deltaSeconds > 0f) || float.IsInfinity(deltaSeconds))
            {
                return;
            }

            var next = SwimStep.Step(_swim, transform.position.ToNumerics(), _controller.isGrounded, _stance, _water, SwimTuning, deltaSeconds);
            SetSwim(next);
        }

        private void SetSwim(CharacterSwimState next)
        {
            var was = _swim.IsSwimming;
            _swim = next;
            if (was == next.IsSwimming)
            {
                return;
            }

            if (next.IsSwimming)
            {
                // CharacterMotorSimulation.StepSwim と同じく、水の中で使えない動きをやめてから泳がせる。
                CancelDodge();
                ReleaseProne(ProneInput.ReasonSwim);
                ReleaseCrouch(CrouchInput.ReasonSwim);
                ReleaseSprint(SprintInput.ReasonSwim);
                _airJumpsUsed = 0;
            }

            Debug.Log(FormatSwimmingLog(next.IsSwimming, next.SurfaceY), this);
            SwimmingChanged?.Invoke(next.IsSwimming);
        }

        // 予測・サーバーの泳ぎは乗り越えの軌跡で縁へ上がるが、オフラインの乗り越えは接地を前提にしているので、掴まって上がる崖のぼりを使う。
        private bool TryClimbOutOfWater(bool jumpPressed, float deltaSeconds, out Vector3 displacement)
        {
            displacement = Vector3.zero;
            if (!jumpPressed || !TryStartMantleWhileHanging())
            {
                return false;
            }

            var overridden = false;
            TryOverrideMoveForMantle(false, deltaSeconds, ref overridden, ref displacement);
            return overridden;
        }

        // 倒れている間は潜らせない。しゃがみの入力が残ったまま倒れると、倒れただけで溺れるため (CharacterMotorSimulation.Paddle と同じ)。
        private Vector3 Paddle(Vector2 move, bool crouch, bool jumpPressed, bool wasSwimming, float deltaSeconds)
        {
            if (!(deltaSeconds > 0f) || float.IsInfinity(deltaSeconds))
            {
                return Vector3.zero;
            }

            var target = ResolveFallDamageTarget();
            var dive = crouch && (target == null || !target.IsDowned);
            var yawRadians = _look.transform.eulerAngles.y * Mathf.Deg2Rad;
            var gap = SwimStep.FloatY(_swim.SurfaceY, _stance) - transform.position.y;
            var velocity = SwimStep.Paddle(
                wasSwimming,
                move.ToNumerics(),
                yawRadians,
                dive,
                jumpPressed || _ascendHeld,
                _motor.Velocity.Y,
                gap,
                CarrySpeedMultiplier,
                SwimTuning,
                deltaSeconds,
                out var displacement);
            _motor.SetVelocity(velocity);
            return displacement.ToUnity();
        }

        // 水底や天井に当たったまま縦の速さを残すと、次の tick もそこへ押し付け続ける。
        private void StopPaddlingAgainst(CollisionFlags collisions)
        {
            var velocity = _motor.Velocity;
            if ((velocity.Y < 0f && (collisions & CollisionFlags.Below) != 0) || (velocity.Y > 0f && (collisions & CollisionFlags.Above) != 0))
            {
                _motor.SetVelocity(new NVector3(velocity.X, 0f, velocity.Z));
            }
        }

        // 地上のジャンプは PlayerMotor.Step が跳ぶので、空中の分だけをここで数える。
        private void StepAirJump(Vector2 move, bool jumpPressed, float yawRadians)
        {
            _airJumpsUsed = _motor.StepAirJump(_airJumpsUsed, jumpPressed, _controller.isGrounded, move.ToNumerics(), yawRadians);
        }

        // 跳んだ tick に着地・つかまったときも 0 に戻すので、CharacterMotorSimulation.Finish と同じく tick の終わりで戻す。
        private void FinishTraversal(bool footing, Vector2 move, bool sprint, bool crouch, int lean, float deltaSeconds)
        {
            if (footing || _swim.IsSwimming || IsMovementOverridden)
            {
                _airJumpsUsed = 0;
            }

            UpdateLean(move, sprint, crouch, lean, deltaSeconds);
        }

        // カバー・乗り越え・崖のぼり・回避・はしごなどの横取りは CharacterMotorState に載らないオフラインの部品が持つので、ここで押していないことにしてから LeanStep に渡す。
        private void UpdateLean(Vector2 move, bool sprint, bool crouch, int lean, float deltaSeconds)
        {
            if (!(deltaSeconds >= 0f) || float.IsInfinity(deltaSeconds))
            {
                return;
            }

            var allowed = lean != 0
                && !IsControlHandedOff
                && !IsInCover
                && !IsVaulting
                && !IsMantling
                && !IsDodging
                && !IsMovementOverridden;

            // 傾けておらず押してもいない tick は答えが 0 のままなので、壁を調べる体を作らずに済ませる。
            if (!allowed && _lean.Amount == 0f)
            {
                return;
            }

            var yawRadians = _look.transform.eulerAngles.y * Mathf.Deg2Rad;
            var input = new CharacterMotorInput(
                move.ToNumerics(),
                yawRadians,
                sprint,
                false,
                crouch,
                false,
                false,
                false,
                false,
                false,
                false,
                false,
                false,
                1f,
                true,
                allowed ? lean : 0);
            _lean = LeanStep.Step(CaptureMotorState(), input, _swim, PlaceLeanBody(), _motor.Spec, deltaSeconds);
        }

        private bool IsInCover
        {
            get
            {
                if (_cover == null)
                {
                    TryGetComponent(out _cover);
                }

                return _cover != null && _cover.isActiveAndEnabled && _cover.IsInCover;
            }
        }

        // LeanStep は壁までの空きを CharacterBody の当たりで測るので、自分のコライダーを除く Unity の当たりで体を作り、tick ごとに今の位置と姿勢へ置く。
        private CharacterBody PlaceLeanBody()
        {
            if (_leanBody == null)
            {
                var moverSpec = new CapsuleMoverSpec(
                    Mathf.Clamp(_controller.slopeLimit, 0f, 90f),
                    Mathf.Max(0f, _controller.stepOffset),
                    Mathf.Max(_controller.skinWidth, 1e-4f),
                    _headroomMask);
                _leanBody = new CharacterBody(new ShotWorldCollision(transform), moverSpec, _stance);
            }

            if (_leanBody.Stance != _currentStance)
            {
                _leanBody.ApplyStance(_currentStance);
            }

            _leanBody.Place(transform.position.ToNumerics(), _controller.isGrounded);
            return _leanBody;
        }

        private CharacterMotorState CaptureMotorState()
        {
            var prone = new CharacterProneState(IsProneActive, false, false, IsDiving, NVector2.Zero, 0f);
            return new CharacterMotorState(
                transform.position.ToNumerics(),
                _motor.Velocity,
                _controller.isGrounded && !_swim.IsSwimming,
                _currentStance,
                CharacterCoverState.Out,
                DodgeSnapshot.Ready,
                CharacterVaultState.None,
                prone,
                CharacterLadderState.None,
                CharacterKnockbackState.None,
                _lean,
                _swim,
                CharacterWallRunState.None,
                _airJumpsUsed);
        }

        private void ResetTraversal()
        {
            SetSwim(CharacterSwimState.None);
            _lean = CharacterLeanState.None;
            _airJumpsUsed = 0;
        }

        private bool IsLandingInWater()
        {
            var position = transform.position;
            var feet = new NVector3(position.x, position.y + _stance.Shape(_currentStance).BottomY, position.z);
            return SwimStep.IsInWater(_water, feet);
        }
    }
}
