using System;
using Fps.Core;
using UnityEngine;
using UnityEngine.AI;

namespace Fps.Runtime
{
    // 飛んでいる間は NavMeshAgent を切り、Rigidbody も付けずに自分で動かす。Agent を付けたままだと NavMesh の上に引き戻され、
    // Rigidbody を足すと NavMeshAgent と位置を奪い合う。
    public sealed partial class EnemyAgent : IKnockbackReceiver
    {
        public const string ReasonKnockback = "knockback";

        // 体の中ほどで横の壁を探す。足元で探すと床の小さな段差でも止まってしまう。
        private const float KnockbackWallProbeHeightRatio = 0.5f;

        private const float KnockbackProbeSkin = 0.05f;

        // 下り坂や段差の上に降りたときも床を見つけられるよう、足元の少し上から下を探す。
        private const float KnockbackGroundProbeLift = 0.5f;

        // NavMesh の面は床のコライダーから少しずれて焼かれるので、この程度の差は寄せたとみなさない。
        private const float KnockbackSnapHorizontalTolerance = 0.1f;
        private const float KnockbackSnapVerticalTolerance = 0.5f;

        private static readonly RaycastHit[] _knockbackHits = new RaycastHit[16];

        [Header("Knockback")]
        [SerializeField, Min(0.01f), Tooltip("吹き飛ばしの重さ。重いほど飛ばない (KnockbackSpec の重さの係数による)")]
        private float _knockbackWeight = KnockbackSpec.DefaultWeight;

        private readonly KnockbackFlight _flight = new KnockbackFlight();
        private KnockbackSpec _knockbackSpec = KnockbackSpec.Default;
        private Vector3 _launchGround;

        public KnockbackSpec KnockbackSpec
        {
            get => _knockbackSpec;
            set => _knockbackSpec = value ?? throw new ArgumentNullException(nameof(value));
        }

        public float KnockbackWeight
        {
            get => _knockbackWeight;
            set
            {
                if (!(value > 0f) || float.IsInfinity(value))
                {
                    throw new ArgumentOutOfRangeException(nameof(value), value, "KnockbackWeight must be positive and finite");
                }

                _knockbackWeight = value;
            }
        }

        public bool IsKnockedBack => _flight.IsFlying;

        public Vector3 KnockbackVelocity => _flight.Velocity.ToUnity();

        public bool Launch(Vector3 velocity, DamageKind kind)
        {
            var sqr = velocity.sqrMagnitude;
            if (float.IsNaN(sqr) || float.IsInfinity(sqr))
            {
                throw new ArgumentException($"velocity must be finite: {velocity}", nameof(velocity));
            }

            if (_brain == null)
            {
                throw new InvalidOperationException($"EnemyAgent '{name}' is not initialized");
            }

            if (_brain.IsDead)
            {
                return false;
            }

            if (!_flight.IsFlying)
            {
                DetachForKnockback();
            }

            _flight.Launch(velocity.ToNumerics());
            Debug.Log(Knockback.FormatLaunchLog(Id, kind, velocity), this);

            // 飛んでいる間は TickKnockback が止めておく処理より先に返すので、この秒数は着地してから減り始める。
            Interrupt(_knockbackSpec.RecoverSeconds, ReasonKnockback);
            return true;
        }

        public bool TickKnockback(float deltaSeconds)
        {
            if (!(deltaSeconds >= 0f) || float.IsInfinity(deltaSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(deltaSeconds), deltaSeconds, "deltaSeconds must be a non-negative finite number");
            }

            if (!_flight.IsFlying)
            {
                return false;
            }

            var up = Vector3.up * _agent.baseOffset;
            var ground = transform.position - up;
            var displacement = _flight.Step(deltaSeconds, _knockbackSpec.Gravity).ToUnity();

            var horizontal = new Vector3(displacement.x, 0f, displacement.z);
            if (horizontal.sqrMagnitude > 1e-10f && TryFindKnockbackWall(ground, horizontal, out var allowed))
            {
                horizontal = horizontal.normalized * allowed;
                _flight.StopHorizontal();
            }

            var next = ground + horizontal;
            var rise = displacement.y;
            if (rise > 0f && TryCastKnockback(next + (Vector3.up * BodyHeight()), Vector3.up, rise + KnockbackProbeSkin, out var ceiling))
            {
                rise = Mathf.Max(0f, ceiling - KnockbackProbeSkin);
                _flight.StopRising();
            }

            if (rise <= 0f && TryFindKnockbackGround(next, -rise, out var groundY))
            {
                if (_flight.CanLand(_knockbackSpec.MinAirSeconds))
                {
                    LandKnockback(new Vector3(next.x, groundY, next.z), Knockback.ReasonLanded);
                    return true;
                }

                // 着地を見始める前に床へ届いても、床より下へは沈ませない。
                rise = Mathf.Max(rise, groundY - next.y);
            }

            next.y += rise;
            transform.position = next + up;
            if (_flight.HasTimedOut(_knockbackSpec.MaxAirSeconds))
            {
                LandKnockback(_launchGround, Knockback.ReasonTimeout);
            }

            return true;
        }

        private void DetachForKnockback()
        {
            _launchGround = GroundPosition;
            if (_agent.enabled && _agent.isOnNavMesh)
            {
                _agent.ResetPath();
            }

            _destination = null;
            _detour = null;
            _goal = null;
            _stuck.Reset();
            SetCrouching(false);
            _sprinting = false;
            _aiming = false;
            _agent.enabled = false;
        }

        // 着地点の近くに NavMesh が無ければ、飛び立った点 (NavMesh の上にいた) の近くへ戻す。崖下などに落ちて歩けなくなるのを避ける。
        private void LandKnockback(Vector3 ground, string reason)
        {
            var seconds = _flight.Land();
            Vector3 to;
            if (NavMesh.SamplePosition(ground, out var hit, _knockbackSpec.LandingSnapRadius, NavMesh.AllAreas))
            {
                to = hit.position;
            }
            else if (NavMesh.SamplePosition(_launchGround, out hit, OffMeshSnapRadius, NavMesh.AllAreas))
            {
                to = hit.position;
            }
            else
            {
                to = _launchGround;
            }

            // 無効のまま Transform を動かしてから有効にすると、NavMeshAgent は新しい位置の近くの NavMesh に載る (PlaceAndStartOver と同じ)。
            transform.position = to + (Vector3.up * _agent.baseOffset);
            _agent.enabled = true;
            if (_agent.isOnNavMesh)
            {
                _agent.Warp(to);
                _agent.isStopped = false;
                _warnedOffMesh = false;
            }

            _lastFinitePosition = transform.position;

            var snapped = HorizontalDistance(ground, to) > KnockbackSnapHorizontalTolerance
                || Mathf.Abs(ground.y - to.y) > KnockbackSnapVerticalTolerance;
            Debug.Log(
                snapped
                    ? Knockback.FormatLandLog(Id, reason, seconds, to, ground)
                    : Knockback.FormatLandLog(Id, reason, seconds, to),
                this);
        }

        private float BodyHeight()
        {
            return Mathf.Max(_agent.height, KnockbackGroundProbeLift);
        }

        private bool TryFindKnockbackWall(Vector3 ground, Vector3 horizontal, out float allowed)
        {
            allowed = 0f;
            var distance = horizontal.magnitude;
            var direction = horizontal / distance;
            var radius = Mathf.Max(0.01f, _agent.radius * 0.9f);
            var origin = ground + (Vector3.up * Mathf.Max(BodyHeight() * KnockbackWallProbeHeightRatio, radius + KnockbackProbeSkin));
            var count = Physics.SphereCastNonAlloc(
                origin, radius, direction, _knockbackHits, distance + KnockbackProbeSkin, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            if (!TryNearestOther(count, out var nearest))
            {
                return false;
            }

            allowed = Mathf.Max(0f, nearest - KnockbackProbeSkin);
            return true;
        }

        private bool TryFindKnockbackGround(Vector3 at, float fall, out float groundY)
        {
            groundY = 0f;
            var origin = at + (Vector3.up * KnockbackGroundProbeLift);
            if (!TryCastKnockback(origin, Vector3.down, KnockbackGroundProbeLift + fall + KnockbackProbeSkin, out var distance))
            {
                return false;
            }

            groundY = origin.y - distance;
            return true;
        }

        private bool TryCastKnockback(Vector3 origin, Vector3 direction, float distance, out float nearest)
        {
            var count = Physics.RaycastNonAlloc(
                origin, direction, _knockbackHits, distance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            return TryNearestOther(count, out nearest);
        }

        // NonAlloc の結果は近い順に並ばない。自分の当たり判定も拾うので外す。
        private bool TryNearestOther(int count, out float nearest)
        {
            nearest = float.PositiveInfinity;
            for (var i = 0; i < count; i++)
            {
                var hit = _knockbackHits[i];
                if (hit.collider == null || hit.collider.transform.IsChildOf(transform))
                {
                    continue;
                }

                nearest = Mathf.Min(nearest, hit.distance);
            }

            return !float.IsPositiveInfinity(nearest);
        }
    }
}
