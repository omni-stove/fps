using System;
using System.Collections.Generic;
using Fps.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace Fps.Runtime
{
    /// <summary>
    /// メインメニューの画面。MainMenu シーン (Fps.Editor.MenuSceneBuilder が生成) に置き、Awake で子の GameObject に
    /// PanelSettings と UIDocument を作って Resources の MainMenu.uxml を読み込む (<see cref="Show"/>)。
    /// ボタンを押すと <see cref="MenuNavigator.Go"/> でその行き先へ進む。ゲームパッドの上下 (とキーボードの矢印・Tab) では
    /// <see cref="ButtonNames"/> の順にフォーカスを動かし、端では反対側へ回り込む。マウスで指したボタンにもフォーカスを移すので、
    /// どの入力でも選んでいるボタンは 1 つになる。
    /// 起動引数に <c>-mode</c> / <c>-lobby</c> があるとき (<see cref="MenuRoute.ShouldSkipMenu"/>) は何も出さず、
    /// NetBootstrap がネット対戦のシーンへ切り替えるのに任せる。
    /// 設定ボタンは <see cref="SettingsRequested"/> に差し込み口の要素を渡すだけで、中身は設定画面の側が入れる。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MainMenuScreen : MonoBehaviour
    {
        /// <summary>Resources からのレイアウト (Assets/game/UI/Resources/Menu/MainMenu.uxml)。</summary>
        public const string LayoutResourcePath = "Menu/MainMenu";

        /// <summary>HUD と同じランタイムテーマを使う。</summary>
        public const string ThemeResourcePath = Hud.ThemeResourcePath;

        public const string PanelSettingsName = "MainMenuPanelSettings";
        public const string DocumentObjectName = "MainMenuDocument";

        public const string CampaignButtonName = "menu-campaign";
        public const string CreateLobbyButtonName = "menu-create-lobby";
        public const string JoinLobbyButtonName = "menu-join-lobby";
        public const string RangeButtonName = "menu-range";
        public const string SettingsButtonName = "menu-settings";
        public const string QuitButtonName = "menu-quit";
        public const string NoticeName = "menu-notice";
        public const string SettingsSlotName = "menu-settings-slot";

        /// <summary>設定画面がまだ差し込まれていないときに出す案内。</summary>
        public const string SettingsUnavailableNotice = "Settings are not available yet.";

        /// <summary>ボタンの名前。この順に上から並べ、ゲームパッドのフォーカスもこの順に動かす。</summary>
        public static readonly IReadOnlyList<string> ButtonNames = new[]
        {
            CampaignButtonName,
            CreateLobbyButtonName,
            JoinLobbyButtonName,
            RangeButtonName,
            SettingsButtonName,
            QuitButtonName,
        };

        private readonly List<Button> _buttons = new List<Button>();
        private readonly List<Action> _unbind = new List<Action>();
        private UIDocument _document;
        private PanelSettings _ownedPanelSettings;
        private VisualElement _navigationRoot;
        private VisualElement _settingsSlot;
        private Label _notice;
        private int _focusIndex = -1;
        private bool _settingsOpen;
        private bool _leaving;

        /// <summary>
        /// 設定ボタンを押したときに、差し込み口の要素 (空にして表示した状態) を渡す。設定画面はここに中身を入れ、
        /// 閉じるときに <see cref="CloseSettings"/> を呼ぶ。購読が無ければ <see cref="SettingsUnavailableNotice"/> を出すだけ。
        /// </summary>
        public event Action<VisualElement> SettingsRequested;

        /// <summary><see cref="ButtonNames"/> の順に並んだボタン。<see cref="Bind"/> の前は空。</summary>
        public IReadOnlyList<Button> Buttons => _buttons;

        /// <summary>選んでいるボタンの位置。まだどこも選んでいなければ -1。</summary>
        public int FocusIndex => _focusIndex;

        /// <summary>設定画面の差し込み口。<see cref="Bind"/> の前は null。</summary>
        public VisualElement SettingsSlot => _settingsSlot;

        /// <summary>設定画面を開いているか。</summary>
        public bool IsSettingsOpen => _settingsOpen;

        /// <summary>出している案内。出していなければ null。</summary>
        public string Notice => _notice != null && _notice.style.display != DisplayStyle.None ? _notice.text : null;

        /// <summary>この画面のために作り、破棄時に一緒に消す PanelSettings。<see cref="Show"/> の前は null。</summary>
        public PanelSettings OwnedPanelSettings => _ownedPanelSettings;

        private void Awake()
        {
            if (MenuRoute.ShouldSkipMenu(Application.isEditor, NetStartup.IsDedicatedServerBuild, Environment.GetCommandLineArgs()))
            {
                Debug.Log(MenuNavigator.LogTag + " skipping the menu: -mode or -lobby was given");
                return;
            }

            try
            {
                Show();
            }
            catch (InvalidOperationException e)
            {
                Debug.LogError(MenuNavigator.LogTag + " cannot show the main menu: " + e.Message);
            }
        }

        private void OnDestroy()
        {
            Unbind();
            if (_ownedPanelSettings != null)
            {
                Destroy(_ownedPanelSettings);
                _ownedPanelSettings = null;
            }
        }

        /// <summary>
        /// PanelSettings と UIDocument を持つ子の GameObject を作り、MainMenu.uxml を読み込んでボタンをつなぐ。
        /// 戻ってきたときの案内 (<see cref="MenuNavigator.TakeNotice"/>) があれば出す。既に出していれば何もしない。
        /// </summary>
        /// <exception cref="InvalidOperationException">Resources にレイアウトかテーマが無い。</exception>
        public void Show()
        {
            if (_document != null)
            {
                return;
            }

            var layout = Resources.Load<VisualTreeAsset>(LayoutResourcePath);
            if (layout == null)
            {
                throw new InvalidOperationException(
                    $"menu layout 'Resources/{LayoutResourcePath}.uxml' was not found (expected under Assets/game/UI/Resources)");
            }

            var theme = Resources.Load<ThemeStyleSheet>(ThemeResourcePath);
            if (theme == null)
            {
                throw new InvalidOperationException(
                    $"theme 'Resources/{ThemeResourcePath}.tss' was not found (expected under Assets/game/UI/Resources)");
            }

            UnityEngine.Cursor.lockState = CursorLockMode.None;
            UnityEngine.Cursor.visible = true;

            _ownedPanelSettings = ScriptableObject.CreateInstance<PanelSettings>();
            _ownedPanelSettings.name = PanelSettingsName;
            _ownedPanelSettings.themeStyleSheet = theme;
            _ownedPanelSettings.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            _ownedPanelSettings.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
            _ownedPanelSettings.referenceResolution = Hud.ReferenceResolution;
            _ownedPanelSettings.match = 0.5f;

            // UIDocument が有効になる前に参照をすべて設定するため、無効で作ってから有効にする
            var documentObject = new GameObject(DocumentObjectName);
            documentObject.SetActive(false);
            documentObject.transform.SetParent(transform, false);
            _document = documentObject.AddComponent<UIDocument>();
            _document.panelSettings = _ownedPanelSettings;
            _document.visualTreeAsset = layout;
            documentObject.SetActive(true);

            Bind(_document.rootVisualElement);
            ShowNotice(MenuNavigator.TakeNotice());
            FocusAt(0);
        }

        /// <summary>
        /// MainMenu.uxml から作った木 root のボタン・案内・差し込み口を探してつなぐ。前につないだ木は外す。
        /// </summary>
        /// <exception cref="InvalidOperationException">root に <see cref="ButtonNames"/> のボタンなどが無い。</exception>
        public void Bind(VisualElement root)
        {
            if (root == null)
            {
                throw new ArgumentNullException(nameof(root));
            }

            Unbind();

            foreach (var buttonName in ButtonNames)
            {
                var button = root.Q<Button>(buttonName);
                if (button == null)
                {
                    _buttons.Clear();
                    throw new InvalidOperationException($"the menu layout has no Button named '{buttonName}'");
                }

                _buttons.Add(button);
            }

            _notice = root.Q<Label>(NoticeName) ?? throw new InvalidOperationException($"the menu layout has no Label named '{NoticeName}'");
            _settingsSlot = root.Q<VisualElement>(SettingsSlotName)
                ?? throw new InvalidOperationException($"the menu layout has no element named '{SettingsSlotName}'");

            for (var i = 0; i < _buttons.Count; i++)
            {
                var button = _buttons[i];
                var index = i;
                var pressed = ButtonNames[i];
                Action clicked = () => Press(pressed);
                EventCallback<PointerEnterEvent> hovered = _ => FocusAt(index);
                EventCallback<FocusInEvent> focused = _ => _focusIndex = index;

                button.tabIndex = i;
                button.clicked += clicked;
                button.RegisterCallback(hovered);
                button.RegisterCallback(focused);
                _unbind.Add(() =>
                {
                    button.clicked -= clicked;
                    button.UnregisterCallback(hovered);
                    button.UnregisterCallback(focused);
                });
            }

            // 何も選んでいないときの入力はパネルの根に届くので、根で受ける (パネルに付く前なら木の根で受ける)
            _navigationRoot = root.panel?.visualTree ?? root;
            _navigationRoot.RegisterCallback<NavigationMoveEvent>(HandleNavigationMove, TrickleDown.TrickleDown);
            _navigationRoot.RegisterCallback<NavigationCancelEvent>(HandleNavigationCancel, TrickleDown.TrickleDown);
            var navigationRoot = _navigationRoot;
            _unbind.Add(() =>
            {
                navigationRoot.UnregisterCallback<NavigationMoveEvent>(HandleNavigationMove, TrickleDown.TrickleDown);
                navigationRoot.UnregisterCallback<NavigationCancelEvent>(HandleNavigationCancel, TrickleDown.TrickleDown);
            });

            _focusIndex = -1;
            _settingsOpen = false;
            _leaving = false;
            SetDisplayed(_settingsSlot, false);
            ShowNotice(null);
        }

        /// <summary>選んでいるボタンを step だけ動かす (<see cref="MenuRoute.MoveFocus"/>)。設定画面を開いている間は動かさない。</summary>
        public void MoveFocus(int step)
        {
            if (_settingsOpen || _buttons.Count == 0)
            {
                return;
            }

            FocusAt(MenuRoute.MoveFocus(_focusIndex, step, _buttons.Count));
        }

        /// <summary>
        /// destination へ進む。進めたらボタンを押せなくし (シーンを読み終えるまでの二度押しを防ぐ)、true を返す。
        /// シーンがビルドに無いなど進めなければ、案内を出して false を返す。
        /// </summary>
        public bool Choose(MenuDestination destination)
        {
            if (_leaving || _settingsOpen)
            {
                return false;
            }

            ShowNotice(null);
            _leaving = true;
            SetButtonsEnabled(false);
            bool went;
            try
            {
                went = MenuNavigator.Go(destination);
            }
            catch
            {
                _leaving = false;
                SetButtonsEnabled(true);
                throw;
            }

            if (!went)
            {
                _leaving = false;
                SetButtonsEnabled(true);
                ShowNotice("Cannot open " + MenuRoute.Plan(destination).ScenePath + ": it is not in this build.");
                FocusAt(_focusIndex);
            }

            return went;
        }

        /// <summary>
        /// 設定画面の差し込み口を空にして出し、<see cref="SettingsRequested"/> に渡す。購読が無ければ案内を出すだけ。
        /// </summary>
        public void OpenSettings()
        {
            if (_settingsSlot == null || _settingsOpen || _leaving)
            {
                return;
            }

            var handlers = SettingsRequested;
            if (handlers == null)
            {
                Debug.Log(MenuNavigator.LogTag + " settings were requested but no settings screen is installed");
                ShowNotice(SettingsUnavailableNotice);
                return;
            }

            ShowNotice(null);
            _settingsSlot.Clear();
            SetDisplayed(_settingsSlot, true);
            _settingsOpen = true;
            SetButtonsEnabled(false);
            handlers(_settingsSlot);
        }

        /// <summary>設定画面を閉じて差し込み口を空にし、設定ボタンにフォーカスを戻す。</summary>
        public void CloseSettings()
        {
            if (!_settingsOpen)
            {
                return;
            }

            _settingsOpen = false;
            _settingsSlot.Clear();
            SetDisplayed(_settingsSlot, false);
            SetButtonsEnabled(true);
            FocusAt(IndexOf(SettingsButtonName));
        }

        /// <summary>案内を出す。null か空なら隠す。</summary>
        public void ShowNotice(string text)
        {
            if (_notice == null)
            {
                return;
            }

            var shown = !string.IsNullOrWhiteSpace(text);
            _notice.text = shown ? text : "";
            SetDisplayed(_notice, shown);
        }

        private void Press(string buttonName)
        {
            switch (buttonName)
            {
                case CampaignButtonName:
                    Choose(MenuDestination.Campaign);
                    break;
                case CreateLobbyButtonName:
                    Choose(MenuDestination.CreateLobby);
                    break;
                case JoinLobbyButtonName:
                    Choose(MenuDestination.JoinLobby);
                    break;
                case RangeButtonName:
                    Choose(MenuDestination.Range);
                    break;
                case SettingsButtonName:
                    OpenSettings();
                    break;
                case QuitButtonName:
                    Choose(MenuDestination.Quit);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(buttonName), buttonName, "unknown menu button");
            }
        }

        private void HandleNavigationMove(NavigationMoveEvent evt)
        {
            // 設定画面の中の移動は設定画面に任せる
            if (_settingsOpen)
            {
                return;
            }

            int step;
            switch (evt.direction)
            {
                case NavigationMoveEvent.Direction.Up:
                case NavigationMoveEvent.Direction.Previous:
                    step = -1;
                    break;
                case NavigationMoveEvent.Direction.Down:
                case NavigationMoveEvent.Direction.Next:
                    step = 1;
                    break;
                default:
                    step = 0;
                    break;
            }

            // 既定の移動は画面上の位置で次を選ぶので、並び順で動かすためにここで止めて自分で動かす
            evt.StopPropagation();
            _navigationRoot.focusController?.IgnoreEvent(evt);
            if (step != 0)
            {
                MoveFocus(step);
            }
            else if (_focusIndex < 0)
            {
                FocusAt(0);
            }
        }

        private void HandleNavigationCancel(NavigationCancelEvent evt)
        {
            if (!_settingsOpen)
            {
                return;
            }

            evt.StopPropagation();
            CloseSettings();
        }

        private void FocusAt(int index)
        {
            if (index < 0 || index >= _buttons.Count)
            {
                return;
            }

            _focusIndex = index;
            var button = _buttons[index];
            button.Focus();

            // パネルに付いた直後は配置が終わっておらず Focus が効かないことがあるので、次の更新でもう一度選ぶ
            if (button.panel != null && button.focusController?.focusedElement != button)
            {
                button.schedule.Execute(() =>
                {
                    if (_focusIndex == index && !_settingsOpen)
                    {
                        button.Focus();
                    }
                });
            }
        }

        private int IndexOf(string buttonName)
        {
            for (var i = 0; i < ButtonNames.Count; i++)
            {
                if (ButtonNames[i] == buttonName)
                {
                    return i;
                }
            }

            return -1;
        }

        private void SetButtonsEnabled(bool enabled)
        {
            foreach (var button in _buttons)
            {
                button.SetEnabled(enabled);
            }
        }

        private void Unbind()
        {
            foreach (var undo in _unbind)
            {
                undo();
            }

            _unbind.Clear();
            _buttons.Clear();
            _navigationRoot = null;
            _notice = null;
            _settingsSlot = null;
        }

        private static void SetDisplayed(VisualElement element, bool displayed)
        {
            element.style.display = displayed ? DisplayStyle.Flex : DisplayStyle.None;
        }
    }
}
