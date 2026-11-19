using System;
using System.Collections.Generic;
using Shooter.Core;
using UnityEngine;

namespace Shooter.Runtime
{
    // 乗っている体をここから押さないのは、体の当たりと接地は乗る側 (PlayerController) の Move でしか正しく決まらないため。
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(ExecutionOrder)]
    public sealed class MovingPlatform : MonoBehaviour, IRideablePlatform
    {
        public const string LogTag = MovingPlatformLog.LogTag;

        // PlayerController (既定の順) より先に動かし、同じフレームのうちに乗っている体を運ばせる
        public const int ExecutionOrder = -100;

        public const string SlabName = "PlatformSlab";

        public static readonly Vector3 DefaultSecondStop = new Vector3(0f, 0f, 6f);

        public static readonly Vector3 DefaultSlabSize = new Vector3(3f, 0.3f, 3f);

        [SerializeField, Tooltip("動き方・速さ・待ち・回転の値。空なら既定の値 (往復・2 m/s・1 秒待つ・回らない)")]
        private MovingPlatformSettings _settings;

        [SerializeField, Tooltip("通る点。置いた位置からのずれ (m、置いたときの向きの座標)。最初の点から始まる。1 つなら進まない (回るだけの床)")]
        private List<Vector3> _stops = DefaultStops();

        [SerializeField, Tooltip("Collider が無いときに作る板の見た目。空なら箱を作る")]
        private GameObject _slabPrefab;

        [SerializeField, Tooltip("Collider が無いときに作る板の大きさ (m)")]
        private Vector3 _slabSize = DefaultSlabSize;

        private readonly HashSet<object> _riders = new HashSet<object>();
        private MovingPlatformSpec _spec;
        private PlatformRoute _route;
        private Vector3 _origin;
        private Quaternion _baseRotation;
        private bool _hasOrigin;

        public MovingPlatformSettings Settings
        {
            get => _settings;
            set => _settings = value;
        }

        public IReadOnlyList<Vector3> Stops => _stops ??= DefaultStops();

        public GameObject SlabPrefab
        {
            get => _slabPrefab;
            set => _slabPrefab = value;
        }

        public Vector3 SlabSize
        {
            get => _slabSize;
            set => _slabSize = value;
        }

        public bool IsInitialized => _route != null;

        public MovingPlatformSpec Spec => _spec;

        public PlatformRoute Route => _route;

        public PlatformMotion LastMotion { get; private set; }

        public float LastStepSeconds { get; private set; }

        // 乗る側が Time.frameCount でなくこの数で新しい動きを見分けるのは、フレームの進まない EditMode のテストでも、同じ動きを 2 度運ばないため
        public int StepCount { get; private set; }

        public int RiderCount => _riders.Count;

        private void Awake()
        {
            if (!IsInitialized && !TryInitialize())
            {
                enabled = false;
            }
        }

        private void OnDisable()
        {
            LastMotion = PlatformMotion.None;
        }

        private void Update()
        {
            Step(Time.deltaTime);
        }

        // 置いた位置を道の原点として覚えるので、2 回目からは最初の位置と向きを使う
        public bool TryInitialize()
        {
            try
            {
                Configure(_settings, Stops);
                return true;
            }
            catch (ArgumentException e)
            {
                Debug.LogError(MovingPlatformLog.InvalidSettings(name, e.Message), this);
                return false;
            }
        }

        public void Configure(MovingPlatformSettings settings, IReadOnlyList<Vector3> stops)
        {
            if (stops == null)
            {
                throw new ArgumentNullException(nameof(stops));
            }

            var spec = MovingPlatformSettings.Resolve(settings);
            var points = new System.Numerics.Vector3[stops.Count];
            for (var i = 0; i < stops.Count; i++)
            {
                points[i] = stops[i].ToNumerics();
            }

            // 作れることを確かめてから置き換え、不正な値で前の道を壊さない
            var route = new PlatformRoute(points, spec);
            if (!_hasOrigin)
            {
                _origin = transform.position;
                _baseRotation = transform.rotation;
                _hasOrigin = true;
            }

            _settings = settings;
            _stops = new List<Vector3>(stops);
            _spec = spec;
            _route = route;
            EnsureShape();
            EnsureKinematicBody();
            ApplyPose();
            LastMotion = PlatformMotion.None;
        }

        public void Step(float seconds)
        {
            if (!(seconds >= 0f) || float.IsInfinity(seconds))
            {
                throw new ArgumentOutOfRangeException(nameof(seconds), seconds, "seconds must be finite and not negative");
            }

            if (!IsInitialized)
            {
                return;
            }

            var before = transform.position;
            var spinBefore = _route.SpinDegrees;
            _route.Step(seconds);
            ApplyPose();
            LastMotion = new PlatformMotion(
                before.ToNumerics(),
                (transform.position - before).ToNumerics(),
                Mathf.DeltaAngle(spinBefore, _route.SpinDegrees) * Mathf.Deg2Rad);
            LastStepSeconds = seconds;
            StepCount++;
        }

        public bool Call(int stop)
        {
            if (!IsInitialized)
            {
                throw new InvalidOperationException("MovingPlatform is not initialized");
            }

            var accepted = _route.Call(stop);
            Debug.Log(MovingPlatformLog.Called(name, stop, _spec.Kind, accepted), this);
            return accepted;
        }

        // 乗っている体が上がる床と頭上の間に挟まれた。最後の Step を取り消して引き返す。
        // 運ぶ側はこの Step の動きを読み終えているので、取り消した動きは誰にも運ばせない
        public void Obstruct()
        {
            if (!IsInitialized)
            {
                return;
            }

            _route.Reverse();
            ApplyPose();
            LastMotion = PlatformMotion.None;
            StepCount++;
            Debug.Log(MovingPlatformLog.Reversed(name, _route.From, _route.To), this);
        }

        public void Board(object rider)
        {
            if (rider == null)
            {
                throw new ArgumentNullException(nameof(rider));
            }

            if (!_riders.Add(rider) || !IsInitialized)
            {
                return;
            }

            if (_spec.Kind == PlatformRouteKind.OnCall && _spec.DepartWhenBoarded && !_route.IsMoving && _route.Calls.Count == 0)
            {
                _route.CallNext();
            }
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

        public Vector3 WorldPoint(int stop)
        {
            if (!IsInitialized)
            {
                throw new InvalidOperationException("MovingPlatform is not initialized");
            }

            return _origin + (_baseRotation * _route.Point(stop).ToUnity());
        }

        private void ApplyPose()
        {
            var position = _origin + (_baseRotation * _route.Position.ToUnity());
            var rotation = Quaternion.Euler(0f, _route.SpinDegrees, 0f) * _baseRotation;
            transform.SetPositionAndRotation(position, rotation);

            // 自動で揃えない設定 (既定) では、同じフレームの CharacterController.Move が動く前の当たりで判定してしまう
            if (!Physics.autoSyncTransforms)
            {
                Physics.SyncTransforms();
            }
        }

        private void EnsureShape()
        {
            if (GetComponentInChildren<Collider>(true) != null)
            {
                return;
            }

            if (_slabPrefab != null)
            {
                var visual = Instantiate(_slabPrefab, transform, false);
                visual.name = SlabName;
                if (GetComponentInChildren<Collider>(true) == null)
                {
                    var box = gameObject.AddComponent<BoxCollider>();
                    box.size = _slabSize;
                }
            }
            else
            {
                var slab = GameObject.CreatePrimitive(PrimitiveType.Cube);
                slab.name = SlabName;
                slab.transform.SetParent(transform, false);
                slab.transform.localScale = _slabSize;
            }

            Debug.LogWarning(MovingPlatformLog.BuiltDefaultShape(name), this);
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

        private static List<Vector3> DefaultStops()
        {
            return new List<Vector3> { Vector3.zero, DefaultSecondStop };
        }
    }
}
