using System;
using System.Collections.Generic;
using System.IO;
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
    /// ロードアウトのボタンは、オンラインと同じアセット (<see cref="Loadout"/>・<see cref="Catalog"/>、MenuSceneBuilder が配線) から
    /// 選ぶ画面 (<see cref="LoadoutSelectPanel"/>) を差し込み口に出し、決めた並びを <see cref="LoadoutStore"/> に保存する。
    /// 送るのはオンラインに入ってから (Fps.Net の NetLoadoutChooser が保存した並びを送る)。
    /// <see cref="Show"/> は <see cref="SettingsView"/> を取り付ける (<see cref="InstallSettingsView"/>)。
    /// キャンペーンのボタンは、ミッションの一覧 (<see cref="Missions"/>、MenuSceneBuilder が配線) とセーブ (<see cref="SaveStorage"/>) から
    /// ミッションと難易度を選ぶ画面 (<see cref="CampaignPanel"/>) を差し込み口に出し、決めた始め方で <see cref="MenuNavigator.StartCampaign"/> を呼ぶ。
    /// 「最初から」はシーンを読めると確かめてからセーブを消す。
    /// ボタンの文字は MainMenu.uxml にキーで書き、<see cref="LocalizedTexts"/> で今の言語にして、言語が変わったら書き直す。
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
        public const string QuickMatchButtonName = "menu-quick-match";
        public const string LoadoutButtonName = "menu-loadout";
        public const string RangeButtonName = "menu-range";
        public const string TutorialButtonName = "menu-tutorial";
        public const string SettingsButtonName = "menu-settings";
        public const string QuitButtonName = "menu-quit";
        public const string NoticeName = "menu-notice";
        public const string SettingsSlotName = "menu-settings-slot";
        public const string LoadoutSlotName = "menu-loadout-slot";
        public const string CampaignSlotName = "menu-campaign-slot";

        /// <summary>設定画面がまだ差し込まれていないときに出す案内。</summary>
        public static string SettingsUnavailableNotice => Localizer.Get("menu.settingsUnavailable");

        /// <summary>ロードアウトのアセットが使えないときに出す案内。reason に理由を入れる。</summary>
        public static string LoadoutUnavailableNotice(string reason)
        {
            return Localizer.Format("menu.loadoutUnavailable", reason);
        }

        /// <summary>行き先のシーンがビルドに無いときに出す案内。</summary>
        public static string SceneMissingNotice(string scenePath)
        {
            return Localizer.Format("menu.sceneMissing", scenePath);
        }

        /// <summary>ボタンの名前。この順に上から並べ、ゲームパッドのフォーカスもこの順に動かす。</summary>
        public static readonly IReadOnlyList<string> ButtonNames = new[]
        {
            CampaignButtonName,
            CreateLobbyButtonName,
            JoinLobbyButtonName,
            QuickMatchButtonName,
            LoadoutButtonName,
            RangeButtonName,
            TutorialButtonName,
            SettingsButtonName,
            QuitButtonName,
        };

        [SerializeField, Tooltip("The online loadout (Assets/game/Weapons/DefaultLoadout.asset); the slot rules and default weapons come from it")]
        private LoadoutDefinition _loadout;

        [SerializeField, Tooltip("The weapons that can be chosen online (Assets/game/Weapons/WeaponCatalog.asset)")]
        private WeaponCatalogAsset _catalog;

        [SerializeField, Tooltip("The campaign missions (Assets/game/Missions/MissionCatalog.asset)")]
        private MissionCatalog _missions;

        private readonly List<Button> _buttons = new List<Button>();
        private readonly List<Action> _unbind = new List<Action>();
        private UIDocument _document;
        private PanelSettings _ownedPanelSettings;
        private VisualElement _navigationRoot;
        private VisualElement _settingsSlot;
        private VisualElement _loadoutSlot;
        private LoadoutSelectPanel _loadoutPanel;
        private LoadoutPreferenceStore _loadoutStore = new LoadoutPreferenceStore();
        private VisualElement _campaignSlot;
        private CampaignSelectPanel _campaignPanel;
        private ISaveStorage _saveStorage;
        private Label _notice;
        private LocalizedText _noticeSource;
        private int _focusIndex = -1;
        private bool _settingsOpen;
        private bool _leaving;
        private SettingsView _settingsView;
        private LocalizedTexts _texts;

        /// <summary>
        /// 設定ボタンを押したときに、差し込み口の要素 (空にして表示した状態) を渡す。設定画面はここに中身を入れ、
        /// 閉じるときに <see cref="CloseSettings"/> を呼ぶ。購読が無ければ <see cref="SettingsUnavailableNotice"/> を出すだけ。
        /// </summary>
        public event Action<VisualElement> SettingsRequested;

        /// <summary><see cref="CloseSettings"/> で設定画面を閉じた直後 (差し込み口を空にする前)。設定画面はここで保存する。</summary>
        public event Action SettingsClosed;

        /// <summary><see cref="InstallSettingsView"/> で取り付けた設定画面。無ければ null。</summary>
        public SettingsView SettingsView => _settingsView;

        /// <summary><see cref="ButtonNames"/> の順に並んだボタン。<see cref="Bind"/> の前は空。</summary>
        public IReadOnlyList<Button> Buttons => _buttons;

        /// <summary>選んでいるボタンの位置。まだどこも選んでいなければ -1。</summary>
        public int FocusIndex => _focusIndex;

        /// <summary>設定画面の差し込み口。<see cref="Bind"/> の前は null。</summary>
        public VisualElement SettingsSlot => _settingsSlot;

        /// <summary>設定画面を開いているか。</summary>
        public bool IsSettingsOpen => _settingsOpen;

        /// <summary>ロードアウトの差し込み口。<see cref="Bind"/> の前は null。</summary>
        public VisualElement LoadoutSlot => _loadoutSlot;

        /// <summary>出しているロードアウトの選択。閉じていれば null。</summary>
        public LoadoutSelectPanel LoadoutPanel => _loadoutPanel;

        /// <summary>ロードアウトの選択を開いているか。</summary>
        public bool IsLoadoutOpen => _loadoutPanel != null;

        /// <summary>スロットのルールと既定の武器を引くロードアウト。MenuSceneBuilder がシーンに配線する。</summary>
        public LoadoutDefinition Loadout
        {
            get => _loadout;
            set => _loadout = value;
        }

        /// <summary>選べる武器の一覧。MenuSceneBuilder がシーンに配線する。</summary>
        public WeaponCatalogAsset Catalog
        {
            get => _catalog;
            set => _catalog = value;
        }

        /// <summary>キャンペーンのミッションの一覧。MenuSceneBuilder がシーンに配線する。</summary>
        public MissionCatalog Missions
        {
            get => _missions;
            set => _missions = value;
        }

        /// <summary>
        /// キャンペーンのセーブの置き場所。CampaignRunner と同じ <see cref="FileSaveStorage.CreateDefault"/> を、初めて使うときに作る。
        /// null を入れると既定に戻す (テストで差し替える)。
        /// </summary>
        public ISaveStorage SaveStorage
        {
            get => _saveStorage ??= FileSaveStorage.CreateDefault();
            set => _saveStorage = value;
        }

        /// <summary>キャンペーンの選択の差し込み口。<see cref="Bind"/> の前は null。</summary>
        public VisualElement CampaignSlot => _campaignSlot;

        /// <summary>出しているキャンペーンの選択。閉じていれば null。</summary>
        public CampaignSelectPanel CampaignPanel => _campaignPanel;

        /// <summary>キャンペーンの選択を開いているか。</summary>
        public bool IsCampaignOpen => _campaignPanel != null;

        /// <summary>選んだロードアウトの保存先。null を入れると既定のキーに戻す (テストで差し替える)。</summary>
        public LoadoutPreferenceStore LoadoutStore
        {
            get => _loadoutStore;
            set => _loadoutStore = value ?? new LoadoutPreferenceStore();
        }

        // 設定・ロードアウト・キャンペーンのどれかを開いている間は、メニューのボタンとその移動を止める
        private bool IsOverlayOpen => _settingsOpen || _loadoutPanel != null || _campaignPanel != null;

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
            // 設定画面を開いたままシーンを離れても、変えた値を失わない。
            CloseSettings();
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
            InstallSettingsView(new SettingsView());
            ShowNotice(MenuNavigator.TakeNotice());
            FocusAt(0);
        }

        /// <summary>
        /// 設定ボタンで view を差し込み口に開き、「戻る」・キャンセルで閉じるようにつなぐ。閉じると view が保存する。
        /// <see cref="Show"/> が呼ぶ。既に取り付けていれば何もしない (1 つの画面に設定画面は 1 つ)。
        /// </summary>
        /// <exception cref="InvalidOperationException">別の設定画面を取り付け済み。</exception>
        public void InstallSettingsView(SettingsView view)
        {
            if (view == null)
            {
                throw new ArgumentNullException(nameof(view));
            }

            if (_settingsView == view)
            {
                return;
            }

            if (_settingsView != null)
            {
                throw new InvalidOperationException("a settings view is already installed on the main menu");
            }

            _settingsView = view;
            SettingsRequested += view.Open;
            SettingsClosed += () => view.Close();
            view.BackRequested += CloseSettings;
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

            // 前の木で開いていた設定画面を、保存させてから手放す。
            CloseSettings();
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
            _loadoutSlot = root.Q<VisualElement>(LoadoutSlotName)
                ?? throw new InvalidOperationException($"the menu layout has no element named '{LoadoutSlotName}'");
            _campaignSlot = root.Q<VisualElement>(CampaignSlotName)
                ?? throw new InvalidOperationException($"the menu layout has no element named '{CampaignSlotName}'");

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

            _texts = LocalizedTexts.Bind(root);
            Localizer.Changed += HandleLanguageChanged;
            _unbind.Add(() => Localizer.Changed -= HandleLanguageChanged);

            _focusIndex = -1;
            _settingsOpen = false;
            _leaving = false;
            SetDisplayed(_settingsSlot, false);
            SetDisplayed(_loadoutSlot, false);
            SetDisplayed(_campaignSlot, false);
            ShowNotice(null);
        }

        /// <summary>
        /// 選んでいるボタンを step だけ動かす (<see cref="MenuRoute.MoveFocus"/>)。設定画面・ロードアウトの選択を開いている間は動かさない。
        /// </summary>
        public void MoveFocus(int step)
        {
            if (IsOverlayOpen || _buttons.Count == 0)
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
            if (_leaving || IsOverlayOpen)
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
                ShowNotice(SceneMissingNotice(MenuRoute.Plan(destination).ScenePath));
                FocusAt(_focusIndex);
            }

            return went;
        }

        /// <summary>
        /// 設定画面の差し込み口を空にして出し、<see cref="SettingsRequested"/> に渡す。購読が無ければ案内を出すだけ。
        /// </summary>
        public void OpenSettings()
        {
            if (_settingsSlot == null || IsOverlayOpen || _leaving)
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
            RaiseSettingsClosed();
            if (_settingsSlot == null)
            {
                return;
            }

            _settingsSlot.Clear();
            SetDisplayed(_settingsSlot, false);
            SetButtonsEnabled(true);
            FocusAt(IndexOf(SettingsButtonName));
        }

        /// <summary>
        /// ロードアウトの選択を差し込み口に出す。最初に選んでおく並びは保存した並び (<see cref="LoadoutStore"/>)、
        /// なければ <see cref="Loadout"/> の既定の武器。アセットが未設定・不正なら理由を案内に出して開かない。
        /// 決めた並びは保存して閉じ、Cancel では保存せずに閉じる。
        /// </summary>
        public void OpenLoadout()
        {
            if (_loadoutSlot == null || IsOverlayOpen || _leaving)
            {
                return;
            }

            if (!LoadoutSelectionAssets.TryCreate(_loadout, _catalog, _loadoutStore.Load(), null, out var selection, out var error))
            {
                Debug.LogWarning(LoadoutPreferenceStore.LogTag + " cannot open the loadout screen: " + error, this);
                ShowNotice(LoadoutUnavailableNotice(error));
                return;
            }

            ShowNotice(null);
            SetDisplayed(_loadoutSlot, true);
            SetButtonsEnabled(false);
            _loadoutPanel = new LoadoutSelectPanel(_loadoutSlot, selection);
            _loadoutPanel.Confirmed += SaveLoadout;
            _loadoutPanel.Cancelled += CloseLoadout;
        }

        /// <summary>ロードアウトの選択を閉じて差し込み口を空にし、ロードアウトのボタンにフォーカスを戻す。開いていなければ何もしない。</summary>
        public void CloseLoadout()
        {
            if (_loadoutPanel == null)
            {
                return;
            }

            _loadoutPanel.Dispose();
            _loadoutPanel = null;
            SetDisplayed(_loadoutSlot, false);
            SetButtonsEnabled(true);
            FocusAt(IndexOf(LoadoutButtonName));
        }

        private void SaveLoadout(IReadOnlyList<string> weaponIds)
        {
            try
            {
                _loadoutStore.Save(weaponIds);
            }
            catch (ArgumentException e)
            {
                // Validate を通った並びは空の Id を持たないので、ここに来るのは選択の側の誤り
                Debug.LogError(LoadoutPreferenceStore.LogTag + " could not save the loadout: " + e.Message, this);
                _loadoutPanel?.ShowStatus(LoadoutFormat.Rejected(e.Message), true);
                return;
            }

            Debug.Log(LoadoutPreferenceStore.LogTag + " saved " + string.Join(",", weaponIds), this);
            CloseLoadout();
            ShowNotice(LoadoutFormat.Saved);
        }

        /// <summary>
        /// キャンペーンの選択を差し込み口に出す。ミッションは <see cref="Missions"/> の順に並べ、<see cref="SaveStorage"/> に再開できるセーブがあるものには
        /// 「続きから」と「最初から」を出す。最初に選んでおくミッションと難易度は前に始めたもの (<see cref="CampaignLaunchSelection.Current"/>)、無ければ先頭と Normal。
        /// カタログが未設定・不正なら理由を案内に出して開かない。
        /// </summary>
        public void OpenCampaign()
        {
            if (_campaignSlot == null || IsOverlayOpen || _leaving)
            {
                return;
            }

            if (!TryCreateCampaignMenu(out var menu, out var error))
            {
                Debug.LogWarning(MenuNavigator.LogTag + " cannot open the campaign: " + error, this);
                ShowNotice(CampaignMenuFormat.Unavailable(error));
                return;
            }

            ShowNotice(null);
            SetDisplayed(_campaignSlot, true);
            SetButtonsEnabled(false);
            _campaignPanel = new CampaignSelectPanel(_campaignSlot, menu);
            _campaignPanel.StartRequested += StartCampaign;
            _campaignPanel.Cancelled += CloseCampaign;
        }

        /// <summary>キャンペーンの選択を閉じて差し込み口を空にし、キャンペーンのボタンにフォーカスを戻す。開いていなければ何もしない。</summary>
        public void CloseCampaign()
        {
            if (_campaignPanel == null)
            {
                return;
            }

            _campaignPanel.Dispose();
            _campaignPanel = null;
            SetDisplayed(_campaignSlot, false);
            SetButtonsEnabled(true);
            FocusAt(IndexOf(CampaignButtonName));
        }

        private bool TryCreateCampaignMenu(out CampaignMenu menu, out string error)
        {
            menu = null;
            if (_missions == null)
            {
                error = "no mission catalog is wired to the menu; rerun Fps/Build Mission Catalog and Fps/Build Main Menu Scene";
                return false;
            }

            try
            {
                _missions.Validate();
            }
            catch (MissionCatalogException e)
            {
                error = e.Message;
                return false;
            }

            var storage = SaveStorage;
            var missions = new List<CampaignMenuMission>(_missions.Missions.Count);
            foreach (var mission in _missions.Missions)
            {
                var hasSave = ProbeSave(storage, mission.Id);
                missions.Add(new CampaignMenuMission(mission.Id, mission.DisplayName, hasSave));
            }

            var last = CampaignLaunchSelection.Current;
            menu = new CampaignMenu(missions, last?.Difficulty ?? CampaignDifficulty.Normal, last?.MissionId);
            error = null;
            return true;
        }

        private bool ProbeSave(ISaveStorage storage, string missionId)
        {
            if (CampaignSaveLookup.TryReadResumable(storage, missionId, out var save, out var problem))
            {
                var checkpoint = save.CheckpointId.Length > 0 ? save.CheckpointId : "(start)";
                Debug.Log($"{CampaignRunner.LogTag} mission={missionId} can continue from checkpoint={checkpoint}", this);
                return true;
            }

            if (problem != null)
            {
                // CampaignRunner もこのセーブを無視して最初から始めるので、「続きから」は出さない
                Debug.LogWarning($"{CampaignRunner.LogTag} mission={missionId} has a save that cannot be resumed: {problem}", this);
            }

            return false;
        }

        private void StartCampaign(CampaignMenuStart start)
        {
            if (_campaignPanel == null || _leaving)
            {
                return;
            }

            var missionId = start.Launch.MissionId;
            Action beforeLoad = null;
            if (start.DeletesSave)
            {
                beforeLoad = () =>
                {
                    CampaignSaveLookup.Delete(SaveStorage, missionId);
                    Debug.Log($"{CampaignRunner.LogTag} deleted the save of mission={missionId} to start from the beginning", this);
                };
            }

            _leaving = true;
            _campaignPanel.SetBusy(true);
            Debug.Log($"{MenuNavigator.LogTag} campaign start {start}", this);
            string failure;
            try
            {
                failure = MenuNavigator.StartCampaign(_missions, start.Launch, beforeLoad)
                    ? null
                    : CampaignMenuFormat.SceneMissing(_campaignPanel.Menu.SelectedMission.DisplayName);
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                Debug.LogError($"{CampaignRunner.LogTag} could not delete the save of mission={missionId}: {e.Message}", this);
                failure = CampaignMenuFormat.DeleteFailed(e.Message);
            }
            catch (Exception e) when (e is MissionCatalogException || e is ArgumentException || e is InvalidOperationException)
            {
                Debug.LogError($"{MenuNavigator.LogTag} could not start mission={missionId}: {e.Message}", this);
                failure = CampaignMenuFormat.CannotStart(e.Message);
            }

            if (failure == null)
            {
                return;
            }

            _leaving = false;

            // 消している途中で失敗しても、残ったセーブの有無に合わせて「続きから」を出し直す
            if (start.DeletesSave && !ProbeSave(SaveStorage, missionId))
            {
                _campaignPanel.Menu.ForgetSave(missionId);
            }

            _campaignPanel.SetBusy(false);
            _campaignPanel.ShowStatus(failure, true);
        }

        private void RaiseSettingsClosed()
        {
            var handlers = SettingsClosed;
            if (handlers == null)
            {
                return;
            }

            // 保存に失敗した購読先があっても、メニューは閉じた状態に戻す。
            foreach (var handler in handlers.GetInvocationList())
            {
                try
                {
                    ((Action)handler)();
                }
                catch (Exception e)
                {
                    Debug.LogException(e, this);
                }
            }
        }

        /// <summary>案内を出す。null か空なら隠す。</summary>
        public void ShowNotice(string text)
        {
            _noticeSource = default;
            ShowNoticeText(text);
        }

        /// <summary>案内を今の言語 (<see cref="Localizer.Resolve"/>) の文にして出し、言語が変わったら文にし直す。文にして空なら隠す。</summary>
        public void ShowNotice(LocalizedText text)
        {
            _noticeSource = text;
            ShowNoticeText(Localizer.Resolve(text));
        }

        private void ShowNoticeText(string text)
        {
            if (_notice == null)
            {
                return;
            }

            var shown = !string.IsNullOrWhiteSpace(text);
            _notice.text = shown ? text : "";
            SetDisplayed(_notice, shown);
        }

        // 言語は設定画面からしか変わらず、設定画面は自分で組み直すので、ここではメニューのボタンなど UXML の文字と、
        // 表のキーで受け取った案内 (切断の理由など) だけを書き直す。
        private void HandleLanguageChanged(GameLanguage language)
        {
            if (this == null)
            {
                return;
            }

            _texts?.Refresh();
            if (!_noticeSource.IsEmpty)
            {
                ShowNoticeText(Localizer.Resolve(_noticeSource));
            }
        }

        private void Press(string buttonName)
        {
            switch (buttonName)
            {
                case CampaignButtonName:
                    OpenCampaign();
                    break;
                case CreateLobbyButtonName:
                    Choose(MenuDestination.CreateLobby);
                    break;
                case JoinLobbyButtonName:
                    Choose(MenuDestination.JoinLobby);
                    break;
                case QuickMatchButtonName:
                    Choose(MenuDestination.QuickMatch);
                    break;
                case LoadoutButtonName:
                    OpenLoadout();
                    break;
                case RangeButtonName:
                    Choose(MenuDestination.Range);
                    break;
                case TutorialButtonName:
                    Choose(MenuDestination.Tutorial);
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
            // 設定画面・ロードアウトの選択の中の移動はそれぞれに任せる
            if (IsOverlayOpen)
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

            // 設定画面で割り当ての入力を待っている間の Esc / Start は、待つのをやめるためのもの。
            if (PlayerBindings.IsCapturingInput)
            {
                return;
            }

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
                    if (_focusIndex == index && !IsOverlayOpen)
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
            if (_loadoutPanel != null)
            {
                _loadoutPanel.Dispose();
                _loadoutPanel = null;
            }

            if (_campaignPanel != null)
            {
                _campaignPanel.Dispose();
                _campaignPanel = null;
            }

            foreach (var undo in _unbind)
            {
                undo();
            }

            _unbind.Clear();
            _buttons.Clear();
            _texts = null;
            _navigationRoot = null;
            _notice = null;
            _noticeSource = default;
            _settingsSlot = null;
            _loadoutSlot = null;
            _campaignSlot = null;
        }

        private static void SetDisplayed(VisualElement element, bool displayed)
        {
            element.style.display = displayed ? DisplayStyle.Flex : DisplayStyle.None;
        }
    }
}
