using System.Collections.Generic;
using UnityEngine;

namespace Shooter.Runtime
{
    [DisallowMultipleComponent]
    public sealed class KillTargetObjective : MonoBehaviour
    {
        [SerializeField, Tooltip("進める MissionRunner。空ならシーンから探す")]
        private MissionRunner _runner;

        [SerializeField, Tooltip("進める目標の Id")]
        private string _objectiveId = string.Empty;

        [SerializeField, Tooltip("倒す標的。空ならこの GameObject と子孫の Damageable すべて")]
        private Damageable[] _targets = System.Array.Empty<Damageable>();

        [SerializeField, Tooltip("倒した標的の GameObject を非アクティブにする")]
        private bool _deactivateKilledTargets = true;

        private readonly List<Damageable> _bound = new List<Damageable>();
        private MissionRunner _boundRunner;

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

        public bool DeactivateKilledTargets
        {
            get => _deactivateKilledTargets;
            set => _deactivateKilledTargets = value;
        }

        public bool IsBound => _boundRunner != null;

        public IReadOnlyList<Damageable> BoundTargets => _bound;

        public void SetTargets(params Damageable[] targets)
        {
            _targets = targets == null ? System.Array.Empty<Damageable>() : (Damageable[])targets.Clone();
        }

        private void Start()
        {
            if (!Bind(_runner))
            {
                enabled = false;
            }
        }

        private void OnDestroy()
        {
            Unbind();
        }

        public bool Bind(MissionRunner runner)
        {
            Unbind();

            var resolved = MissionRunner.Resolve(runner, _objectiveId, this);
            if (resolved == null)
            {
                return false;
            }

            var targets = CollectTargets();
            if (targets.Count == 0)
            {
                Debug.LogError(
                    $"{MissionRunner.LogTag} KillTargetObjective '{name}' に標的 (Damageable) がありません。", this);
                return false;
            }

            _boundRunner = resolved;
            foreach (var target in targets)
            {
                target.Died += OnTargetDied;
                _bound.Add(target);
            }

            return true;
        }

        private List<Damageable> CollectTargets()
        {
            var source = _targets != null && _targets.Length > 0
                ? (IEnumerable<Damageable>)_targets
                : GetComponentsInChildren<Damageable>(true);

            var unique = new List<Damageable>();
            foreach (var target in source)
            {
                if (target != null && !unique.Contains(target))
                {
                    unique.Add(target);
                }
            }

            return unique;
        }

        private void Unbind()
        {
            foreach (var target in _bound)
            {
                if (target != null)
                {
                    target.Died -= OnTargetDied;
                }
            }

            _bound.Clear();
            _boundRunner = null;
        }

        private void OnTargetDied(Damageable target)
        {
            if (_boundRunner == null || !_boundRunner.IsStarted)
            {
                return;
            }

            _boundRunner.Mission.Progress(_objectiveId);

            if (_deactivateKilledTargets && target != null)
            {
                target.gameObject.SetActive(false);
            }
        }
    }
}
