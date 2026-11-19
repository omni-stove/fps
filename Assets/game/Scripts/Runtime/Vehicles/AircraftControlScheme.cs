using System;
using Shooter.Core;
using UnityEngine;

namespace Shooter.Runtime
{
    [DisallowMultipleComponent]
    public sealed class AircraftControlScheme : MonoBehaviour, IVehicleControlScheme, IVehicleControlSchemeReset
    {
        [SerializeField, Tooltip("当て方の強さを読む機体のアセット (Aim の欄)。空なら既定値")]
        private AircraftDefinition _definition;

        private AircraftAimSpec _configuredSpec;
        private AircraftAimSpec _definitionSpec;
        private float _throttle;

        public AircraftDefinition Definition
        {
            get => _definition;
            set
            {
                _definition = value;
                _configuredSpec = null;
                _definitionSpec = null;
            }
        }

        public AircraftAimSpec Spec => _configuredSpec ?? (_definitionSpec ??= BuildSpec());

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

        public void ResetState()
        {
            _throttle = 0f;
        }

        private void OnValidate()
        {
            _definitionSpec = null;
        }

        public void Configure(AircraftAimSpec spec)
        {
            _configuredSpec = spec ?? throw new ArgumentNullException(nameof(spec));
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

        private AircraftAimSpec BuildSpec()
        {
            return _definition != null ? _definition.ToAimSpec() : AircraftAimSpec.Default;
        }
    }
}
