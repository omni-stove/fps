using System;
using System.Collections.Generic;
using System.IO;
using Shooter.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace Shooter.Runtime
{
    // マウスで指したボタンにもフォーカスを移すのは、どの入力でも選んでいるボタンを 1 つにするため。
    [DisallowMultipleComponent]
    public sealed class MainMenuScreen : MonoBehaviour
    {
        public const string LayoutResourcePath = "Menu/MainMenu";

        public const string ThemeResourcePath = Hud.ThemeResourcePath;

        public const string PanelSettingsName = "MainMenuPanelSettings";
        public const string DocumentObjectName = "MainMenuDocument";

        public const string CampaignButtonName = "menu-campaign";
        public const string QuickMatchButtonName = "menu-quick-match";
        public const string LoadoutButtonName = "menu-loadout";
        public const string ProfileButtonName = "menu-profile";
        public const string RangeButtonName = "menu-range";
        public const string TutorialButtonName = "menu-tutorial";
        public const string SettingsButtonName = "menu-settings";
        public const string QuitButtonName = "menu-quit";
        public const string NoticeName = "menu-notice";
        public const string SettingsSlotName = "menu-settings-slot";
        public const string LoadoutSlotName = "menu-loadout-slot";
        public const string CampaignSlotName = "menu-campaign-slot";
        public const string ProfileSlotName = "menu-profile-slot";

        public static string SettingsUnavailableNotice => Localizer.Get("menu.settingsUnavailable");

        public static string ProfileUnavailableNotice => Localizer.Get("menu.profileUnavailable");

        public static string LoadoutUnavailableNotice(string reason)
        {
            return Localizer.Format("menu.loadoutUnavailable", reason);
        }

        public static string SceneMissingNotice(string scenePath)
        {
            return Localizer.Format("menu.sceneMissing", scenePath);
        }

        public static readonly IReadOnlyList<string> ButtonNames = new[]
        {
            CampaignButtonName,
            QuickMatchButtonName,
            LoadoutButtonName,
            ProfileButtonName,
            RangeButtonName,
            TutorialButtonName,
            SettingsButtonName,
            QuitButtonName,
        };

        [SerializeField, Tooltip("The online loadout (Assets/game/Weapons/DefaultLoadout.asset); the slot rules and default weapons come from it. Empty uses the GameContent one")]
        private LoadoutDefinition _loadout;

        [SerializeField, Tooltip("The weapons that can be chosen online (Assets/game/Weapons/WeaponCatalog.asset). Empty uses the GameContent one")]
        private WeaponCatalogAsset _catalog;

        [SerializeField, Tooltip("The campaign missions (Assets/game/Missions/MissionCatalog.asset). Empty uses the GameContent one")]
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
        private VisualElement _profileSlot;
        private ProfilePanel _profilePanel;
        private ProgressionRecorder _progression;
        private ISaveStorage _saveStorage;
        private Label _notice;
        private LocalizedText _noticeSource;
        private int _focusIndex = -1;
        private bool _settingsOpen;
        private bool _leaving;
        private SettingsView _settingsView;
        private LocalizedTexts _texts;

        public event Action<VisualElement> SettingsRequested;

        public event Action SettingsClosed;

        public SettingsView SettingsView => _settingsView;

        public IReadOnlyList<Button> Buttons => _buttons;

        public int FocusIndex => _focusIndex;

        public VisualElement SettingsSlot => _settingsSlot;

        public bool IsSettingsOpen => _settingsOpen;

        public VisualElement LoadoutSlot => _loadoutSlot;

        public LoadoutSelectPanel LoadoutPanel => _loadoutPanel;

        public bool IsLoadoutOpen => _loadoutPanel != null;

        public LoadoutDefinition Loadout
        {
            get => _loadout;
            set => _loadout = value;
        }

        public WeaponCatalogAsset Catalog
        {
            get => _catalog;
            set => _catalog = value;
        }

        public MissionCatalog Missions
        {
            get => _missions;
            set => _missions = value;
        }

        public LoadoutDefinition EffectiveLoadout => GameContent.Pick(_loadout, c => c.Loadout);

        public WeaponCatalogAsset EffectiveCatalog => GameContent.Pick(_catalog, c => c.WeaponCatalog);

        public MissionCatalog EffectiveMissions => GameContent.Pick(_missions, c => c.Missions);

        public ISaveStorage SaveStorage
        {
            get => _saveStorage ??= FileSaveStorage.CreateDefault();
            set => _saveStorage = value;
        }

        public VisualElement CampaignSlot => _campaignSlot;

        public CampaignSelectPanel CampaignPanel => _campaignPanel;

        public bool IsCampaignOpen => _campaignPanel != null;

        public LoadoutPreferenceStore LoadoutStore
        {
            get => _loadoutStore;
            set => _loadoutStore = value ?? new LoadoutPreferenceStore();
        }

        public ProgressionRecorder Progression
        {
            get => _progression ?? ProgressionBootstrap.Recorder;
            set => _progression = value;
        }

        public VisualElement ProfileSlot => _profileSlot;

        public ProfilePanel ProfilePanel => _profilePanel;

        public bool IsProfileOpen => _profilePanel != null;

        private bool IsOverlayOpen => _settingsOpen || _loadoutPanel != null || _campaignPanel != null || _profilePanel != null;

        public string Notice => _notice != null && _notice.style.display != DisplayStyle.None ? _notice.text : null;

        public PanelSettings OwnedPanelSettings => _ownedPanelSettings;

        private void Awake()
        {
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
                    $"menu layout 'Resources/{LayoutResourcePath}.uxml' was not found (expected under UI/Resources of the Demo sample)");
            }

            var theme = Resources.Load<ThemeStyleSheet>(ThemeResourcePath);
            if (theme == null)
            {
                throw new InvalidOperationException(
                    $"theme 'Resources/{ThemeResourcePath}.tss' was not found (expected under UI/Resources of the Demo sample)");
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
            ShowReturnReason(MenuNavigator.TakeReturnReason());
            FocusAt(0);
        }

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
            _profileSlot = root.Q<VisualElement>(ProfileSlotName)
                ?? throw new InvalidOperationException($"the menu layout has no element named '{ProfileSlotName}'");

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
            SetDisplayed(_profileSlot, false);
            ShowNotice(null);
        }

        public void MoveFocus(int step)
        {
            if (IsOverlayOpen || _buttons.Count == 0)
            {
                return;
            }

            FocusAt(MenuRoute.MoveFocus(_focusIndex, step, _buttons.Count));
        }

        // 進めたらボタンを押せなくするのは、シーンを読み終えるまでの二度押しを防ぐため。
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
            LocalizedText refusal;
            try
            {
                went = MenuNavigator.Go(destination, out refusal);
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
                if (refusal.IsEmpty)
                {
                    ShowNotice(SceneMissingNotice(MenuNavigator.Plan(destination).ScenePath));
                }
                else
                {
                    ShowNotice(refusal);
                }

                FocusAt(_focusIndex);
            }

            return went;
        }

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

        public void OpenLoadout()
        {
            if (_loadoutSlot == null || IsOverlayOpen || _leaving)
            {
                return;
            }

            var progression = Progression;
            Func<string, bool> isAvailable = progression != null ? progression.IsUnlocked : (Func<string, bool>)null;
            if (!LoadoutSelectionAssets.TryCreate(
                    EffectiveLoadout, EffectiveCatalog, _loadoutStore.Load(), null, out var selection, out var error, isAvailable))
            {
                Debug.LogWarning(LoadoutPreferenceStore.LogTag + " cannot open the loadout screen: " + error, this);
                ShowNotice(LoadoutUnavailableNotice(error));
                return;
            }

            ShowNotice(null);
            SetDisplayed(_loadoutSlot, true);
            SetButtonsEnabled(false);
            _loadoutPanel = new LoadoutSelectPanel(_loadoutSlot, selection, progression?.Ledger);
            _loadoutPanel.Confirmed += SaveLoadout;
            _loadoutPanel.Cancelled += CloseLoadout;
        }

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

        public void OpenProfile()
        {
            if (_profileSlot == null || IsOverlayOpen || _leaving)
            {
                return;
            }

            var progression = Progression;
            if (progression == null)
            {
                Debug.LogWarning(MenuNavigator.LogTag + " cannot open the profile: no progression recorder is installed", this);
                ShowNotice(ProfileUnavailableNotice);
                return;
            }

            ShowNotice(null);
            SetDisplayed(_profileSlot, true);
            SetButtonsEnabled(false);
            _profilePanel = new ProfilePanel(_profileSlot, progression.Ledger, progression.Profile, ProfileWeapons(), KitTuning.Items);
            _profilePanel.Cancelled += CloseProfile;
        }

        public void CloseProfile()
        {
            if (_profilePanel == null)
            {
                return;
            }

            _profilePanel.Dispose();
            _profilePanel = null;
            SetDisplayed(_profileSlot, false);
            SetButtonsEnabled(true);
            FocusAt(IndexOf(ProfileButtonName));
        }

        // 武器の一覧が壊れていてもプロフィールは出せるので、名前の代わりに Id を出す (ProfilePanel は一覧が無ければ Id を出す)
        private WeaponCatalog ProfileWeapons()
        {
            var catalog = EffectiveCatalog;
            if (catalog == null)
            {
                return null;
            }

            try
            {
                return catalog.ToCatalog();
            }
            catch (WeaponDefinitionException e)
            {
                Debug.LogWarning(MenuNavigator.LogTag + " the profile shows unlock ids because the weapon catalog is invalid: " + e.Message, this);
                return null;
            }
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
            var catalog = EffectiveMissions;
            if (catalog == null)
            {
                error = "no mission catalog is wired to the menu or set in the GameContent; rerun Shooter/Build Mission Catalog and Shooter/Build Main Menu Scene";
                return false;
            }

            try
            {
                catalog.Validate();
            }
            catch (MissionCatalogException e)
            {
                error = e.Message;
                return false;
            }

            var storage = SaveStorage;
            var missions = new List<CampaignMenuMission>(catalog.Missions.Count);
            foreach (var mission in catalog.Missions)
            {
                var hasSave = ProbeSave(storage, mission.Id);
                missions.Add(new CampaignMenuMission(mission.Id, Localizer.Resolve(DisplayNames.Mission(mission.DisplayNameKey)), hasSave));
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
                failure = MenuNavigator.StartCampaign(EffectiveMissions, start.Launch, beforeLoad)
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

        public void ShowNotice(string text)
        {
            _noticeSource = default;
            ShowNoticeText(text);
        }

        public void ShowNotice(LocalizedText text)
        {
            _noticeSource = text;
            ShowNoticeText(Localizer.Resolve(text));
        }

        public void ShowReturnReason(MenuReturnReason reason)
        {
            ShowNotice(DisconnectNoticeText.For(reason));
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
                case QuickMatchButtonName:
                    Choose(MenuDestination.QuickMatch);
                    break;
                case LoadoutButtonName:
                    OpenLoadout();
                    break;
                case ProfileButtonName:
                    OpenProfile();
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

            if (_profilePanel != null)
            {
                _profilePanel.Dispose();
                _profilePanel = null;
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
            _profileSlot = null;
        }

        private static void SetDisplayed(VisualElement element, bool displayed)
        {
            element.style.display = displayed ? DisplayStyle.Flex : DisplayStyle.None;
        }
    }
}
