using System;
using Shooter.Core;
using UnityEngine;

namespace Shooter.Runtime
{
    [DisallowMultipleComponent]
    public sealed class AircraftControlScheme : MonoBehaviour, IVehicleControlScheme
    {
        [SerializeField, Min(0f), Tooltip("視線が上下にずれたときに、ピッチを倒す強さ")]
        private float _pitchGain = AircraftAimSpec.DefaultPitchGain;

        [SerializeField, Min(0f), Tooltip("視線が左右にずれたときに、ヨーを倒す強さ")]
        private float _yawGain = AircraftAimSpec.DefaultYawGain;

        [SerializeField, Min(0f), Tooltip("視線が左右にずれたときに、ロールを倒す強さ")]
        private float _rollGain = AircraftAimSpec.DefaultRollGain;

        [SerializeField, Min(0f), Tooltip("傾いた機体を水平へ戻す強さ")]
        private float _levelGain = AircraftAimSpec.DefaultLevelGain;

        [SerializeField, Min(0f), Tooltip("移動の前後を押し切ったときに、スロットルの開き具合が変わる速さ (1/秒)")]
        private float _throttleRate = AircraftAimSpec.DefaultThrottleRate;

        private AircraftAimSpec _spec;
        private float _throttle;

        public AircraftAimSpec Spec => _spec ??= new AircraftAimSpec(_pitchGain, _yawGain, _rollGain, _levelGain, _throttleRate);

        public float Throttle
        {
            get => _throttle;
            set
            {
                if (!(value >= 0f && value <= 1f))
                {
                    throw new ArgumentOutOfRangeException(nameof(value), value, "throttle must be between 0 and 1");
                }

                _throttle = value;
            }
        }

        private void OnValidate()
        {
            _spec = null;
        }

        public void Configure(AircraftAimSpec spec)
        {
            _spec = spec ?? throw new ArgumentNullException(nameof(spec));
            _pitchGain = spec.PitchGain;
            _yawGain = spec.YawGain;
            _rollGain = spec.RollGain;
            _levelGain = spec.LevelGain;
            _throttleRate = spec.ThrottleRate;
        }

        public VehicleMotorInput ToMotorInput(VehicleSeat seat, VehicleSeatInput input, float deltaSeconds)
        {
            var push = input.Move.y;
            _throttle = AircraftAim.StepThrottle(Spec, _throttle, push, Mathf.Max(0f, deltaSeconds));
            var rotation = transform.rotation;
            var localAim = Quaternion.Inverse(rotation) * input.AimDirection;
            var rightWingUp = Mathf.Clamp((rotation * Vector3.right).y, -1f, 1f);
            return AircraftAim.Compute(Spec, localAim.ToNumerics(), rightWingUp, input.Move.x, AircraftAim.ThrottleAxis(_throttle, push));
        }
    }
}
