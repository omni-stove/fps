using System;
using System.Collections.Generic;
using System.Globalization;
using Fps.Core;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace Fps.Runtime
{
    /// <summary>
    /// 設定画面。感度・ADS 感度・視野角・音量 (<see cref="ControlValue"/>) のスライダー、敵味方の色の組 (<see cref="ColorPalette"/>) の切り替えと見本、
    /// 音の合図の字幕 (<see cref="ControlSettings.ShowSubtitles"/>) の On/Off、入力機器ごとの Hold/Toggle の切り替えを
    /// UI Toolkit の要素としてコードで組み、<see cref="Open"/> で渡された入れ物 (メインメニューの差し込み口やポーズ画面の枠) に入れる。
    /// 値を変えるとすぐ <see cref="ISettingsSink.Apply"/> (既定は <see cref="AppliedControlSettings.Sink"/>) で効かせ、<see cref="Close"/> (か <see cref="Save"/>) で
    /// <see cref="ControlSettingsStore"/> に保存する。スライダーを動かすたびにディスクへ書かないよう、保存は閉じるときにまとめる。
    /// 「戻る」とキャンセル (ゲームパッドの B など) では <see cref="BackRequested"/> を呼ぶだけで、閉じるのは開いた側が決める。
    /// <para>
    /// キーとボタンの割り当ては <see cref="PlayerBindings"/> で機器ごとに変え (<see cref="StartRebind"/>)、上書きはすぐ効く。
    /// ほかの割り当てと重なったら知らせ、<see cref="UndoRebind"/> で前の割り当てに戻せる。閉じるときに <see cref="InputBindingOverridesStore"/> に保存する。
    /// </para>
    /// <para>
    /// 画面の文字は <see cref="Localizer"/> から引く。言語 (<see cref="SetLanguage"/>) を変えると、開いたまま画面を今の言語で組み直す。
    /// </para>
    /// </summary>
    public sealed class SettingsView
    {
        public const string LogTag = AppliedControlSettings.LogTag;

        /// <summary>Resources からの見た目 (Assets/game/UI/Resources/Menu/Settings.uss)。</summary>
        public const string StyleResourcePath = "Menu/Settings";

        public const string RootName = "settings";

        /// <summary>画面の見出しの Label の name。</summary>
        public const string TitleName = "settings-title";

        /// <summary>画面の言語 (<see cref="ControlSettings.Language"/>) を順に切り替えるボタンの name。</summary>
        public const string LanguageButtonName = "settings-language";

        public const string BackButtonName = "settings-back";
        public const string ResetButtonName = "settings-reset";
        public const string BindingsResetButtonName = "settings-bindings-reset";
        public const string RebindNoticeName = "settings-rebind-notice";
        public const string RebindCancelButtonName = "settings-rebind-cancel";
        public const string RebindUndoButtonName = "settings-rebind-undo";
        public const string RebindKeepButtonName = "settings-rebind-keep";

        /// <summary>敵味方の色の組 (<see cref="ControlSettings.Palette"/>) を順に切り替えるボタンの name。</summary>
        public const string PaletteButtonName = "settings-palette";

        /// <summary>今の色の組で味方の見え方を出す見本の Label の name。</summary>
        public const string PaletteFriendlySampleName = "settings-palette-friendly";

        /// <summary>今の色の組で敵の見え方を出す見本の Label の name。</summary>
        public const string PaletteEnemySampleName = "settings-palette-enemy";

        /// <summary>音の合図の字幕 (<see cref="ControlSettings.ShowSubtitles"/>) の On/Off を切り替えるボタンの name。</summary>
        public const string SubtitlesButtonName = "settings-subtitles";

        /// <summary>今の言語の「オン」。</summary>
        public static string OnText => Localizer.Get("settings.on");

        /// <summary>今の言語の「オフ」。</summary>
        public static string OffText => Localizer.Get("settings.off");

        /// <summary>今の言語の、見本の味方の名前。</summary>
        public static string FriendlySampleText => Localizer.Get("settings.palette.friendlySample");

        /// <summary>今の言語の、見本の敵の名前。</summary>
        public static string EnemySampleText => Localizer.Get("settings.palette.enemySample");

        /// <summary>今の言語の「長押し」。</summary>
        public static string HoldText => Localizer.Get("settings.mode.hold");

        /// <summary>今の言語の「切り替え」。</summary>
        public static string ToggleText => Localizer.Get("settings.mode.toggle");

        /// <summary>今の言語で、入力を待っている割り当てのボタンに出す文字。</summary>
        public static string WaitingText => Localizer.Get("settings.rebind.waiting");

        /// <summary>割り当てを変えられない所 (ゲームパッドの移動など) に出す文字。言語に依らない。</summary>
        public const string NotRebindableText = "—";

        private static readonly InputDeviceKind[] _devices = { InputDeviceKind.KeyboardMouse, InputDeviceKind.Gamepad };
        private static readonly HoldAction[] _actions = { HoldAction.Sprint, HoldAction.Crouch, HoldAction.Aim };

        private static bool _warnedMissingStyle;

        private readonly ControlSettingsStore _store;
        private readonly InputBindingOverridesStore _bindingStore;
        private readonly InputActionAsset _actionAsset;
        private readonly ISettingsSink _sink;
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
        private Button _languageButton;

        /// <param name="store">保存先。null ならゲームの既定のキー。テストでは本物の設定を壊さないよう別のキーを渡す。</param>
        /// <param name="bindingStore">割り当ての上書きの保存先。null ならゲームの既定のキー。</param>
        /// <param name="actions">割り当てを変える Input Actions。null なら開くときのプロジェクト全体のもの (<see cref="InputSystem.actions"/>)。</param>
        /// <param name="sink">今の設定を読み、変えた値を効かせる先。null ならゲーム全体の <see cref="AppliedControlSettings.Sink"/>。</param>
        public SettingsView(
            ControlSettingsStore store = null,
            InputBindingOverridesStore bindingStore = null,
            InputActionAsset actions = null,
            ISettingsSink sink = null)
        {
            _store = store ?? new ControlSettingsStore();
            _bindingStore = bindingStore ?? new InputBindingOverridesStore();
            _actionAsset = actions;
            _sink = sink ?? AppliedControlSettings.Sink;
        }

        /// <summary>「戻る」かキャンセルが押されたとき。開いた側が <see cref="Close"/> を呼んで閉じる。</summary>
        public event Action BackRequested;

        /// <summary>保存先。</summary>
        public ControlSettingsStore Store => _store;

        /// <summary>開いているか。</summary>
        public bool IsOpen => _root != null;

        /// <summary>開いている画面の根の要素。閉じていれば null。</summary>
        public VisualElement Root => _root;

        /// <summary>画面で編集している設定 (すでに効いている)。閉じていれば null。</summary>
        public ControlSettings Settings => _editing;

        /// <summary>割り当ての上書きの保存先。</summary>
        public InputBindingOverridesStore BindingStore => _bindingStore;

        /// <summary>開いている画面が割り当てを変える Player マップ。閉じているか、Input Actions が無ければ null。</summary>
        public PlayerBindings Bindings => _bindings;

        /// <summary>直前に変えた割り当てがほかと重なっていて、まだ取り消すか決めていなければその結果。無ければ null。</summary>
        public RebindResult PendingConflict => _pendingConflict;

        /// <summary>まだ保存していない変更 (値・モード・割り当て) があるか。</summary>
        public bool HasUnsavedChanges => _editing != null && (!_editing.Equals(_saved) || _bindingsDirty);

        /// <summary>slot の割り当てを変えるボタンの name。</summary>
        public static string RebindButtonName(RebindSlot slot)
        {
            var name = "settings-bind-" + Slug(slot.Device) + "-" + slot.ActionName.ToLowerInvariant();
            return slot.Part == null ? name : name + "-" + slot.Part.ToLowerInvariant();
        }

        /// <summary>今の言語で、重なりを知らせる文。</summary>
        public static string FormatConflict(string display, IReadOnlyList<BindingConflict> conflicts)
        {
            if (conflicts == null || conflicts.Count == 0)
            {
                return string.Empty;
            }

            var labels = new List<string>();
            foreach (var conflict in conflicts)
            {
                if (!labels.Contains(conflict.Label))
                {
                    labels.Add(conflict.Label);
                }
            }

            var joined = string.Join(Localizer.Get("settings.rebind.conflictSeparator"), labels);
            return Localizer.Format("settings.rebind.conflict", display, joined);
        }

        /// <summary>value のスライダーの name。</summary>
        public static string SliderName(ControlValue value)
        {
            return "settings-" + Slug(value);
        }

        /// <summary>value の今の値を出す Label の name。</summary>
        public static string ValueLabelName(ControlValue value)
        {
            return SliderName(value) + "-value";
        }

        /// <summary>device の action の Hold/Toggle を切り替えるボタンの name。</summary>
        public static string ModeButtonName(InputDeviceKind device, HoldAction action)
        {
            return "settings-mode-" + Slug(device) + "-" + Slug(action);
        }

        /// <summary>スライダーの横に出す値。視野角は度、それ以外は %。</summary>
        public static string FormatValue(ControlValue value, int amount)
        {
            var number = amount.ToString(CultureInfo.InvariantCulture);
            return value == ControlValue.FieldOfView ? number + "°" : number + "%";
        }

        /// <summary>今の言語で、Hold/Toggle のボタンに出す文字。</summary>
        public static string FormatMode(HoldMode mode)
        {
            return mode == HoldMode.Toggle ? ToggleText : HoldText;
        }

        /// <summary>今の言語で、On/Off のボタンに出す文字。</summary>
        public static string FormatOnOff(bool on)
        {
            return on ? OnText : OffText;
        }

        /// <summary>今の言語で、色の組のボタンに出す文字。</summary>
        /// <exception cref="ArgumentOutOfRangeException">palette が定義済みの値でない。</exception>
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

        /// <summary>言語のボタンに出す文字。今の言語が読めなくても選べるよう、その言語自身での名前 (<see cref="GameLanguages.NativeName"/>)。</summary>
        /// <exception cref="ArgumentOutOfRangeException">language が定義済みの値でない。</exception>
        public static string FormatLanguage(GameLanguage language)
        {
            return GameLanguages.NativeName(language);
        }

        /// <summary><see cref="TeamPalette.All"/> の並びで palette の次の組。最後の次は最初に戻る。</summary>
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

        /// <summary>
        /// 今効いている設定 (<see cref="ISettingsSink.Current"/>) で画面を組んで container に入れ、最初のスライダーにフォーカスを置く。
        /// 既に開いていれば、前の画面を閉じて (保存して) から開き直す。
        /// </summary>
        public void Open(VisualElement container)
        {
            if (container == null)
            {
                throw new ArgumentNullException(nameof(container));
            }

            Close();

            _editing = _sink.Current;
            _saved = _editing;
            _bindings = CreateBindings();
            _bindingsDirty = false;
            _pendingConflict = null;
            _root = Build();
            container.Add(_root);
            Refresh();
            Localizer.Changed += HandleLanguageChanged;
            FocusFirst();
        }

        /// <summary>
        /// 割り当ての入力を待っていればやめ、変更があれば保存し、画面を入れ物から外す。保存したら true。開いていなければ何もせず false。
        /// 重なりを知らせたまま閉じたときは、変えた割り当てをそのまま残す。
        /// </summary>
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
            _bindings?.Dispose();
            _bindings = null;
            _bindingsDirty = false;
            _pendingConflict = null;
            return saved;
        }

        // 画面の要素を外して参照を捨てる。編集中の値と割り当ては残す。
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
            _languageButton = null;
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

        /// <summary>
        /// まだ保存していない変更があれば、値とモードは <see cref="ControlSettingsStore"/> に、割り当ての上書きは <see cref="InputBindingOverridesStore"/> に書き出す。
        /// 書き出して失敗が無ければ true。失敗したらエラーを出して false (変更はこの起動の間は効いたままで、次に閉じるときにまた書き出す)。
        /// </summary>
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

        /// <summary>value を amount にしてすぐ効かせ、スライダーと表示を合わせる。</summary>
        /// <exception cref="InvalidOperationException">開いていない。</exception>
        /// <exception cref="ArgumentOutOfRangeException">amount が <see cref="ControlSettings.RangeOf"/> の範囲の外。</exception>
        public void SetValue(ControlValue value, int amount)
        {
            EnsureOpen();
            Edit(_editing.With(value, amount));
        }

        /// <summary>device の action のモードを mode にしてすぐ効かせ、ボタンの表示を合わせる。</summary>
        /// <exception cref="InvalidOperationException">開いていない。</exception>
        public void SetMode(InputDeviceKind device, HoldAction action, HoldMode mode)
        {
            EnsureOpen();
            Edit(_editing.With(device, action, mode));
        }

        /// <summary>
        /// 敵味方の色の組を palette にしてすぐ効かせ (キャラクター・HUD・拠点は <see cref="AppliedControlSettings.Changed"/> で塗り直す)、
        /// ボタンと見本の表示を合わせる。
        /// </summary>
        /// <exception cref="InvalidOperationException">開いていない。</exception>
        /// <exception cref="ArgumentOutOfRangeException">palette が定義済みの値でない。</exception>
        public void SetPalette(ColorPalette palette)
        {
            EnsureOpen();
            Edit(_editing.WithPalette(palette));
        }

        /// <summary>
        /// 音の合図の字幕を出すか (<see cref="ControlSettings.ShowSubtitles"/>) を show にしてすぐ効かせ
        /// (HUD の <see cref="SubtitleView"/> は <see cref="AppliedControlSettings.Changed"/> で切り替わる)、ボタンの表示を合わせる。
        /// </summary>
        /// <exception cref="InvalidOperationException">開いていない。</exception>
        public void SetShowSubtitles(bool show)
        {
            EnsureOpen();
            Edit(_editing.WithShowSubtitles(show));
        }

        /// <summary>
        /// 画面の言語 (<see cref="ControlSettings.Language"/>) を language にしてすぐ効かせ、画面を今の言語で組み直す
        /// (<see cref="Localizer.Changed"/> で知らせる)。保存は閉じるとき。
        /// </summary>
        /// <exception cref="InvalidOperationException">開いていない。</exception>
        /// <exception cref="ArgumentOutOfRangeException">language が定義済みの値でない。</exception>
        public void SetLanguage(GameLanguage language)
        {
            EnsureOpen();
            Edit(_editing.WithLanguage(language));
        }

        /// <summary>
        /// すべて初期値 (<see cref="ControlSettings.Default"/>) に戻してすぐ効かせる。色の組も標準に、字幕も Off に戻す。保存は閉じるとき。
        /// この画面に無い回線の表示 (<see cref="ControlSettings.ShowNetStats"/>) と、読めない言語に戻って押した人が戻せなくならないよう言語は、今のまま残す。
        /// </summary>
        /// <exception cref="InvalidOperationException">開いていない。</exception>
        public void ResetToDefaults()
        {
            EnsureOpen();
            Edit(ControlSettings.Default.WithLanguage(_editing.Language));
        }

        /// <summary>
        /// slot の新しい入力を待ち始め、ボタンと案内を「待っている」表示にする。入力が来たらすぐ効かせ、ほかと重なれば <see cref="PendingConflict"/> に置いて知らせる。
        /// まだ決めていない重なりは、そのまま残すことにする。既に待っていれば何もせず false。
        /// </summary>
        /// <exception cref="InvalidOperationException">開いていないか、Input Actions が無い。</exception>
        /// <exception cref="ArgumentException">slot がこの Input Actions に無い。</exception>
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

        /// <summary>入力を待っていればやめる (割り当ては変えない)。開いていなければ何もしない。</summary>
        public void CancelRebind()
        {
            _bindings?.CancelRebind();
        }

        /// <summary>
        /// slot を path (例: "&lt;Keyboard&gt;/f") にする。入力を受け取ったときと同じく、すぐ効かせて重なりを調べる。
        /// 入力を待っていればやめてから変える。
        /// </summary>
        /// <exception cref="InvalidOperationException">開いていないか、Input Actions が無い。</exception>
        /// <exception cref="ArgumentException">path が空か、slot がこの Input Actions に無い。</exception>
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

        /// <summary>
        /// 重なりを知らせている割り当てを、変える前に戻す。戻したら true。知らせていなければ何もせず false。
        /// </summary>
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

        /// <summary>重なりを知らせている割り当てを、そのまま残す。知らせていなければ何もせず false。</summary>
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

        /// <summary>入力を待っていればやめ、割り当てをすべて既定に戻してすぐ効かせる。保存は閉じるとき。</summary>
        /// <exception cref="InvalidOperationException">開いていないか、Input Actions が無い。</exception>
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
            // 回線の表示はこの画面に無く、開いている間にも F3 (Fps.Net の NetHud) で切り替わるので、開いたときの値で戻さない
            settings = settings.WithShowNetStats(_sink.Current.ShowNetStats);
            if (settings.Equals(_editing))
            {
                return;
            }

            _editing = settings;
            _sink.Apply(settings);
            Refresh();
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
                content.Add(BuildSliderRow(value));
            }

            content.Add(NewLabel(Localizer.Get("settings.section.accessibility"), "settings__section"));
            content.Add(BuildPaletteRow());
            content.Add(BuildPaletteSampleRow());
            content.Add(BuildSubtitlesRow());

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
            row.Add(NewLabel(PlayerBindings.LabelOf(actionName, part), "settings__caption"));
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

        private VisualElement BuildLanguageRow()
        {
            var row = NewRow();
            row.Add(NewLabel(Localizer.Get("settings.language"), "settings__caption"));
            _languageButton = NewButton(LanguageButtonName, string.Empty, () => SetLanguage(GameLanguages.Next(_editing.Language)));
            _languageButton.AddToClassList("settings__palette");
            row.Add(_languageButton);
            return row;
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

            if (_languageButton != null)
            {
                _languageButton.text = FormatLanguage(_editing.Language);
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
                var cancelKey = slot.Device == InputDeviceKind.Gamepad ? "Start" : "Esc";
                _rebindNotice.text = Localizer.Format(
                    "settings.rebind.prompt",
                    PlayerBindings.LabelOf(slot.ActionName, slot.Part),
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

        // element にフォーカスを置く。置けない要素 (null や focusable でないもの) なら何もせず false。
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

        /// <summary>「戻る」を押したのと同じく <see cref="BackRequested"/> を呼ぶ。開いていなければ何もしない。</summary>
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
                    $"{LogTag} style 'Resources/{StyleResourcePath}.uss' was not found (expected under Assets/game/UI/Resources); the settings are unstyled");
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
