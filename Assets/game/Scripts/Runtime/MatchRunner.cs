using System;
using System.Collections.Generic;
using Shooter.Core;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace Shooter.Runtime
{
    // 標的ダミーは自分で復活し、Match はキルの数を数えるので、定義のリスポーンとキル 1 回の点は使わない。
    [DisallowMultipleComponent]
    public sealed class MatchRunner : MonoBehaviour
    {
        public const string LogTag = MatchLog.Tag;

        public const float DefaultTimeLimitSeconds = 0f;

        public const int DefaultKillsToWin = 10;

        public const int PlayerId = 0;

        // 再開に Reload を使っても、終了後は WeaponController を止めるので、リロードと取り合うことはない。
        public const string RestartActionPath = InputActionPaths.Reload;

        [SerializeField]
        [Tooltip("試合の制限時間と勝利に必要なキル数を読むフリーフォーオールの定義。配線すると下の 2 つの代わりにこれを読む。空なら入口 (GameContent) のものを使う")]
        private FreeForAllDefinition _definition;

        [SerializeField]
        [Min(0f)]
        [Tooltip("制限時間 (秒)。0 なら時間切れにならず、HUD に残り時間も出さない。")]
        private float _timeLimitSeconds = DefaultTimeLimitSeconds;

        [SerializeField]
        [Min(1)]
        [Tooltip("勝利に必要なキル数。標的ダミーは復活するので、同じ標的を何度倒しても数える。")]
        private int _killsToWin = DefaultKillsToWin;

        private readonly Dictionary<Damageable, int> _targetIds = new Dictionary<Damageable, int>();

        // スコアボードに出す、試合中に倒された回数 (Id ごと)。Match はプレイヤーのキル数しか数えないのでここで数える
        private readonly Dictionary<int, int> _deaths = new Dictionary<int, int>();
        private Match _match;
        // 止めるときに、後から別の側が出した一覧まで消さないよう、出した一覧を覚えておく
        private ScoreboardEntry[] _publishedScoreboard;
        private BootstrapWorld _world;
        private Damageable _player;
        // シーンに置いたまま再コンパイルすると、Unity が private の値も保存し直し、null の InputAction を空のアクションとして戻してしまう (再生でシーンを読み直さないので、そのまま使われる)。
        [NonSerialized]
        private InputAction _restartAction;
        private bool _enabledRestartAction;

        public FreeForAllDefinition Definition
        {
            get => _definition;
            set => _definition = value;
        }

        public FreeForAllDefinition EffectiveDefinition => GameContent.Pick(_definition, c => c.FreeForAll);

        public float TimeLimitSeconds
        {
            get => _timeLimitSeconds;
            set
            {
                if (!(value >= 0f))
                {
                    throw new ArgumentOutOfRangeException(nameof(value), value, "TimeLimitSeconds must not be negative");
                }

                _timeLimitSeconds = value;
            }
        }

        public int KillsToWin
        {
            get => _killsToWin;
            set
            {
                if (value <= 0)
                {
                    throw new ArgumentOutOfRangeException(nameof(value), value, "KillsToWin must be positive");
                }

                _killsToWin = value;
            }
        }

        public static float MatchTimeLimit(float timeLimitSeconds)
        {
            return timeLimitSeconds > 0f ? timeLimitSeconds : float.PositiveInfinity;
        }

        public Match CreateMatch()
        {
            var definition = EffectiveDefinition;
            if (definition == null)
            {
                return new Match(MatchTimeLimit(_timeLimitSeconds), _killsToWin, PlayerId);
            }

            try
            {
                return new Match(definition.TimeLimitSeconds, definition.ScoreLimit, PlayerId);
            }
            catch (ArgumentOutOfRangeException e)
            {
                throw new ArgumentException($"free for all definition '{definition.name}' is invalid: {e.Message}", e);
            }
        }

        public Match Match => _match;

        public bool IsStarted => _match != null;

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
                Begin(world, CreateMatch());
            }
            catch (ArgumentException e)
            {
                Debug.LogError($"{LogTag} 試合を始められません: {e.Message}", this);
                return false;
            }

            return true;
        }

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
            _deaths.Clear();
            foreach (var pair in ids)
            {
                _targetIds.Add(pair.Key, pair.Value);
                pair.Key.Died += OnTargetDied;
            }

            _player.Died += OnPlayerDied;
            _match.StateChanged += OnStateChanged;

            world.HudModel.BindMatch(_match);

            _match.Start();
            ShowScoreboard();
        }

        public int PlayerDeaths => DeathsOf(PlayerId);

        public int TargetDeaths(int targetId) => DeathsOf(targetId);

        private int DeathsOf(int id)
        {
            return _deaths.TryGetValue(id, out var count) ? count : 0;
        }

        private void CountDeath(int id)
        {
            _deaths[id] = DeathsOf(id) + 1;
        }

        public IReadOnlyList<ScoreboardEntry> PublishedScoreboard => _publishedScoreboard;

        // 標的は撃ち返さないので、標的のキル数はいつも 0 にする。
        private void ShowScoreboard()
        {
            if (_match == null)
            {
                return;
            }

            // 止めるときに自分の出した一覧かを参照で見分けるので、出すたびに新しい配列にする
            // ソロでは標的を撃つのがプレイヤーだけで、標的は撃ち返さないので、誰にもアシストは付かない
            var entries = new ScoreboardEntry[_targetIds.Count + 1];
            entries[0] = new ScoreboardEntry(
                PlayerId, ScoreboardName.LocalPlayer, Team.Alpha, _match.Kills, PlayerDeaths, 0, ScoreboardConnection.None, true);
            var i = 1;
            foreach (var targetId in _targetIds.Values)
            {
                entries[i++] = new ScoreboardEntry(
                    targetId, ScoreboardName.Target(targetId - PlayerId), Team.Bravo, 0, DeathsOf(targetId), 0,
                    ScoreboardConnection.None, false);
            }

            _publishedScoreboard = entries;
            GameSignals.Scoreboard.Publish(entries);
        }

        public void Tick(float deltaSeconds)
        {
            if (!IsStarted)
            {
                throw new InvalidOperationException($"MatchRunner '{name}' has not started a match");
            }

            _match.Tick(deltaSeconds);
        }

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
                    $"{LogTag} シーン '{scene.path}' が Build Settings にないため再読み込みできません。Shooter.Editor.BootstrapSceneBuilder.Build でシーンを生成してください。",
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
                // 終わった後に倒れても Match はキルを数えないので、デスもそれに合わせて数えない。
                if (_match.State == GameModeState.Playing)
                {
                    CountDeath(targetId);
                }

                // ソロでは標的を倒せるのはプレイヤーだけなので、倒したのはプレイヤーとして報告する。
                _match.ReportKill(PlayerId, targetId);
                ShowScoreboard();
            }
        }

        private void OnPlayerDied(Damageable _)
        {
            if (_match == null)
            {
                return;
            }

            if (_match.State == GameModeState.Playing)
            {
                CountDeath(PlayerId);
            }

            _match.ReportDeath(PlayerId);
            ShowScoreboard();
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
            ShowRestartKey();

            // ソロの得点はキル数そのもの (勝ちの条件が KillsToWin) なので、自陣の得点にもキル数を渡す。
            if (_match != null && _match.Result.HasValue)
            {
                CombatEvents.RaiseMatchFinished(new MatchResult(_match.Result.Value, _match.Kills, PlayerDeaths, _match.Kills));
            }
        }

        private void ShowRestartKey()
        {
            var model = _world != null ? _world.HudModel : null;
            if (model == null || _restartAction == null)
            {
                return;
            }

            var actions = InputSystem.actions;
            var device = InputKeyLabels.DetectDevice(
                new[] { actions?.FindAction(InputActionPaths.Move), actions?.FindAction(InputActionPaths.Look), _restartAction },
                InputDeviceKind.KeyboardMouse);
            model.SetRestartKey(InputKeyLabels.For(_restartAction, device));
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

            if (_world.Grenades != null)
            {
                _world.Grenades.enabled = false;
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

        // プロジェクト全体のアクションは他の部品と共有なので、自分が有効にしたときだけ後で無効に戻す。
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

            if (_publishedScoreboard != null)
            {
                GameSignals.Scoreboard.ClearIfCurrent(_publishedScoreboard);
            }

            _publishedScoreboard = null;
            _player = null;
            _world = null;
            _match = null;
        }
    }
}
