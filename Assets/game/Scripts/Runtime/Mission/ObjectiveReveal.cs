using System;
using System.Collections.Generic;
using Fps.Core;
using UnityEngine;

namespace Fps.Runtime
{
    /// <summary>
    /// 目標が Active になったら、それまで非アクティブにしておいた <see cref="Targets"/> (増援の敵など) をアクティブにする。
    /// 目標が Active になる前に終わっていた (Completed / Failed) なら出さずに止まる。出すのは 1 度だけ。
    /// MissionRunner は Inspector で指定しなければシーンから探す。
    /// </summary>
    /// <remarks>
    /// <see cref="Mission.Restore"/> (チェックポイントからの再開) はイベントを呼ばないので、<see cref="Mission.ObjectiveChanged"/> を
    /// 購読せず、出すか止まるまで毎フレーム目標の状態を読む。
    /// </remarks>
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

        /// <summary>出し終えたら true。</summary>
        public bool IsRevealed => _revealed;

        /// <summary>出す GameObject を差し替える。</summary>
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

        /// <summary>
        /// runner (null ならシーンから探す) の目標に結び付ける。解決できない・出す物が 1 つも無いときは
        /// 理由を Debug.LogError に出して false を返す。
        /// </summary>
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

        /// <summary>
        /// 目標の状態を読み、Active なら出す。Active になる前に終わっていたら出さずに止まる。出し終えていたら true を返す。
        /// </summary>
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
