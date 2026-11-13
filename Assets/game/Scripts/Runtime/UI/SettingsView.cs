using System;
using System.Collections.Generic;
using System.Globalization;
using Fps.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace Fps.Runtime
{
    /// <summary>
    /// 設定画面。感度・ADS 感度・視野角・音量 (<see cref="ControlValue"/>) のスライダーと、入力機器ごとの Hold/Toggle の切り替えを
    /// UI Toolkit の要素としてコードで組み、<see cref="Open"/> で渡された入れ物 (メインメニューの差し込み口やポーズ画面の枠) に入れる。
    /// 値を変えるとすぐ <see cref="AppliedControlSettings.Apply"/> で効かせ、<see cref="Close"/> (か <see cref="Save"/>) で
    /// <see cref="ControlSettingsStore"/> に保存する。スライダーを動かすたびにディスクへ書かないよう、保存は閉じるときにまとめる。
    /// 「戻る」とキャンセル (ゲームパッドの B など) では <see cref="BackRequested"/> を呼ぶだけで、閉じるのは開いた側が決める。
    /// </summary>
    public sealed class SettingsView
    {
        public const string LogTag = AppliedControlSettings.LogTag;

        /// <summary>Resources からの見た目 (Assets/game/UI/Resources/Menu/Settings.uss)。</summary>
        public const string StyleResourcePath = "Menu/Settings";

        public const string RootName = "settings";
        public const string BackButtonName = "settings-back";
        public const string ResetButtonName = "settings-reset";

        public const string HoldText = "長押し";
        public const string ToggleText = "切り替え";

        private static readonly InputDeviceKind[] _devices = { InputDeviceKind.KeyboardMouse, InputDeviceKind.Gamepad };
        private static readonly HoldAction[] _actions = { HoldAction.Sprint, HoldAction.Crouch, HoldAction.Aim };

        private static bool _warnedMissingStyle;

        private readonly ControlSettingsStore _store;
        private readonly Dictionary<ControlValue, SliderInt> _sliders = new Dictionary<ControlValue, SliderInt>();
        private readonly Dictionary<ControlValue, Label> _valueLabels = new Dictionary<ControlValue, Label>();
        private readonly Dictionary<string, Button> _modeButtons = new Dictionary<string, Button>(StringComparer.Ordinal);
        private VisualElement _root;
        private ControlSettings _editing;
        private ControlSettings _saved;

        /// <param name="store">保存先。null ならゲームの既定のキー。テストでは本物の設定を壊さないよう別のキーを渡す。</param>
        public SettingsView(ControlSettingsStore store = null)
        {
            _store = store ?? new ControlSettingsStore();
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

        /// <summary>まだ保存していない変更があるか。</summary>
        public bool HasUnsavedChanges => _editing != null && !_editing.Equals(_saved);

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

        /// <summary>Hold/Toggle のボタンに出す文字。</summary>
        public static string FormatMode(HoldMode mode)
        {
            return mode == HoldMode.Toggle ? ToggleText : HoldText;
        }

        /// <summary>
        /// 今効いている設定 (<see cref="AppliedControlSettings.Current"/>) で画面を組んで container に入れ、最初のスライダーにフォーカスを置く。
        /// 既に開いていれば、前の画面を閉じて (保存して) から開き直す。
        /// </summary>
        public void Open(VisualElement container)
        {
            if (container == null)
            {
                throw new ArgumentNullException(nameof(container));
            }

            Close();

            _editing = AppliedControlSettings.Current;
            _saved = _editing;
            _root = Build();
            container.Add(_root);
            Refresh();
            FocusFirst();
        }

        /// <summary>
        /// 変更があれば保存し、画面を入れ物から外す。保存したら true。開いていなければ何もせず false。
        /// </summary>
        public bool Close()
        {
            if (_root == null)
            {
                return false;
            }

            var saved = Save();
            _root.UnregisterCallback<NavigationCancelEvent>(HandleNavigationCancel);
            _root.RemoveFromHierarchy();
            _root = null;
            _editing = null;
            _saved = null;
            _sliders.Clear();
            _valueLabels.Clear();
            _modeButtons.Clear();
            return saved;
        }

        /// <summary>
        /// まだ保存していない変更があれば <see cref="ControlSettingsStore"/> に書き出す。書き出したら true。
        /// 書き出しに失敗したらエラーを出して false (値はこの起動の間は効いたまま)。
        /// </summary>
        public bool Save()
        {
            if (!HasUnsavedChanges)
            {
                return false;
            }

            try
            {
                _store.Save(_editing);
            }
            catch (Exception e)
            {
                Debug.LogError($"{LogTag} could not save the settings to '{_store.Key}': {e.Message}");
                Debug.LogException(e);
                return false;
            }

            _saved = _editing;
            Debug.Log($"{LogTag} saved {_editing.ToJson()}");
            return true;
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

        /// <summary>すべて初期値 (<see cref="ControlSettings.Default"/>) に戻してすぐ効かせる。保存は閉じるとき。</summary>
        /// <exception cref="InvalidOperationException">開いていない。</exception>
        public void ResetToDefaults()
        {
            EnsureOpen();
            Edit(ControlSettings.Default);
        }

        private void Edit(ControlSettings settings)
        {
            if (settings.Equals(_editing))
            {
                return;
            }

            _editing = settings;
            AppliedControlSettings.Apply(settings);
            Refresh();
        }

        private VisualElement Build()
        {
            var root = new VisualElement { name = RootName };
            root.AddToClassList("settings");
            AddStyle(root);

            root.Add(NewLabel("設定", "settings__title"));

            root.Add(NewLabel("視点と音", "settings__section"));
            foreach (var value in ControlSettings.Values)
            {
                root.Add(BuildSliderRow(value));
            }

            root.Add(NewLabel("ボタンの扱い", "settings__section"));
            var header = NewRow();
            header.Add(NewLabel(string.Empty, "settings__caption"));
            foreach (var device in _devices)
            {
                header.Add(NewLabel(DeviceText(device), "settings__column"));
            }

            root.Add(header);
            foreach (var action in _actions)
            {
                root.Add(BuildModeRow(action));
            }

            var footer = new VisualElement();
            footer.AddToClassList("settings__footer");
            footer.Add(NewButton(ResetButtonName, "初期値に戻す", ResetToDefaults));
            footer.Add(NewButton(BackButtonName, "戻る", RequestBack));
            root.Add(footer);

            root.RegisterCallback<NavigationCancelEvent>(HandleNavigationCancel);
            return root;
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
        }

        private void FocusFirst()
        {
            if (!_sliders.TryGetValue(ControlSettings.Values[0], out var first))
            {
                return;
            }

            first.Focus();

            // パネルに付いた直後は配置が終わっておらず Focus が効かないことがあるので、次の更新でもう一度選ぶ
            if (first.panel != null && first.focusController?.focusedElement != first)
            {
                first.schedule.Execute(() =>
                {
                    if (_root != null && first.panel != null)
                    {
                        first.Focus();
                    }
                });
            }
        }

        private void HandleNavigationCancel(NavigationCancelEvent evt)
        {
            evt.StopPropagation();
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
                    return "マウス感度";
                case ControlValue.GamepadSensitivity:
                    return "ゲームパッド感度";
                case ControlValue.AdsSensitivity:
                    return "ADS 感度";
                case ControlValue.FieldOfView:
                    return "視野角";
                case ControlValue.MasterVolume:
                    return "全体の音量";
                case ControlValue.EffectsVolume:
                    return "効果音の音量";
                default:
                    throw new ArgumentOutOfRangeException(nameof(value), value, "unknown control value");
            }
        }

        private static string DeviceText(InputDeviceKind device)
        {
            return device == InputDeviceKind.Gamepad ? "ゲームパッド" : "キーボード/マウス";
        }

        private static string ActionText(HoldAction action)
        {
            switch (action)
            {
                case HoldAction.Sprint:
                    return "ダッシュ";
                case HoldAction.Crouch:
                    return "しゃがみ";
                default:
                    return "覗き込み (ADS)";
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
