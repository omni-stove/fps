using System.Collections.Generic;
using Shooter.Core;
using UnityEngine;

namespace Shooter.Runtime
{
    public abstract class ObjectiveAreaBase : MonoBehaviour, IMinimapObjective
    {
        public const string DefaultPlayerTag = GameBootstrap.PlayerTag;

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

        public ObjectiveZone Zone => _zone;

        public bool IsBound => _zone != null;

        public Vector3 Position => transform.position;

        // 達成した・まだ始まっていない目標は、今向かう先ではないので印を出さない。
        public bool ShowsOnMinimap => isActiveAndEnabled && _zone != null && _zone.IsActive;

        protected abstract ObjectiveZoneMode Mode { get; }

        protected virtual void Start()
        {
            if (!Bind(_runner))
            {
                enabled = false;
            }
        }

        protected virtual void OnEnable()
        {
            MinimapObjectives.Add(this);
        }

        protected virtual void OnDisable()
        {
            MinimapObjectives.Remove(this);
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

        public void HandleEnter(Collider other)
        {
            if (_zone == null || !IsPlayer(other) || !_inside.Add(other))
            {
                return;
            }

            _zone.Enter();
        }

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
