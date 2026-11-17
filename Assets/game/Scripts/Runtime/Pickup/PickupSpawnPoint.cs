using System;
using Shooter.Core;
using UnityEngine;

namespace Shooter.Runtime
{
    [DisallowMultipleComponent]
    public sealed class PickupSpawnPoint : MonoBehaviour
    {
        public const string LogTag = PickupLog.Tag;

        public const float DefaultRespawnSeconds = 20f;

        [SerializeField, Tooltip("拾える物の調整値。入れると下の中身・拾い方・再出現の秒数・範囲の代わりにこれを使う")]
        private PickupDefinition _definition;

        [SerializeField, Tooltip("出す物の中身")]
        private PickupContents _contents = PickupContents.ForHealth(PickupContents.DefaultHealthAmount);

        [SerializeField, Tooltip("拾い方")]
        private PickupCollectMode _collectMode = PickupCollectMode.Touch;

        [SerializeField, Min(0f), Tooltip("拾われてから再び出るまでの秒数。0 なら一度きり")]
        private float _respawnSeconds = DefaultRespawnSeconds;

        [SerializeField, Min(Pickup.MinRadius), Tooltip("拾える範囲の半径 (m)")]
        private float _radius = Pickup.DefaultRadius;

        [SerializeField, Range(0f, Pickup.MaxLookAngleDegrees), Tooltip("Look で、照準の向きからこの角度 (度) 以内に見た目が見えていれば照準が合っているとする。0 なら見た目に照準の線が当たったときだけ")]
        private float _lookAngleDegrees = Pickup.DefaultLookAngleDegrees;

        [SerializeField, Tooltip("出す物の見た目 (Pickup.VisualPrefab)。調整値を入れても、これを使う。空なら中身の種類で色を分けた仮の箱")]
        private GameObject _visualPrefab;

        private PickupRespawn _respawn;
        private Pickup _pickup;

        public event Action<PickupSpawnPoint, PickupCollection> Collected;

        public PickupDefinition Definition
        {
            get => _definition;
            set => _definition = value;
        }

        public PickupContents Contents
        {
            get => _contents;
            set
            {
                if (value == null)
                {
                    throw new ArgumentNullException(nameof(value));
                }

                value.ToSpec();
                _contents = value;
            }
        }

        public PickupCollectMode CollectMode
        {
            get => _collectMode;
            set => _collectMode = value;
        }

        public float RespawnSeconds
        {
            get => _respawnSeconds;
            set
            {
                if (!(value >= 0f) || float.IsInfinity(value))
                {
                    throw new ArgumentOutOfRangeException(nameof(value), value, "RespawnSeconds must be a non-negative finite number");
                }

                _respawnSeconds = value;
            }
        }

        public float Radius
        {
            get => _radius;
            set => _radius = Mathf.Max(Pickup.MinRadius, value);
        }

        public float LookAngleDegrees
        {
            get => _lookAngleDegrees;
            set => _lookAngleDegrees = float.IsNaN(value) ? 0f : Mathf.Clamp(value, 0f, Pickup.MaxLookAngleDegrees);
        }

        public GameObject VisualPrefab
        {
            get => _visualPrefab;
            set => _visualPrefab = value;
        }

        public Pickup Current => _pickup;

        public bool IsAvailable => _respawn != null && _respawn.IsAvailable;

        public float? RemainingSeconds => _respawn?.RemainingSeconds;

        private void Start()
        {
            if (!TrySpawn())
            {
                enabled = false;
            }
        }

        private void Update()
        {
            if (_respawn != null)
            {
                Tick(Time.deltaTime);
            }
        }

        private void OnDestroy()
        {
            if (_pickup != null)
            {
                _pickup.Collected -= OnPickupCollected;
            }
        }

        public bool TrySpawn()
        {
            if (_pickup != null)
            {
                return true;
            }

            var definition = _definition;
            if (definition == null && _contents == null)
            {
                Debug.LogError($"{LogTag} PickupSpawnPoint '{name}' に中身がありません。シーン生成スクリプトで Contents か Definition を配線してください。", this);
                return false;
            }

            PickupSpec spec;
            try
            {
                if (definition != null)
                {
                    definition.Validate();
                    spec = definition.ToSpec();
                    _respawn = definition.CreateRespawn();
                }
                else
                {
                    spec = _contents.ToSpec();
                    _respawn = new PickupRespawn(_respawnSeconds);
                }
            }
            catch (ArgumentException e)
            {
                Debug.LogError($"{LogTag} PickupSpawnPoint '{name}' の設定が不正です: {e.Message}", this);
                _respawn = null;
                return false;
            }

            var contents = definition != null ? definition.Contents : _contents;
            var collectMode = definition != null ? definition.CollectMode : _collectMode;
            var radius = definition != null ? definition.Radius : _radius;
            var lookAngle = definition != null ? definition.LookAngleDegrees : _lookAngleDegrees;
            _pickup = Pickup.Create(contents, transform.position, collectMode, transform, radius, _visualPrefab, lookAngle);
            _pickup.DestroyOnCollect = false;
            _pickup.Collected += OnPickupCollected;
            Debug.Log(PickupLog.Spawned(_pickup.name, spec, transform.position.ToNumerics()), this);
            return true;
        }

        public void Tick(float deltaSeconds)
        {
            if (_respawn == null)
            {
                throw new InvalidOperationException($"PickupSpawnPoint '{name}' has not spawned its pickup");
            }

            if (!_respawn.Tick(deltaSeconds) || _pickup == null)
            {
                return;
            }

            _pickup.Restock();
            _pickup.gameObject.SetActive(true);
            Debug.Log(PickupLog.Respawned(_pickup.name), this);
        }

        private void OnPickupCollected(PickupCollection collection)
        {
            _respawn.Collect();
            Collected?.Invoke(this, collection);
        }

        private void OnDrawGizmos()
        {
            var contents = _definition != null ? _definition.Contents : _contents;
            var radius = _definition != null ? _definition.Radius : _radius;
            Gizmos.color = contents != null ? Pickup.ColorOf(contents.Kind) : Color.white;
            Gizmos.DrawWireSphere(transform.position + (Vector3.up * radius), radius);
        }
    }
}
