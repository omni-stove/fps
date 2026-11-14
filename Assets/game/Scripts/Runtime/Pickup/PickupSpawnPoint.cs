using System;
using Fps.Core;
using UnityEngine;

namespace Fps.Runtime
{
    /// <summary>
    /// Scene に置いた点に拾える物を出す。Start で自分の子に <see cref="Pickup"/> を作り (位置は Transform の位置が足元)、
    /// 拾われたら <see cref="RespawnSeconds"/> 後に同じ物をまた出す (<see cref="PickupRespawn"/>)。0 秒なら一度きり。
    /// 中身はシーン生成スクリプトが <see cref="Contents"/> に配線する。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PickupSpawnPoint : MonoBehaviour
    {
        public const string LogTag = PickupLog.Tag;

        /// <summary>再出現までの既定の秒数。</summary>
        public const float DefaultRespawnSeconds = 20f;

        [SerializeField, Tooltip("出す物の中身")]
        private PickupContents _contents = PickupContents.ForHealth(PickupContents.DefaultHealthAmount);

        [SerializeField, Tooltip("拾い方")]
        private PickupCollectMode _collectMode = PickupCollectMode.Touch;

        [SerializeField, Min(0f), Tooltip("拾われてから再び出るまでの秒数。0 なら一度きり")]
        private float _respawnSeconds = DefaultRespawnSeconds;

        [SerializeField, Min(Pickup.MinRadius), Tooltip("拾える範囲の半径 (m)")]
        private float _radius = Pickup.DefaultRadius;

        private PickupRespawn _respawn;
        private Pickup _pickup;

        /// <summary>出した物が拾われたときに、この点と拾われたときの情報を渡して呼ばれる。</summary>
        public event Action<PickupSpawnPoint, PickupCollection> Collected;

        /// <summary>出す物の中身。出した後に変えても、出ている物は変わらない。null は <see cref="ArgumentNullException"/>、不正なら <see cref="ArgumentException"/>。</summary>
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

        /// <summary>拾われてから再び出るまでの秒数。0 なら一度きり。出した後に変えても次の <see cref="TrySpawn"/> まで効かない。</summary>
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

        /// <summary>出している物。出す前や出せなかったときは null。拾われて次を待つ間も同じ物 (無効になっている)。</summary>
        public Pickup Current => _pickup;

        /// <summary>出していて拾えるなら true。</summary>
        public bool IsAvailable => _respawn != null && _respawn.IsAvailable;

        /// <summary>再び出るまでの残り秒数。出ているとき・出す前・もう出ないときは null。</summary>
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

        /// <summary>
        /// 子に拾える物を作って出し、<c>[Pickup] spawned</c> を出す。既に出していれば何もせず true。
        /// 中身が不正なら理由を Debug.LogError に出して false。
        /// </summary>
        public bool TrySpawn()
        {
            if (_pickup != null)
            {
                return true;
            }

            if (_contents == null)
            {
                Debug.LogError($"{LogTag} PickupSpawnPoint '{name}' に中身がありません。シーン生成スクリプトで Contents を配線してください。", this);
                return false;
            }

            PickupSpec spec;
            try
            {
                spec = _contents.ToSpec();
                _respawn = new PickupRespawn(_respawnSeconds);
            }
            catch (ArgumentException e)
            {
                Debug.LogError($"{LogTag} PickupSpawnPoint '{name}' の設定が不正です: {e.Message}", this);
                _respawn = null;
                return false;
            }

            _pickup = Pickup.Create(_contents, transform.position, _collectMode, transform, _radius);
            _pickup.DestroyOnCollect = false;
            _pickup.Collected += OnPickupCollected;
            Debug.Log(PickupLog.Spawned(_pickup.name, spec, transform.position.ToNumerics()), this);
            return true;
        }

        /// <summary>時間を deltaSeconds 進め、再び出す時間になったら出して <c>[Pickup] respawned</c> を出す。Play 中は Update から呼ぶ。</summary>
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
            Gizmos.color = _contents != null ? Pickup.ColorOf(_contents.Kind) : Color.white;
            Gizmos.DrawWireSphere(transform.position + (Vector3.up * _radius), _radius);
        }
    }
}
