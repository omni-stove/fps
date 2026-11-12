using System.Collections.Generic;
using Fps.Core;
using UnityEngine;

namespace Fps.Runtime
{
    /// <summary>
    /// トリガーの Collider に結び付いた目標の共通部分。プレイヤー (<see cref="PlayerTag"/> の付いた Collider) の出入りを
    /// <see cref="ObjectiveZone"/> に伝えるだけで、進めるかどうかの判断はそちらに任せる。
    /// MissionRunner は Inspector で指定しなければシーンから探す。
    /// </summary>
    public abstract class ObjectiveAreaBase : MonoBehaviour
    {
        /// <summary>GameBootstrap がプレイヤーに付ける Unity 組み込みのタグ。</summary>
        public const string DefaultPlayerTag = "Player";

        [SerializeField, Tooltip("進める MissionRunner。空ならシーンから探す")]
        private MissionRunner _runner;

        [SerializeField, Tooltip("進める目標の Id")]
        private string _objectiveId = string.Empty;

        [SerializeField, Tooltip("プレイヤーとみなす Collider のタグ")]
        private string _playerTag = DefaultPlayerTag;

        private readonly HashSet<Collider> _inside = new HashSet<Collider>();
        private ObjectiveZone _zone;

        public string ObjectiveId
        {
            get => _objectiveId;
            set => _objectiveId = value;
        }

        public MissionRunner Runner
        {
            get => _runner;
            set => _runner = value;
        }

        public string PlayerTag
        {
            get => _playerTag;
            set => _playerTag = value;
        }

        /// <summary>判断を任せている zone。Bind 前や失敗時は null。</summary>
        public ObjectiveZone Zone => _zone;

        public bool IsBound => _zone != null;

        /// <summary>この範囲が目標をどう進めるか。</summary>
        protected abstract ObjectiveZoneMode Mode { get; }

        protected virtual void Start()
        {
            if (!Bind(_runner))
            {
                enabled = false;
            }
        }

        protected virtual void OnDestroy()
        {
            Unbind();
        }

        protected void OnTriggerEnter(Collider other)
        {
            HandleEnter(other);
        }

        protected void OnTriggerExit(Collider other)
        {
            HandleExit(other);
        }

        /// <summary>
        /// runner (null ならシーンから探す) の目標に結び付ける。トリガーの Collider が無い・解決できないときは
        /// 理由を Debug.LogError に出して false を返す。
        /// </summary>
        public bool Bind(MissionRunner runner)
        {
            Unbind();

            if (!HasTriggerCollider())
            {
                Debug.LogError(
                    $"{MissionRunner.LogTag} {GetType().Name} '{name}' に isTrigger の Collider がありません。", this);
                return false;
            }

            var resolved = MissionRunner.Resolve(runner, _objectiveId, this);
            if (resolved == null)
            {
                return false;
            }

            _zone = new ObjectiveZone(resolved.Mission, _objectiveId, Mode);
            return true;
        }

        /// <summary>Collider が範囲に入った。プレイヤーの Collider なら zone に伝える。</summary>
        public void HandleEnter(Collider other)
        {
            if (_zone == null || !IsPlayer(other) || !_inside.Add(other))
            {
                return;
            }

            _zone.Enter();
        }

        /// <summary>Collider が範囲から出た。入っていたプレイヤーの Collider なら zone に伝える。</summary>
        public void HandleExit(Collider other)
        {
            if (_zone == null || other == null || !_inside.Remove(other))
            {
                return;
            }

            _zone.Exit();
        }

        protected void Unbind()
        {
            _zone?.Dispose();
            _zone = null;
            _inside.Clear();
        }

        private bool IsPlayer(Collider other)
        {
            return other != null && !string.IsNullOrEmpty(_playerTag) && other.CompareTag(_playerTag);
        }

        private bool HasTriggerCollider()
        {
            foreach (var collider in GetComponents<Collider>())
            {
                if (collider.isTrigger)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
