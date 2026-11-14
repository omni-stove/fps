using System;
using Fps.Core;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace Fps.Runtime
{
    /// <summary>
    /// プレイヤーの今のアクション (<see cref="PlayerActionReader.Current"/>) を画面の左上に 1 項目 1 行で出すデバッグ表示。
    /// 行の文字列と有効かどうかは <see cref="ActionDebugFormat.Lines"/> が決め、有効な行は色を変える。
    /// Editor と Development Build だけで動き (<see cref="IsSupported"/>)、それ以外と専用サーバーでは何も作らず何もしない。
    /// 表示は HUD とは別の UIDocument と PanelSettings をコードで作って出し、HUD の uxml/uss には触らない。
    /// F3 で表示と非表示を切り替える (入力アセットは変えず <see cref="Keyboard.current"/> を直接読む)。既定は表示。
    /// 読み元は <see cref="GameBootstrap"/> が <see cref="Bind"/> で渡す。渡されないまま Awake を迎えたら同じ GameObject から探し、
    /// 無ければエラーを出して自分を無効にする。
    /// </summary>
    [DisallowMultipleComponent]
    // PlayerActionReader (DefaultExecutionOrder 100) がその LateUpdate で組み立てた値を同じフレームのうちに出す。
    [DefaultExecutionOrder(110)]
    public sealed class ActionDebugOverlay : MonoBehaviour
    {
        public const string LogTag = "[ActionDebug]";

        /// <summary>表示用に作る GameObject の名前。</summary>
        public const string DocumentObjectName = "ActionDebugOverlay";

        /// <summary>表示用に作る PanelSettings の名前。</summary>
        public const string PanelSettingsName = "ActionDebugPanelSettings";

        /// <summary>HUD (sortingOrder 0) より手前に出す。</summary>
        public const float SortingOrder = 100f;

        /// <summary>行を並べる枠の名前 (UI Toolkit Debugger で探すとき用)。</summary>
        public const string ContainerName = "action-debug";

        private const float ScreenMargin = 16f;
        private const float Padding = 8f;
        private const float FontSize = 18f;

        private static readonly Color BackgroundColor = new Color(0f, 0f, 0f, 0.55f);
        private static readonly Color InactiveColor = new Color(0.7f, 0.7f, 0.7f, 1f);
        private static readonly Color ActiveColor = new Color(1f, 0.82f, 0.2f, 1f);

        [SerializeField, Tooltip("Where the shown actions are read from; GameBootstrap wires it")]
        private PlayerActionReader _reader;

        private bool _visible = true;
        private GameObject _documentObject;
        private PanelSettings _panelSettings;
        private VisualElement _container;
        private Label[] _labels;
        private PlayerActionState _shown;
        private bool _hasShown;

        /// <summary>この Build でデバッグ表示を出すか。Editor と Development Build だけ true。</summary>
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

        /// <summary>表示するアクションの読み元。未配線なら null。</summary>
        public PlayerActionReader Reader => _reader;

        private static bool ShouldRun => IsSupported && !NetStartup.ShouldDisablePresentation(NetStartup.IsDedicatedServerBuild);

        /// <summary>表示中か (F3 で切り替わる)。動いていなくても、次に動いたときに出すかどうかを返す。</summary>
        public bool IsVisible => _visible;

        /// <summary>表示に使っている UIDocument。まだ作っていなければ null。</summary>
        public UIDocument Document => _documentObject != null ? _documentObject.GetComponent<UIDocument>() : null;

        private void Awake()
        {
            // 出さない Build では読み元が無くてもエラーにせず、黙って止まる。
            if (!ShouldRun)
            {
                enabled = false;
                return;
            }

            if (_reader != null)
            {
                return;
            }

            _reader = GetComponent<PlayerActionReader>();
            if (_reader == null)
            {
                Debug.LogError($"{LogTag} '{name}' has no PlayerActionReader; the shown actions are read from it", this);
                enabled = false;
            }
        }

        /// <summary>表示するアクションの読み元を渡す。</summary>
        public void Bind(PlayerActionReader reader)
        {
            if (reader == null)
            {
                throw new ArgumentNullException(nameof(reader));
            }

            _reader = reader;
            _hasShown = false;
        }

        /// <summary>表示と非表示を切り替える。F3 を押したときと同じ。</summary>
        public void SetVisible(bool visible)
        {
            _visible = visible;
            ApplyVisibility();
        }

        private void OnEnable()
        {
            if (!ShouldRun || !TryCreateDocument())
            {
                enabled = false;
                return;
            }

            _documentObject.SetActive(true);
            TryBuildView();
        }

        private void OnDisable()
        {
            // UIDocument は有効にし直すたびに木を作り直すので、ここで掴んでいる要素を捨てる。
            _container = null;
            _labels = null;
            if (_documentObject != null)
            {
                _documentObject.SetActive(false);
            }
        }

        private void OnDestroy()
        {
            DestroySafely(_documentObject);
            DestroySafely(_panelSettings);
            _documentObject = null;
            _panelSettings = null;
        }

        private void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.f3Key.wasPressedThisFrame)
            {
                SetVisible(!_visible);
            }
        }

        private void LateUpdate()
        {
            if (_reader == null)
            {
                Debug.LogError($"{LogTag} the PlayerActionReader of '{name}' was destroyed; stopping the debug overlay", this);
                enabled = false;
                return;
            }

            if (!_visible)
            {
                return;
            }

            // OnEnable の時点で UIDocument の木がまだ無かった場合の取りこぼしを拾う。
            if (_labels == null && !TryBuildView())
            {
                return;
            }

            var state = _reader.Current;
            if (_hasShown && state == _shown)
            {
                return;
            }

            Show(state);
        }

        private void Show(PlayerActionState state)
        {
            var lines = ActionDebugFormat.Lines(state);
            for (var i = 0; i < _labels.Length; i++)
            {
                var label = _labels[i];
                var line = lines[i];
                label.text = line.Text;
                label.style.color = line.IsActive ? ActiveColor : InactiveColor;
                label.style.unityFontStyleAndWeight = line.IsActive ? FontStyle.Bold : FontStyle.Normal;
            }

            _shown = state;
            _hasShown = true;
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
                    $"{LogTag} theme 'Resources/{Hud.ThemeResourcePath}.tss' was not found (expected under Assets/game/UI/Resources); the debug overlay cannot be shown",
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
            _documentObject = new GameObject(DocumentObjectName);
            _documentObject.SetActive(false);
            _documentObject.AddComponent<UIDocument>().panelSettings = _panelSettings;
            return true;
        }

        /// <summary>UIDocument の今の木に、行の数だけ Label を並べた枠を足す。木がまだ無ければ false。</summary>
        private bool TryBuildView()
        {
            var document = Document;
            var root = document != null ? document.rootVisualElement : null;
            if (root == null)
            {
                return false;
            }

            root.pickingMode = PickingMode.Ignore;

            _container = new VisualElement { name = ContainerName, pickingMode = PickingMode.Ignore };
            _container.style.position = Position.Absolute;
            _container.style.left = ScreenMargin;
            _container.style.top = ScreenMargin;
            _container.style.paddingLeft = Padding;
            _container.style.paddingRight = Padding;
            _container.style.paddingTop = Padding;
            _container.style.paddingBottom = Padding;
            _container.style.backgroundColor = BackgroundColor;

            _labels = new Label[ActionDebugFormat.LineCount];
            for (var i = 0; i < _labels.Length; i++)
            {
                var label = new Label { pickingMode = PickingMode.Ignore };
                label.style.fontSize = FontSize;
                label.style.marginLeft = 0f;
                label.style.marginRight = 0f;
                label.style.marginTop = 0f;
                label.style.marginBottom = 0f;
                label.style.paddingLeft = 0f;
                label.style.paddingRight = 0f;
                label.style.paddingTop = 0f;
                label.style.paddingBottom = 0f;
                _labels[i] = label;
                _container.Add(label);
            }

            root.Add(_container);
            _hasShown = false;
            ApplyVisibility();
            return true;
        }

        private void ApplyVisibility()
        {
            if (_container != null)
            {
                _container.style.display = _visible ? DisplayStyle.Flex : DisplayStyle.None;
            }

            // 隠している間は行を書き換えないので、出し直したときに今の値で描き直す。
            _hasShown = false;
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
