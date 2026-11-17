using System;
using System.Collections.Generic;
using Shooter.Core;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace Shooter.Runtime
{
    // デバッグ用のパネルのために HUD の uxml/uss と入力アセットを変えないよう、表示は別の UIDocument と PanelSettings をコードで作り、
    // F1 は Keyboard.current を直接読む。
    [DisallowMultipleComponent]
    public sealed class GameRulesDebugPanel : MonoBehaviour
    {
        public const string LogTag = GameRulesDebugDraft.LogTag;

        public const string ObjectName = "GameRulesDebugPanel";

        public const string PanelSettingsName = "GameRulesDebugPanelSettings";

        // 名前を付けるのは UI Toolkit Debugger で探すときのため。
        public const string ContainerName = "rules-debug";

        public const string ApplyButtonName = "rules-debug-apply";

        public const string ResetButtonName = "rules-debug-reset";

        public const string SlowMotionButtonName = "rules-debug-slow-motion";

        public const string SlowMotionReason = "debug";

        public const float DefaultSlowMotionScale = 0.3f;

        public const float DefaultSlowMotionSeconds = 5f;

        // InputActionPaths のアクションはすべてこのマップにあるので、このマップだけを止める。
        public const string PlayerActionMapName = "Player";

        public const string FieldsName = "rules-debug-fields";

        public const string FieldLabelKeyPrefix = "debug.rules.field.";

        // HUD (0) と ActionDebugOverlay (100) より手前に出す。
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

        public float SlowMotionScale
        {
            get => _slowMotionScale;
            set
            {
                TimeScaleArbitration.Validate(SlowMotionReason, value, _slowMotionSeconds);
                _slowMotionScale = value;
            }
        }

        public float SlowMotionSeconds
        {
            get => _slowMotionSeconds;
            set
            {
                TimeScaleArbitration.Validate(SlowMotionReason, _slowMotionScale, value);
                _slowMotionSeconds = value;
            }
        }

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

        public static GameRulesDebugPanel Instance => _instance;

        public bool IsOpen => _draft != null;

        public GameRulesDebugDraft Draft => _draft;

        public static string FieldLabelKey(GameRulesDebugField field)
        {
            return FieldLabelKeyPrefix + field;
        }

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

        public void ResetRules()
        {
            Debug.Log(GameRulesDebugDraft.FormatResetLog());
            GameRulesOverride.Clear();
            Close(restoreInput: true);
            ReloadActiveScene();
        }

        // パネルを閉じないのは、ルールを読み直さずに試せるようにするため。
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
                    $"{LogTag} theme 'Resources/{Hud.ThemeResourcePath}.tss' was not found (expected under UI/Resources of the Demo sample); the rules panel cannot be shown",
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
