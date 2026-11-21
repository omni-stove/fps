using System;
using Shooter.Core;
using UnityEngine;

namespace Shooter.Runtime
{
    // 接地をコライダーの接触でなく真下へのレイで見るのは、脚の形がモデルごとに違っても同じ判定になり、物理を進めないテストでも同じ道を通せるため。
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody))]
    public sealed class SimpleHorseMotor : MonoBehaviour, IVehicleMotor, IVehicleTopSpeed
    {
        public const float DefaultGroundProbeHeight = 0.5f;
        public const float DefaultGroundDistance = 0.15f;

        private const int MaxGroundHits = 8;

        private const RigidbodyConstraints UprightConstraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;

        private static readonly RaycastHit[] _groundBuffer = new RaycastHit[MaxGroundHits];

        [SerializeField, Tooltip("走り方の値 (Create > Shooter > Horse Definition)。空なら既定値で走る")]
        private HorseDefinition _definition;

        [SerializeField, Tooltip("接地を見るために、足もとの地面を測るレイヤー")]
        private LayerMask _groundMask = Physics.DefaultRaycastLayers;

        [SerializeField, Min(0f), Tooltip("接地を見るレイを撃ち始める、原点からの高さ (m)。少し埋まった地面も拾えるようにする")]
        private float _groundProbeHeight = DefaultGroundProbeHeight;

        [SerializeField, Min(0f), Tooltip("原点 (蹄の高さに置く) の下この距離 (m) までに地面があれば接地とみなす")]
        private float _groundDistance = DefaultGroundDistance;

        private HorseSpec _configuredSpec;
        private HorseSpec _definitionSpec;
        private Rigidbody _body;
        private VehicleMotorInput _input;
        private bool _jumpRequested;
        private bool _jumping;
        private bool _grounded;
        private Vector3 _groundNormal = Vector3.up;
        private float _forwardSpeed;
        private HorseGait _gait;

        public HorseDefinition Definition
        {
            get => _definition;
            set
            {
                _definition = value;
                _configuredSpec = null;
                _definitionSpec = null;
                Body.mass = Spec.Mass;
            }
        }

        // Configure で渡した値をアセットより優先するのは、テストやコードから組んだ馬がアセット無しでも値を差し替えられるようにするため。
        public HorseSpec Spec => _configuredSpec ?? (_definitionSpec ??= BuildSpec());

        public Rigidbody Body => _body != null ? _body : (_body = GetComponent<Rigidbody>());

        public VehicleMotorInput Input => _input;

        public float TopSpeed => Spec.CanterSpeed;

        public HorseGait Gait => _gait;

        public float ForwardSpeed => _forwardSpeed;

        public bool IsGrounded => _grounded;

        public Vector3 GroundNormal => _groundNormal;

        public LayerMask GroundMask
        {
            get => _groundMask;
            set => _groundMask = value;
        }

        public float GroundProbeHeight
        {
            get => _groundProbeHeight;
            set => _groundProbeHeight = RequireNonNegative(value, nameof(value));
        }

        public float GroundDistance
        {
            get => _groundDistance;
            set => _groundDistance = RequireNonNegative(value, nameof(value));
        }

        private void Awake()
        {
            Body.mass = Spec.Mass;
            KeepUpright(Body);
        }

        private void OnValidate()
        {
            _definitionSpec = null;
        }

        private void FixedUpdate()
        {
            Step(Time.fixedDeltaTime);
        }

        // 渡した値をシリアライズしないのは、残す値の置き場をアセット (Definition) だけにし、シーンとアセットで値が食い違わないようにするため。
        public void Configure(HorseSpec spec)
        {
            _configuredSpec = spec ?? throw new ArgumentNullException(nameof(spec));
            Body.mass = spec.Mass;
        }

        // 跳ぶのはジャンプの軸を押した瞬間だけにする。押したままだと、着地した途端にまた跳び続けてしまうため。
        public void SetInput(VehicleMotorInput input)
        {
            if (input[HorseStride.JumpAxis] > 0f && !(_input[HorseStride.JumpAxis] > 0f))
            {
                _jumpRequested = true;
            }

            _input = input;
        }

        // FixedUpdate から呼ぶ。物理の進まない Edit Mode のテストからも直接呼べるよう public にする。
        public void Step(float deltaSeconds)
        {
            if (!(deltaSeconds >= 0f) || float.IsInfinity(deltaSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(deltaSeconds), deltaSeconds, "deltaSeconds must be a non-negative finite number");
            }

            var spec = Spec;
            var body = Body;
            KeepUpright(body);
            // 押した瞬間の頼みは、空中で受けても次の接地まで取っておかない。着地した途端に勝手に跳ぶと、乗り手が押した覚えのないジャンプになるため。
            var jumpRequested = _jumpRequested;
            _jumpRequested = false;

            var touching = ProbeGround(body.position, out _groundNormal);
            var velocity = body.linearVelocity;
            // 跳んだ直後はまだレイが地面に届くので、昇っている間は接地とみなさない。そうしないと次のステップでもう一度跳べてしまう。
            if (!touching || velocity.y <= 0f)
            {
                _jumping = false;
            }

            _grounded = touching && !_jumping;

            var forward = Heading(body.rotation);
            var forwardSpeed = Vector3.Dot(velocity, forward);
            var speed = forwardSpeed;
            if (_grounded)
            {
                var target = HorseStride.TargetSpeed(spec, _input, forwardSpeed);
                if (target != 0f && IsTooSteep(spec, _groundNormal, forward * Mathf.Sign(target)))
                {
                    target = 0f;
                }

                speed = HorseStride.Step(spec, forwardSpeed, target, deltaSeconds);
            }

            _forwardSpeed = speed;
            _gait = HorseStride.GaitOf(spec, speed);
            var yawRate = HorseStride.TurnRate(spec, forwardSpeed) * _input[HorseStride.TurnAxis];
            if (body.isKinematic)
            {
                return;
            }

            if (_grounded)
            {
                var horizontal = new Vector3(velocity.x, 0f, velocity.z);
                var lateral = horizontal - (forward * forwardSpeed);
                lateral = Vector3.MoveTowards(lateral, Vector3.zero, spec.Deceleration * deltaSeconds);
                var vertical = velocity.y;
                if (jumpRequested && spec.JumpSpeed > 0f)
                {
                    vertical = spec.JumpSpeed;
                    _jumping = true;
                    _grounded = false;
                }

                velocity = (forward * speed) + lateral + (Vector3.up * vertical);
            }

            body.linearVelocity = velocity;
            body.angularVelocity = new Vector3(0f, yawRate * Mathf.Deg2Rad, 0f);
        }

        // 坂を登る向きに進むときだけ止めるのは、急な坂でも下りや横切りまで止めると、登れない坂から降りられなくなるため。
        private static bool IsTooSteep(HorseSpec spec, Vector3 groundNormal, Vector3 direction)
        {
            if (Vector3.Angle(groundNormal, Vector3.up) <= spec.MaxSlopeAngle)
            {
                return false;
            }

            return Vector3.Dot(direction, groundNormal) < 0f;
        }

        // 鼻先が真上か真下を向くと水平の向きが無いので、そのときは背の向きを進む向きにする。
        private static Vector3 Heading(Quaternion rotation)
        {
            var forward = Vector3.ProjectOnPlane(rotation * Vector3.forward, Vector3.up);
            if (forward.sqrMagnitude > 1e-4f)
            {
                return forward.normalized;
            }

            var up = Vector3.ProjectOnPlane(rotation * Vector3.up, Vector3.up);
            return up.sqrMagnitude > 1e-4f ? up.normalized : Vector3.forward;
        }

        // 毎ステップ掛け直すのは、ゲームやほかの部品が Rigidbody の制約を差し替えても、馬が前後左右へ転ばないようにするため。
        private static void KeepUpright(Rigidbody body)
        {
            body.constraints |= UprightConstraints;
        }

        private bool ProbeGround(Vector3 origin, out Vector3 normal)
        {
            normal = Vector3.up;
            var start = origin + (Vector3.up * _groundProbeHeight);
            var reach = _groundProbeHeight + _groundDistance;
            var count = Physics.RaycastNonAlloc(start, Vector3.down, _groundBuffer, reach, _groundMask, QueryTriggerInteraction.Ignore);
            var nearest = float.PositiveInfinity;
            for (var i = 0; i < count; i++)
            {
                var hit = _groundBuffer[i];
                if (hit.collider == null || hit.collider.transform.IsChildOf(transform) || hit.distance >= nearest)
                {
                    continue;
                }

                nearest = hit.distance;
                normal = hit.normal;
            }

            Array.Clear(_groundBuffer, 0, count);
            return !float.IsPositiveInfinity(nearest);
        }

        private HorseSpec BuildSpec()
        {
            return _definition != null ? _definition.ToSpec() : HorseSpec.Default;
        }

        private static float RequireNonNegative(float value, string name)
        {
            if (!(value >= 0f) || float.IsInfinity(value))
            {
                throw new ArgumentOutOfRangeException(name, value, name + " must be a non-negative finite number");
            }

            return value;
        }
    }
}
