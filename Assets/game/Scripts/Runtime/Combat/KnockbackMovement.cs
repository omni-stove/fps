using System;
using Fps.Core;
using UnityEngine;

namespace Fps.Runtime
{
    [DisallowMultipleComponent]
    public sealed class KnockbackMovement : MonoBehaviour, IMovementOverride, IKnockbackReceiver
    {
        // はしごや乗り物の座席より上にする。爆発で飛ばされている間は掴まっていられない。
        public const int OverridePriority = 100;

        public const string ReasonCancelled = "cancelled";

        // 着地した Tick に動かさないと CharacterController が床に触れず、次の Tick で接地が外れて PlayerMotor が落ち始める。
        private static readonly Vector3 _settleDisplacement = new Vector3(0f, -0.05f, 0f);

        [SerializeField, Min(0.01f), Tooltip("吹き飛ばしの重さ。重いほど飛ばない (KnockbackSpec の重さの係数による)")]
        private float _weight = KnockbackSpec.DefaultWeight;

        private readonly KnockbackFlight _flight = new KnockbackFlight();
        private KnockbackSpec _spec = KnockbackSpec.Default;
        private PlayerController _mover;
        private Damageable _body;

        public KnockbackSpec KnockbackSpec
        {
            get => _spec;
            set => _spec = value ?? throw new ArgumentNullException(nameof(value));
        }

        public float KnockbackWeight
        {
            get => _weight;
            set
            {
                if (!(value > 0f) || float.IsInfinity(value))
                {
                    throw new ArgumentOutOfRangeException(nameof(value), value, "KnockbackWeight must be positive and finite");
                }

                _weight = value;
            }
        }

        public bool IsKnockedBack => _flight.IsFlying;

        public float AirSeconds => _flight.ElapsedSeconds;

        public Vector3 Velocity => _flight.Velocity.ToUnity();

        public PlayerController Mover => _mover;

        public Damageable Body => _body;

        public string Id => _body != null ? _body.Id : name;

        int IMovementOverride.Priority => OverridePriority;

        bool IMovementOverride.IsActive => _flight.IsFlying;

        PlayerActionConditions IMovementOverride.Conditions => PlayerActionConditions.None;

        public static KnockbackMovement Attach(PlayerController mover, Damageable body)
        {
            if (mover == null)
            {
                throw new ArgumentNullException(nameof(mover));
            }

            if (body == null)
            {
                throw new ArgumentNullException(nameof(body));
            }

            // Knockback は Damageable の GameObject から受け手を探すので、体の側に付ける。
            var movement = body.GetComponent<KnockbackMovement>();
            if (movement == null)
            {
                movement = body.gameObject.AddComponent<KnockbackMovement>();
            }

            movement.Bind(mover, body);
            return movement;
        }

        public void Bind(PlayerController mover, Damageable body)
        {
            if (mover == null)
            {
                throw new ArgumentNullException(nameof(mover));
            }

            if (body == null)
            {
                throw new ArgumentNullException(nameof(body));
            }

            Unbind();
            _mover = mover;
            _body = body;
            _mover.RegisterMovementOverride(this);
            _body.Damaged += OnDamaged;
            _body.Died += OnDied;
            _body.HealthReset += OnHealthReset;
        }

        public void Unbind()
        {
            if (_body != null)
            {
                _body.Damaged -= OnDamaged;
                _body.Died -= OnDied;
                _body.HealthReset -= OnHealthReset;
            }

            // 登録を外すと選ばれていれば End が呼ばれ、そこで飛んでいる最中のものを打ち切る。
            if (_mover != null)
            {
                _mover.UnregisterMovementOverride(this);
            }

            _flight.Clear();
            _mover = null;
            _body = null;
        }

        /// <summary>
        /// 飛行中の状態を捨てる。乗り物に座っている間は優先度の高い横取りに負けて Step が呼ばれず、
        /// 受けた吹き飛ばしが凍ったまま残るので、乗り降りでここから消す。
        /// </summary>
        public void ClearFlight()
        {
            _flight.Clear();
        }

        public bool Launch(Vector3 velocity, DamageKind kind)
        {
            var sqr = velocity.sqrMagnitude;
            if (float.IsNaN(sqr) || float.IsInfinity(sqr))
            {
                throw new ArgumentException($"velocity must be finite: {velocity}", nameof(velocity));
            }

            if (_mover == null)
            {
                throw new InvalidOperationException($"KnockbackMovement '{name}' is not bound to a PlayerController");
            }

            if (_body != null && _body.IsDead)
            {
                return false;
            }

            _flight.Launch(velocity.ToNumerics());
            Debug.Log(Knockback.FormatLaunchLog(Id, kind, velocity), this);
            return true;
        }

        void IMovementOverride.Begin(PlayerController mover)
        {
        }

        MovementOverrideStep IMovementOverride.Step(PlayerController mover, MovementOverrideInput input, float deltaSeconds)
        {
            if (!_flight.IsFlying)
            {
                return MovementOverrideStep.Move(_settleDisplacement, Vector3.zero);
            }

            if ((mover.Controller.collisionFlags & CollisionFlags.Above) != 0)
            {
                _flight.StopRising();
            }

            if (input.IsGrounded && _flight.CanLand(_spec.MinAirSeconds))
            {
                var seconds = _flight.Land();
                Debug.Log(Knockback.FormatLandLog(Id, Knockback.ReasonLanded, seconds, mover.transform.position), this);
                return MovementOverrideStep.Move(_settleDisplacement, Vector3.zero);
            }

            if (_flight.HasTimedOut(_spec.MaxAirSeconds))
            {
                // 落ちている速さは PlayerMotor に引き継ぎ、そのまま落ち続けさせる (落下のダメージもそちらで決まる)。
                var falling = _flight.Velocity.ToUnity();
                var seconds = _flight.Land();
                Debug.Log(Knockback.FormatLandLog(Id, Knockback.ReasonTimeout, seconds, mover.transform.position), this);
                return MovementOverrideStep.Move(Vector3.zero, falling);
            }

            var spec = mover.Motor.Spec;
            var steer = System.Numerics.Vector2.Zero;
            var airControl = 0f;

            // 入力が無いときまで寄せると、手を離しているだけで空中で止まってしまう。
            if (input.Move.sqrMagnitude > 1e-4f)
            {
                var wish = Quaternion.Euler(0f, mover.transform.eulerAngles.y, 0f)
                    * new Vector3(input.Move.x, 0f, input.Move.y) * spec.WalkSpeed;
                steer = new System.Numerics.Vector2(wish.x, wish.z);
                airControl = _spec.AirControl;
            }

            var displacement = _flight.Step(deltaSeconds, _spec.Gravity, steer, airControl).ToUnity();
            return MovementOverrideStep.Move(displacement, _flight.Velocity.ToUnity());
        }

        void IMovementOverride.End(PlayerController mover)
        {
            if (!_flight.IsFlying)
            {
                return;
            }

            var seconds = _flight.Land();
            Debug.Log(Knockback.FormatLandLog(Id, ReasonCancelled, seconds, mover.transform.position), this);
        }

        private void OnDamaged(Damageable self, DamageReport report)
        {
            Knockback.ApplyFromReport(self, report);
        }

        private void OnDied(Damageable self)
        {
            _flight.Clear();
        }

        private void OnHealthReset(Health health)
        {
            _flight.Clear();
        }

        private void OnDestroy()
        {
            Unbind();
        }
    }
}
