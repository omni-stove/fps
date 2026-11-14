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
    /// 実行中に <see cref="GameRules"/> の項目 (<see cref="GameRulesDebugField"/>) を切り替えるデバッグパネル。
    /// Editor と Development Build だけで (<see cref="IsSupported"/>)、シーンをまたいで 1 つ常駐する (<see cref="Install"/>)。専用サーバーでは作らない。
    /// F1 で開閉する (入力アセットは変えず <see cref="Keyboard.current"/> を直接読む)。開くと今のシーンの <see cref="GameBootstrap"/> が使ったルール
    /// (<see cref="BootstrapWorld.Rules"/>) を初期値に、オンオフの項目はトグル、それ以外はドロップダウンで出す。
    /// 適用 (APPLY) で <see cref="GameRulesOverride.Set"/>、リセット (RESET) で <see cref="GameRulesOverride.Clear"/> してから今のシーンを読み直す。
    /// SLOW-MO は今のルールのまま時間の速さを要求する (<see cref="RequestSlowMotion"/>)。
    /// 開いている間はプロジェクト全体の入力の Player マップを止め、カーソルを出す。
    /// 表示は HUD とは別の UIDocument と PanelSettings をコードで作って出す。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GameRulesDebugPanel : MonoBehaviour
    {
        public const string LogTag = GameRulesDebugDraft.LogTag;

        /// <summary>常駐させる GameObject の名前。</summary>
        public const string ObjectName = "GameRulesDebugPanel";

        /// <summary>表示用に作る PanelSettings の名前。</summary>
        public const string PanelSettingsName = "GameRulesDebugPanelSettings";

        /// <summary>パネルの枠の名前 (UI Toolkit Debugger で探すとき用)。</summary>
        public const string ContainerName = "rules-debug";

        /// <summary>適用ボタンの名前。</summary>
        public const string ApplyButtonName = "rules-debug-apply";

        /// <summary>リセットボタンの名前。</summary>
        public const string ResetButtonName = "rules-debug-reset";

        /// <summary>時間の速さを要求するボタンの名前。</summary>
        public const string SlowMotionButtonName = "rules-debug-slow-motion";

        /// <summary>時間の速さを要求するボタンが <see cref="TimeScaleArbiter.Request"/> に渡す理由。</summary>
        public const string SlowMotionReason = "debug";

        /// <summary><see cref="SlowMotionScale"/> の既定値。</summary>
        public const float DefaultSlowMotionScale = 0.3f;

        /// <summary><see cref="SlowMotionSeconds"/> の既定値。</summary>
        public const float DefaultSlowMotionSeconds = 5f;

        /// <summary>止める入力のマップ。<see cref="InputActionPaths"/> のアクションはすべてここにある。</summary>
        public const string PlayerActionMapName = "Player";

        /// <summary>項目を並べるスクロールの枠の名前。</summary>
        public const string FieldsName = "rules-debug-fields";

        /// <summary>項目の見出しを引く表のキーの頭。後ろに <see cref="GameRulesDebugField"/> の名前を付ける。</summary>
        public const string FieldLabelKeyPrefix = "debug.rules.field.";

        /// <summary>HUD (0) と <see cref="ActionDebugOverlay"/> (100) より手前に出す。</summary>
        public const float SortingOrder = 200f;

        private const float ScreenMargin = 16f;
        private const float Padding = 12f;
        private const float FontSize = 18f;
        private const float Width = 460f;
        private const float RowSpacing = 4f;

        private static readonly Color BackgroundColor = new Color(0f, 0f, 0f, 0.8f);
        private static readonly Color TextColor = Color.white;

        private static GameRulesDebugPanel _instance;

        private readonly List<InputAction> _suspendedActions = new List<InputAction>();
        private bool _warnedMissingMap;
        private readonly Dictionary<GameRulesDebugField, VisualElement> _controls = new Dictionary<GameRulesDebugField, VisualElement>();

        private GameObject _documentObject;
        private PanelSettings _panelSettings;
        private VisualElement _container;
        private GameRulesDebugDraft _draft;
        private BootstrapWorld _world;
        private float _slowMotionScale = DefaultSlowMotionScale;
        private float _slowMotionSeconds = DefaultSlowMotionSeconds;

        /// <summary>時間の速さのボタンで要求する倍率 (0 より大きく 1 以下)。</summary>
        /// <exception cref="ArgumentOutOfRangeException">範囲外。</exception>
        public float SlowMotionScale
        {
            get => _slowMotionScale;
            set
            {
                TimeScaleArbitration.Validate(SlowMotionReason, value, _slowMotionSeconds);
                _slowMotionScale = value;
            }
        }

        /// <summary>時間の速さのボタンで要求する実時間の秒数 (0 より大きい)。</summary>
        /// <exception cref="ArgumentOutOfRangeException">範囲外。</exception>
        public float SlowMotionSeconds
        {
            get => _slowMotionSeconds;
            set
            {
                TimeScaleArbitration.Validate(SlowMotionReason, _slowMotionScale, value);
                _slowMotionSeconds = value;
            }
        }

        /// <summary>この Build でパネルを使えるか。Editor と Development Build だけ true。</summary>
        public static bool IsSupported
        {
            get
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                return true;
#else
                return false;
#endif
            }
        }

        /// <summary>常駐しているパネル。作っていなければ null。</summary>
        public static GameRulesDebugPanel Instance => _instance;

        /// <summary>開いているか。</summary>
        public bool IsOpen => _draft != null;

        /// <summary>開いている間の編集中の値。閉じていれば null。</summary>
        public GameRulesDebugDraft Draft => _draft;

        /// <summary>field の見出しを引く表のキー。</summary>
        public static string FieldLabelKey(GameRulesDebugField field)
        {
            return FieldLabelKeyPrefix + field;
        }

        /// <summary>今の言語での field の見出し。表に無ければ項目の名前をそのまま出す。</summary>
        public static string FieldLabel(GameRulesDebugField field)
        {
            return Localizer.TryGet(FieldLabelKey(field), out var text) ? text : field.ToString();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _instance = null;
        }

        // GameBootstrap を改変しないために、シーンとは別に自分で常駐する。
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (!IsSupported || NetStartup.ShouldDisablePresentation(NetStartup.IsDedicatedServerBuild) || _instance != null)
            {
                return;
            }

            var host = new GameObject(ObjectName);
            DontDestroyOnLoad(host);
            host.AddComponent<GameRulesDebugPanel>();
        }

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Debug.LogWarning($"{LogTag} only one GameRulesDebugPanel stays resident; destroying the duplicate '{name}'", this);
                Destroy(gameObject);
                return;
            }

            _instance = this;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            if (_instance != this)
            {
                return;
            }

            Close(restoreInput: true);
            DestroySafely(_documentObject);
            DestroySafely(_panelSettings);
            _documentObject = null;
            _panelSettings = null;
            _instance = null;
        }

        private void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.f1Key.wasPressedThisFrame)
            {
                if (IsOpen)
                {
                    Close(restoreInput: true);
                }
                else
                {
                    Open();
                }
            }

            // FirstPersonLook はフォーカスが戻るとカーソルをロックし直すので、開いている間は外し続ける。
            if (IsOpen)
            {
                SuspendPlayerInput();
            }

            if (IsOpen && (UnityEngine.Cursor.lockState != CursorLockMode.None || !UnityEngine.Cursor.visible))
            {
                ShowCursor();
            }
        }

        /// <summary>
        /// 今のシーンの <see cref="GameBootstrap"/> が使ったルールを初期値にして開く。開いていれば何もしない。
        /// GameBootstrap が無いかまだ生成していないシーンでは警告を出して false。
        /// </summary>
        public bool Open()
        {
            if (IsOpen)
            {
                return true;
            }

            var bootstrap = FindAnyObjectByType<GameBootstrap>();
            var world = bootstrap != null ? bootstrap.World : null;
            if (world == null)
            {
                Debug.LogWarning($"{LogTag} this scene has no GameBootstrap that has spawned; the rules panel cannot be opened");
                return false;
            }

            if (!TryCreateDocument() || !TryBuildView(new GameRulesDebugDraft(world.Rules)))
            {
                return false;
            }

            _world = world;
            _suspendedActions.Clear();
            _warnedMissingMap = false;
            SuspendPlayerInput();
            ShowCursor();
            return true;
        }

        /// <summary>
        /// 閉じる。restoreInput が true なら、開いたときに止めた入力を戻し、プレイヤーが視点を操作できるならカーソルをロックし直す。
        /// 閉じていれば何もしない。
        /// </summary>
        public void Close(bool restoreInput)
        {
            if (!IsOpen)
            {
                return;
            }

            _draft = null;
            _world = null;
            _controls.Clear();
            if (_container != null)
            {
                _container.RemoveFromHierarchy();
                _container = null;
            }

            if (restoreInput)
            {
                ResumePlayerInput();
            }
            else
            {
                _suspendedActions.Clear();
            }
        }

        /// <summary>
        /// 編集中の値で <see cref="GameRulesOverride.Set"/> し、変えた項目をログに出して今のシーンを読み直す。
        /// 値の組み合わせが不正なら、エラーを出して開いたまま false。
        /// </summary>
        public bool Apply()
        {
            if (!IsOpen)
            {
                return false;
            }

            GameRules rules;
            try
            {
                rules = _draft.Build();
            }
            catch (ArgumentException e)
            {
                Debug.LogError($"{LogTag} could not build the rules: {e.Message}");
                return false;
            }

            Debug.Log(GameRulesDebugDraft.FormatApplyLog(_world.Rules, rules));
            GameRulesOverride.Set(rules);
            Close(restoreInput: true);
            ReloadActiveScene();
            return true;
        }

        /// <summary><see cref="GameRulesOverride.Clear"/> して今のシーンを読み直す。</summary>
        public void ResetRules()
        {
            Debug.Log(GameRulesDebugDraft.FormatResetLog());
            GameRulesOverride.Clear();
            Close(restoreInput: true);
            ReloadActiveScene();
        }

        /// <summary>
        /// <see cref="SlowMotionScale"/> の速さを <see cref="SlowMotionSeconds"/> 秒だけ <see cref="TimeScaleArbiter.Request"/> で要求する。
        /// 受けたか断ったか (とその理由) は TimeScaleArbiter がログに出す。パネルは閉じない (ルールを読み直さずに試せるように)。
        /// </summary>
        public TimeScaleRefusal RequestSlowMotion()
        {
            return TimeScaleArbiter.Request(SlowMotionReason, _slowMotionScale, _slowMotionSeconds);
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            // 新しいシーンの部品は有効になるときに入力を自分で入れ直すので、ここで戻すと誰も止めない入力が残る。
            if (mode == LoadSceneMode.Single)
            {
                Close(restoreInput: false);
            }
        }

        private void SuspendPlayerInput()
        {
            var actions = InputSystem.actions;
            var map = actions != null ? actions.FindActionMap(PlayerActionMapName) : null;
            if (map == null)
            {
                if (!_warnedMissingMap)
                {
                    _warnedMissingMap = true;
                    Debug.LogWarning($"{LogTag} the '{PlayerActionMapName}' input map was not found; the player keeps moving while the panel is open");
                }

                return;
            }

            // 開いている間に別の部品 (PauseMenu の再開など) が有効に戻したものも毎フレーム止め直す。部品は自分が有効にしたアクションだけを後で無効にするので、元から有効だったものだけを止めて戻す。
            foreach (var action in map.actions)
            {
                if (action.enabled)
                {
                    action.Disable();
                    if (!_suspendedActions.Contains(action))
                    {
                        _suspendedActions.Add(action);
                    }
                }
            }
        }

        private void ResumePlayerInput()
        {
            foreach (var action in _suspendedActions)
            {
                action.Enable();
            }

            _suspendedActions.Clear();

            if (!PauseMenu.IsAnyOpen)
            {
                FirstPersonLook.RefreshCursorLock();
            }
        }

        private static void ShowCursor()
        {
            UnityEngine.Cursor.lockState = CursorLockMode.None;
            UnityEngine.Cursor.visible = true;
        }

        private void ReloadActiveScene()
        {
            var scene = SceneManager.GetActiveScene();
            if (scene.buildIndex >= 0)
            {
                SceneManager.LoadScene(scene.buildIndex, LoadSceneMode.Single);
                return;
            }

#if UNITY_EDITOR
            // Build Settings に無いシーンを直接開いて Play したときも、Editor ではパスで読み直せる。
            UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(
                scene.path, new LoadSceneParameters(LoadSceneMode.Single));
#else
            Debug.LogError($"{LogTag} scene '{scene.path}' is not in Build Settings, so it cannot be reloaded; the override takes effect from the next scene load", this);
#endif
        }

        /// <summary>
        /// 表示用の PanelSettings と UIDocument を持つ GameObject を作る。作ってあれば何もしない。
        /// 文字の描画に要るテーマは HUD と同じもの (<see cref="Hud.ThemeResourcePath"/>) を使う。見つからなければエラーを出して false。
        /// </summary>
        private bool TryCreateDocument()
        {
            if (_documentObject != null)
            {
                return true;
            }

            var theme = Resources.Load<ThemeStyleSheet>(Hud.ThemeResourcePath);
            if (theme == null)
            {
                Debug.LogError(
                    $"{LogTag} theme 'Resources/{Hud.ThemeResourcePath}.tss' was not found (expected under Assets/game/UI/Resources); the rules panel cannot be shown",
                    this);
                return false;
            }

            _panelSettings = ScriptableObject.CreateInstance<PanelSettings>();
            _panelSettings.name = PanelSettingsName;
            _panelSettings.themeStyleSheet = theme;
            _panelSettings.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            _panelSettings.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
            _panelSettings.referenceResolution = Hud.ReferenceResolution;
            _panelSettings.match = 0.5f;
            _panelSettings.sortingOrder = SortingOrder;

            // UIDocument は有効になった時点の PanelSettings で木を作るので、無効で作ってから設定する。
            _documentObject = new GameObject(ObjectName + "Document");
            _documentObject.SetActive(false);
            _documentObject.transform.SetParent(transform, false);
            _documentObject.AddComponent<UIDocument>().panelSettings = _panelSettings;
            _documentObject.SetActive(true);
            return true;
        }

        private bool TryBuildView(GameRulesDebugDraft draft)
        {
            var document = _documentObject.GetComponent<UIDocument>();
            var root = document != null ? document.rootVisualElement : null;
            if (root == null)
            {
                Debug.LogError($"{LogTag} the UIDocument has no visual tree; the rules panel cannot be shown", this);
                return false;
            }

            // 閉じている間に画面のクリックを奪わないよう、根は拾わずパネルの枠だけが拾う。
            root.pickingMode = PickingMode.Ignore;

            _draft = draft;
            _container = new VisualElement { name = ContainerName };
            _container.style.position = Position.Absolute;
            _container.style.right = ScreenMargin;
            _container.style.top = ScreenMargin;
            _container.style.width = Width;
            _container.style.paddingLeft = Padding;
            _container.style.paddingRight = Padding;
            _container.style.paddingTop = Padding;
            _container.style.paddingBottom = Padding;
            _container.style.backgroundColor = BackgroundColor;
            // 基準解像度の高さで上限を決めると、横長の画面で下が画面外に出るため、上下を両方固定する。
            _container.style.bottom = ScreenMargin;

            var title = new Label("GameRules (F1)");
            title.style.fontSize = FontSize;
            title.style.color = TextColor;
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.marginBottom = RowSpacing * 2f;
            _container.Add(title);

            // 項目が画面の高さに収まらないので、項目だけをスクロールさせて適用とリセットのボタンは常に見せる。
            var fields = new ScrollView(ScrollViewMode.Vertical) { name = FieldsName };
            fields.style.flexShrink = 1f;
            foreach (var field in GameRulesDebugDraft.Fields)
            {
                var control = CreateControl(field);
                control.style.marginBottom = RowSpacing;
                _controls[field] = control;
                fields.Add(control);
            }

            _container.Add(fields);

            var buttons = new VisualElement();
            buttons.style.flexDirection = FlexDirection.Row;
            buttons.style.justifyContent = Justify.FlexEnd;
            buttons.style.marginTop = RowSpacing * 2f;
            // テーマのフォントが日本語の字形を持つとは限らないので、他の画面 (MainMenu.uxml) と同じく英字にする。
            buttons.Add(CreateButton(SlowMotionButtonName, "SLOW-MO", () => RequestSlowMotion()));
            buttons.Add(CreateButton(ResetButtonName, "RESET", ResetRules));
            buttons.Add(CreateButton(ApplyButtonName, "APPLY", () => Apply()));
            _container.Add(buttons);

            RefreshControls();
            root.Add(_container);
            return true;
        }

        private VisualElement CreateControl(GameRulesDebugField field)
        {
            var label = FieldLabel(field);
            if (GameRulesDebugDraft.IsToggle(field))
            {
                var toggle = new Toggle(label);
                toggle.RegisterValueChangedCallback(e => OnChanged(field, e.newValue ? 1 : 0));
                StyleLabel(toggle.labelElement);
                return toggle;
            }

            var dropdown = new DropdownField(label, new List<string>(GameRulesDebugDraft.Options(field)), 0);
            dropdown.RegisterValueChangedCallback(_ => OnChanged(field, dropdown.index));
            StyleLabel(dropdown.labelElement);
            return dropdown;
        }

        private static Button CreateButton(string elementName, string text, Action onClick)
        {
            var button = new Button(onClick) { name = elementName, text = text };
            button.style.fontSize = FontSize;
            button.style.minWidth = 100f;
            return button;
        }

        private static void StyleLabel(Label label)
        {
            label.style.fontSize = FontSize;
            label.style.color = TextColor;
            label.style.minWidth = 180f;
        }

        private void OnChanged(GameRulesDebugField field, int option)
        {
            if (_draft == null || option < 0)
            {
                return;
            }

            _draft.Set(field, option);

            // カバーとブラインドファイアは連動して変わるので、どれを変えても全部を読み直す。
            RefreshControls();
        }

        private void RefreshControls()
        {
            foreach (var pair in _controls)
            {
                var option = _draft.Get(pair.Key);
                switch (pair.Value)
                {
                    case Toggle toggle:
                        toggle.SetValueWithoutNotify(option == 1);
                        break;
                    case DropdownField dropdown:
                        dropdown.SetValueWithoutNotify(dropdown.choices[option]);
                        break;
                }
            }
        }

        private static void DestroySafely(UnityEngine.Object target)
        {
            if (target == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(target);
            }
            else
            {
                DestroyImmediate(target);
            }
        }
    }
}
