using System;
using Shooter.Core;
using UnityEngine;

namespace Shooter.Runtime
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody))]
    public sealed class SimpleAircraftMotor : MonoBehaviour, IVehicleMotor, IVehicleTopSpeed, IBotPilotedMotor
    {
        [SerializeField, Tooltip("飛び方の値 (Create > Shooter > Aircraft Definition)。空なら既定値で飛ぶ")]
        private AircraftDefinition _definition;

        private AircraftSpec _configuredSpec;
        private AircraftSpec _definitionSpec;
        private Rigidbody _body;
        private VehicleMotorInput _input;
        private AircraftFlightCommand _lastCommand;

        public AircraftDefinition Definition
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
        public AircraftSpec Spec => _configuredSpec ?? (_definitionSpec ??= BuildSpec());

        public Rigidbody Body => _body != null ? _body : (_body = GetComponent<Rigidbody>());

        public VehicleMotorInput Input => _input;

        public AircraftFlightCommand LastCommand => _lastCommand;

        public AircraftFlightState State => _lastCommand.State;

        public float ForwardSpeed => Vector3.Dot(Body.linearVelocity, transform.forward);

        public float TopSpeed => AircraftAero.TopSpeed(Spec, Physics.gravity.magnitude);

        public Type BotPilotType => typeof(BotAircraftPilot);

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
            Step();
        }

        // 渡した値をシリアライズしないのは、残す値の置き場をアセット (Definition) だけにし、シーンとアセットで値が食い違わないようにするため。
        public void Configure(AircraftSpec spec)
        {
            _configuredSpec = spec ?? throw new ArgumentNullException(nameof(spec));
            Body.mass = spec.Mass;
        }

        public void SetInput(VehicleMotorInput input)
        {
            _input = input;
        }

        // FixedUpdate から呼ぶ。物理の進まない Edit Mode のテストからも直接呼べるよう public にする。
        public void Step()
        {
            var body = Body;
            var toLocal = Quaternion.Inverse(body.rotation);
            var localVelocity = toLocal * body.linearVelocity;
            var localAngularVelocity = toLocal * body.angularVelocity;

            // 毎ステップ引き直すのは、壊れ方や車輪を受け持つ部品が乗っている間に付け外しされても、次のステップから効くようにするため。
            var ground = TryGetComponent<IAircraftGroundSource>(out var groundSource)
                ? groundSource.GroundContact(_input, localVelocity, localAngularVelocity)
                : AircraftGroundContact.Airborne;
            var condition = TryGetComponent<IAircraftConditionSource>(out var conditionSource)
                ? conditionSource.Condition
                : AircraftCondition.Intact;

            _lastCommand = AircraftFlight.Compute(
                Spec,
                _input,
                localVelocity.ToNumerics(),
                localAngularVelocity.ToNumerics(),
                Physics.gravity.magnitude,
                ground,
                condition);
            if (body.isKinematic)
            {
                return;
            }

            body.AddRelativeForce(_lastCommand.LocalAcceleration.ToUnity(), ForceMode.Acceleration);
            body.AddRelativeTorque(_lastCommand.LocalAngularAcceleration.ToUnity(), ForceMode.Acceleration);
        }

        private AircraftSpec BuildSpec()
        {
            return _definition != null ? _definition.ToSpec() : AircraftSpec.Default;
        }
    }
}
