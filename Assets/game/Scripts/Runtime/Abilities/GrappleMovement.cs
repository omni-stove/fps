using System;
using Fps.Core;
using UnityEngine;

namespace Fps.Runtime
{
    public sealed class GrappleMovement : IMovementOverride
    {
        // 吹き飛ばし・ジップライン・パラシュート (100) には譲り、スライディング (10) には勝つ。
        public const int OverridePriority = 50;

        private readonly GrapplePull _pull;
        private Phase _phase;
        private bool _started;
        private Vector3 _target;
        private Vector3 _wallNormal;

        public GrappleMovement(GrappleSpec spec)
        {
            _pull = new GrapplePull(spec ?? throw new ArgumentNullException(nameof(spec)));
        }

        private enum Phase
        {
            Idle,
            Pulling,
            Hanging,
        }

        public int Priority => OverridePriority;

        public bool IsActive => _phase != Phase.Idle;

        public PlayerActionConditions Conditions => PlayerActionConditions.None;

        public GrapplePull Pull => _pull;

        public bool IsPulling => _phase == Phase.Pulling;

        public bool IsHanging => _phase == Phase.Hanging;

        public bool HasArrived { get; private set; }

        public GrappleEnd Ended { get; private set; }

        // 離した Tick に渡した速度。Jump で離したときの勢いをログに出すのに使う。
        public Vector3 ReleaseVelocity { get; private set; }

        public Vector3 Target
        {
            get => _target;
            set
            {
                RequireFinite(value, nameof(value));
                _target = value;
            }
        }

        // wallNormal は着いたときに向く壁の外向きの法線。壁でなければ (床・天井) zero を渡し、向きを変えない。
        public void Arm(Vector3 target, Vector3 wallNormal)
        {
            RequireFinite(target, nameof(target));
            RequireFinite(wallNormal, nameof(wallNormal));
            _target = target;
            _wallNormal = wallNormal;
            _phase = Phase.Pulling;
            _started = false;
            HasArrived = false;
            Ended = GrappleEnd.None;
            ReleaseVelocity = Vector3.zero;
        }

        // 崖のぼりに移るときなど、能力の側から終える。終えた理由は残さない (離したのではない)。
        public void Finish()
        {
            _phase = Phase.Idle;
        }

        public GrappleEnd Release(GrappleEnd reason)
        {
            if (_phase == Phase.Idle)
            {
                return Ended;
            }

            var velocity = _phase == Phase.Pulling && _started ? _pull.Release(reason).ToUnity() : Vector3.zero;
            Stop(reason, velocity);
            return reason;
        }

        // 選ばれたときの位置と勢いから引き始める。能力が効果を出した Tick と、PlayerController が選び直す Tick はずれることがあるので、ここで測る。
        public void Begin(PlayerController mover)
        {
            if (_phase != Phase.Pulling || _started)
            {
                return;
            }

            var velocity = mover.Motor != null ? mover.Motor.Velocity : System.Numerics.Vector3.Zero;
            _pull.Start(mover.transform.position.ToNumerics(), velocity, _target.ToNumerics());
            _started = true;
        }

        public MovementOverrideStep Step(PlayerController mover, MovementOverrideInput input, float deltaSeconds)
        {
            switch (_phase)
            {
                case Phase.Pulling:
                    return StepPull(mover, input, deltaSeconds);

                case Phase.Hanging:
                    // 縁で止まっている間は重力を掛けず、その場に留める。Jump なら跳ねて離れる。
                    if (input.JumpPressed)
                    {
                        var hop = Vector3.up * Mathf.Sqrt(2f * mover.Gravity * mover.JumpHeight);
                        Stop(GrappleEnd.Jumped, hop);
                        return MovementOverrideStep.Move(hop * deltaSeconds, hop);
                    }

                    return new MovementOverrideStep(Vector3.zero, Vector3.zero, crouched: false);

                default:
                    return MovementOverrideStep.Move(Vector3.zero, Vector3.zero);
            }
        }

        // ほかの横取りに替わった・登録を外された・PlayerController が止まったとき。自分で終えたならもう Idle になっている。
        public void End(PlayerController mover)
        {
            Release(GrappleEnd.Interrupted);
        }

        private MovementOverrideStep StepPull(PlayerController mover, MovementOverrideInput input, float deltaSeconds)
        {
            if (!_started)
            {
                Begin(mover);
            }

            if (input.JumpPressed)
            {
                var momentum = _pull.Release(GrappleEnd.Jumped).ToUnity();
                Stop(GrappleEnd.Jumped, momentum);
                return MovementOverrideStep.Move(momentum * deltaSeconds, momentum);
            }

            var displacement = _pull.Step(mover.transform.position.ToNumerics(), _target.ToNumerics(), deltaSeconds).ToUnity();
            if (_pull.IsPulling)
            {
                return new MovementOverrideStep(displacement, _pull.Velocity.ToUnity(), crouched: false);
            }

            if (_pull.Ended != GrappleEnd.Arrived)
            {
                Stop(_pull.Ended, Vector3.zero);
                return MovementOverrideStep.Move(Vector3.zero, Vector3.zero);
            }

            _phase = Phase.Hanging;
            HasArrived = true;
            return new MovementOverrideStep(displacement, Vector3.zero, yawDegrees: FacingYaw(), crouched: false);
        }

        // 崖のぼりは本体の正面の段を調べるので、着いたら壁へ向ける。
        private float? FacingYaw()
        {
            var into = new Vector3(-_wallNormal.x, 0f, -_wallNormal.z);
            if (into.sqrMagnitude < 1e-6f)
            {
                return null;
            }

            return Mathf.Atan2(into.x, into.z) * Mathf.Rad2Deg;
        }

        private void Stop(GrappleEnd reason, Vector3 velocity)
        {
            _phase = Phase.Idle;
            Ended = reason;
            ReleaseVelocity = velocity;
        }

        private static void RequireFinite(Vector3 value, string name)
        {
            if (float.IsNaN(value.x) || float.IsInfinity(value.x)
                || float.IsNaN(value.y) || float.IsInfinity(value.y)
                || float.IsNaN(value.z) || float.IsInfinity(value.z))
            {
                throw new ArgumentException($"{name} must be finite: {value}", name);
            }
        }
    }
}
