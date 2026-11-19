using System;
using System.Collections.Generic;
using Shooter.Core;
using UnityEngine;

namespace Shooter.Runtime
{
    // 時間を Tick で自分で数えるのは、Update が走らない Edit Mode のテストでも直接進められるようにするため。
    [DisallowMultipleComponent]
    public sealed class DebrisPool : MonoBehaviour
    {
        public const string DefaultObjectName = "DebrisPool";
        public const string DebrisName = "Debris";
        public const int DefaultCapacity = 200;
        public const float DefaultLifetime = 4f;

        // Runtime は Protocol を参照しないので、キャラクターの落下の下限 (ConnectAcceptedMessage.DefaultKillPlaneY) と同じ値をここにも持つ。
        public const float DefaultKillPlaneY = -50f;
        public const float DefaultMaxTravel = 100f;

        // Ignore Raycast は Physics.DefaultRaycastLayers に含まれないので、破片が射撃 (Hitscan) や爆発の範囲 (StructureDamage) に拾われない。
        public const int DebrisLayer = 2;

        private static DebrisPool _instance;

        [SerializeField, Min(0), Tooltip("同時に存在できる破片の数。超えたら古い物から消す")]
        private int _capacity = DefaultCapacity;

        [SerializeField, Min(0.01f), Tooltip("破片が消えるまでの秒数")]
        private float _lifetime = DefaultLifetime;

        [SerializeField, Tooltip("破片がこの高さ (m) より下へ落ちたら、寿命を待たずに消す")]
        private float _killPlaneY = DefaultKillPlaneY;

        [SerializeField, Min(0f), Tooltip("破片が出た位置からこの距離 (m) より遠くへ行ったら、寿命を待たずに消す。0 なら距離では消さない")]
        private float _maxTravel = DefaultMaxTravel;

        [SerializeField, Tooltip("破片の見た目。大きさ 1m の形にする (出すときに破片の大きさへ拡大する)。当たり判定が無ければ箱の当たり判定を足す。空なら仮の箱を作る")]
        private GameObject _debrisPrefab;

        private readonly List<Entry> _entries = new List<Entry>();
        private double _clock;

        public GameObject DebrisPrefab
        {
            get => _debrisPrefab;
            set => _debrisPrefab = value;
        }

        public int Capacity
        {
            get => _capacity;
            set
            {
                if (value < 0)
                {
                    throw new ArgumentOutOfRangeException(nameof(value), value, "Capacity must not be negative");
                }

                _capacity = value;
                Prune();
                TrimTo(_capacity);
            }
        }

        public float Lifetime
        {
            get => _lifetime;
            set
            {
                if (!(value > 0f) || float.IsInfinity(value))
                {
                    throw new ArgumentOutOfRangeException(nameof(value), value, "Lifetime must be positive and finite");
                }

                _lifetime = value;
            }
        }

        public float KillPlaneY
        {
            get => _killPlaneY;
            set
            {
                if (float.IsNaN(value))
                {
                    throw new ArgumentOutOfRangeException(nameof(value), value, "KillPlaneY must not be NaN");
                }

                _killPlaneY = value;
            }
        }

        public float MaxTravel
        {
            get => _maxTravel;
            set
            {
                if (!(value >= 0f) || float.IsInfinity(value))
                {
                    throw new ArgumentOutOfRangeException(nameof(value), value, "MaxTravel must be zero or a positive finite number");
                }

                _maxTravel = value;
            }
        }

        public int Count
        {
            get
            {
                Prune();
                return _entries.Count;
            }
        }

        public static DebrisPool GetOrCreate()
        {
            if (_instance != null)
            {
                return _instance;
            }

            _instance = FindAnyObjectByType<DebrisPool>();
            if (_instance == null)
            {
                _instance = new GameObject(DefaultObjectName).AddComponent<DebrisPool>();
            }

            return _instance;
        }

        private void Awake()
        {
            if (_instance == null)
            {
                _instance = this;
            }
        }

        private void OnDestroy()
        {
            if (_instance == this)
            {
                _instance = null;
            }
        }

        private void Update()
        {
            Tick(Time.deltaTime);
        }

        public GameObject Spawn(Vector3 position, Quaternion rotation, Vector3 size, Material material, Vector3 velocity)
        {
            return Spawn(position, rotation, size, material, velocity, null, null, null);
        }

        public GameObject Spawn(
            Vector3 position,
            Quaternion rotation,
            Vector3 size,
            Material material,
            Vector3 velocity,
            DebrisCrushDamage crushDamage,
            ShotAttribution? attribution,
            string source)
        {
            if (!(size.x > 0f) || !(size.y > 0f) || !(size.z > 0f))
            {
                throw new ArgumentOutOfRangeException(nameof(size), size, "size must be positive on every axis");
            }

            if (_capacity == 0)
            {
                return null;
            }

            Prune();
            TrimTo(_capacity - 1);

            var debris = _debrisPrefab != null
                ? VisualPrefab.Instantiate(_debrisPrefab, transform, DebrisName, stripColliders: false)
                : GameObject.CreatePrimitive(PrimitiveType.Cube);
            try
            {
                debris.name = DebrisName;
                VisualPrefab.SetLayer(debris, DebrisLayer);
                debris.transform.SetParent(transform, false);
                debris.transform.SetPositionAndRotation(position, rotation);
                debris.transform.localScale = size;
                if (_debrisPrefab != null)
                {
                    // 当たり判定が無いと床をすり抜けて落ち続け、下敷きのダメージ (DebrisCrusher) も起きない。
                    if (debris.GetComponentInChildren<Collider>(true) == null)
                    {
                        debris.AddComponent<BoxCollider>();
                    }
                }
                else if (material != null)
                {
                    debris.GetComponent<Renderer>().sharedMaterial = material;
                }

                var body = debris.GetComponent<Rigidbody>();
                if (body == null)
                {
                    body = debris.AddComponent<Rigidbody>();
                }

                body.mass = Mathf.Max(0.05f, size.x * size.y * size.z * 100f);
                body.linearVelocity = velocity;

                if (crushDamage != null)
                {
                    debris.AddComponent<DebrisCrusher>().Initialize(crushDamage, attribution, source);
                }
            }
            catch
            {
                DestroySafely(debris);
                throw;
            }

            _entries.Add(new Entry(debris, _clock + _lifetime, position));
            return debris;
        }

        public int Tick(float deltaSeconds)
        {
            if (!(deltaSeconds >= 0f) || float.IsInfinity(deltaSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(deltaSeconds), deltaSeconds, "deltaSeconds must be a non-negative finite number");
            }

            _clock += deltaSeconds;
            var removed = 0;
            var fell = 0;
            var strayed = 0;
            for (var i = _entries.Count - 1; i >= 0; i--)
            {
                var entry = _entries[i];
                if (entry.Debris == null)
                {
                    _entries.RemoveAt(i);
                    continue;
                }

                var position = entry.Debris.transform.position;
                var below = position.y < _killPlaneY;
                var away = !below && _maxTravel > 0f && (position - entry.Origin).sqrMagnitude > _maxTravel * _maxTravel;
                if (entry.ExpiresAt <= _clock || below || away)
                {
                    _entries.RemoveAt(i);
                    DestroySafely(entry.Debris);
                    removed++;
                    if (below)
                    {
                        fell++;
                    }
                    else if (away)
                    {
                        strayed++;
                    }
                }
            }

            if (fell > 0 || strayed > 0)
            {
                Debug.Log(FormatOutOfBoundsLog(fell, strayed), this);
            }

            return removed;
        }

        public static string FormatOutOfBoundsLog(int fell, int strayed)
        {
            return $"{DestructibleStructure.LogTag} debris removed out of bounds fell={fell} strayed={strayed}";
        }

        public void Clear()
        {
            TrimTo(0);
        }

        private void TrimTo(int max)
        {
            var target = Math.Max(0, max);
            while (_entries.Count > target)
            {
                var oldest = _entries[0].Debris;
                _entries.RemoveAt(0);
                DestroySafely(oldest);
            }
        }

        private void Prune()
        {
            _entries.RemoveAll(entry => entry.Debris == null);
        }

        private static void DestroySafely(UnityEngine.Object target)
        {
            if (target == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(target);
            }
            else
            {
                DestroyImmediate(target);
            }
        }

        private readonly struct Entry
        {
            public GameObject Debris { get; }

            public double ExpiresAt { get; }

            public Vector3 Origin { get; }

            public Entry(GameObject debris, double expiresAt, Vector3 origin)
            {
                Debris = debris;
                ExpiresAt = expiresAt;
                Origin = origin;
            }
        }
    }
}
