using System;
using System.Collections.Generic;
using Fps.Core;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace Fps.Runtime
{
    /// <summary>
    /// ソロの試合 (<see cref="Fps.Core.Match"/>) をシーンで進める。Start で同じ GameObject (無ければシーン) の
    /// <see cref="GameBootstrap"/> が作った <see cref="BootstrapWorld"/> を拾い、試合を始めて Update で時間を進める。
    /// 標的ダミーの死亡はプレイヤーのキル、プレイヤーの死亡は敗北として Match に渡すだけで、勝敗の判断は Match が行う。
    /// 状態が変わるたびに <see cref="MatchLog.SoloState"/> の書式で Debug.Log に出し、終わったらプレイヤーの移動と射撃を止める。
    /// 終了後に <see cref="RestartActionPath"/> (キーボードの R) を押すと今のシーンを読み込み直して最初からやり直す。
    /// 勝敗をすぐ確かめたいときは <see cref="KillsToWin"/> と <see cref="TimeLimitSeconds"/> を小さくする。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MatchRunner : MonoBehaviour
    {
        public const string LogTag = MatchLog.Tag;

        /// <summary>制限時間 (秒)。</summary>
        public const float TimeLimitSeconds = 60f;

        /// <summary>勝利に必要なキル数。標的ダミーは復活するので、同じ標的を何度倒しても数える。</summary>
        public const int KillsToWin = 10;

        /// <summary>Match の中でのプレイヤーの ID。標的ダミーには左から 1, 2, ... を振る。</summary>
        public const int PlayerId = 0;

        /// <summary>
        /// 再開に使うアクション。Assets/InputSystem_Actions.inputactions の Reload (キーボードの R)。
        /// 終了後は <see cref="WeaponController"/> を止めるので、リロードと取り合うことはない。
        /// </summary>
        public const string RestartActionPath = WeaponController.ReloadActionPath;

        private readonly Dictionary<Damageable, int> _targetIds = new Dictionary<Damageable, int>();
        private Match _match;
        private BootstrapWorld _world;
        private Damageable _player;
        private InputAction _restartAction;
        private bool _enabledRestartAction;

        /// <summary>進めている試合。開始前や開始に失敗したときは null。</summary>
        public Match Match => _match;

        public bool IsStarted => _match != null;

        /// <summary>終了後に押すと再開するアクション。解決できなければ null (試合は進むが再開できない)。</summary>
        public InputAction RestartAction => _restartAction;

        private void Start()
        {
            if (!IsStarted && !TryBeginFromBootstrap())
            {
                enabled = false;
            }
        }

        private void OnEnable()
        {
            // 終了後に無効→有効にされたら、再開の入力をもう一度受け付ける。
            if (IsStarted && _match.IsOver)
            {
                AcquireRestartAction();
            }
        }

        private void OnDisable()
        {
            ReleaseRestartAction();
        }

        private void OnDestroy()
        {
            Stop();
        }

        private void Update()
        {
            if (!IsStarted)
            {
                return;
            }

            Tick(Time.deltaTime);

            if (_match.IsOver && _restartAction != null && _restartAction.WasPressedThisFrame())
            {
                Restart();
            }
        }

        /// <summary>
        /// <see cref="GameBootstrap"/> の生成物を拾い、<see cref="TimeLimitSeconds"/> と <see cref="KillsToWin"/> で試合を始める。
        /// 既に始まっていれば何もせず true。始められなければ理由を Debug.LogError に出して false を返す。
        /// </summary>
        public bool TryBeginFromBootstrap()
        {
            if (IsStarted)
            {
                return true;
            }

            var bootstrap = GetComponent<GameBootstrap>();
            if (bootstrap == null)
            {
                bootstrap = FindAnyObjectByType<GameBootstrap>();
            }

            if (bootstrap == null)
            {
                Debug.LogError($"{LogTag} シーンに GameBootstrap がありません。", this);
                return false;
            }

            var world = bootstrap.World;
            if (world == null)
            {
                Debug.LogError($"{LogTag} GameBootstrap '{bootstrap.name}' がプレイヤーと標的を生成していません。上のエラーを確認してください。", this);
                return false;
            }

            // 再開できなくても試合自体は進められるので、解決に失敗してもエラーを出して続ける。
            _restartAction = ResolveRestartAction();

            try
            {
                Begin(world, new Match(TimeLimitSeconds, KillsToWin, PlayerId));
            }
            catch (ArgumentException e)
            {
                Debug.LogError($"{LogTag} 試合を始められません: {e.Message}", this);
                return false;
            }

            return true;
        }

        /// <summary>
        /// world の標的ダミーとプレイヤーの死亡を match に繋ぎ、match を始める。
        /// match は <see cref="GameModeState.Waiting"/> で、プレイヤーの ID が <see cref="PlayerId"/> であること。
        /// 失敗したら例外を投げ、何も購読しない。
        /// </summary>
        public void Begin(BootstrapWorld world, Match match)
        {
            if (world == null)
            {
                throw new ArgumentNullException(nameof(world));
            }

            if (match == null)
            {
                throw new ArgumentNullException(nameof(match));
            }

            if (IsStarted)
            {
                throw new InvalidOperationException($"MatchRunner '{name}' has already started a match");
            }

            if (match.State != GameModeState.Waiting)
            {
                throw new ArgumentException($"match must be Waiting, but is {match.State}", nameof(match));
            }

            if (match.PlayerId != PlayerId)
            {
                throw new ArgumentException($"match.PlayerId must be {PlayerId}, but is {match.PlayerId}", nameof(match));
            }

            if (world.PlayerDamageable == null)
            {
                throw new ArgumentException("world has no player Damageable", nameof(world));
            }

            // 先に ID を全部決めてから購読する (途中の標的が壊れていても購読が半端に残らない)。
            var ids = new Dictionary<Damageable, int>();
            for (var i = 0; i < world.Targets.Count; i++)
            {
                var dummy = world.Targets[i];
                if (dummy == null || dummy.Damageable == null)
                {
                    throw new ArgumentException($"world target #{i} has no Damageable", nameof(world));
                }

                ids[dummy.Damageable] = PlayerId + i + 1;
            }

            _world = world;
            _player = world.PlayerDamageable;
            _match = match;
            foreach (var pair in ids)
            {
                _targetIds.Add(pair.Key, pair.Value);
                pair.Key.Died += OnTargetDied;
            }

            _player.Died += OnPlayerDied;
            _match.StateChanged += OnStateChanged;
            _match.Start();
        }

        /// <summary>試合の時間を deltaSeconds 進める。Play 中は Update から呼ぶ。Edit Mode のテストでは直接呼ぶ。</summary>
        public void Tick(float deltaSeconds)
        {
            if (!IsStarted)
            {
                throw new InvalidOperationException($"MatchRunner '{name}' has not started a match");
            }

            _match.Tick(deltaSeconds);
        }

        /// <summary>今のシーンを読み込み直して最初からやり直す。Play 中にのみ呼べる。</summary>
        [ContextMenu("Restart")]
        public void Restart()
        {
            if (!Application.isPlaying)
            {
                throw new InvalidOperationException("MatchRunner.Restart can only reload the scene in Play mode");
            }

            var scene = SceneManager.GetActiveScene();
            if (scene.buildIndex < 0)
            {
                Debug.LogError(
                    $"{LogTag} シーン '{scene.path}' が Build Settings にないため再読み込みできません。Fps.Editor.BootstrapSceneBuilder.Build でシーンを生成してください。",
                    this);
                return;
            }

            Debug.Log(MatchLog.Restart(), this);
            SceneManager.LoadScene(scene.buildIndex);
        }

        private void OnTargetDied(Damageable target)
        {
            if (_match != null && _targetIds.TryGetValue(target, out var targetId))
            {
                // ソロでは標的を倒せるのはプレイヤーだけなので、倒したのはプレイヤーとして報告する。
                _match.ReportKill(PlayerId, targetId);
            }
        }

        private void OnPlayerDied(Damageable _)
        {
            _match?.ReportDeath(PlayerId);
        }

        private void OnStateChanged(GameModeState state)
        {
            Debug.Log(MatchLog.SoloState(_match), this);

            if (state != GameModeState.Ended)
            {
                return;
            }

            StopPlayer();
            AcquireRestartAction();
        }

        private void StopPlayer()
        {
            // 武器を先に止める。止めると Reload アクションが無効に戻ることがあるので、再開の入力はその後で有効にする。
            if (_world.Weapon != null)
            {
                _world.Weapon.enabled = false;
            }

            if (_world.Mover != null)
            {
                _world.Mover.enabled = false;
            }
        }

        private InputAction ResolveRestartAction()
        {
            var actions = InputSystem.actions;
            if (actions == null)
            {
                Debug.LogError(
                    $"{LogTag} プロジェクト全体の Input Actions が設定されていないため、R で再開できません (Project Settings > Input System Package)。",
                    this);
                return null;
            }

            var action = actions.FindAction(RestartActionPath);
            if (action == null)
            {
                Debug.LogError($"{LogTag} Input Actions '{actions.name}' に '{RestartActionPath}' がないため、R で再開できません。", this);
            }

            return action;
        }

        /// <summary>プロジェクト全体のアクションは他の部品と共有なので、自分が有効にしたときだけ後で無効に戻す。</summary>
        private void AcquireRestartAction()
        {
            // Edit Mode では OnDisable が呼ばれず無効に戻せないので、Play 中だけ有効にする。
            if (_restartAction == null || !Application.isPlaying || !isActiveAndEnabled || _restartAction.enabled)
            {
                return;
            }

            _restartAction.Enable();
            _enabledRestartAction = true;
        }

        private void ReleaseRestartAction()
        {
            if (_enabledRestartAction && _restartAction != null)
            {
                _restartAction.Disable();
            }

            _enabledRestartAction = false;
        }

        /// <summary>購読を解除して試合から離れる。破棄時にも呼ばれる。</summary>
        public void Stop()
        {
            ReleaseRestartAction();

            foreach (var target in _targetIds.Keys)
            {
                if (target != null)
                {
                    target.Died -= OnTargetDied;
                }
            }

            _targetIds.Clear();

            if (_player != null)
            {
                _player.Died -= OnPlayerDied;
            }

            if (_match != null)
            {
                _match.StateChanged -= OnStateChanged;
            }

            _player = null;
            _world = null;
            _match = null;
        }
    }
}
