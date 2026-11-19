using System;
using Shooter.Core;
using UnityEngine;

namespace Shooter.Runtime
{
    // 当て方を必須にするのは、Vehicle と並べて付けただけで車の当て方 (CarControlScheme) に落ちず、コレクティブと旋回が効くようにするため。
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(HelicopterControlScheme))]
    public sealed class SimpleHelicopterMotor : MonoBehaviour, IVehicleMotor, IVehicleTopSpeed
    {
        private const int MaxGroundHits = 8;

        private static readonly RaycastHit[] _groundBuffer = new RaycastHit[MaxGroundHits];

        [SerializeField, Tooltip("飛び方の値 (Create > Shooter > Helicopter Definition)。空なら既定値で飛ぶ")]
        private HelicopterDefinition _definition;

        [SerializeField, Tooltip("地面効果のために、真下の地面までを測るレイヤー")]
        private LayerMask _groundMask = Physics.DefaultRaycastLayers;

        private HelicopterSpec _configuredSpec;
        private HelicopterSpec _definitionSpec;
        private Rigidbody _body;
        private VehicleMotorInput _input;
        private HelicopterFlightCommand _lastCommand;
        private float _rotor;
        private float _groundHeight = float.PositiveInfinity;

        public HelicopterDefinition Definition
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

        // Configure で渡した値をアセットより優先するのは、テストやコードから組んだ機体がアセット無しでも値を差し替えられるようにするため。
        public HelicopterSpec Spec => _configuredSpec ?? (_definitionSpec ??= BuildSpec());

        public Rigidbody Body => _body != null ? _body : (_body = GetComponent<Rigidbody>());

        public VehicleMotorInput Input => _input;

        public HelicopterFlightCommand LastCommand => _lastCommand;

        public float GroundHeight => _groundHeight;

        public float TopSpeed => Spec.MaxSpeed;

        public LayerMask GroundMask
        {
            get => _groundMask;
            set => _groundMask = value;
        }

        // 外から入れられるようにするのは、空中に置いた機体を回り切ったローターで始められるようにするため。
        public float Rotor
        {
            get => _rotor;
            set
            {
                if (!(value >= 0f && value <= 1f))
                {
                    throw new ArgumentOutOfRangeException(nameof(value), value, "rotor must be between 0 and 1");
                }

                _rotor = value;
            }
        }

        private void Awake()
        {
            Body.mass = Spec.Mass;
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
        public void Configure(HelicopterSpec spec)
        {
            _configuredSpec = spec ?? throw new ArgumentNullException(nameof(spec));
            Body.mass = spec.Mass;
        }

        public void SetInput(VehicleMotorInput input)
        {
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
            _rotor = HelicopterFlight.StepRotor(spec, _rotor, _input[HelicopterFlight.EngineAxis] > 0f, deltaSeconds);
            _groundHeight = MeasureGroundHeight(spec, body.position);
            var rotation = body.rotation;
            // 重力を切った体でホバーの分を足さないのは、釣り合わせる相手の重力が無いのに押し上げてしまうため。
            var gravity = body.useGravity ? Physics.gravity.magnitude : 0f;
            _lastCommand = HelicopterFlight.Compute(
                spec,
                _input,
                _rotor,
                (rotation * Vector3.forward).ToNumerics(),
                (rotation * Vector3.up).ToNumerics(),
                body.linearVelocity.ToNumerics(),
                body.angularVelocity.ToNumerics(),
                gravity,
                _groundHeight);
            if (body.isKinematic)
            {
                return;
            }

            body.AddForce(_lastCommand.Acceleration.ToUnity(), ForceMode.Acceleration);
            body.AddTorque(_lastCommand.AngularAcceleration.ToUnity(), ForceMode.Acceleration);
        }

        // 地面効果の届く高さまでしか測らないのは、それより遠い地面は飛び方に関わらず、長いレイを毎回撃たずに済むため。
        private float MeasureGroundHeight(HelicopterSpec spec, Vector3 origin)
        {
            if (!(spec.GroundEffectHeight > 0f))
            {
                return float.PositiveInfinity;
            }

            var count = Physics.RaycastNonAlloc(origin, Vector3.down, _groundBuffer, spec.GroundEffectHeight, _groundMask, QueryTriggerInteraction.Ignore);
            var nearest = float.PositiveInfinity;
            for (var i = 0; i < count; i++)
            {
                var hit = _groundBuffer[i];
                if (hit.collider == null || hit.collider.transform.IsChildOf(transform) || hit.distance >= nearest)
                {
                    continue;
                }

                nearest = hit.distance;
            }

            Array.Clear(_groundBuffer, 0, count);
            return nearest;
        }

        private HelicopterSpec BuildSpec()
        {
            return _definition != null ? _definition.ToSpec() : HelicopterSpec.Default;
        }
    }
}
