using System;
using Fps.Core;
using UnityEngine;

namespace Fps.Runtime
{
    /// <summary>
    /// ダッシュ中にしゃがむと滑り込むスライディングを受け持つ、プレイヤーのコンポーネント。<see cref="PlayerController"/> の移動を
    /// <see cref="IMovementOverride"/> として横取りし、速さの移り変わりは <see cref="SlideMotion"/> に任せる。
    /// <list type="bullet">
    /// <item>始まり: <see cref="Rule"/> が <see cref="Slide.On"/> で、接地して歩きより速く動いているときに、ダッシュ中 (<see cref="PlayerController.IsSprinting"/>) に
    /// 立った姿勢からしゃがむ (<see cref="PlayerController.IsCrouchActive"/>) か、<see cref="TryStart"/> を呼んだとき。
    /// <see cref="Blocker"/> が true (カバー中) の間・乗り越えか回避の最中・ほかの横取りの最中・操作を手放している間は始まらない。
    /// 滑る向きは始まったときの移動の向きで、視点を回しても変わらない。</item>
    /// <item>最中: 低い姿勢 (しゃがみの当たり判定) のまま足場に沿って滑る。撃てるが、速く動いているので照準は広がる (<see cref="GameBootstrap"/> の配線)。
    /// 壁に当たって進めなければ、その分だけ遅くなる。</item>
    /// <item>終わり: 速さが落ちるか最長の秒数で (<see cref="SlideMotion"/>)、ジャンプで (勢いを持ったまま跳ぶ)、宙に出て少し経つと
    /// (<see cref="AirborneGraceSeconds"/>)、死亡・リスポーン・ルールを Off にしたときに終わる。
    /// その後はしゃがみの入力どおりで、しゃがみを押したままならしゃがみ、離していれば (頭上が空いていれば) 立つ。</item>
    /// </list>
    /// 始まりと終わりは <c>[Slide] start ..</c> / <c>[Slide] end reason=..</c> でログに出す。滑っている間は <see cref="PlayerActionConditions.Sliding"/> を立てる。
    /// 調整値は Inspector の値 (既定は <see cref="SlideMotion"/> の Default*) で、<see cref="Configure"/> のたびに読み直す。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SlideMovement : MonoBehaviour, IMovementOverride
    {
        /// <summary><see cref="Priority"/>。はしごや乗り物の座席など、掴まる・乗る動きより低くする。</summary>
        public const int DefaultPriority = 10;

        /// <summary>接地が外れてから、宙に出たとして終えるまでの秒数。坂の継ぎ目で一瞬浮いただけでは終えない。</summary>
        public const float AirborneGraceSeconds = 0.15f;

        /// <summary>前の Tick で動かそうとした水平の距離のうち、実際に進めた割合がこれ未満なら壁に当たったとみなす。</summary>
        public const float BlockedRatio = 0.5f;

        /// <summary>足場の傾きを測る Ray を、体の足元からさらに伸ばす長さ (m)。坂の上でカプセルの中心の真下が足より低くても届く長さ。</summary>
        public const float GroundProbeMargin = 0.6f;

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

        /// <summary>
        /// スライディングを使うか。既定は <see cref="Slide.Off"/>。<see cref="SlideActionInstaller"/> がルールの <see cref="ActionRules.Slide"/> を入れる。
        /// Off にすると滑っている最中でもやめる。
        /// </summary>
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

        /// <summary>true を返す間は始めない (カバー中など)。滑っている最中に true になればやめる。null なら止めない。</summary>
        public Func<bool> Blocker { get; set; }

        /// <summary>横取りしている <see cref="PlayerController"/>。まだ無ければ null。</summary>
        public PlayerController Mover => _mover;

        /// <summary>速さの移り変わり。<see cref="Configure"/> で調整値が変わっていれば作り直す (滑っている最中なら終えてから)。</summary>
        public SlideMotion Motion => _motion;

        /// <summary>滑っている最中か。</summary>
        public bool IsSliding => _motion.IsSliding;

        public int Priority => DefaultPriority;

        /// <summary>
        /// 滑っている最中か、今の Tick で滑り始めるか。<see cref="PlayerController"/> が Tick のたびに 1 度読む。
        /// ダッシュ中にしゃがんだかは、ダッシュとしゃがみを解釈し終えてから姿勢を当てるまでの間 (= ここ) にしか見分けられないので、
        /// 読むたびに始められるかを調べ、始めるなら向きと速さを覚えて <see cref="Begin"/> で使う。
        /// </summary>
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

        /// <summary>
        /// rules の <see cref="ActionRules.Slide"/> を <see cref="Rule"/> に入れ、On なら有効に、それ以外なら無効にする (滑っていればやめる)。
        /// 同じ GameObject の <see cref="PlayerController"/> に横取りとして登録し (無効なら外し)、Inspector の調整値が変わっていれば <see cref="Motion"/> を作り直す。
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">調整値が <see cref="SlideMotion"/> の条件を満たさない。</exception>
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

        /// <summary>
        /// 調整値を変える。滑っている最中なら終えてから作り直す。値は <see cref="SlideMotion(float, float, float, float, float, float)"/> の条件を満たすこと。
        /// </summary>
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

        /// <summary>
        /// damageable が死んだとき・体力を作り直した (リスポーンした) ときに滑るのをやめるよう購読する。前に渡したものの購読は外す。null 可。
        /// </summary>
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

        /// <summary>
        /// ダッシュとしゃがみの入力によらず、次の Tick で今の移動の向きへ滑り始めるよう頼む (入力の無い smoke やほかの動きからのつなぎ)。
        /// 始められない (ルールが Off・接地していない・歩きより遅い・カバーや乗り越えの最中など) なら頼まずに false。
        /// </summary>
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

        /// <summary>滑っている最中ならやめる。<see cref="TryStart"/> で頼んだまま始まっていなければ取り消す。やめたら true。</summary>
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

            if (input.JumpPressed && input.IsGrounded)
            {
                return Jump(mover, direction, deltaSeconds);
            }

            var grounded = TryProbeGround(mover, out var normal);
            var along = grounded ? Vector3.ProjectOnPlane(direction, normal) : direction;
            along = along.sqrMagnitude > 1e-6f ? along.normalized : direction;
            var slopeSine = Mathf.Clamp(-along.y, -1f, 1f);

            var travelled = _motion.Step(deltaSeconds, grounded ? slopeSine : 0f);
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
            _motion.TryStart(_pendingDirection, _pendingSpeed);
            Debug.Log(SlideMotion.FormatStartLog(_motion.Speed, _motion.Direction), this);
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

        // 始められるなら、今の水平の速度から向きと速さを返す。
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
            var horizontal = new System.Numerics.Vector2(velocity.X, velocity.Z);
            speed = horizontal.Length();

            // 歩きより速くなければダッシュで走っているとは言えず、滑り出しの加速が歩きやその場からの瞬間移動になってしまう。
            if (!(speed > _mover.WalkSpeed))
            {
                speed = 0f;
                return false;
            }

            direction = horizontal / speed;
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
            if (_expectedHorizontal > 1e-4f)
            {
                var moved = Vector3.Dot(position - _stepFrom, direction);
                if (moved < _expectedHorizontal * BlockedRatio)
                {
                    _motion.LimitSpeed(Mathf.Max(0f, _motion.Speed * Mathf.Max(0f, moved) / _expectedHorizontal));
                }
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
            var start = Mathf.Sqrt(2f * mover.Gravity * mover.JumpHeight);
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
