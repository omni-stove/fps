using System;
using Shooter.Core;
using UnityEngine;

namespace Shooter.Runtime
{
    [DisallowMultipleComponent]
    public sealed class SlideMovement : MonoBehaviour, IMovementOverride
    {
        // はしごや乗り物の座席など、掴まる・乗る動きより低くする。
        public const int DefaultPriority = 10;

        public const float AirborneGraceSeconds = SlideStep.AirborneGraceSeconds;

        public const float BlockedRatio = SlideStep.BlockedRatio;

        public const float GroundProbeMargin = SlideStep.GroundProbeMargin;

        private const int MaxGroundHits = 8;

        private static readonly RaycastHit[] _groundBuffer = new RaycastHit[MaxGroundHits];

        [SerializeField, Min(0f), Tooltip("滑り出す速さ (m/s)。終わる速さより大きい")]
        private float _startSpeed = SlideMotion.DefaultStartSpeed;

        [SerializeField, Min(0f), Tooltip("平らな床で 1 秒あたりに落ちる速さ (m/s²)")]
        private float _deceleration = SlideMotion.DefaultDeceleration;

        [SerializeField, Min(0f), Tooltip("最短の秒数の後、これ以下に落ちたら終わる速さ (m/s)")]
        private float _endSpeed = SlideMotion.DefaultEndSpeed;

        [SerializeField, Min(0f), Tooltip("速さが落ちても続ける最短の秒数")]
        private float _minSeconds = SlideMotion.DefaultMinSeconds;

        [SerializeField, Min(0.01f), Tooltip("最長の秒数")]
        private float _maxSeconds = SlideMotion.DefaultMaxSeconds;

        [SerializeField, Min(0f), Tooltip("坂の傾きの sin 1 あたりの加速度 (m/s²)。下りで速くなり、上りで遅くなる")]
        private float _slopeAcceleration = SlideMotion.DefaultSlopeAcceleration;

        [SerializeField, Tooltip("滑る足場として傾きを測るレイヤー")]
        private LayerMask _groundMask = Physics.DefaultRaycastLayers;

        private PlayerController _mover;
        private Damageable _damageable;
        private SlideMotion _motion = new SlideMotion();
        private Slide _rule = Slide.Off;
        private bool _startRequested;
        private bool _hasPendingStart;
        private System.Numerics.Vector2 _pendingDirection;
        private float _pendingSpeed;
        private float _verticalSpeed;
        private float _airborneSeconds;
        private Vector3 _stepFrom;
        private float _expectedHorizontal;

        public Slide Rule
        {
            get => _rule;
            set
            {
                if (!Enum.IsDefined(typeof(Slide), value))
                {
                    throw new ArgumentOutOfRangeException(nameof(value), value, "value is not a defined Slide");
                }

                _rule = value;
                if (value != Slide.On)
                {
                    Cancel();
                }
            }
        }

        public Func<bool> Blocker { get; set; }

        public PlayerController Mover => _mover;

        public SlideMotion Motion => _motion;

        public bool IsSliding => _motion.IsSliding;

        public int Priority => DefaultPriority;

        // ダッシュ中にしゃがんだかは、ダッシュとしゃがみを解釈し終えてから姿勢を当てるまでの間 (= ここ) にしか見分けられないので、
        // 読むたびに始められるかを調べ、始めるなら向きと速さを覚えて Begin で使う。
        public bool IsActive
        {
            get
            {
                var requested = _startRequested;
                _startRequested = false;
                _hasPendingStart = false;
                if (_motion.IsSliding)
                {
                    if (CanContinue())
                    {
                        return true;
                    }

                    FinishSlide(SlideEnd.Cancelled);
                    return false;
                }

                if (!(requested || WantsToStartFromInput()) || !TryMeasureEntry(out _pendingDirection, out _pendingSpeed))
                {
                    return false;
                }

                _hasPendingStart = true;
                return true;
            }
        }

        public PlayerActionConditions Conditions => PlayerActionConditions.Sliding;

        private void Awake()
        {
            EnsureMover();
        }

        private void OnEnable()
        {
            SyncRegistration();
        }

        private void OnDisable()
        {
            Cancel();
            if (_mover != null)
            {
                _mover.UnregisterMovementOverride(this);
            }
        }

        private void OnDestroy()
        {
            BindDamageable(null);
            if (_mover != null)
            {
                _mover.UnregisterMovementOverride(this);
            }
        }

        public void Configure(GameRules rules)
        {
            if (rules == null)
            {
                throw new ArgumentNullException(nameof(rules));
            }

            EnsureMover();
            Rule = rules.Actions.Slide;
            RebuildMotion();
            enabled = _rule == Slide.On;

            // Edit Mode では enabled を変えても OnEnable / OnDisable が走らないので、登録はここでも揃える。
            SyncRegistration();
        }

        public void SetTuning(float startSpeed, float deceleration, float endSpeed, float minSeconds, float maxSeconds, float slopeAcceleration)
        {
            // 先に作って検査し、不正なら今の値を残す。
            var motion = new SlideMotion(startSpeed, deceleration, endSpeed, minSeconds, maxSeconds, slopeAcceleration);
            Cancel();
            _startSpeed = startSpeed;
            _deceleration = deceleration;
            _endSpeed = endSpeed;
            _minSeconds = minSeconds;
            _maxSeconds = maxSeconds;
            _slopeAcceleration = slopeAcceleration;
            _motion = motion;
        }

        public void ApplyTuning(SlideTuning tuning)
        {
            if (tuning == null)
            {
                throw new ArgumentNullException(nameof(tuning));
            }

            _startSpeed = tuning.StartSpeed;
            _deceleration = tuning.Deceleration;
            _endSpeed = tuning.EndSpeed;
            _minSeconds = tuning.MinSeconds;
            _maxSeconds = tuning.MaxSeconds;
            _slopeAcceleration = tuning.SlopeAcceleration;
            RebuildMotion();
        }

        public void BindDamageable(Damageable damageable)
        {
            // 破棄済みでも C# のオブジェクトは残っているので、購読は外せる。
            if (!ReferenceEquals(_damageable, null))
            {
                _damageable.Died -= OnDied;
                _damageable.HealthReset -= OnHealthReset;
            }

            _damageable = damageable;
            if (!ReferenceEquals(_damageable, null))
            {
                _damageable.Died += OnDied;
                _damageable.HealthReset += OnHealthReset;
            }
        }

        public bool TryStart()
        {
            // Edit Mode では OnEnable が走らず isActiveAndEnabled が false のままなので、enabled と階層の有効を直接見る。
            if (!enabled || !gameObject.activeInHierarchy || _motion.IsSliding || !TryMeasureEntry(out _, out _))
            {
                return false;
            }

            _startRequested = true;
            return true;
        }

        public bool Cancel()
        {
            _startRequested = false;
            _hasPendingStart = false;
            return FinishSlide(SlideEnd.Cancelled);
        }

        public void Begin(PlayerController mover)
        {
            StartPending(mover);
        }

        public MovementOverrideStep Step(PlayerController mover, MovementOverrideInput input, float deltaSeconds)
        {
            // 前の Tick で終えたのと同じ Tick に TryStart で頼まれると、選ばれたまま Begin を経ずにここへ来る。
            StartPending(mover);
            var direction = new Vector3(_motion.Direction.X, 0f, _motion.Direction.Y);
            if (!_motion.IsSliding)
            {
                return MovementOverrideStep.Move(Vector3.zero, direction * _motion.Speed);
            }

            LimitIfBlocked(mover, direction);

            if (input.JumpPressed && input.IsGrounded && mover.JumpRule == Shooter.Core.Jump.On)
            {
                return Jump(mover, direction, deltaSeconds);
            }

            var grounded = TryProbeGround(mover, out var normal);
            var slopeSine = SlideStep.SlopeSine(
                new System.Numerics.Vector3(direction.x, direction.y, direction.z),
                new System.Numerics.Vector3(normal.x, normal.y, normal.z),
                grounded,
                out var slopeAlong);
            var along = new Vector3(slopeAlong.X, slopeAlong.Y, slopeAlong.Z);

            var travelled = _motion.Step(deltaSeconds, slopeSine);
            var displacement = along * travelled;
            var velocity = along * _motion.Speed;

            if (grounded || input.IsGrounded)
            {
                _airborneSeconds = 0f;
                _verticalSpeed = 0f;

                // 下り坂で体が足場から離れないよう、PlayerMotor の接地中と同じだけ下へ押し付ける。
                displacement.y += PlayerMotor.GroundedVerticalSpeed * deltaSeconds;
            }
            else
            {
                var before = _verticalSpeed;
                _verticalSpeed -= mover.Gravity * deltaSeconds;
                displacement.y += (before + _verticalSpeed) * 0.5f * deltaSeconds;
                velocity.y = _verticalSpeed;
                _airborneSeconds += deltaSeconds;
                if (_airborneSeconds >= AirborneGraceSeconds)
                {
                    _motion.Finish(SlideEnd.Airborne);
                }
            }

            RememberStep(mover, displacement, direction);
            ReportIfEnded();
            return new MovementOverrideStep(displacement, velocity, crouched: true);
        }

        public void End(PlayerController mover)
        {
            _hasPendingStart = false;
            FinishSlide(SlideEnd.Cancelled);
        }

        private void StartPending(PlayerController mover)
        {
            if (!_hasPendingStart || _motion.IsSliding)
            {
                return;
            }

            _hasPendingStart = false;
            _verticalSpeed = 0f;
            _airborneSeconds = 0f;
            _expectedHorizontal = 0f;
            _stepFrom = mover.transform.position;
            var started = _motion.TryStart(_pendingDirection, _pendingSpeed);
            Debug.Log(SlideMotion.FormatStartLog(_motion.Speed, _motion.Direction), this);
            if (started)
            {
                mover.SpendStamina(StaminaUse.Slide);
            }
        }

        private bool CanContinue()
        {
            return _rule == Slide.On && !IsBlocked();
        }

        // 立った姿勢からしゃがみ始めたときだけにする。滑り終えてしゃがんだままのときに、ダッシュを押したままでも滑り直さないため。
        private bool WantsToStartFromInput()
        {
            return _mover != null
                && _mover.IsInitialized
                && _mover.IsSprinting
                && _mover.IsCrouchActive
                && !_mover.IsCrouched;
        }

        private bool TryMeasureEntry(out System.Numerics.Vector2 direction, out float speed)
        {
            direction = System.Numerics.Vector2.Zero;
            speed = 0f;
            if (_rule != Slide.On
                || _mover == null
                || !_mover.IsInitialized
                || !_mover.IsGrounded
                || _mover.IsVaulting
                || _mover.IsDodging
                || _mover.IsControlHandedOff
                || IsBlocked())
            {
                return false;
            }

            var other = _mover.ActiveMovementOverride;
            if (other != null && !ReferenceEquals(other, this))
            {
                return false;
            }

            var velocity = _mover.Motor.Velocity;
            if (!SlideStep.TryMeasureEntry(new System.Numerics.Vector2(velocity.X, velocity.Z), _mover.WalkSpeed, out direction, out speed))
            {
                return false;
            }

            // 滑り続けている間は呼ばれないので、払えるかを聞くのは滑り出すときだけになる。ほかの条件より後に聞き、断ったログを出し過ぎない。
            if (!_mover.HasStaminaFor(StaminaUse.Slide))
            {
                direction = System.Numerics.Vector2.Zero;
                speed = 0f;
                return false;
            }

            return true;
        }

        private bool IsBlocked()
        {
            return Blocker != null && Blocker();
        }

        // 前の Tick で動かそうとした水平の距離と、実際に動いた距離を比べる。CharacterController.velocity は Time.deltaTime で割った値で、
        // Tick に渡す秒数と合うとは限らないので使わない。
        private void LimitIfBlocked(PlayerController mover, Vector3 direction)
        {
            var position = mover.transform.position;
            var moved = Vector3.Dot(position - _stepFrom, direction);
            if (SlideStep.TryLimitForBlocked(_motion.Speed, moved, _expectedHorizontal, out var limited))
            {
                _motion.LimitSpeed(limited);
            }

            _stepFrom = position;
            _expectedHorizontal = 0f;
        }

        private void RememberStep(PlayerController mover, Vector3 displacement, Vector3 direction)
        {
            _stepFrom = mover.transform.position;
            _expectedHorizontal = Vector3.Dot(displacement, direction);
        }

        // 滑ってきた勢いを水平の速度に残したまま、PlayerMotor のジャンプと同じ初速で跳ぶ。続きの宙の動きは PlayerMotor が受け持つ。
        private MovementOverrideStep Jump(PlayerController mover, Vector3 direction, float deltaSeconds)
        {
            var horizontal = direction * _motion.Speed;
            var start = SlideStep.JumpSpeed(mover.Gravity, mover.JumpHeight);
            var end = start - (mover.Gravity * deltaSeconds);
            var displacement = (horizontal * deltaSeconds) + (Vector3.up * ((start + end) * 0.5f * deltaSeconds));
            _motion.Finish(SlideEnd.Jumped);
            ReportIfEnded();
            _expectedHorizontal = 0f;
            return new MovementOverrideStep(displacement, horizontal + (Vector3.up * end), crouched: false);
        }

        private bool TryProbeGround(PlayerController mover, out Vector3 normal)
        {
            normal = Vector3.up;
            var controller = mover.Controller;
            var body = mover.transform;
            var origin = body.TransformPoint(controller.center);
            var reach = (controller.height * 0.5f * Mathf.Abs(body.lossyScale.y)) + GroundProbeMargin;
            var count = Physics.RaycastNonAlloc(origin, Vector3.down, _groundBuffer, reach, _groundMask, QueryTriggerInteraction.Ignore);
            var found = false;
            var nearest = float.PositiveInfinity;
            for (var i = 0; i < count; i++)
            {
                var hit = _groundBuffer[i];
                if (hit.collider == null || hit.collider.transform.IsChildOf(body) || hit.distance >= nearest)
                {
                    continue;
                }

                nearest = hit.distance;
                normal = hit.normal;
                found = true;
            }

            Array.Clear(_groundBuffer, 0, count);
            return found;
        }

        private bool FinishSlide(SlideEnd reason)
        {
            if (!_motion.Finish(reason))
            {
                return false;
            }

            ReportIfEnded();
            return true;
        }

        private void ReportIfEnded()
        {
            if (_motion.IsSliding || _motion.LastEnd == SlideEnd.None)
            {
                return;
            }

            Debug.Log(SlideMotion.FormatEndLog(_motion.LastEnd, _motion.Speed, _motion.Elapsed, _motion.Distance), this);
        }

        private void OnDied(Damageable _)
        {
            Cancel();
        }

        private void OnHealthReset(Health _)
        {
            Cancel();
        }

        private void EnsureMover()
        {
            if (_mover == null)
            {
                _mover = GetComponent<PlayerController>();
            }
        }

        // 値が変わっていなければ作り直さない。作り直すと最後に終わった理由 (LastEnd) が消え、ルールを入れ直しただけで読めなくなる。
        private void RebuildMotion()
        {
            if (_motion.StartSpeed.Equals(_startSpeed)
                && _motion.Deceleration.Equals(_deceleration)
                && _motion.EndSpeed.Equals(_endSpeed)
                && _motion.MinSeconds.Equals(_minSeconds)
                && _motion.MaxSeconds.Equals(_maxSeconds)
                && _motion.SlopeAcceleration.Equals(_slopeAcceleration))
            {
                return;
            }

            SetTuning(_startSpeed, _deceleration, _endSpeed, _minSeconds, _maxSeconds, _slopeAcceleration);
        }

        private void SyncRegistration()
        {
            if (_mover == null)
            {
                return;
            }

            if (enabled && _rule == Slide.On)
            {
                _mover.RegisterMovementOverride(this);
            }
            else
            {
                _mover.UnregisterMovementOverride(this);
            }
        }
    }
}
