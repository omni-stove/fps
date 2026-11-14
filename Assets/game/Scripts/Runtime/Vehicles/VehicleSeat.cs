using System;
using System.Collections.Generic;
using Fps.Core;
using UnityEngine;

namespace Fps.Runtime
{
    /// <summary>
    /// 乗り物の座席 1 つ。<see cref="Vehicle"/> の子に置き、この Transform の位置に乗っている人の足元を、向きに正面を合わせる。
    /// 種類・姿勢・カメラは <see cref="Spec"/> (乗り物の <see cref="VehicleSpec.Seats"/> の同じ番号) が決める。
    /// 乗る位置 (<see cref="EnterPoints"/>) は乗り込める距離を測る点で、無ければ座席そのものから測る。
    /// 降りる位置 (<see cref="ExitPoints"/>) は並びの順に試し、塞がっていれば次へ進む。座席の候補の後には乗り物の候補を試す。
    /// 座席からの視点 (<see cref="ViewPoint"/>) が無ければ、乗っている人の目から見る。
    /// 座っている人の入力は <see cref="SubmitInput"/> で乗り物の <see cref="IVehicleInputSink"/> に流す。
    /// </summary>
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

        [SerializeField, Tooltip("乗り込める距離を測る点。空なら座席から測る")]
        private Transform[] _enterPoints = Array.Empty<Transform>();

        [SerializeField, Tooltip("降りる位置の候補 (足元)。並びの順に試し、塞がっていれば次へ")]
        private Transform[] _exitPoints = Array.Empty<Transform>();

        [SerializeField, Tooltip("座席からの視点。空なら乗っている人の目から見る")]
        private Transform _viewPoint;

        private VehicleSeatSpec _spec;

        /// <summary>この座席を持つ乗り物。<see cref="Vehicle"/> が座席を集めたときに決まる。</summary>
        public Vehicle Vehicle { get; private set; }

        /// <summary>乗り物の中での番号 (<see cref="VehicleSpec.Seats"/> の添字)。乗り物に集められていなければ -1。</summary>
        public int Index { get; private set; } = -1;

        /// <summary>この座席の設定。</summary>
        public VehicleSeatSpec Spec => _spec ??= new VehicleSeatSpec(_role, _pose, _cameraView, _ownWeaponArcDegrees);

        public VehicleSeatRole Role => Spec.Role;

        public VehicleSeatPose Pose => Spec.Pose;

        public VehicleCameraView CameraView => Spec.CameraView;

        /// <summary>座っている人。空いていれば null。</summary>
        public IVehicleRider Occupant => Vehicle != null && Index >= 0 ? Vehicle.OccupantOf(Index) : null;

        public bool IsOccupied => Occupant != null;

        public IReadOnlyList<Transform> EnterPoints => _enterPoints;

        public IReadOnlyList<Transform> ExitPoints => _exitPoints;

        public Transform ViewPoint => _viewPoint;

        /// <summary>座っている人の足元を置く位置。</summary>
        public Vector3 SitPosition => transform.position;

        /// <summary>座っている人の正面の向き (ワールドの yaw、度)。</summary>
        public float SitYawDegrees => Quaternion.LookRotation(Flatten(transform.forward), Vector3.up).eulerAngles.y;

        private void OnValidate()
        {
            _spec = null;
        }

        /// <summary>設定を差し替える。乗り物の <see cref="Vehicle.Configure"/> が座席の番号ごとに呼ぶ。</summary>
        public void ApplySpec(VehicleSeatSpec spec)
        {
            _spec = spec ?? throw new ArgumentNullException(nameof(spec));
            _role = spec.Role;
            _pose = spec.Pose;
            _cameraView = spec.CameraView;
            _ownWeaponArcDegrees = spec.OwnWeaponArcDegrees;
        }

        /// <summary>乗る位置を差し替える。null なら無し (座席から測る)。null の要素は飛ばす。</summary>
        public void SetEnterPoints(params Transform[] points)
        {
            _enterPoints = Compact(points);
        }

        /// <summary>降りる位置の候補を差し替える。null なら無し。null の要素は飛ばす。</summary>
        public void SetExitPoints(params Transform[] points)
        {
            _exitPoints = Compact(points);
        }

        /// <summary>座席からの視点を差し替える。null なら乗っている人の目から見る。</summary>
        public void SetViewPoint(Transform viewPoint)
        {
            _viewPoint = viewPoint;
        }

        /// <summary>from から一番近い乗る位置までの距離 (m)。乗る位置が無ければ座席までの距離。</summary>
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

        /// <summary>
        /// rider がこの座席に座っていれば、このフレームの入力を乗り物の <see cref="IVehicleInputSink"/> に流して true を返す。
        /// 座っていなければ (他の人の座席・降りた後) 何もせず false。
        /// </summary>
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
