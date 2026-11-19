using System;
using System.Collections.Generic;
using Shooter.Core;
using UnityEngine;

namespace Shooter.Runtime
{
    // Mission.Restore (チェックポイントからの再開) はイベントを呼ばないので、Mission.ObjectiveChanged を
    // 購読せず、出すか止まるまで毎フレーム目標の状態を読む。
    [DisallowMultipleComponent]
    public sealed class ObjectiveReveal : MonoBehaviour
    {
        [SerializeField, Tooltip("見る MissionRunner。空ならシーンから探す")]
        private MissionRunner _runner;

        [SerializeField, Tooltip("Active になったら出す目標の Id")]
        private string _objectiveId = string.Empty;

        [SerializeField, Tooltip("出すまで非アクティブにしておく GameObject")]
        private GameObject[] _targets = Array.Empty<GameObject>();

        private MissionRunner _boundRunner;
        private bool _revealed;

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

        public IReadOnlyList<GameObject> Targets => _targets ?? Array.Empty<GameObject>();

        public bool IsBound => _boundRunner != null;

        public bool IsRevealed => _revealed;

        public void SetTargets(params GameObject[] targets)
        {
            _targets = targets == null ? Array.Empty<GameObject>() : (GameObject[])targets.Clone();
        }

        private void Start()
        {
            if (!Bind(_runner))
            {
                enabled = false;
                return;
            }

            Refresh();
        }

        private void Update()
        {
            Refresh();
        }

        public bool Bind(MissionRunner runner)
        {
            _boundRunner = null;
            var resolved = MissionRunner.Resolve(runner, _objectiveId, this);
            if (resolved == null)
            {
                return false;
            }

            if (CountTargets() == 0)
            {
                Debug.LogError($"{MissionRunner.LogTag} ObjectiveReveal '{name}' に出す GameObject がありません。", this);
                return false;
            }

            _boundRunner = resolved;
            return true;
        }

        public bool Refresh()
        {
            if (_revealed || _boundRunner == null || !_boundRunner.IsStarted)
            {
                return _revealed;
            }

            var state = _boundRunner.Mission.GetState(_objectiveId);
            if (state == ObjectiveState.Active)
            {
                Reveal();
            }
            else if (state == ObjectiveState.Completed || state == ObjectiveState.Failed)
            {
                Debug.Log($"{MissionRunner.LogTag} reveal skipped objective={_objectiveId} state={state}", this);
                enabled = false;
            }

            return _revealed;
        }

        private void Reveal()
        {
            _revealed = true;
            var count = 0;
            foreach (var target in Targets)
            {
                if (target != null)
                {
                    target.SetActive(true);
                    count++;
                }
            }

            Debug.Log($"{MissionRunner.LogTag} revealed objective={_objectiveId} targets={count}", this);
            enabled = false;
        }

        private int CountTargets()
        {
            var count = 0;
            foreach (var target in Targets)
            {
                if (target != null)
                {
                    count++;
                }
            }

            return count;
        }
    }
}
