using System;
using System.Collections.Generic;
using Shooter.Core;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace Shooter.Runtime
{
    /// <summary>
    /// ソロの試合 (<see cref="Shooter.Core.Match"/>) をシーンで進める。Start で同じ GameObject (無ければシーン) の
    /// <see cref="GameBootstrap"/> が作った <see cref="BootstrapWorld"/> を拾い、試合を始めて Update で時間を進める。
    /// 標的ダミーの死亡はプレイヤーのキル、プレイヤーの死亡は敗北として Match に渡すだけで、勝敗の判断は Match が行う。
    /// 状態が変わるたびに <see cref="MatchLog.SoloState"/> の書式で Debug.Log に出し、終わったらプレイヤーの移動と射撃を止め、
    /// HUD に勝敗と、やり直すキーを出す。
    /// 終了後に <see cref="RestartActionPath"/> (キーボードの R、ゲームパッドの X) を押すと今のシーンを読み込み直して最初からやり直す。
    /// 制限時間と勝利に必要なキル数は、<see cref="Definition"/>、入口 (<see cref="GameContent.FreeForAll"/>) のフリーフォーオールの定義
    /// (<see cref="FreeForAllDefinition.TimeLimitSeconds"/>・<see cref="FreeForAllDefinition.ScoreLimit"/>)、Inspector の値の順で読む。
    /// 標的ダミーは自分で復活し、Match はキルの数を数えるので、定義のリスポーンとキル 1 回の点は使わない。
    /// Inspector の制限時間は既定では無く (<see cref="TimeLimitSeconds"/> が 0)、HUD に残り時間も出さない。
    /// 勝敗をすぐ確かめたいときは <see cref="KillsToWin"/> を小さくする。
    /// 開始時とキル・デスのたびに自分と標的のキル数・デス数をスコアボードの行にして <see cref="GameSignals.Scoreboard"/> に出し、止めたら消す。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MatchRunner : MonoBehaviour
    {
        public const string LogTag = MatchLog.Tag;

        /// <summary><see cref="TimeLimitSeconds"/> の既定。0 は制限時間なし。</summary>
        public const float DefaultTimeLimitSeconds = 0f;

        /// <summary><see cref="KillsToWin"/> の既定。</summary>
        public const int DefaultKillsToWin = 10;

        /// <summary>Match の中でのプレイヤーの ID。標的ダミーには左から 1, 2, ... を振る。</summary>
        public const int PlayerId = 0;

        /// <summary>
        /// 再開に使うアクション。Assets/InputSystem_Actions.inputactions の Reload (キーボードの R、ゲームパッドの X)。
        /// 終了後は <see cref="WeaponController"/> を止めるので、リロードと取り合うことはない。
        /// </summary>
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

        /// <summary>制限時間と勝利に必要なキル数を読む定義。null なら入口 (<see cref="GameContent"/>) のもの、それも無ければ Inspector の値を使う。</summary>
        public FreeForAllDefinition Definition
        {
            get => _definition;
            set => _definition = value;
        }

        /// <summary>配線した <see cref="Definition"/>、空なら入口 (<see cref="GameContent"/>) のもの。どちらも無ければ null。</summary>
        public FreeForAllDefinition EffectiveDefinition => GameContent.Pick(_definition, c => c.FreeForAll);

        /// <summary>Inspector の制限時間 (秒)。0 なら制限時間なし。次に始める試合から使う。<see cref="EffectiveDefinition"/> があるときは使わない。</summary>
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

        /// <summary>Inspector の勝利に必要なキル数。1 以上。次に始める試合から使う。<see cref="EffectiveDefinition"/> があるときは使わない。</summary>
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

        /// <summary><see cref="TimeLimitSeconds"/> を <see cref="Shooter.Core.Match"/> に渡す値にする。0 以下は制限時間なし。</summary>
        public static float MatchTimeLimit(float timeLimitSeconds)
        {
            return timeLimitSeconds > 0f ? timeLimitSeconds : float.PositiveInfinity;
        }

        /// <summary>
        /// <see cref="EffectiveDefinition"/> があればその制限時間と点の上限で、無ければ Inspector の値で、まだ始めていない試合を作る。
        /// 定義の値が不正ならアセット名を含む <see cref="ArgumentException"/>。
        /// </summary>
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
        /// <see cref="GameBootstrap"/> の生成物を拾い、<see cref="CreateMatch"/> の試合を始める。
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
                Begin(world, CreateMatch());
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
            _deaths.Clear();
            foreach (var pair in ids)
            {
                _targetIds.Add(pair.Key, pair.Value);
                pair.Key.Died += OnTargetDied;
            }

            _player.Died += OnPlayerDied;
            _match.StateChanged += OnStateChanged;

            // HUD の値に残り時間と勝敗を写す。
            world.HudModel.BindMatch(_match);

            _match.Start();
            ShowScoreboard();
        }

        /// <summary>プレイヤーの、この試合で倒された回数。開始前は 0。</summary>
        public int PlayerDeaths => DeathsOf(PlayerId);

        /// <summary>targetId の標的が、この試合で倒された回数。知らない Id は 0。</summary>
        public int TargetDeaths(int targetId) => DeathsOf(targetId);

        private int DeathsOf(int id)
        {
            return _deaths.TryGetValue(id, out var count) ? count : 0;
        }

        private void CountDeath(int id)
        {
            _deaths[id] = DeathsOf(id) + 1;
        }

        /// <summary>最後に <see cref="GameSignals.Scoreboard"/> に出した一覧。出す前と止めた後は null。</summary>
        public IReadOnlyList<ScoreboardEntry> PublishedScoreboard => _publishedScoreboard;

        /// <summary>
        /// 自分 (Alpha) と標的 (Bravo) のキル数・デス数を <see cref="GameSignals.Scoreboard"/> に出す。標的は撃ち返さないのでキル数はいつも 0。
        /// </summary>
        private void ShowScoreboard()
        {
            if (_match == null)
            {
                return;
            }

            // 止めるときに自分の出した一覧かを参照で見分けるので、出すたびに新しい配列にする
            var entries = new ScoreboardEntry[_targetIds.Count + 1];
            entries[0] = new ScoreboardEntry(
                PlayerId, ScoreboardName.LocalPlayer, Team.Alpha, _match.Kills, PlayerDeaths, ScoreboardConnection.None, true);
            var i = 1;
            foreach (var targetId in _targetIds.Values)
            {
                entries[i++] = new ScoreboardEntry(
                    targetId, ScoreboardName.Target(targetId - PlayerId), Team.Bravo, 0, DeathsOf(targetId),
                    ScoreboardConnection.None, false);
            }

            _publishedScoreboard = entries;
            GameSignals.Scoreboard.Publish(entries);
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
        }

        /// <summary>HUD の値の結果の下に、やり直すキーを今の入力機器の表記で出す。再開のアクションが無ければ何もしない。</summary>
        private void ShowRestartKey()
        {
            var model = _world != null ? _world.HudModel : null;
            if (model == null || _restartAction == null)
            {
                return;
            }

            var actions = InputSystem.actions;
            var device = TutorialPresenter.DetectDevice(
                new[] { actions?.FindAction(InputActionPaths.Move), actions?.FindAction(InputActionPaths.Look), _restartAction },
                InputDeviceKind.KeyboardMouse);
            model.SetRestartKey(TutorialKeyLabels.For(_restartAction, device));
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
