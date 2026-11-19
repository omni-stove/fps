using System;
using Shooter.Core;
using UnityEngine;

namespace Shooter.Runtime
{
    [DisallowMultipleComponent]
    public sealed class AircraftControlScheme : MonoBehaviour, IVehicleControlScheme, IVehicleControlSchemeReset
    {
        private const int MaxGroundHits = 8;

        private static readonly RaycastHit[] _groundBuffer = new RaycastHit[MaxGroundHits];

        [SerializeField, Tooltip("当て方の強さを読む機体のアセット (Aim の欄)。空なら既定値")]
        private AircraftDefinition _definition;

        [SerializeField, Tooltip("引き起こすかを決めるために、真下の地面までを測るレイヤー")]
        private LayerMask _groundMask = Physics.DefaultRaycastLayers;

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

        public LayerMask GroundMask
        {
            get => _groundMask;
            set => _groundMask = value;
        }

        public VehicleMotorInput ToMotorInput(VehicleSeat seat, VehicleSeatInput input, float deltaSeconds)
        {
            var spec = Spec;
            var push = input.Move.y;
            _throttle = AircraftAim.StepThrottle(spec, _throttle, push, Mathf.Max(0f, deltaSeconds));
            var rotation = transform.rotation;
            var toLocal = Quaternion.Inverse(rotation);
            var localAim = toLocal * input.AimDirection;
            var rightWingUp = Mathf.Clamp((rotation * Vector3.right).y, -1f, 1f);
            var situation = Situation(spec, toLocal);
            return AircraftAim.Compute(
                spec, localAim.ToNumerics(), rightWingUp, input.Move.x, AircraftAim.ThrottleAxis(_throttle, push), situation);
        }

        // 毎回引き直すのは、モーターや Rigidbody が乗っている間に付け外しされても、次の入力から効くようにするため。
        private AircraftAimSituation Situation(AircraftAimSpec spec, Quaternion toLocal)
        {
            var state = TryGetComponent<SimpleAircraftMotor>(out var motor) ? motor.State : default;
            var climbRate = TryGetComponent<Rigidbody>(out var body) ? body.linearVelocity.y : 0f;
            var groundDistance = GroundDistance(spec, climbRate);
            return new AircraftAimSituation(state, (toLocal * Vector3.up).ToNumerics(), climbRate, groundDistance);
        }

        // 引き起こす秒数の分だけ降りる距離までしか測らないのは、それより遠い地面は引き起こしに関わらず、長いレイを毎回撃たずに済むため。
        private float GroundDistance(AircraftAimSpec spec, float climbRate)
        {
            var sink = -climbRate;
            if (!(sink > spec.MinSinkRate) || !(spec.GroundWarningSeconds > 0f))
            {
                return float.PositiveInfinity;
            }

            var reach = sink * spec.GroundWarningSeconds;
            var count = Physics.RaycastNonAlloc(transform.position, Vector3.down, _groundBuffer, reach, _groundMask, QueryTriggerInteraction.Ignore);
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

        private AircraftAimSpec BuildSpec()
        {
            return _definition != null ? _definition.ToAimSpec() : AircraftAimSpec.Default;
        }
    }
}
