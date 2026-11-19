using System;
using System.Collections.Generic;
using Shooter.Core;
using UnityEngine;

namespace Shooter.Runtime
{
    // 進みは PayloadRunner が Payload から決めて MoveTo で渡す。台車が自分で進まないのは、押す人数と守り側を知っているのが Runner だから。
    // 乗っている体をここから押さないのは MovingPlatform と同じく、体の当たりと接地は乗る側の Move でしか正しく決まらないため。
    [DisallowMultipleComponent]
    public sealed class PayloadCart : MonoBehaviour, IRideablePlatform
    {
        public const string BodyName = "CartBody";

        public static readonly Vector3 DefaultBodySize = new Vector3(2f, 1f, 3f);

        // 坂を真上に上る区間では水平の向きが決まらないので、この長さより短ければ向きを変えない。
        private const float _minHeadingLength = 1e-4f;

        [SerializeField, Tooltip("道筋の点。始点から終点の順に並べる。台車の子にすると台車と一緒に動くので、台車の外に置く。読むのは始めの 1 回だけ")]
        private Transform[] _points = Array.Empty<Transform>();

        [SerializeField, Tooltip("中継点にする点の番号 (上の並びの何番目か、0 から)。始点と終点は選べない。道筋の順に並べる")]
        private int[] _checkpoints = Array.Empty<int>();

        [SerializeField, Tooltip("台車のそばにいる人を数える範囲。空なら子の ControlPointZone を使い、それも無ければ既定の半径のものを足す")]
        private ControlPointZone _zone;

        [SerializeField, Tooltip("Collider が無いときに作る台車の見た目。空なら箱を作る")]
        private GameObject _bodyPrefab;

        [SerializeField, Tooltip("Collider が無いときに作る台車の大きさ (m)。置いた位置を底の中心にする")]
        private Vector3 _bodySize = DefaultBodySize;

        private readonly HashSet<object> _riders = new HashSet<object>();
        private PayloadRoute _route;
        private double _distance;

        public IReadOnlyList<Transform> Points => _points ?? Array.Empty<Transform>();

        public IReadOnlyList<int> Checkpoints => _checkpoints ?? Array.Empty<int>();

        public ControlPointZone Zone => _zone;

        public GameObject BodyPrefab
        {
            get => _bodyPrefab;
            set => _bodyPrefab = value;
        }

        public Vector3 BodySize
        {
            get => _bodySize;
            set => _bodySize = value;
        }

        public bool IsInitialized => _route != null;

        public PayloadRoute Route => _route;

        public double Distance => _distance;

        public int StepCount { get; private set; }

        public PlatformMotion LastMotion { get; private set; }

        public float LastStepSeconds { get; private set; }

        public int RiderCount => _riders.Count;

        public void SetRoute(IReadOnlyList<Transform> points, IReadOnlyList<int> checkpoints)
        {
            _points = Copy(points);
            _checkpoints = Copy(checkpoints);
        }

        public void SetZone(ControlPointZone zone)
        {
            _zone = zone;
        }

        // 点の位置をここで写すので、組み立てた後に点を動かしても道筋は変わらない。
        public PayloadRoute BuildRoute()
        {
            var points = Points;
            if (points.Count == 0)
            {
                throw new InvalidOperationException($"PayloadCart '{name}' has no route points; list the route from start to end");
            }

            var positions = new System.Numerics.Vector3[points.Count];
            for (var i = 0; i < positions.Length; i++)
            {
                var point = points[i];
                if (point == null)
                {
                    throw new InvalidOperationException($"PayloadCart '{name}' route point #{i} is not set");
                }

                positions[i] = point.position.ToNumerics();
            }

            PayloadRoute route;
            try
            {
                route = new PayloadRoute(positions, Checkpoints);
            }
            catch (ArgumentException e)
            {
                throw new ArgumentException($"PayloadCart '{name}' has an invalid route: {e.Message}", nameof(Points), e);
            }

            _route = route;
            EnsureZone();
            EnsureShape();
            EnsureKinematicBody();
            ApplyPose(0d);
            LastMotion = PlatformMotion.None;
            StepCount++;
            return route;
        }

        public void MoveTo(double distance, float seconds)
        {
            if (!IsInitialized)
            {
                throw new InvalidOperationException($"PayloadCart '{name}' has no route; call BuildRoute first");
            }

            if (!(seconds >= 0f) || float.IsInfinity(seconds))
            {
                throw new ArgumentOutOfRangeException(nameof(seconds), seconds, "seconds must be finite and not negative");
            }

            var before = transform.position;
            var yawBefore = transform.eulerAngles.y;
            ApplyPose(distance);
            LastMotion = new PlatformMotion(
                before.ToNumerics(),
                (transform.position - before).ToNumerics(),
                Mathf.DeltaAngle(yawBefore, transform.eulerAngles.y) * Mathf.Deg2Rad);
            LastStepSeconds = seconds;
            StepCount++;
        }

        // 台車の進みは試合の決まりなので戻さず、この一歩だけを乗る人に運ばせない。
        public void Obstruct()
        {
            LastMotion = PlatformMotion.None;
            StepCount++;
            Debug.Log(PayloadLog.Obstructed(name), this);
        }

        public void Board(object rider)
        {
            if (rider == null)
            {
                throw new ArgumentNullException(nameof(rider));
            }

            _riders.Add(rider);
        }

        public void Leave(object rider)
        {
            if (rider == null)
            {
                throw new ArgumentNullException(nameof(rider));
            }

            _riders.Remove(rider);
        }

        public bool IsRiddenBy(object rider)
        {
            return rider != null && _riders.Contains(rider);
        }

        private void ApplyPose(double distance)
        {
            _distance = _route.Clamp(distance);
            var position = _route.PositionAt(_distance).ToUnity();
            var direction = _route.DirectionAt(_distance);
            var rotation = transform.rotation;
            var horizontal = new Vector2(direction.X, direction.Z);
            if (horizontal.magnitude > _minHeadingLength)
            {
                rotation = Quaternion.Euler(0f, Mathf.Atan2(direction.X, direction.Z) * Mathf.Rad2Deg, 0f);
            }

            transform.SetPositionAndRotation(position, rotation);

            // 自動で揃えない設定 (既定) では、同じフレームの CharacterController.Move が動く前の当たりで判定してしまう
            if (!Physics.autoSyncTransforms)
            {
                Physics.SyncTransforms();
            }
        }

        private void EnsureZone()
        {
            if (_zone != null)
            {
                return;
            }

            _zone = GetComponentInChildren<ControlPointZone>(true);
            if (_zone != null)
            {
                return;
            }

            _zone = gameObject.AddComponent<ControlPointZone>();
            Debug.Log(PayloadLog.BuiltZone(name), this);
        }

        private void EnsureShape()
        {
            if (GetComponentInChildren<Collider>(true) != null)
            {
                return;
            }

            var center = new Vector3(0f, _bodySize.y * 0.5f, 0f);
            if (_bodyPrefab != null)
            {
                var visual = Instantiate(_bodyPrefab, transform, false);
                visual.name = BodyName;
                if (GetComponentInChildren<Collider>(true) == null)
                {
                    var box = gameObject.AddComponent<BoxCollider>();
                    box.size = _bodySize;
                    box.center = center;
                }
            }
            else
            {
                var body = GameObject.CreatePrimitive(PrimitiveType.Cube);
                body.name = BodyName;
                body.transform.SetParent(transform, false);
                body.transform.localPosition = center;
                body.transform.localScale = _bodySize;
            }

            Debug.LogWarning(PayloadLog.BuiltDefaultCart(name), this);
        }

        // 静的な Collider を毎フレーム動かすと物理が静的な木を組み直すので、動く Collider として扱わせる
        private void EnsureKinematicBody()
        {
            var body = GetComponent<Rigidbody>();
            if (body == null)
            {
                body = gameObject.AddComponent<Rigidbody>();
            }

            body.isKinematic = true;
            body.useGravity = false;
            body.interpolation = RigidbodyInterpolation.None;
        }

        private static T[] Copy<T>(IReadOnlyList<T> items)
        {
            if (items == null)
            {
                return Array.Empty<T>();
            }

            var copy = new T[items.Count];
            for (var i = 0; i < copy.Length; i++)
            {
                copy[i] = items[i];
            }

            return copy;
        }

        private void OnDrawGizmos()
        {
            var points = Points;
            Gizmos.color = Color.yellow;
            for (var i = 1; i < points.Count; i++)
            {
                if (points[i - 1] != null && points[i] != null)
                {
                    Gizmos.DrawLine(points[i - 1].position, points[i].position);
                }
            }
        }
    }
}
