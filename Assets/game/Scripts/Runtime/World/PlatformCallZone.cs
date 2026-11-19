using System;
using UnityEngine;

namespace Shooter.Runtime
{
    // ボタンを押させず踏んだら呼ぶのは、乗り場に立つだけで来ることが見て分かり、入力の割り当てを増やさずに済むため。
    [DisallowMultipleComponent]
    public sealed class PlatformCallZone : MonoBehaviour
    {
        public static readonly Vector3 DefaultSize = new Vector3(2f, 2f, 2f);

        [SerializeField, Tooltip("呼ぶ床 (OnCall のエレベーター)")]
        private MovingPlatform _platform;

        [SerializeField, Min(0), Tooltip("呼ぶ点の番号 (MovingPlatform の点の並びの何番目か)")]
        private int _stop;

        [SerializeField, Tooltip("プレイヤーとみなす Collider のタグ")]
        private string _playerTag = GameBootstrap.PlayerTag;

        [SerializeField, Tooltip("Collider が無いときに作る踏む範囲の大きさ (m)")]
        private Vector3 _size = DefaultSize;

        public MovingPlatform Platform
        {
            get => _platform;
            set => _platform = value;
        }

        public int Stop
        {
            get => _stop;
            set
            {
                if (value < 0)
                {
                    throw new ArgumentOutOfRangeException(nameof(value), value, "stop must not be negative");
                }

                _stop = value;
            }
        }

        public string PlayerTag
        {
            get => _playerTag;
            set => _playerTag = value;
        }

        public Vector3 Size
        {
            get => _size;
            set => _size = value;
        }

        private void Awake()
        {
            EnsureTrigger();
        }

        // 置いた Collider があればそれを使い、無いときだけ箱を作る。どちらも踏んで通れるよう isTrigger にする。
        public Collider EnsureTrigger()
        {
            var collider = GetComponent<Collider>();
            if (collider == null)
            {
                var box = gameObject.AddComponent<BoxCollider>();
                box.size = _size;
                collider = box;
            }

            collider.isTrigger = true;
            return collider;
        }

        private void OnTriggerEnter(Collider other)
        {
            // 無効なコンポーネントにもトリガーの通知は届くので、ここで止める。
            if (enabled)
            {
                HandleEnter(other);
            }
        }

        public bool HandleEnter(Collider other)
        {
            return IsPlayer(other) && Request();
        }

        public bool Request()
        {
            if (_platform == null)
            {
                Debug.LogWarning(MovingPlatformLog.CallZoneWithoutPlatform(name), this);
                return false;
            }

            if (!_platform.IsInitialized && !_platform.TryInitialize())
            {
                return false;
            }

            var count = _platform.Route.Count;
            if (_stop >= count)
            {
                Debug.LogError(MovingPlatformLog.CallZoneStopOutOfRange(name, _platform.name, _stop, count), this);
                return false;
            }

            return _platform.Call(_stop);
        }

        private bool IsPlayer(Collider other)
        {
            return other != null && !string.IsNullOrEmpty(_playerTag) && other.CompareTag(_playerTag);
        }
    }
}
