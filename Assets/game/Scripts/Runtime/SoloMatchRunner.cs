using System;
using System.Collections.Generic;
using Fps.Core;
using UnityEngine;

namespace Fps.Runtime
{
    /// <summary>
    /// ソロの試合 (<see cref="Fps.Core.Match"/>) をシーンで進める。Play 開始時に <see cref="Fps.Core.Match.Start"/>、毎フレーム
    /// <see cref="Fps.Core.Match.Tick"/> を呼び、プレイヤーの死亡と標的 (複数可) の死亡を報告する。勝敗の判断はすべて Match が行う。
    /// 参照は <see cref="Initialize"/> でコードから渡す (<see cref="GameBootstrap"/> が呼ぶ)。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SoloMatchRunner : MonoBehaviour
    {
        public const string LogTag = "[Match]";

        /// <summary>制限時間の既定値 (3 分)。</summary>
        public const float DefaultTimeLimitSeconds = 180f;

        /// <summary>勝利に必要なキル数の既定値。どれか 1 体を倒せば勝ち。</summary>
        public const int DefaultKillsToWin = 1;

        /// <summary>Match に渡すプレイヤーの ID。</summary>
        public const int PlayerId = 0;

        /// <summary>最初の標的を倒したときに Match へ報告する ID。i 番目の標的は TargetId + i。</summary>
        public const int TargetId = 1;

        private Match _match;
        private Damageable _player;
        private readonly List<Damageable> _targets = new List<Damageable>();

        /// <summary>進めている試合。未初期化なら null。</summary>
        public Match Match => _match;

        public bool IsInitialized => _match != null;

        /// <summary>
        /// 試合を作り、プレイヤーと標的たちの死亡を購読する。試合はまだ始めない (Play 開始時の Start で始める)。
        /// 引数が不正なら例外を投げ、前の状態は変えない。
        /// </summary>
        /// <param name="player">プレイヤーの体力。死んだら敗北。</param>
        /// <param name="targets">標的。どれかを倒すたびにキル 1 つ (復活して再び倒してもまた数える)。null や空なら標的なし。null の要素は飛ばす。</param>
        /// <param name="timeLimitSeconds">制限時間 (秒)。正の有限値。</param>
        /// <param name="killsToWin">勝利に必要なキル数。1 以上。</param>
        public void Initialize(Damageable player, IReadOnlyList<Damageable> targets, float timeLimitSeconds, int killsToWin)
        {
            if (player == null)
            {
                throw new ArgumentNullException(nameof(player));
            }

            // 先に作って値の検査を通してから差し替える。
            var match = new Match(timeLimitSeconds, killsToWin, PlayerId);

            Unsubscribe();
            _match = match;
            _player = player;
            if (targets != null)
            {
                foreach (var target in targets)
                {
                    if (target != null && !_targets.Contains(target))
                    {
                        _targets.Add(target);
                    }
                }
            }

            _match.StateChanged += OnStateChanged;
            _player.Died += OnPlayerDied;
            foreach (var target in _targets)
            {
                target.Died += OnTargetDied;
            }
        }

        private void Start()
        {
            if (!IsInitialized)
            {
                Debug.LogError($"{LogTag} SoloMatchRunner '{name}' was not initialized; call Initialize before Start", this);
                enabled = false;
                return;
            }

            _match.Start();
        }

        private void Update()
        {
            _match.Tick(Time.deltaTime);
        }

        private void OnDestroy()
        {
            Unsubscribe();
        }

        private void Unsubscribe()
        {
            if (_match != null)
            {
                _match.StateChanged -= OnStateChanged;
            }

            if (_player != null)
            {
                _player.Died -= OnPlayerDied;
            }

            foreach (var target in _targets)
            {
                if (target != null)
                {
                    target.Died -= OnTargetDied;
                }
            }

            _targets.Clear();
        }

        private void OnStateChanged(GameModeState state)
        {
            if (state == GameModeState.Ended)
            {
                Debug.Log($"{LogTag} solo ended result={_match.Result} kills={_match.Kills}", this);
            }
            else
            {
                Debug.Log($"{LogTag} solo state={state} timeLimit={_match.TimeLimitSeconds}", this);
            }
        }

        private void OnPlayerDied(Damageable player)
        {
            _match.ReportDeath(PlayerId);
        }

        private void OnTargetDied(Damageable target)
        {
            _match.ReportKill(PlayerId, TargetId + _targets.IndexOf(target));
        }
    }
}
