using System;
using System.Collections.Generic;
using Shooter.Core;
using UnityEngine;

namespace Shooter.Runtime
{
    [DisallowMultipleComponent]
    public sealed class VehicleSeat : MonoBehaviour
    {
        [SerializeField, Tooltip("座席の種類。乗り物の Spec を Configure すると上書きされる")]
        private VehicleSeatRole _role = VehicleSeatRole.Passenger;

        [SerializeField, Tooltip("座っている間の姿勢")]
        private VehicleSeatPose _pose = VehicleSeatSpec.DefaultPose;

        [SerializeField, Tooltip("座っている間のカメラ")]
        private VehicleCameraView _cameraView = VehicleSeatSpec.DefaultCameraView;

        [SerializeField, Range(0f, VehicleSeatSpec.MaxOwnWeaponArcDegrees), Tooltip("座ったまま自分の武器で撃てる向きの範囲 (度)。0 は撃てない")]
        private float _ownWeaponArcDegrees = VehicleSeatSpec.DefaultOwnWeaponArcDegrees;

        [SerializeField, Min(1f), Tooltip("自分の武器で撃つときの散らばりに掛ける倍率")]
        private float _ownWeaponSpreadMultiplier = VehicleSeatSpec.DefaultOwnWeaponSpreadMultiplier;

        [SerializeField, Tooltip("乗り込める距離を測る点。空なら座席から測る")]
        private Transform[] _enterPoints = Array.Empty<Transform>();

        [SerializeField, Tooltip("降りる位置の候補 (足元)。並びの順に試し、塞がっていれば次へ")]
        private Transform[] _exitPoints = Array.Empty<Transform>();

        [SerializeField, Tooltip("座席からの視点。空なら乗っている人の目から見る")]
        private Transform _viewPoint;

        private VehicleSeatSpec _spec;

        public Vehicle Vehicle { get; private set; }

        public int Index { get; private set; } = -1;

        public VehicleSeatSpec Spec => _spec ??= new VehicleSeatSpec(_role, _pose, _cameraView, _ownWeaponArcDegrees, _ownWeaponSpreadMultiplier);

        public VehicleSeatRole Role => Spec.Role;

        public VehicleSeatPose Pose => Spec.Pose;

        public VehicleCameraView CameraView => Spec.CameraView;

        public IVehicleRider Occupant => Vehicle != null && Index >= 0 ? Vehicle.OccupantOf(Index) : null;

        public bool IsOccupied => Occupant != null;

        public IReadOnlyList<Transform> EnterPoints => _enterPoints;

        public IReadOnlyList<Transform> ExitPoints => _exitPoints;

        public Transform ViewPoint => _viewPoint;

        public Vector3 SitPosition => transform.position;

        public float SitYawDegrees => Quaternion.LookRotation(Flatten(transform.forward), Vector3.up).eulerAngles.y;

        private void OnValidate()
        {
            _spec = null;
        }

        public void ApplySpec(VehicleSeatSpec spec)
        {
            _spec = spec ?? throw new ArgumentNullException(nameof(spec));
            _role = spec.Role;
            _pose = spec.Pose;
            _cameraView = spec.CameraView;
            _ownWeaponArcDegrees = spec.OwnWeaponArcDegrees;
            _ownWeaponSpreadMultiplier = spec.OwnWeaponSpreadMultiplier;
        }

        public void SetEnterPoints(params Transform[] points)
        {
            _enterPoints = Compact(points);
        }

        public void SetExitPoints(params Transform[] points)
        {
            _exitPoints = Compact(points);
        }

        public void SetViewPoint(Transform viewPoint)
        {
            _viewPoint = viewPoint;
        }

        public float DistanceToEnter(Vector3 from)
        {
            var best = float.PositiveInfinity;
            var any = false;
            foreach (var point in _enterPoints)
            {
                if (point == null)
                {
                    continue;
                }

                any = true;
                best = Mathf.Min(best, Vector3.Distance(from, point.position));
            }

            return any ? best : Vector3.Distance(from, transform.position);
        }

        public bool SubmitInput(IVehicleRider rider, VehicleSeatInput input, float deltaSeconds)
        {
            if (rider == null)
            {
                throw new ArgumentNullException(nameof(rider));
            }

            if (!(deltaSeconds >= 0f) || float.IsInfinity(deltaSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(deltaSeconds), deltaSeconds, "deltaSeconds must be a non-negative finite number");
            }

            if (!ReferenceEquals(Occupant, rider))
            {
                return false;
            }

            Vehicle.DispatchInput(this, input, deltaSeconds);
            return true;
        }

        internal void AttachTo(Vehicle vehicle, int index)
        {
            Vehicle = vehicle;
            Index = index;
        }

        internal void Detach()
        {
            Vehicle = null;
            Index = -1;
        }

        private static Vector3 Flatten(Vector3 forward)
        {
            forward.y = 0f;
            return forward.sqrMagnitude > 1e-8f ? forward : Vector3.forward;
        }

        private static Transform[] Compact(Transform[] points)
        {
            if (points == null)
            {
                return Array.Empty<Transform>();
            }

            var list = new List<Transform>(points.Length);
            foreach (var point in points)
            {
                if (point != null)
                {
                    list.Add(point);
                }
            }

            return list.ToArray();
        }
    }
}
