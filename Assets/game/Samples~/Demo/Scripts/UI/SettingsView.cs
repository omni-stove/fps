using System;
using System.Collections.Generic;
using System.Globalization;
using Shooter.Core;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace Shooter.Runtime
{
    // 値はすぐ効かせるが、スライダーを動かすたびにディスクへ書かないよう、保存は閉じるときにまとめる。
    public sealed class SettingsView
    {
        public const string LogTag = AppliedControlSettings.LogTag;

        public const string StyleResourcePath = "Menu/Settings";

        public const string RootName = "settings";

        public const string TitleName = "settings-title";

        public const string LanguageButtonName = "settings-language";

        public const string BackButtonName = "settings-back";
        public const string ResetButtonName = "settings-reset";
        public const string BindingsResetButtonName = "settings-bindings-reset";
        public const string RebindNoticeName = "settings-rebind-notice";
        public const string RebindCancelButtonName = "settings-rebind-cancel";
        public const string RebindUndoButtonName = "settings-rebind-undo";
        public const string RebindKeepButtonName = "settings-rebind-keep";

        public const string PaletteButtonName = "settings-palette";

        public const string PaletteFriendlySampleName = "settings-palette-friendly";

        public const string PaletteEnemySampleName = "settings-palette-enemy";

        public const string SubtitlesButtonName = "settings-subtitles";

        public const string BloodButtonName = "settings-blood";

        public const string AimAssistButtonName = "settings-aim-assist";

        public const string InvertLookYButtonName = "settings-invert-look-y";

        public const string RumbleButtonName = "settings-rumble";

        public const string RegionButtonName = "settings-region";

        public const string TelemetryButtonName = "settings-telemetry";

        public const string RegionTextKeyPrefix = "settings.region.";

        public const string VoiceMicrophoneButtonName = "settings-voice-microphone";

        public const string VoiceVolumeSliderName = "settings-voice-volume";

        public const string VoiceVolumeValueName = VoiceVolumeSliderName + "-value";

        public static string OnText => Localizer.Get("settings.on");

        public static string OffText => Localizer.Get("settings.off");

        public static string FriendlySampleText => Localizer.Get("settings.palette.friendlySample");

        public static string EnemySampleText => Localizer.Get("settings.palette.enemySample");

        public static string HoldText => Localizer.Get("settings.mode.hold");

        public static string ToggleText => Localizer.Get("settings.mode.toggle");

        public static string WaitingText => Localizer.Get("settings.rebind.waiting");

        public const string NotRebindableText = "—";

        private static readonly InputDeviceKind[] _devices = { InputDeviceKind.KeyboardMouse, InputDeviceKind.Gamepad };
        private static readonly HoldAction[] _actions = { HoldAction.Sprint, HoldAction.Crouch, HoldAction.Aim };

        // 視点と音の見出しではなく、血の表示と同じ演出の見出しに並べる数値。
        private static readonly ControlValue[] _effectValues = { ControlValue.CameraShakeIntensity, ControlValue.ViewmodelMotionIntensity };

        // 視点と音の見出しではなく、振動の On/Off と同じゲームパッドの見出しに並べる数値。
        private static readonly ControlValue[] _gamepadValues = { ControlValue.StickDeadzone, ControlValue.RumbleStrength };

        private static bool _warnedMissingStyle;

        private readonly ControlSettingsStore _store;
        private readonly InputBindingOverridesStore _bindingStore;
        private readonly InputActionAsset _actionAsset;
        private readonly ISettingsSink _sink;
        private readonly LanguagePreferenceStore _languageStore;
        private readonly Dictionary<ControlValue, SliderInt> _sliders = new Dictionary<ControlValue, SliderInt>();
        private readonly Dictionary<ControlValue, Label> _valueLabels = new Dictionary<ControlValue, Label>();
        private readonly Dictionary<string, Button> _modeButtons = new Dictionary<string, Button>(StringComparer.Ordinal);
        private readonly Dictionary<RebindSlot, Button> _rebindButtons = new Dictionary<RebindSlot, Button>();
        private VisualElement _root;
        private ScrollView _scroll;
        private ControlSettings _editing;
        private ControlSettings _saved;
        private PlayerBindings _bindings;
        private bool _bindingsDirty;
        private RebindResult _pendingConflict;
        private VisualElement _rebindNoticeRow;
        private Label _rebindNotice;
        private Button _rebindCancelButton;
        private Button _rebindUndoButton;
        private Button _rebindKeepButton;
        private Button _paletteButton;
        private Label _friendlySample;
        private Label _enemySample;
        private Button _subtitlesButton;
        private Button _bloodButton;
        private Button _aimAssistButton;
        private Button _invertLookYButton;
        private Button _rumbleButton;
        private Button _regionButton;
        private Button _telemetryButton;
        private readonly IReadOnlyList<string> _regionsGiven;
        private IReadOnlyList<string> _regions;
        private Button _voiceMicrophoneButton;
        private SliderInt _voiceVolumeSlider;
        private Label _voiceVolumeLabel;
        private readonly VoiceSettingsStore _voiceStore;
        private VoiceSettings _voiceEditing;
        private VoiceSettings _voiceSaved;
        private Button _languageButton;
        private IReadOnlyList<GameLanguage> _languages = Array.Empty<GameLanguage>();
        private readonly Dictionary<GameLanguage, string> _languageNames = new Dictionary<GameLanguage, string>();

        public SettingsView(
            ControlSettingsStore store = null,
            InputBindingOverridesStore bindingStore = null,
            InputActionAsset actions = null,
            ISettingsSink sink = null,
            LanguagePreferenceStore languageStore = null,
            VoiceSettingsStore voiceStore = null,
            IReadOnlyList<string> regions = null)
        {
            List<string> offered = null;
            if (regions != null)
            {
                offered = new List<string>();
                foreach (var region in regions)
                {
                    if (!ControlSettings.IsValidRegion(region) || region == ControlSettings.AutoRegion || offered.Contains(region))
                    {
                        throw new ArgumentException($"'{region}' is not a region that can be offered", nameof(regions));
                    }

                    offered.Add(region);
                }
            }

            _store = store ?? new ControlSettingsStore();
            _bindingStore = bindingStore ?? new InputBindingOverridesStore();
            _actionAsset = actions;
            _sink = sink ?? AppliedControlSettings.Sink;
            _languageStore = languageStore ?? new LanguagePreferenceStore();
            _voiceStore = voiceStore ?? new VoiceSettingsStore();
            _regionsGiven = offered?.AsReadOnly();
        }

        public event Action BackRequested;

        public ControlSettingsStore Store => _store;

        public LanguagePreferenceStore LanguageStore => _languageStore;

        public VoiceSettingsStore VoiceStore => _voiceStore;

        public VoiceSettings Voice => _voiceEditing;

        public bool IsOpen => _root != null;

        public VisualElement Root => _root;

        public ControlSettings Settings => _editing;

        public InputBindingOverridesStore BindingStore => _bindingStore;

        public PlayerBindings Bindings => _bindings;

        public IReadOnlyList<GameLanguage> Languages => _languages;

        // Refresh はスライダーを動かすたびに走るので、Resources の一覧は最初に要るときに 1 度だけ読む。
        public IReadOnlyList<string> Regions => _regions ??= _regionsGiven ?? QuickMatchRegionList.Load();

        public RebindResult PendingConflict => _pendingConflict;

        public bool HasUnsavedChanges =>
            _editing != null && (!_editing.Equals(_saved) || _bindingsDirty || !_voiceEditing.Equals(_voiceSaved));

        public static string RebindButtonName(RebindSlot slot)
        {
            var name = "settings-bind-" + Slug(slot.Device) + "-" + slot.ActionName.ToLowerInvariant();
            return slot.Part == null ? name : name + "-" + slot.Part.ToLowerInvariant();
        }

        public static string FormatConflict(string display, IReadOnlyList<BindingConflict> conflicts)
        {
            if (conflicts == null || conflicts.Count == 0)
            {
                return string.Empty;
            }

            var labels = new List<string>();
            foreach (var conflict in conflicts)
            {
                var label = InputActionLabels.LabelOf(conflict);
                if (!labels.Contains(label))
                {
                    labels.Add(label);
                }
            }

            var joined = string.Join(Localizer.Get("settings.rebind.conflictSeparator"), labels);
            return Localizer.Format("settings.rebind.conflict", display, joined);
        }

        public static string SliderName(ControlValue value)
        {
            return "settings-" + Slug(value);
        }

        public static string ValueLabelName(ControlValue value)
        {
            return SliderName(value) + "-value";
        }

        public static string ModeButtonName(InputDeviceKind device, HoldAction action)
        {
            return "settings-mode-" + Slug(device) + "-" + Slug(action);
        }

        public static string FormatValue(ControlValue value, int amount)
        {
            switch (value)
            {
                case ControlValue.FieldOfView:
                    return amount.ToString(CultureInfo.InvariantCulture) + "°";
                case ControlValue.StickDeadzone:
                    return (amount / 10m).ToString("0.#", CultureInfo.InvariantCulture) + "%";
                default:
                    return amount.ToString(CultureInfo.InvariantCulture) + "%";
            }
        }

        public static string FormatVoiceVolume(int percent)
        {
            return percent.ToString(CultureInfo.InvariantCulture) + "%";
        }

        public static string FormatMode(HoldMode mode)
        {
            return mode == HoldMode.Toggle ? ToggleText : HoldText;
        }

        public static string FormatOnOff(bool on)
        {
            return on ? OnText : OffText;
        }

        public static string FormatPalette(ColorPalette palette)
        {
            switch (palette)
            {
                case ColorPalette.Standard:
                    return Localizer.Get("settings.palette.standard");
                case ColorPalette.RedGreen:
                    return Localizer.Get("settings.palette.redGreen");
                case ColorPalette.BlueYellow:
                    return Localizer.Get("settings.palette.blueYellow");
                default:
                    throw new ArgumentOutOfRangeException(nameof(palette), palette, "unknown color palette");
            }
        }

        // 今の言語が読めなくても選べるよう、その言語自身での名前を出す。
        public static string FormatLanguage(GameLanguage language)
        {
            return Localizer.NativeNameOf(language);
        }

        public static GameLanguage NextLanguage(GameLanguage language, IReadOnlyList<GameLanguage> available)
        {
            if (available == null)
            {
                throw new ArgumentNullException(nameof(available));
            }

            if (available.Count == 0)
            {
                return language;
            }

            for (var i = 0; i < available.Count; i++)
            {
                if (available[i] == language)
                {
                    return available[(i + 1) % available.Count];
                }
            }

            return available[0];
        }

        // 一覧に名前の無い地域は、サーバーが載せる名前をそのまま出せば選んだものと分かる。
        public static string FormatRegion(string region)
        {
            if (region == null)
            {
                throw new ArgumentNullException(nameof(region));
            }

            return Localizer.TryGet(RegionTextKeyPrefix + region, out var text) ? text : region;
        }

        // 一覧から消えた地域を保存していても押せば一覧の地域へ移れるよう、見つからなければ Auto の次 (一覧の先頭) にする。
        public static string NextRegion(string region, IReadOnlyList<string> available)
        {
            if (available == null)
            {
                throw new ArgumentNullException(nameof(available));
            }

            if (region == ControlSettings.AutoRegion)
            {
                return available.Count > 0 ? available[0] : ControlSettings.AutoRegion;
            }

            for (var i = 0; i < available.Count; i++)
            {
                if (available[i] == region)
                {
                    return i + 1 < available.Count ? available[i + 1] : ControlSettings.AutoRegion;
                }
            }

            return available.Count > 0 ? available[0] : ControlSettings.AutoRegion;
        }

        public static ColorPalette NextPalette(ColorPalette palette)
        {
            var all = TeamPalette.All;
            for (var i = 0; i < all.Count; i++)
            {
                if (all[i].Kind == palette)
                {
                    return all[(i + 1) % all.Count].Kind;
                }
            }

            return all[0].Kind;
        }

        public void Open(VisualElement container)
        {
            if (container == null)
            {
                throw new ArgumentNullException(nameof(container));
            }

            Close();

            _editing = _sink.Current;
            _saved = _editing;
            _voiceEditing = _voiceStore.Load();
            _voiceSaved = _voiceEditing;
            _bindings = CreateBindings();
            _bindingsDirty = false;
            _pendingConflict = null;
            _root = Build();
            container.Add(_root);
            Refresh();
            Localizer.Changed += HandleLanguageChanged;
            FocusFirst();
        }

        public bool Close()
        {
            if (_root == null)
            {
                return false;
            }

            _bindings?.CancelRebind();
            var saved = Save();
            Localizer.Changed -= HandleLanguageChanged;
            DetachElements();
            _editing = null;
            _saved = null;
            _voiceEditing = null;
            _voiceSaved = null;
            _bindings?.Dispose();
            _bindings = null;
            _bindingsDirty = false;
            _pendingConflict = null;
            return saved;
        }

        private void DetachElements()
        {
            _root.UnregisterCallback<NavigationCancelEvent>(HandleNavigationCancel);
            _root.RemoveFromHierarchy();
            _root = null;
            _scroll = null;
            _sliders.Clear();
            _valueLabels.Clear();
            _modeButtons.Clear();
            _rebindButtons.Clear();
            _rebindNoticeRow = null;
            _rebindNotice = null;
            _rebindCancelButton = null;
            _rebindUndoButton = null;
            _rebindKeepButton = null;
            _paletteButton = null;
            _friendlySample = null;
            _enemySample = null;
            _subtitlesButton = null;
            _bloodButton = null;
            _aimAssistButton = null;
            _invertLookYButton = null;
            _rumbleButton = null;
            _regionButton = null;
            _telemetryButton = null;
            _voiceMicrophoneButton = null;
            _voiceVolumeSlider = null;
            _voiceVolumeLabel = null;
            _languageButton = null;
            _languages = Array.Empty<GameLanguage>();
            _languageNames.Clear();
        }

        // 文字は組むときに入れたものが多く、1 つずつ引き直すと入れ忘れが出やすいので、画面ごと今の言語で組み直す。
        private void HandleLanguageChanged(GameLanguage language)
        {
            if (_root == null)
            {
                return;
            }

            var container = _root.parent;
            var index = container?.IndexOf(_root) ?? -1;
            var focusedName = OwnNameOf(_root.focusController?.focusedElement as VisualElement);
            DetachElements();

            _root = Build();
            if (container != null)
            {
                container.Insert(Math.Min(index, container.childCount), _root);
            }

            Refresh();
            if (string.IsNullOrEmpty(focusedName) || !FocusLater(_root.Q<VisualElement>(focusedName)))
            {
                FocusFirst();
            }
        }

        public bool Save()
        {
            if (!HasUnsavedChanges)
            {
                return false;
            }

            var failed = false;

            // 開いている間に F3 で切り替えて保存した回線の表示を、開いたときの値で書き戻さない
            _editing = _editing.WithShowNetStats(_sink.Current.ShowNetStats);
            if (!_editing.Equals(_saved))
            {
                try
                {
                    _store.Save(_editing);
                    _saved = _editing;
                    Debug.Log($"{LogTag} saved {_editing.ToJson()}");
                }
                catch (Exception e)
                {
                    Debug.LogError($"{LogTag} could not save the settings to '{_store.Key}': {e.Message}");
                    Debug.LogException(e);
                    failed = true;
                }
            }

            if (!_voiceEditing.Equals(_voiceSaved))
            {
                try
                {
                    _voiceStore.Save(_voiceEditing);
                    _voiceSaved = _voiceEditing;
                    Debug.Log($"{LogTag} saved the voice settings {_voiceEditing.ToJson()}");
                }
                catch (Exception e)
                {
                    Debug.LogError($"{LogTag} could not save the voice settings to '{_voiceStore.Key}': {e.Message}");
                    Debug.LogException(e);
                    failed = true;
                }
            }

            if (_bindingsDirty && _bindings != null)
            {
                try
                {
                    _bindingStore.Save(_bindings.Asset);
                    _bindingsDirty = false;
                    Debug.Log($"{LogTag} saved the binding overrides to '{_bindingStore.Key}' (overrides={_bindings.HasOverrides})");
                }
                catch (Exception e)
                {
                    Debug.LogError($"{LogTag} could not save the binding overrides to '{_bindingStore.Key}': {e.Message}");
                    Debug.LogException(e);
                    failed = true;
                }
            }

            return !failed;
        }

        public void SetValue(ControlValue value, int amount)
        {
            EnsureOpen();
            Edit(_editing.With(value, amount));
        }

        public void SetMode(InputDeviceKind device, HoldAction action, HoldMode mode)
        {
            EnsureOpen();
            Edit(_editing.With(device, action, mode));
        }

        public void SetPalette(ColorPalette palette)
        {
            EnsureOpen();
            Edit(_editing.WithPalette(palette));
        }

        public void SetShowSubtitles(bool show)
        {
            EnsureOpen();
            Edit(_editing.WithShowSubtitles(show));
        }

        public void SetShowBlood(bool show)
        {
            EnsureOpen();
            Edit(_editing.WithShowBlood(show));
        }

        public void SetAimAssist(bool enabled)
        {
            EnsureOpen();
            Edit(_editing.WithAimAssist(enabled));
        }

        public void SetInvertLookY(bool invert)
        {
            EnsureOpen();
            Edit(_editing.WithInvertLookY(invert));
        }

        public void SetRumbleEnabled(bool enabled)
        {
            EnsureOpen();
            Edit(_editing.WithRumbleEnabled(enabled));
        }

        public void SetQuickMatchRegion(string region)
        {
            EnsureOpen();
            Edit(_editing.WithQuickMatchRegion(region));
        }

        public void SetTelemetryConsent(bool consent)
        {
            EnsureOpen();
            Edit(_editing.WithTelemetryConsent(consent));
        }

        public void SetMicrophoneEnabled(bool enabled)
        {
            EnsureOpen();
            EditVoice(_voiceEditing.WithMicrophoneEnabled(enabled));
        }

        public void SetVoiceVolume(int percent)
        {
            EnsureOpen();
            EditVoice(new VoiceSettings(_voiceEditing.MicrophoneEnabled, percent));
        }

        public void SetLanguage(GameLanguage language)
        {
            EnsureOpen();
            _ = GameLanguages.Code(language);
            if (language == Localizer.Language)
            {
                return;
            }

            try
            {
                Localizer.SwitchTo(language, _languageStore);
            }
            catch (Exception e)
            {
                // 言語はもう効いているので、画面はそのまま新しい言語で続ける。
                Debug.LogError($"{LogTag} could not save the language to '{_languageStore.Key}': {e.Message}");
                Debug.LogException(e);
            }

            // 言語の知らせを受けた側がこの画面を閉じていれば、組み直す画面は無い。
            if (_root == null)
            {
                return;
            }

            Refresh();
        }

        // 回線の表示 (ControlSettings.ShowNetStats) はこの画面に無いので、言語は読めない言語に戻って押した人が戻せなくならないよう、今のまま残す。
        public void ResetToDefaults()
        {
            EnsureOpen();
            EditVoice(VoiceSettings.Default);
            Edit(ControlSettings.Default);
        }

        public bool StartRebind(RebindSlot slot)
        {
            var bindings = EnsureBindings();
            if (bindings.IsRebinding)
            {
                return false;
            }

            _pendingConflict = null;
            if (!bindings.StartRebind(slot, HandleRebindFinished))
            {
                return false;
            }

            RefreshBindings();
            return true;
        }

        public void CancelRebind()
        {
            _bindings?.CancelRebind();
        }

        public RebindResult SetBinding(RebindSlot slot, string path)
        {
            var bindings = EnsureBindings();
            bindings.CancelRebind();

            var previousOverride = bindings.OverridePathOf(slot);
            var previousPath = bindings.PathOf(slot);
            bindings.ApplyOverride(slot, path);
            var current = bindings.PathOf(slot);
            var result = new RebindResult(slot, true, previousOverride, previousPath, current, bindings.FindConflicts(slot, current));
            HandleRebindFinished(result);
            return result;
        }

        public bool UndoRebind()
        {
            if (_pendingConflict == null || _bindings == null)
            {
                return false;
            }

            var result = _pendingConflict;
            _pendingConflict = null;
            _bindings.RestoreOverride(result.Slot, result.PreviousOverridePath);
            _bindingsDirty = true;
            Debug.Log($"{PlayerBindings.LogTag} undid {result.Slot} back to {_bindings.PathOf(result.Slot)}");
            RefreshBindings();
            Focus(result.Slot);
            return true;
        }

        public bool KeepRebind()
        {
            if (_pendingConflict == null)
            {
                return false;
            }

            var slot = _pendingConflict.Slot;
            _pendingConflict = null;
            RefreshBindings();
            Focus(slot);
            return true;
        }

        public void ResetBindingsToDefaults()
        {
            var bindings = EnsureBindings();
            var hadOverrides = bindings.HasOverrides;
            bindings.ResetAll();
            _pendingConflict = null;
            if (hadOverrides)
            {
                _bindingsDirty = true;
                Debug.Log($"{PlayerBindings.LogTag} reset every binding to its default");
            }

            RefreshBindings();
        }

        private void HandleRebindFinished(RebindResult result)
        {
            if (_root == null)
            {
                return;
            }

            if (result.Completed && !string.Equals(result.PreviousOverridePath, _bindings.OverridePathOf(result.Slot), StringComparison.Ordinal))
            {
                _bindingsDirty = true;
            }

            _pendingConflict = result.Completed && result.Conflicts.Count > 0 ? result : null;
            RefreshBindings();

            // 待っている間は案内に出したボタンへ移らないので、重なりがあれば決めるボタンへ、無ければ押したボタンへ戻す。
            if (_pendingConflict != null)
            {
                _rebindUndoButton?.Focus();
            }
            else
            {
                Focus(result.Slot);
            }
        }

        private PlayerBindings CreateBindings()
        {
            var asset = _actionAsset != null ? _actionAsset : InputSystem.actions;
            if (asset == null)
            {
                Debug.LogWarning(
                    $"{LogTag} project-wide Input Actions are not set (Project Settings > Input System Package); the bindings cannot be changed");
                return null;
            }

            try
            {
                return new PlayerBindings(asset);
            }
            catch (ArgumentException e)
            {
                Debug.LogError($"{LogTag} {e.Message}; the bindings cannot be changed");
                return null;
            }
        }

        private PlayerBindings EnsureBindings()
        {
            EnsureOpen();
            if (_bindings == null)
            {
                throw new InvalidOperationException("the settings view has no Input Actions to rebind");
            }

            return _bindings;
        }

        private void Focus(RebindSlot slot)
        {
            if (_rebindButtons.TryGetValue(slot, out var button))
            {
                button.Focus();
            }
        }

        private void Edit(ControlSettings settings)
        {
            // 回線の表示はこの画面に無く、開いている間にも F3 (Shooter.Net の NetHud) で切り替わるので、開いたときの値で戻さない
            settings = settings.WithShowNetStats(_sink.Current.ShowNetStats);
            if (settings.Equals(_editing))
            {
                return;
            }

            _editing = settings;
            _sink.Apply(settings);
            Refresh();
        }

        private void EditVoice(VoiceSettings settings)
        {
            if (settings.Equals(_voiceEditing))
            {
                return;
            }

            _voiceEditing = settings;
            RefreshVoice();
        }

        private VisualElement Build()
        {
            var root = new VisualElement { name = RootName };
            root.AddToClassList("settings");
            AddStyle(root);

            var title = NewLabel(Localizer.Get("settings.title"), "settings__title");
            title.name = TitleName;
            root.Add(title);

            // 割り当ての行を足すとポーズ画面の枠に収まらないので、見出しと下のボタン以外はスクロールさせる。
            _scroll = new ScrollView(ScrollViewMode.Vertical);
            _scroll.AddToClassList("settings__scroll");
            _scroll.RegisterCallback<FocusInEvent>(ScrollToFocused);
            root.Add(_scroll);
            var content = _scroll.contentContainer;

            content.Add(NewLabel(Localizer.Get("settings.section.viewAndSound"), "settings__section"));
            foreach (var value in ControlSettings.Values)
            {
                if (Array.IndexOf(_effectValues, value) < 0 && Array.IndexOf(_gamepadValues, value) < 0)
                {
                    content.Add(BuildSliderRow(value));
                }

                // 狙いに関わる設定なので、視野角や音量より前の、感度の並びのすぐ後に置く。
                if (value == ControlValue.AdsSensitivity)
                {
                    content.Add(BuildAimAssistRow());
                    content.Add(BuildInvertLookYRow());
                }
            }

            content.Add(NewLabel(Localizer.Get("settings.section.gamepad"), "settings__section"));
            content.Add(BuildSliderRow(ControlValue.StickDeadzone));
            content.Add(BuildRumbleRow());
            content.Add(BuildSliderRow(ControlValue.RumbleStrength));

            content.Add(NewLabel(Localizer.Get("settings.section.effects"), "settings__section"));
            foreach (var value in _effectValues)
            {
                content.Add(BuildSliderRow(value));
            }

            content.Add(BuildBloodRow());

            content.Add(NewLabel(Localizer.Get("settings.section.accessibility"), "settings__section"));
            content.Add(BuildPaletteRow());
            content.Add(BuildPaletteSampleRow());
            content.Add(BuildSubtitlesRow());

            content.Add(NewLabel(Localizer.Get(VoiceText.SettingsSectionKey), "settings__section"));
            content.Add(BuildVoiceMicrophoneRow());
            content.Add(BuildVoiceVolumeRow());

            content.Add(NewLabel(Localizer.Get("settings.section.online"), "settings__section"));
            content.Add(BuildRegionRow());

            content.Add(NewLabel(Localizer.Get("settings.section.privacy"), "settings__section"));
            content.Add(BuildTelemetryRow());

            content.Add(NewLabel(Localizer.Get("settings.section.language"), "settings__section"));
            content.Add(BuildLanguageRow());

            content.Add(NewLabel(Localizer.Get("settings.section.holdModes"), "settings__section"));
            content.Add(BuildDeviceHeader());
            foreach (var action in _actions)
            {
                content.Add(BuildModeRow(action));
            }

            content.Add(NewLabel(Localizer.Get("settings.section.bindings"), "settings__section"));
            if (_bindings == null)
            {
                content.Add(NewLabel(Localizer.Get("settings.bindings.unavailable"), "settings__note"));
            }
            else
            {
                content.Add(BuildDeviceHeader());
                var seen = new HashSet<string>(StringComparer.Ordinal);
                foreach (var slot in PlayerBindings.Slots)
                {
                    if (seen.Add(slot.ActionName + "/" + slot.Part))
                    {
                        content.Add(BuildBindingRow(slot.ActionName, slot.Part));
                    }
                }
            }

            root.Add(BuildRebindNotice());

            var footer = new VisualElement();
            footer.AddToClassList("settings__footer");
            footer.Add(NewButton(ResetButtonName, Localizer.Get("settings.reset"), ResetToDefaults));
            var resetBindings = NewButton(BindingsResetButtonName, Localizer.Get("settings.bindings.reset"), ResetBindingsToDefaults);
            resetBindings.SetEnabled(_bindings != null);
            footer.Add(resetBindings);
            footer.Add(NewButton(BackButtonName, Localizer.Get("settings.back"), RequestBack));
            root.Add(footer);

            root.RegisterCallback<NavigationCancelEvent>(HandleNavigationCancel);
            return root;
        }

        private static VisualElement BuildDeviceHeader()
        {
            var header = NewRow();
            header.Add(NewLabel(string.Empty, "settings__caption"));
            foreach (var device in _devices)
            {
                header.Add(NewLabel(DeviceText(device), "settings__column"));
            }

            return header;
        }

        private VisualElement BuildBindingRow(string actionName, string part)
        {
            var row = NewRow();
            row.Add(NewLabel(InputActionLabels.LabelOf(actionName, part), "settings__caption"));
            foreach (var device in _devices)
            {
                var slot = new RebindSlot(actionName, device, part);
                if (!IsListed(slot) || !_bindings.Has(slot))
                {
                    row.Add(NewLabel(NotRebindableText, "settings__column"));
                    continue;
                }

                var button = NewButton(RebindButtonName(slot), string.Empty, () => StartRebind(slot));
                button.AddToClassList("settings__bind");
                row.Add(button);
                _rebindButtons[slot] = button;
            }

            return row;
        }

        private VisualElement BuildRebindNotice()
        {
            _rebindNoticeRow = new VisualElement();
            _rebindNoticeRow.AddToClassList("settings__notice-row");

            _rebindNotice = NewLabel(string.Empty, "settings__notice");
            _rebindNotice.name = RebindNoticeName;
            _rebindNoticeRow.Add(_rebindNotice);

            _rebindCancelButton = NewButton(RebindCancelButtonName, Localizer.Get("settings.rebind.cancel"), CancelRebind);
            _rebindUndoButton = NewButton(RebindUndoButtonName, Localizer.Get("settings.rebind.undo"), () => UndoRebind());
            _rebindKeepButton = NewButton(RebindKeepButtonName, Localizer.Get("settings.rebind.keep"), () => KeepRebind());
            _rebindNoticeRow.Add(_rebindCancelButton);
            _rebindNoticeRow.Add(_rebindUndoButton);
            _rebindNoticeRow.Add(_rebindKeepButton);
            return _rebindNoticeRow;
        }

        private void ScrollToFocused(FocusInEvent evt)
        {
            // ScrollTo は中身の外 (スクロールバーなど) を渡すと投げる。
            if (_scroll != null && evt.target is VisualElement focused && focused != _scroll.contentContainer
                && _scroll.contentContainer.Contains(focused))
            {
                _scroll.ScrollTo(focused);
            }
        }

        private static bool IsListed(RebindSlot slot)
        {
            foreach (var listed in PlayerBindings.Slots)
            {
                if (listed.Equals(slot))
                {
                    return true;
                }
            }

            return false;
        }

        private VisualElement BuildSliderRow(ControlValue value)
        {
            var range = ControlSettings.RangeOf(value);
            var row = NewRow();
            row.Add(NewLabel(ValueText(value), "settings__caption"));

            var slider = new SliderInt(range.Min, range.Max) { name = SliderName(value) };
            slider.AddToClassList("settings__slider");
            slider.RegisterValueChangedCallback(evt => SetValue(value, range.Clamp(evt.newValue)));
            row.Add(slider);
            _sliders[value] = slider;

            var label = NewLabel(string.Empty, "settings__value");
            label.name = ValueLabelName(value);
            row.Add(label);
            _valueLabels[value] = label;
            return row;
        }

        private VisualElement BuildPaletteRow()
        {
            var row = NewRow();
            row.Add(NewLabel(Localizer.Get("settings.palette"), "settings__caption"));
            _paletteButton = NewButton(PaletteButtonName, string.Empty, () => SetPalette(NextPalette(_editing.Palette)));
            _paletteButton.AddToClassList("settings__palette");
            row.Add(_paletteButton);
            return row;
        }

        // 組を切り替えたとき、試合に入らなくても色と印の違いが分かるように、ネームプレートと同じ書き方の見本を並べる
        private VisualElement BuildPaletteSampleRow()
        {
            var row = NewRow();
            row.Add(NewLabel(string.Empty, "settings__caption"));
            _friendlySample = NewLabel(string.Empty, "settings__palette-sample");
            _friendlySample.name = PaletteFriendlySampleName;
            row.Add(_friendlySample);
            _enemySample = NewLabel(string.Empty, "settings__palette-sample");
            _enemySample.name = PaletteEnemySampleName;
            row.Add(_enemySample);
            return row;
        }

        private void RefreshPalette()
        {
            var palette = TeamPalette.For(_editing.Palette);
            if (_paletteButton != null)
            {
                _paletteButton.text = FormatPalette(palette.Kind);
            }

            if (_friendlySample != null)
            {
                _friendlySample.text = palette.NameplateText(TeamRelation.Friendly, FriendlySampleText);
                _friendlySample.style.color = TeamColors.ToColor(palette.Friendly);
            }

            if (_enemySample != null)
            {
                _enemySample.text = palette.NameplateText(TeamRelation.Enemy, EnemySampleText);
                _enemySample.style.color = TeamColors.ToColor(palette.Enemy);
            }
        }

        private VisualElement BuildSubtitlesRow()
        {
            var row = NewRow();
            row.Add(NewLabel(Localizer.Get("settings.subtitles"), "settings__caption"));
            _subtitlesButton = NewButton(SubtitlesButtonName, string.Empty, () => SetShowSubtitles(!_editing.ShowSubtitles));
            _subtitlesButton.AddToClassList("settings__mode");
            row.Add(_subtitlesButton);
            return row;
        }

        private VisualElement BuildBloodRow()
        {
            var row = NewRow();
            row.Add(NewLabel(Localizer.Get("settings.blood"), "settings__caption"));
            _bloodButton = NewButton(BloodButtonName, string.Empty, () => SetShowBlood(!_editing.ShowBlood));
            _bloodButton.AddToClassList("settings__mode");
            row.Add(_bloodButton);
            return row;
        }

        private VisualElement BuildAimAssistRow()
        {
            var row = NewRow();
            row.Add(NewLabel(Localizer.Get("settings.aimAssist"), "settings__caption"));
            _aimAssistButton = NewButton(AimAssistButtonName, string.Empty, () => SetAimAssist(!_editing.AimAssist));
            _aimAssistButton.AddToClassList("settings__mode");
            row.Add(_aimAssistButton);
            return row;
        }

        private VisualElement BuildInvertLookYRow()
        {
            var row = NewRow();
            row.Add(NewLabel(Localizer.Get("settings.invertLookY"), "settings__caption"));
            _invertLookYButton = NewButton(InvertLookYButtonName, string.Empty, () => SetInvertLookY(!_editing.InvertLookY));
            _invertLookYButton.AddToClassList("settings__mode");
            row.Add(_invertLookYButton);
            return row;
        }

        private VisualElement BuildRumbleRow()
        {
            var row = NewRow();
            row.Add(NewLabel(Localizer.Get("settings.rumble"), "settings__caption"));
            _rumbleButton = NewButton(RumbleButtonName, string.Empty, () => SetRumbleEnabled(!_editing.RumbleEnabled));
            _rumbleButton.AddToClassList("settings__mode");
            row.Add(_rumbleButton);
            return row;
        }

        private VisualElement BuildRegionRow()
        {
            var row = NewRow();
            row.Add(NewLabel(Localizer.Get("settings.region"), "settings__caption"));
            _regionButton = NewButton(
                RegionButtonName, string.Empty, () => SetQuickMatchRegion(NextRegion(_editing.QuickMatchRegion, Regions)));
            _regionButton.AddToClassList("settings__palette");
            row.Add(_regionButton);
            return row;
        }

        private VisualElement BuildTelemetryRow()
        {
            var row = NewRow();
            row.Add(NewLabel(Localizer.Get("settings.telemetry"), "settings__caption"));
            _telemetryButton = NewButton(TelemetryButtonName, string.Empty, () => SetTelemetryConsent(!_editing.TelemetryConsent));
            _telemetryButton.AddToClassList("settings__mode");
            row.Add(_telemetryButton);
            return row;
        }

        private VisualElement BuildVoiceMicrophoneRow()
        {
            var row = NewRow();
            row.Add(NewLabel(Localizer.Get(VoiceText.MicrophoneKey), "settings__caption"));
            _voiceMicrophoneButton = NewButton(
                VoiceMicrophoneButtonName, string.Empty, () => SetMicrophoneEnabled(!_voiceEditing.MicrophoneEnabled));
            _voiceMicrophoneButton.AddToClassList("settings__mode");
            row.Add(_voiceMicrophoneButton);
            return row;
        }

        private VisualElement BuildVoiceVolumeRow()
        {
            var row = NewRow();
            row.Add(NewLabel(Localizer.Get(VoiceText.VolumeKey), "settings__caption"));

            _voiceVolumeSlider = new SliderInt(0, VoiceSettings.MaxVolumePercent) { name = VoiceVolumeSliderName };
            _voiceVolumeSlider.AddToClassList("settings__slider");
            _voiceVolumeSlider.RegisterValueChangedCallback(
                evt => SetVoiceVolume(Math.Max(0, Math.Min(VoiceSettings.MaxVolumePercent, evt.newValue))));
            row.Add(_voiceVolumeSlider);

            _voiceVolumeLabel = NewLabel(string.Empty, "settings__value");
            _voiceVolumeLabel.name = VoiceVolumeValueName;
            row.Add(_voiceVolumeLabel);
            return row;
        }

        private void RefreshVoice()
        {
            if (_voiceEditing == null)
            {
                return;
            }

            if (_voiceMicrophoneButton != null)
            {
                _voiceMicrophoneButton.text = FormatOnOff(_voiceEditing.MicrophoneEnabled);
            }

            if (_voiceVolumeSlider != null)
            {
                _voiceVolumeSlider.SetValueWithoutNotify(_voiceEditing.VolumePercent);
            }

            if (_voiceVolumeLabel != null)
            {
                _voiceVolumeLabel.text = FormatVoiceVolume(_voiceEditing.VolumePercent);
            }
        }

        private VisualElement BuildLanguageRow()
        {
            var row = NewRow();
            row.Add(NewLabel(Localizer.Get("settings.language"), "settings__caption"));
            _languages = Localizer.AvailableLanguages();
            _languageButton = NewButton(LanguageButtonName, string.Empty, () => SetLanguage(NextLanguage(Localizer.Language, _languages)));
            _languageButton.AddToClassList("settings__palette");
            row.Add(_languageButton);
            return row;
        }

        // Refresh はスライダーを動かすたびに走るので、言語の表を読んで引いた名前は画面を組み直すまで持つ。
        private string LanguageNameOf(GameLanguage language)
        {
            if (!_languageNames.TryGetValue(language, out var name))
            {
                name = FormatLanguage(language);
                _languageNames[language] = name;
            }

            return name;
        }

        private VisualElement BuildModeRow(HoldAction action)
        {
            var row = NewRow();
            row.Add(NewLabel(ActionText(action), "settings__caption"));
            foreach (var device in _devices)
            {
                var button = NewButton(ModeButtonName(device, action), string.Empty, () =>
                {
                    var current = _editing.Get(device, action);
                    SetMode(device, action, current == HoldMode.Hold ? HoldMode.Toggle : HoldMode.Hold);
                });
                button.AddToClassList("settings__mode");
                row.Add(button);
                _modeButtons[ModeButtonName(device, action)] = button;
            }

            return row;
        }

        private void Refresh()
        {
            foreach (var pair in _sliders)
            {
                var amount = _editing.Get(pair.Key);
                pair.Value.SetValueWithoutNotify(amount);
                _valueLabels[pair.Key].text = FormatValue(pair.Key, amount);
            }

            foreach (var device in _devices)
            {
                foreach (var action in _actions)
                {
                    _modeButtons[ModeButtonName(device, action)].text = FormatMode(_editing.Get(device, action));
                }
            }

            RefreshPalette();
            if (_subtitlesButton != null)
            {
                _subtitlesButton.text = FormatOnOff(_editing.ShowSubtitles);
            }

            if (_bloodButton != null)
            {
                _bloodButton.text = FormatOnOff(_editing.ShowBlood);
            }

            if (_aimAssistButton != null)
            {
                _aimAssistButton.text = FormatOnOff(_editing.AimAssist);
            }

            if (_invertLookYButton != null)
            {
                _invertLookYButton.text = FormatOnOff(_editing.InvertLookY);
            }

            if (_rumbleButton != null)
            {
                _rumbleButton.text = FormatOnOff(_editing.RumbleEnabled);
            }

            // 振動を切っている間は強さが効かないので、動かしても何も変わらないスライダーを触らせない。
            if (_sliders.TryGetValue(ControlValue.RumbleStrength, out var rumbleStrength))
            {
                rumbleStrength.SetEnabled(_editing.RumbleEnabled);
            }

            if (_regionButton != null)
            {
                var region = _editing.QuickMatchRegion;
                _regionButton.text = FormatRegion(region);
                _regionButton.SetEnabled(NextRegion(region, Regions) != region);
            }

            if (_telemetryButton != null)
            {
                _telemetryButton.text = FormatOnOff(_editing.TelemetryConsent);
            }

            RefreshVoice();

            if (_languageButton != null)
            {
                var language = Localizer.Language;
                _languageButton.text = LanguageNameOf(language);
                _languageButton.SetEnabled(NextLanguage(language, _languages) != language);
            }

            RefreshBindings();
        }

        private void RefreshBindings()
        {
            if (_root == null || _bindings == null)
            {
                SetDisplayed(_rebindNoticeRow, false);
                return;
            }

            var waiting = _bindings.RebindingSlot;
            foreach (var pair in _rebindButtons)
            {
                var isWaiting = waiting.HasValue && waiting.Value.Equals(pair.Key);
                pair.Value.text = isWaiting ? WaitingText : _bindings.DisplayOf(pair.Key);
                pair.Value.EnableInClassList("settings__bind--waiting", isWaiting);
                pair.Value.EnableInClassList("settings__bind--overridden", _bindings.IsOverridden(pair.Key));
                pair.Value.EnableInClassList(
                    "settings__bind--conflict",
                    _pendingConflict != null && _pendingConflict.Slot.Equals(pair.Key));
            }

            if (waiting.HasValue)
            {
                var slot = waiting.Value;
                var cancelKey = Localizer.Get(
                    slot.Device == InputDeviceKind.Gamepad ? "settings.rebind.cancelKey.gamepad" : "settings.rebind.cancelKey.keyboardMouse");
                _rebindNotice.text = Localizer.Format(
                    "settings.rebind.prompt",
                    InputActionLabels.LabelOf(slot.ActionName, slot.Part),
                    DeviceText(slot.Device),
                    cancelKey);
                SetDisplayed(_rebindNoticeRow, true);
                SetDisplayed(_rebindCancelButton, true);
                SetDisplayed(_rebindUndoButton, false);
                SetDisplayed(_rebindKeepButton, false);
            }
            else if (_pendingConflict != null)
            {
                _rebindNotice.text = FormatConflict(_bindings.DisplayOf(_pendingConflict.Slot), _pendingConflict.Conflicts);
                SetDisplayed(_rebindNoticeRow, true);
                SetDisplayed(_rebindCancelButton, false);
                SetDisplayed(_rebindUndoButton, true);
                SetDisplayed(_rebindKeepButton, true);
            }
            else
            {
                _rebindNotice.text = string.Empty;
                SetDisplayed(_rebindNoticeRow, false);
            }
        }

        private static void SetDisplayed(VisualElement element, bool displayed)
        {
            if (element != null)
            {
                element.style.display = displayed ? DisplayStyle.Flex : DisplayStyle.None;
            }
        }

        private void FocusFirst()
        {
            if (_sliders.TryGetValue(ControlSettings.Values[0], out var first))
            {
                FocusLater(first);
            }
        }

        // スライダーなどは中の部品にフォーカスが入り、その部品の name はどの行でも同じなので、この画面が付けた name を持つ先祖まで遡る。
        private string OwnNameOf(VisualElement element)
        {
            for (var current = element; current != null && current != _root; current = current.parent)
            {
                if (current.name != null && current.name.StartsWith(RootName + "-", StringComparison.Ordinal))
                {
                    return current.name;
                }
            }

            return null;
        }

        private bool FocusLater(VisualElement element)
        {
            if (element == null || !element.focusable)
            {
                return false;
            }

            element.Focus();

            // パネルに付いた直後は配置が終わっておらず Focus が効かないことがあるので、次の更新でもう一度選ぶ
            if (element.panel != null && element.focusController?.focusedElement != element)
            {
                element.schedule.Execute(() =>
                {
                    if (_root != null && element.panel != null && _root.Contains(element))
                    {
                        element.Focus();
                    }
                });
            }

            return true;
        }

        private void HandleNavigationCancel(NavigationCancelEvent evt)
        {
            evt.StopPropagation();

            // 割り当ての入力を待っている間の Esc / Start は、待つのをやめるためのもの。
            if (PlayerBindings.IsCapturingInput)
            {
                return;
            }

            RequestBack();
        }

        public void RequestBack()
        {
            if (_root != null)
            {
                BackRequested?.Invoke();
            }
        }

        private void EnsureOpen()
        {
            if (_root == null)
            {
                throw new InvalidOperationException("the settings view is not open");
            }
        }

        private static void AddStyle(VisualElement root)
        {
            var style = Resources.Load<StyleSheet>(StyleResourcePath);
            if (style != null)
            {
                root.styleSheets.Add(style);
                return;
            }

            // 見た目が崩れるだけで操作はできるので、止めずに一度だけ知らせる。
            if (!_warnedMissingStyle)
            {
                _warnedMissingStyle = true;
                Debug.LogWarning(
                    $"{LogTag} style 'Resources/{StyleResourcePath}.uss' was not found (expected under UI/Resources of the Demo sample); the settings are unstyled");
            }
        }

        private static VisualElement NewRow()
        {
            var row = new VisualElement();
            row.AddToClassList("settings__row");
            return row;
        }

        private static Label NewLabel(string text, string className)
        {
            var label = new Label(text);
            label.AddToClassList(className);
            return label;
        }

        private static Button NewButton(string name, string text, Action clicked)
        {
            var button = new Button(clicked) { name = name, text = text };
            button.AddToClassList("settings__button");
            return button;
        }

        private static string ValueText(ControlValue value)
        {
            switch (value)
            {
                case ControlValue.MouseSensitivity:
                    return Localizer.Get("settings.value.mouseSensitivity");
                case ControlValue.GamepadSensitivity:
                    return Localizer.Get("settings.value.gamepadSensitivity");
                case ControlValue.AdsSensitivity:
                    return Localizer.Get("settings.value.adsSensitivity");
                case ControlValue.FieldOfView:
                    return Localizer.Get("settings.value.fieldOfView");
                case ControlValue.MasterVolume:
                    return Localizer.Get("settings.value.masterVolume");
                case ControlValue.EffectsVolume:
                    return Localizer.Get("settings.value.effectsVolume");
                case ControlValue.CameraShakeIntensity:
                    return Localizer.Get("settings.value.cameraShake");
                case ControlValue.ViewmodelMotionIntensity:
                    return Localizer.Get("settings.value.viewmodelMotion");
                case ControlValue.StickDeadzone:
                    return Localizer.Get("settings.value.stickDeadzone");
                case ControlValue.RumbleStrength:
                    return Localizer.Get("settings.value.rumbleStrength");
                default:
                    throw new ArgumentOutOfRangeException(nameof(value), value, "unknown control value");
            }
        }

        private static string DeviceText(InputDeviceKind device)
        {
            return Localizer.Get(device == InputDeviceKind.Gamepad ? "settings.device.gamepad" : "settings.device.keyboardMouse");
        }

        private static string ActionText(HoldAction action)
        {
            switch (action)
            {
                case HoldAction.Sprint:
                    return Localizer.Get("settings.action.sprint");
                case HoldAction.Crouch:
                    return Localizer.Get("settings.action.crouch");
                default:
                    return Localizer.Get("settings.action.aim");
            }
        }

        private static string Slug(ControlValue value)
        {
            switch (value)
            {
                case ControlValue.MouseSensitivity:
                    return "mouse-sensitivity";
                case ControlValue.GamepadSensitivity:
                    return "gamepad-sensitivity";
                case ControlValue.AdsSensitivity:
                    return "ads-sensitivity";
                case ControlValue.FieldOfView:
                    return "field-of-view";
                case ControlValue.MasterVolume:
                    return "master-volume";
                case ControlValue.EffectsVolume:
                    return "effects-volume";
                case ControlValue.CameraShakeIntensity:
                    return "camera-shake";
                case ControlValue.ViewmodelMotionIntensity:
                    return "viewmodel-motion";
                case ControlValue.StickDeadzone:
                    return "stick-deadzone";
                case ControlValue.RumbleStrength:
                    return "rumble-strength";
                default:
                    throw new ArgumentOutOfRangeException(nameof(value), value, "unknown control value");
            }
        }

        private static string Slug(InputDeviceKind device)
        {
            switch (device)
            {
                case InputDeviceKind.KeyboardMouse:
                    return "keyboard-mouse";
                case InputDeviceKind.Gamepad:
                    return "gamepad";
                default:
                    throw new ArgumentOutOfRangeException(nameof(device), device, "unknown input device kind");
            }
        }

        private static string Slug(HoldAction action)
        {
            switch (action)
            {
                case HoldAction.Sprint:
                    return "sprint";
                case HoldAction.Crouch:
                    return "crouch";
                case HoldAction.Aim:
                    return "aim";
                default:
                    throw new ArgumentOutOfRangeException(nameof(action), action, "unknown hold action");
            }
        }
    }
}
