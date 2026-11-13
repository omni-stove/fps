using System;
using System.Collections.Generic;
using UnityEngine;

namespace Fps.Runtime
{
    /// <summary>
    /// 建造物が壊れたときに出す、見た目だけの破片 (Rigidbody 付きの小さな Cube) をまとめて持つ。
    /// 破片は <see cref="Lifetime"/> 秒で消え、<see cref="Capacity"/> 個を超えたら古い物から消す。
    /// シーンに 1 つあればよく、<see cref="GetOrCreate"/> で探すか作る。
    /// 時間は自分で数える (<see cref="Tick"/>) ので、Update が走らない Edit Mode のテストでも直接進められる。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DebrisPool : MonoBehaviour
    {
        public const string DefaultObjectName = "DebrisPool";
        public const string DebrisName = "Debris";
        public const int DefaultCapacity = 200;
        public const float DefaultLifetime = 4f;

        /// <summary>
        /// 破片を置くレイヤー。<see cref="Physics.DefaultRaycastLayers"/> に含まれないので、
        /// 射撃 (<see cref="Hitscan"/>) や爆発の範囲 (<see cref="StructureDamage"/>) に拾われない。
        /// </summary>
        public const int DebrisLayer = 2; // Ignore Raycast

        private static DebrisPool _instance;

        [SerializeField, Min(0), Tooltip("同時に存在できる破片の数。超えたら古い物から消す")]
        private int _capacity = DefaultCapacity;

        [SerializeField, Min(0.01f), Tooltip("破片が消えるまでの秒数")]
        private float _lifetime = DefaultLifetime;

        private readonly List<Entry> _entries = new List<Entry>();
        private double _clock;

        /// <summary>同時に存在できる破片の数。0 なら破片を出さない。下げたときは超えた分をすぐ古い物から消す。</summary>
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

        /// <summary>これから出す破片が消えるまでの秒数。0 より大きい有限値。出し済みの破片には反映しない。</summary>
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

        /// <summary>今ある破片の数 (外から消された物は数えない)。</summary>
        public int Count
        {
            get
            {
                Prune();
                return _entries.Count;
            }
        }

        /// <summary>
        /// シーンにある DebrisPool を返す。無ければ <see cref="DefaultObjectName"/> の GameObject を作って付ける。
        /// </summary>
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

        /// <summary>
        /// 破片を 1 つ出す。<see cref="Capacity"/> に達していれば先に一番古い破片を消す。
        /// Capacity が 0 なら何も出さずに null を返す。
        /// </summary>
        /// <param name="position">中心の位置。</param>
        /// <param name="rotation">向き。</param>
        /// <param name="size">大きさ (m)。各軸 0 より大きい。</param>
        /// <param name="material">見た目の材質。null なら Cube の既定のまま。</param>
        /// <param name="velocity">出したときの速度。</param>
        public GameObject Spawn(Vector3 position, Quaternion rotation, Vector3 size, Material material, Vector3 velocity)
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

            var debris = GameObject.CreatePrimitive(PrimitiveType.Cube);
            try
            {
                debris.name = DebrisName;
                debris.layer = DebrisLayer;
                debris.transform.SetParent(transform, false);
                debris.transform.SetPositionAndRotation(position, rotation);
                debris.transform.localScale = size;
                if (material != null)
                {
                    debris.GetComponent<Renderer>().sharedMaterial = material;
                }

                var body = debris.AddComponent<Rigidbody>();
                body.mass = Mathf.Max(0.05f, size.x * size.y * size.z * 100f);
                body.linearVelocity = velocity;
            }
            catch
            {
                DestroySafely(debris);
                throw;
            }

            _entries.Add(new Entry(debris, _clock + _lifetime));
            return debris;
        }

        /// <summary>時間を deltaSeconds 進め、寿命の過ぎた破片を消す。消した数を返す。</summary>
        /// <param name="deltaSeconds">前回からの経過時間 (秒)。0 以上の有限値。</param>
        public int Tick(float deltaSeconds)
        {
            if (!(deltaSeconds >= 0f) || float.IsInfinity(deltaSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(deltaSeconds), deltaSeconds, "deltaSeconds must be a non-negative finite number");
            }

            _clock += deltaSeconds;
            var removed = 0;
            for (var i = _entries.Count - 1; i >= 0; i--)
            {
                var entry = _entries[i];
                if (entry.Debris == null)
                {
                    _entries.RemoveAt(i);
                    continue;
                }

                if (entry.ExpiresAt <= _clock)
                {
                    _entries.RemoveAt(i);
                    DestroySafely(entry.Debris);
                    removed++;
                }
            }

            return removed;
        }

        /// <summary>今ある破片をすべて消す。</summary>
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

            public Entry(GameObject debris, double expiresAt)
            {
                Debris = debris;
                ExpiresAt = expiresAt;
            }
        }
    }
}
