using System;
using System.Collections.Generic;
using Shooter.Core;
using UnityEngine;

namespace Shooter.Runtime
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody))]
    public sealed class AircraftWheels : MonoBehaviour, IAircraftGroundSource
    {
        // 車輪の底より少し上から測るのは、脚が縮んで底が地面へ沈んでいても、地面を下から測り損ねないようにするため。
        private const float ProbeLift = 0.5f;

        // 当たりを全部でなく固定の数まで受けるのは、毎ステップ測ってもごみを出さないため。
        private const int MaxProbeHits = 16;

        [SerializeField, Tooltip("前輪の向き・ブレーキの値を読む機体のアセット (Ground の欄)。空なら既定値")]
        private AircraftDefinition _definition;

        [SerializeField, Tooltip("浮いているときに、地面からの高さを測る相手のレイヤー")]
        private LayerMask _groundLayers = Physics.DefaultRaycastLayers;

        private readonly List<WheelCollider> _wheels = new List<WheelCollider>();
        private readonly List<WheelCollider> _frontWheels = new List<WheelCollider>();
        private readonly List<WheelCollider> _found = new List<WheelCollider>();
        private readonly RaycastHit[] _hits = new RaycastHit[MaxProbeHits];
        private AircraftGroundSpec _configuredSpec;
        private AircraftGroundSpec _definitionSpec;
        private Rigidbody _body;
        private bool _collected;
        private AircraftGroundCommand _lastCommand;
        private AircraftGroundContact _lastContact;

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

        // Configure で渡した値をアセットより優先するのは、SimpleAircraftMotor と同じく、コードから組んだ機体がアセット無しでも値を差し替えられるようにするため。
        public AircraftGroundSpec Spec => _configuredSpec ?? (_definitionSpec ??= BuildSpec());

        public Rigidbody Body => _body != null ? _body : (_body = GetComponent<Rigidbody>());

        public LayerMask GroundLayers
        {
            get => _groundLayers;
            set => _groundLayers = value;
        }

        public IReadOnlyList<WheelCollider> Wheels
        {
            get
            {
                EnsureCollected();
                return _wheels;
            }
        }

        public IReadOnlyList<WheelCollider> FrontWheels
        {
            get
            {
                EnsureCollected();
                return _frontWheels;
            }
        }

        public AircraftGroundCommand LastCommand => _lastCommand;

        public AircraftGroundContact LastContact => _lastContact;

        private void Awake()
        {
            CollectWheels();
        }

        private void OnValidate()
        {
            _definitionSpec = null;
        }

        public void Configure(AircraftGroundSpec spec)
        {
            _configuredSpec = spec ?? throw new ArgumentNullException(nameof(spec));
        }

        // 車輪を付け外ししたり重心を動かしたりした後に呼ぶ。毎ステップ集め直さないのは、子を辿るとステップごとに手間とごみが出るため。
        public void CollectWheels()
        {
            _wheels.Clear();
            _frontWheels.Clear();
            _found.Clear();
            GetComponentsInChildren(true, _found);

            var body = Body;
            var centerOfMass = body.centerOfMass;
            foreach (var wheel in _found)
            {
                // 子にさらに Rigidbody を持つ部品 (外れた脚など) の車輪は、この機体の重さを支えないので数えない。
                if (wheel.GetComponentInParent<Rigidbody>(true) != body)
                {
                    continue;
                }

                _wheels.Add(wheel);
                if (LocalCenter(wheel).z > centerOfMass.z)
                {
                    _frontWheels.Add(wheel);
                }
            }

            _found.Clear();
            _collected = true;
        }

        public bool IsFrontWheel(WheelCollider wheel)
        {
            EnsureCollected();
            return wheel != null && _frontWheels.Contains(wheel);
        }

        public AircraftGroundContact GroundContact(VehicleMotorInput input, Vector3 localVelocity, Vector3 localAngularVelocity)
        {
            EnsureCollected();
            var grounded = false;
            foreach (var wheel in _wheels)
            {
                if (IsLive(wheel) && wheel.isGrounded)
                {
                    grounded = true;
                    break;
                }
            }

            Apply(AircraftGround.Compute(Spec, input, localVelocity.z, grounded));

            // 地上の力は WheelCollider が物理の側で掛けるので、ここで足す加速は無い。
            _lastContact = new AircraftGroundContact(
                grounded, System.Numerics.Vector3.Zero, System.Numerics.Vector3.Zero, grounded ? 0f : MeasureHeight());
            return _lastContact;
        }

        public void Apply(AircraftGroundCommand command)
        {
            EnsureCollected();
            _lastCommand = command;
            foreach (var wheel in _wheels)
            {
                if (wheel == null)
                {
                    continue;
                }

                wheel.steerAngle = 0f;
                wheel.brakeTorque = command.BrakeTorque;
            }

            foreach (var wheel in _frontWheels)
            {
                if (wheel != null)
                {
                    wheel.steerAngle = command.SteerAngle;
                }
            }
        }

        public float? MeasureHeight()
        {
            EnsureCollected();
            var origin = LowestPoint() + (Vector3.up * ProbeLift);
            var count = Physics.RaycastNonAlloc(
                origin, Vector3.down, _hits, Spec.HeightProbeDistance + ProbeLift, _groundLayers, QueryTriggerInteraction.Ignore);

            float? nearest = null;
            for (var i = 0; i < count; i++)
            {
                var hit = _hits[i];
                if (hit.collider == null || hit.collider.transform.IsChildOf(transform))
                {
                    continue;
                }

                var height = Mathf.Max(0f, hit.distance - ProbeLift);
                if (!nearest.HasValue || height < nearest.Value)
                {
                    nearest = height;
                }
            }

            return nearest;
        }

        private void EnsureCollected()
        {
            if (!_collected)
            {
                CollectWheels();
            }
        }

        // 車輪が無ければ機体の原点から測るのは、脚をしまった機体や脚が全部外れた機体でも高さを返せるようにするため。
        private Vector3 LowestPoint()
        {
            var lowest = transform.position;
            var found = false;
            foreach (var wheel in _wheels)
            {
                if (!IsLive(wheel))
                {
                    continue;
                }

                var wheelTransform = wheel.transform;
                var bottom = wheelTransform.TransformPoint(wheel.center) - (wheelTransform.up * (wheel.suspensionDistance + wheel.radius));
                if (!found || bottom.y < lowest.y)
                {
                    lowest = bottom;
                    found = true;
                }
            }

            return lowest;
        }

        private Vector3 LocalCenter(WheelCollider wheel)
        {
            return transform.InverseTransformPoint(wheel.transform.TransformPoint(wheel.center));
        }

        private static bool IsLive(WheelCollider wheel)
        {
            return wheel != null && wheel.enabled && wheel.gameObject.activeInHierarchy;
        }

        private AircraftGroundSpec BuildSpec()
        {
            return _definition != null ? _definition.ToGroundSpec() : AircraftGroundSpec.Default;
        }
    }
}
