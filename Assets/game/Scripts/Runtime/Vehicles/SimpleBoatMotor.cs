using System;
using Shooter.Core;
using UnityEngine;

namespace Shooter.Runtime
{
    // 当て方は持たず、VehicleControlSchemes が既定で返す CarControlScheme の軸 (前後・左右) をそのまま読む。
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody))]
    public sealed class SimpleBoatMotor : MonoBehaviour, IVehicleMotor, IVehicleTopSpeed
    {
        [SerializeField, Tooltip("浮き方と進み方の値 (Create > Shooter > Boat Definition)。空なら既定値で進む")]
        private BoatDefinition _definition;

        private BoatSpec _configuredSpec;
        private BoatSpec _definitionSpec;
        private IWaterVolumes _water;
        private Rigidbody _body;
        private VehicleMotorInput _input;
        private BoatHullCommand _lastCommand;

        public BoatDefinition Definition
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

        // Configure で渡した値をアセットより優先するのは、テストやコードから組んだ船がアセット無しでも値を差し替えられるようにするため。
        public BoatSpec Spec => _configuredSpec ?? (_definitionSpec ??= BuildSpec());

        // 既定でシーンの Water を毎回引くのは、乗っている間に水が付け外しされても次のステップから効くようにするため。
        public IWaterVolumes Water
        {
            get => _water ?? Traversal.WaterVolumes.Scene;
            set => _water = value;
        }

        public Rigidbody Body => _body != null ? _body : (_body = GetComponent<Rigidbody>());

        public VehicleMotorInput Input => _input;

        public BoatHullCommand LastCommand => _lastCommand;

        public bool IsAfloat => _lastCommand.IsAfloat;

        public float ForwardSpeed => Vector3.Dot(Body.linearVelocity, transform.forward);

        public float TopSpeed => Spec.MaxForwardSpeed;

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
        public void Configure(BoatSpec spec)
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
            var rotation = body.rotation;
            var state = new BoatHullState(
                body.position.ToNumerics(),
                new System.Numerics.Quaternion(rotation.x, rotation.y, rotation.z, rotation.w),
                body.worldCenterOfMass.ToNumerics(),
                body.linearVelocity.ToNumerics(),
                body.angularVelocity.ToNumerics());
            // 重力を切った船に浮力だけ掛けると上へ飛んでいくので、浮力は実際に掛かっている重力に釣り合わせる。
            var gravity = body.useGravity ? Physics.gravity.magnitude : 0f;
            _lastCommand = BoatHull.Compute(Spec, _input, state, Water, gravity);
            if (body.isKinematic)
            {
                return;
            }

            body.AddForce(_lastCommand.Force.ToUnity(), ForceMode.Force);
            body.AddTorque(_lastCommand.Torque.ToUnity(), ForceMode.Force);
            body.AddTorque(_lastCommand.AngularAcceleration.ToUnity(), ForceMode.Acceleration);
        }

        private BoatSpec BuildSpec()
        {
            return _definition != null ? _definition.ToSpec() : BoatSpec.Default;
        }
    }
}
