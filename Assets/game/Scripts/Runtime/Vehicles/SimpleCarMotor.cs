using System;
using System.Collections.Generic;
using Shooter.Core;
using UnityEngine;

namespace Shooter.Runtime
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody))]
    public sealed class SimpleCarMotor : MonoBehaviour, IVehicleMotor, IVehicleTopSpeed
    {
        [SerializeField, Min(0.01f), Tooltip("車の質量 (kg)。Rigidbody に入れる")]
        private float _mass = CarSpec.DefaultMass;

        [SerializeField, Min(0f), Tooltip("アクセルを踏み切ったときの、回す車輪すべての合計のトルク (N·m)")]
        private float _motorTorque = CarSpec.DefaultMotorTorque;

        [SerializeField, Min(0f), Tooltip("ブレーキを踏み切ったときの、車輪 1 つあたりのトルク (N·m)")]
        private float _brakeTorque = CarSpec.DefaultBrakeTorque;

        [SerializeField, Min(0f), Tooltip("何も踏んでいないときに掛けるブレーキ (N·m)")]
        private float _idleBrakeTorque = CarSpec.DefaultIdleBrakeTorque;

        [SerializeField, Range(0f, CarSpec.MaxSteerAngleLimit), Tooltip("ハンドルを切り切ったときの、前輪の向き (度)")]
        private float _maxSteerAngle = CarSpec.DefaultMaxSteerAngle;

        [SerializeField, Min(0f), Tooltip("前へ進むときの、エンジンの力を掛ける上限の速さ (m/s)")]
        private float _maxForwardSpeed = CarSpec.DefaultMaxForwardSpeed;

        [SerializeField, Min(0f), Tooltip("後ろへ進むときの、エンジンの力を掛ける上限の速さ (m/s)")]
        private float _maxReverseSpeed = CarSpec.DefaultMaxReverseSpeed;

        [SerializeField, Range(1f, 179f), Tooltip("真上からこの角度 (度) より傾いたら、ひっくり返ったとみなす")]
        private float _flipAngle = CarSpec.DefaultFlipAngle;

        [SerializeField, Min(0f), Tooltip("この速さ (m/s) 以下でなければ起こせない")]
        private float _rightingMaxSpeed = CarSpec.DefaultRightingMaxSpeed;

        [SerializeField, Min(0f), Tooltip("起こすときに持ち上げる高さ (m)")]
        private float _rightingLift = CarSpec.DefaultRightingLift;

        [SerializeField]
        private CarWheel[] _wheels = Array.Empty<CarWheel>();

        private CarSpec _spec;
        private Rigidbody _body;
        private VehicleMotorInput _input;
        private bool _rightRequested;
        private CarDriveCommand _lastCommand;

        public CarSpec Spec => _spec ??= BuildSpec();

        public Rigidbody Body => _body != null ? _body : (_body = GetComponent<Rigidbody>());

        public VehicleMotorInput Input => _input;

        public IReadOnlyList<CarWheel> Wheels => _wheels;

        public CarDriveCommand LastCommand => _lastCommand;

        public float TopSpeed => Spec.MaxForwardSpeed;

        public float ForwardSpeed => Vector3.Dot(Body.linearVelocity, transform.forward);

        public float TiltDegrees => Vector3.Angle(transform.up, Vector3.up);

        public bool IsTipped => CarDrive.IsTipped(Spec, TiltDegrees);

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

        private void LateUpdate()
        {
            SyncVisuals();
        }

        public void Configure(CarSpec spec)
        {
            _spec = spec ?? throw new ArgumentNullException(nameof(spec));
            _mass = spec.Mass;
            _motorTorque = spec.MotorTorque;
            _brakeTorque = spec.BrakeTorque;
            _idleBrakeTorque = spec.IdleBrakeTorque;
            _maxSteerAngle = spec.MaxSteerAngle;
            _maxForwardSpeed = spec.MaxForwardSpeed;
            _maxReverseSpeed = spec.MaxReverseSpeed;
            _flipAngle = spec.FlipAngle;
            _rightingMaxSpeed = spec.RightingMaxSpeed;
            _rightingLift = spec.RightingLift;
            Body.mass = spec.Mass;
        }

        public void SetWheels(params CarWheel[] wheels)
        {
            if (wheels == null)
            {
                throw new ArgumentNullException(nameof(wheels));
            }

            for (var i = 0; i < wheels.Length; i++)
            {
                if (wheels[i] == null || wheels[i].Collider == null)
                {
                    throw new ArgumentException($"wheels[{i}] has no WheelCollider", nameof(wheels));
                }
            }

            _wheels = (CarWheel[])wheels.Clone();
        }

        // 起こすのは上の軸を押した瞬間だけにする。押したままだと、起こした直後にまた傾いたときに勝手に起き直してしまうため。
        public void SetInput(VehicleMotorInput input)
        {
            if (input[CarDrive.RightAxis] > 0f && !(_input[CarDrive.RightAxis] > 0f))
            {
                _rightRequested = true;
            }

            _input = input;
        }

        // FixedUpdate から呼ぶ。物理の進まない Edit Mode のテストからも直接呼べるよう public にする。
        public void Step()
        {
            if (_rightRequested)
            {
                _rightRequested = false;
                TryRight();
            }

            _lastCommand = CarDrive.Compute(Spec, _input, ForwardSpeed);
            Apply(_lastCommand);
        }

        public bool TryRight()
        {
            var body = Body;
            var tilt = TiltDegrees;
            if (!CarDrive.CanRight(Spec, tilt, body.linearVelocity.magnitude))
            {
                return false;
            }

            var rotation = Quaternion.LookRotation(Heading(), Vector3.up);
            var position = transform.position + (Vector3.up * Spec.RightingLift);
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            transform.SetPositionAndRotation(position, rotation);
            body.position = position;
            body.rotation = rotation;
            Debug.Log(VehicleLog.Righted(name, tilt), this);
            return true;
        }

        private void Apply(CarDriveCommand command)
        {
            var driven = 0;
            foreach (var wheel in _wheels)
            {
                if (wheel != null && wheel.Collider != null && wheel.Drives)
                {
                    driven++;
                }
            }

            var motorPerWheel = driven > 0 ? command.MotorTorque / driven : 0f;
            foreach (var wheel in _wheels)
            {
                if (wheel == null || wheel.Collider == null)
                {
                    continue;
                }

                wheel.Collider.steerAngle = wheel.Steers ? command.SteerAngle : 0f;
                wheel.Collider.motorTorque = wheel.Drives ? motorPerWheel : 0f;
                wheel.Collider.brakeTorque = command.BrakeTorque;
            }
        }

        private void SyncVisuals()
        {
            foreach (var wheel in _wheels)
            {
                if (wheel == null || wheel.Collider == null || wheel.Visual == null)
                {
                    continue;
                }

                wheel.Collider.GetWorldPose(out var position, out var rotation);
                wheel.Visual.SetPositionAndRotation(position, rotation);
            }
        }

        // 鼻先から立った車は前が真下を向いて水平の向きが無いので、そのときは屋根の向きを進む向きにする。
        private Vector3 Heading()
        {
            var forward = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
            if (forward.sqrMagnitude > 1e-4f)
            {
                return forward.normalized;
            }

            var up = Vector3.ProjectOnPlane(transform.up, Vector3.up);
            return up.sqrMagnitude > 1e-4f ? up.normalized : Vector3.forward;
        }

        private CarSpec BuildSpec()
        {
            return new CarSpec(
                _mass,
                _motorTorque,
                _brakeTorque,
                _idleBrakeTorque,
                _maxSteerAngle,
                _maxForwardSpeed,
                _maxReverseSpeed,
                _flipAngle,
                _rightingMaxSpeed,
                _rightingLift);
        }
    }
}
