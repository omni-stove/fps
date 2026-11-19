using System;
using Shooter.Core;
using UnityEngine;

namespace Shooter.Runtime
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody))]
    public sealed class SimpleAircraftMotor : MonoBehaviour, IVehicleMotor, IVehicleTopSpeed
    {
        [SerializeField, Min(0.01f), Tooltip("機体の質量 (kg)。Rigidbody に入れる。飛び方は変わらず、ぶつかったときの重さだけが変わる")]
        private float _mass = AircraftSpec.DefaultMass;

        [SerializeField, Min(0.01f), Tooltip("スロットルを開き切ったときの、前へ押す加速 (m/s²)")]
        private float _maxThrust = AircraftSpec.DefaultMaxThrust;

        [SerializeField, Min(0.01f), Tooltip("水平に飛んでスロットルを開き切ったときに落ち着く速さ (m/s)")]
        private float _maxSpeed = AircraftSpec.DefaultMaxSpeed;

        [SerializeField, Min(0.01f), Tooltip("この速さ (m/s) で、翼が重さと同じだけ持ち上げる。これより遅いと沈む")]
        private float _liftSpeed = AircraftSpec.DefaultLiftSpeed;

        [SerializeField, Min(0.01f), Tooltip("この速さ (m/s) で、舵が効き切る")]
        private float _controlSpeed = AircraftSpec.DefaultControlSpeed;

        [SerializeField, Range(0f, 1f), Tooltip("止まっていても残る舵の効き (0 から 1)")]
        private float _minControl = AircraftSpec.DefaultMinControl;

        [SerializeField, Min(0f), Tooltip("ピッチを倒し切ったときの、機首を上下に回す速さ (度/秒)")]
        private float _pitchRate = AircraftSpec.DefaultPitchRate;

        [SerializeField, Min(0f), Tooltip("ロールを倒し切ったときの、機体を傾ける速さ (度/秒)")]
        private float _rollRate = AircraftSpec.DefaultRollRate;

        [SerializeField, Min(0f), Tooltip("ヨーを倒し切ったときの、機首を左右に回す速さ (度/秒)")]
        private float _yawRate = AircraftSpec.DefaultYawRate;

        [SerializeField, Min(0f), Tooltip("回る速さを舵の求める速さへ寄せる強さ (1/秒)。大きいほど機敏")]
        private float _responsiveness = AircraftSpec.DefaultResponsiveness;

        [SerializeField, Min(0f), Tooltip("横と上下へ流れる速さを、機首の向きへ寄せる強さ (1/秒)。大きいほど滑らずに曲がる")]
        private float _alignment = AircraftSpec.DefaultAlignment;

        [SerializeField, Min(0f), Tooltip("エアブレーキを掛け切ったときに、空気の抵抗へ足す倍率")]
        private float _airBrakeDrag = AircraftSpec.DefaultAirBrakeDrag;

        private AircraftSpec _spec;
        private Rigidbody _body;
        private VehicleMotorInput _input;
        private AircraftFlightCommand _lastCommand;

        public AircraftSpec Spec => _spec ??= BuildSpec();

        public Rigidbody Body => _body != null ? _body : (_body = GetComponent<Rigidbody>());

        public VehicleMotorInput Input => _input;

        public AircraftFlightCommand LastCommand => _lastCommand;

        public float ForwardSpeed => Vector3.Dot(Body.linearVelocity, transform.forward);

        public float TopSpeed => AircraftFlight.TopSpeed(Spec, Physics.gravity.magnitude);

        private void Awake()
        {
            Body.mass = Spec.Mass;
        }

        private void OnValidate()
        {
            _spec = null;
        }

        private void FixedUpdate()
        {
            Step();
        }

        public void Configure(AircraftSpec spec)
        {
            _spec = spec ?? throw new ArgumentNullException(nameof(spec));
            _mass = spec.Mass;
            _maxThrust = spec.MaxThrust;
            _maxSpeed = spec.MaxSpeed;
            _liftSpeed = spec.LiftSpeed;
            _controlSpeed = spec.ControlSpeed;
            _minControl = spec.MinControl;
            _pitchRate = spec.PitchRate;
            _rollRate = spec.RollRate;
            _yawRate = spec.YawRate;
            _responsiveness = spec.Responsiveness;
            _alignment = spec.Alignment;
            _airBrakeDrag = spec.AirBrakeDrag;
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
            _lastCommand = AircraftFlight.Compute(
                Spec,
                _input,
                (toLocal * body.linearVelocity).ToNumerics(),
                (toLocal * body.angularVelocity).ToNumerics(),
                Physics.gravity.magnitude);
            if (body.isKinematic)
            {
                return;
            }

            body.AddRelativeForce(_lastCommand.LocalAcceleration.ToUnity(), ForceMode.Acceleration);
            body.AddRelativeTorque(_lastCommand.LocalAngularAcceleration.ToUnity(), ForceMode.Acceleration);
        }

        private AircraftSpec BuildSpec()
        {
            return new AircraftSpec(
                _mass,
                _maxThrust,
                _maxSpeed,
                _liftSpeed,
                _controlSpeed,
                _minControl,
                _pitchRate,
                _rollRate,
                _yawRate,
                _responsiveness,
                _alignment,
                _airBrakeDrag);
        }
    }
}
