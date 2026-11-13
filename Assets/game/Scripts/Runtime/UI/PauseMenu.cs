using System;
using System.Collections.Generic;
using Fps.Core;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace Fps.Runtime
{
    /// <summary>
    /// ポーズ画面 (再開・設定・メニューに戻る)。<see cref="Hud"/> と同じ GameObject に <see cref="Hud.Create"/> が付け、
    /// Hud.uxml の <see cref="UxmlName"/> を使う。Play 中は <see cref="PauseActionPath"/> (Esc / ゲームパッドの Start) で開閉する。
    /// <para>
    /// 開いている間は、Pause と同じアクションマップ (Player) の有効なアクションを Pause 以外すべて無効にしてプレイヤーの入力を止め、
    /// カーソルを出す。時間は <see cref="TimeMode"/> に従う: <see cref="PauseTimeMode.Freeze"/> (オフライン、GameBootstrap が設定する) は
    /// Time.timeScale を 0 にして閉じたら戻し、<see cref="PauseTimeMode.KeepRunning"/> (オンライン、既定) は時間に触らず重ねるだけにする。
    /// 何を止めて何を戻すかは Core の <see cref="PauseState"/> が決める。
    /// </para>
    /// <para>
    /// アクションは他の部品と共有するプロジェクト全体のもの (<see cref="InputSystem.actions"/>) なので、部品ごとの有効化を待たず
    /// アクション自体を無効にする。開いている間に有効にされたアクション (出現し直したプレイヤーなど) も毎フレーム止め直す。
    /// 閉じたときは自分が止めたものだけを有効に戻す。
    /// </para>
    /// <para>
    /// 「メニューに戻る」は、メインメニューへの遷移がまだ無いので今のシーンを読み込み直す。読み込む前に <see cref="ReturningToMenu"/> を
    /// 呼ぶので、オンラインはそこで接続を切る。「設定」は <see cref="OpenSettings"/> を渡すまで押せない。
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PauseMenu : HudElementView
    {
        public const string LogTag = "[Pause]";

        /// <summary>Hud.uxml のポーズ画面の要素。</summary>
        public const string UxmlName = "pause-menu";

        public const string ResumeButtonName = "pause-resume";
        public const string SettingsButtonName = "pause-settings";
        public const string ReturnToMenuButtonName = "pause-return-to-menu";
        public const string NoteLabelName = "pause-note";

        /// <summary>開閉に使うアクション。開いている間もこれだけは止めない。</summary>
        public const string PauseActionPath = InputActionPaths.Pause;

        /// <summary><see cref="PauseTimeMode.KeepRunning"/> のときに画面に出す案内。</summary>
        public const string KeepRunningNote = "オンラインでは試合は止まりません";

        // ドメインの再読み込みを切っているので、Play の開始時に ResetOnPlay で 0 に戻す。
        private static int _openCount;

        private readonly PauseState _state = new PauseState(PauseTimeMode.KeepRunning, new[] { PauseActionPath });
        private readonly List<string> _enabledScratch = new List<string>();
        private InputActionAsset _actions;
        private InputAction _pauseAction;
        private bool _enabledPauseAction;
        private Action _openSettings;
        private bool _leaving;

        private Button _resumeButton;
        private Button _settingsButton;
        private Button _returnToMenuButton;
        private Label _noteLabel;

        /// <summary>どれかのポーズ画面が開いているか。<see cref="FirstPersonLook"/> がカーソルをロックし直さないために見る。</summary>
        public static bool IsAnyOpen => _openCount > 0;

        public override string ElementName => UxmlName;

        /// <summary>開いているか。</summary>
        public bool IsOpen => _state.IsOpen;

        /// <summary>今止めているアクション (例: "Player/Move")。閉じている間は空。</summary>
        public IReadOnlyList<string> SuspendedActions => _state.SuspendedActions;

        /// <summary>
        /// 開いている間に時間を止めるか。既定は <see cref="PauseTimeMode.KeepRunning"/>。閉じている間だけ変えられる。
        /// </summary>
        /// <exception cref="InvalidOperationException">開いている間に違う値を入れた。</exception>
        public PauseTimeMode TimeMode
        {
            get => _state.TimeMode;
            set
            {
                _state.TimeMode = value;
                RefreshNote();
            }
        }

        /// <summary>
        /// 「設定」で呼ぶ処理。null の間は「設定」を押せない。設定画面 (SettingsView) ができたらそれを開く処理を渡す。
        /// </summary>
        public Action OpenSettings
        {
            get => _openSettings;
            set
            {
                _openSettings = value;
                RefreshSettingsButton();
            }
        }

        /// <summary>開いたとき。</summary>
        public event Action Opened;

        /// <summary>閉じたとき。</summary>
        public event Action Closed;

        /// <summary>
        /// 「メニューに戻る」でシーンを離れる直前 (閉じて時間と入力を戻した後)。オンラインはここで接続を切る。
        /// 購読先が投げても、ログに出してシーンは離れる。
        /// </summary>
        public event Action ReturningToMenu;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnPlay()
        {
            _openCount = 0;
        }

        private void OnEnable()
        {
            _leaving = false;
            if (!Application.isPlaying)
            {
                return;
            }

            AcquirePauseAction();
        }

        private void OnDisable()
        {
            // 開いたまま消える (シーンを離れる・オンラインのプレイヤーが消える) と時間と入力が止まったままになる。
            Close();
            ReleasePauseAction();
        }

        private void Update()
        {
            if (_pauseAction != null && _pauseAction.WasPressedThisFrame())
            {
                Toggle();
            }

            if (_state.IsOpen)
            {
                SuspendPlayerInput();
            }
        }

        /// <summary>開いていれば閉じ、閉じていれば開く。</summary>
        public void Toggle()
        {
            if (_state.IsOpen)
            {
                Close();
            }
            else
            {
                Open();
            }
        }

        /// <summary>
        /// 開く。<see cref="TimeMode"/> が <see cref="PauseTimeMode.Freeze"/> なら時間を止める。プレイヤーの入力を止めてカーソルを出し、
        /// 「再開」にフォーカスを置く (ゲームパッドでそのまま選べるように)。既に開いていれば何もしない。
        /// </summary>
        public void Open()
        {
            if (!_state.Open(Time.timeScale, out var timeScale))
            {
                return;
            }

            _openCount++;
            if (timeScale.HasValue)
            {
                Time.timeScale = timeScale.Value;
            }

            SuspendPlayerInput();
            UnityEngine.Cursor.lockState = CursorLockMode.None;
            UnityEngine.Cursor.visible = true;
            RefreshVisibility();
            _resumeButton?.Focus();

            Debug.Log($"{LogTag} opened time={(_state.FreezesTime ? "frozen" : "running")} suspended={_state.SuspendedActions.Count}", this);
            Raise(Opened, nameof(Opened));
        }

        /// <summary>閉じる。止めた時間と入力を戻し、視点操作が有効ならカーソルをロックし直す。開いていなければ何もしない。</summary>
        public void Close()
        {
            if (!_state.Close(out var timeScale, out var actionsToEnable))
            {
                return;
            }

            _openCount = Math.Max(0, _openCount - 1);
            if (timeScale.HasValue)
            {
                Time.timeScale = timeScale.Value;
            }

            ResumeActions(actionsToEnable);
            RefreshVisibility();
            FirstPersonLook.RefreshCursorLock();

            Debug.Log($"{LogTag} closed time={Time.timeScale} resumed={actionsToEnable.Count}", this);
            Raise(Closed, nameof(Closed));
        }

        /// <summary>
        /// メニューに戻る。閉じて時間と入力を戻し、<see cref="ReturningToMenu"/> を呼んでから、今のシーンを読み込み直す
        /// (メインメニューへの遷移ができたらそちらに差し替える)。Play 中にのみ呼べる。
        /// </summary>
        /// <exception cref="InvalidOperationException">Play 中でない。</exception>
        public void ReturnToMenu()
        {
            if (!Application.isPlaying)
            {
                throw new InvalidOperationException("PauseMenu.ReturnToMenu can only leave the scene in Play mode");
            }

            if (_leaving)
            {
                return;
            }

            var scene = SceneManager.GetActiveScene();
            if (scene.buildIndex < 0)
            {
                Debug.LogError(
                    $"{LogTag} scene '{scene.path}' is not in the build settings, so it cannot be reloaded to return to the menu; run Fps.Editor.BuildSceneList",
                    this);
                return;
            }

            _leaving = true;
            Close();
            Raise(ReturningToMenu, nameof(ReturningToMenu));

            Debug.Log($"{LogTag} returning to the menu: reloading '{scene.path}' (there is no main menu to go to yet)", this);
            SceneManager.LoadScene(scene.buildIndex);
        }

        protected override void OnBound(VisualElement element)
        {
            _resumeButton = FindRequired<Button>(element, ResumeButtonName);
            _settingsButton = FindRequired<Button>(element, SettingsButtonName);
            _returnToMenuButton = FindRequired<Button>(element, ReturnToMenuButtonName);
            _noteLabel = FindRequired<Label>(element, NoteLabelName);

            _resumeButton.clicked += Close;
            _settingsButton.clicked += HandleSettingsClicked;
            _returnToMenuButton.clicked += HandleReturnToMenuClicked;

            RefreshSettingsButton();
            RefreshNote();
            RefreshVisibility();
        }

        protected override void OnUnbinding(VisualElement element)
        {
            if (_resumeButton != null)
            {
                _resumeButton.clicked -= Close;
            }

            if (_settingsButton != null)
            {
                _settingsButton.clicked -= HandleSettingsClicked;
            }

            if (_returnToMenuButton != null)
            {
                _returnToMenuButton.clicked -= HandleReturnToMenuClicked;
            }

            _resumeButton = null;
            _settingsButton = null;
            _returnToMenuButton = null;
            _noteLabel = null;
        }

        private void HandleSettingsClicked()
        {
            if (_openSettings == null)
            {
                return;
            }

            try
            {
                _openSettings();
            }
            catch (Exception e)
            {
                Debug.LogException(e, this);
            }
        }

        private void HandleReturnToMenuClicked()
        {
            ReturnToMenu();
        }

        private void AcquirePauseAction()
        {
            var actions = InputSystem.actions;
            if (actions == null)
            {
                Debug.LogError(
                    $"{LogTag} project-wide Input Actions are not set (Project Settings > Input System Package); the pause menu cannot be opened",
                    this);
                return;
            }

            var pause = actions.FindAction(PauseActionPath);
            if (pause == null)
            {
                Debug.LogError($"{LogTag} Input Actions '{actions.name}' has no '{PauseActionPath}'; the pause menu cannot be opened", this);
                return;
            }

            _actions = actions;
            _pauseAction = pause;

            // プロジェクト全体のアクションは他の部品と共有なので、自分が有効にしたときだけ後で無効に戻す。
            if (!pause.enabled)
            {
                pause.Enable();
                _enabledPauseAction = true;
            }
        }

        private void ReleasePauseAction()
        {
            if (_enabledPauseAction && _pauseAction != null)
            {
                _pauseAction.Disable();
            }

            _enabledPauseAction = false;
            _pauseAction = null;
            _actions = null;
        }

        private void SuspendPlayerInput()
        {
            var map = _pauseAction != null ? _pauseAction.actionMap : null;
            if (map == null)
            {
                return;
            }

            _enabledScratch.Clear();
            foreach (var action in map.actions)
            {
                if (action.enabled)
                {
                    _enabledScratch.Add(PathOf(action));
                }
            }

            var toSuspend = _state.Suspend(_enabledScratch);
            if (toSuspend.Count == 0)
            {
                return;
            }

            foreach (var action in map.actions)
            {
                if (action.enabled && Contains(toSuspend, PathOf(action)))
                {
                    action.Disable();
                }
            }
        }

        private static bool Contains(IReadOnlyList<string> paths, string path)
        {
            for (var i = 0; i < paths.Count; i++)
            {
                if (string.Equals(paths[i], path, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private void ResumeActions(IReadOnlyList<string> paths)
        {
            if (_actions == null)
            {
                return;
            }

            foreach (var path in paths)
            {
                var action = _actions.FindAction(path);
                if (action == null)
                {
                    Debug.LogWarning($"{LogTag} could not find '{path}' to enable again after the pause", this);
                    continue;
                }

                if (!action.enabled)
                {
                    action.Enable();
                }
            }
        }

        private void RefreshVisibility()
        {
            if (Element != null)
            {
                Element.style.display = _state.IsOpen ? DisplayStyle.Flex : DisplayStyle.None;
            }
        }

        private void RefreshNote()
        {
            if (_noteLabel == null)
            {
                return;
            }

            var keepsRunning = _state.TimeMode == PauseTimeMode.KeepRunning;
            _noteLabel.text = keepsRunning ? KeepRunningNote : string.Empty;
            _noteLabel.style.display = keepsRunning ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private void RefreshSettingsButton()
        {
            _settingsButton?.SetEnabled(_openSettings != null);
        }

        private void Raise(Action handlers, string eventName)
        {
            if (handlers == null)
            {
                return;
            }

            // 1 つの購読先が投げても、ほかの購読先と開閉の続き (シーンを離れるなど) を止めない。
            foreach (var handler in handlers.GetInvocationList())
            {
                try
                {
                    ((Action)handler)();
                }
                catch (Exception e)
                {
                    Debug.LogError($"{LogTag} a {eventName} handler threw: {e.Message}", this);
                    Debug.LogException(e, this);
                }
            }
        }

        private static string PathOf(InputAction action)
        {
            return action.actionMap.name + "/" + action.name;
        }

        private static T FindRequired<T>(VisualElement element, string elementName)
            where T : VisualElement
        {
            var found = element.Q<T>(elementName);
            if (found == null)
            {
                throw new InvalidOperationException(
                    $"the pause menu layout has no {typeof(T).Name} named '{elementName}'; check Assets/game/UI/Resources/Hud/Hud.uxml");
            }

            return found;
        }
    }
}
