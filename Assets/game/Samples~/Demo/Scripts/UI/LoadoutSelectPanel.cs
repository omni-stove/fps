using System;
using System.Collections.Generic;
using Shooter.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace Shooter.Runtime
{
    // 既定の移動は画面上の位置で次を選ぶので、パネルの根で移動・決定・取り消しを受けて止め、行の順で動かす。
    public sealed class LoadoutSelectPanel : IDisposable
    {
        public const string PanelClass = "loadout-select";
        public const string TitleClass = "loadout-select__title";
        public const string SlotClass = "loadout-select__slot";
        public const string FocusedSlotClass = "loadout-select__slot--focused";
        public const string SlotTitleClass = "loadout-select__slot-title";
        public const string OptionsClass = "loadout-select__options";
        public const string OptionClass = "loadout-select__option";
        public const string SelectedOptionClass = "loadout-select__option--selected";
        public const string OptionNameClass = "loadout-select__option-name";
        public const string OptionCategoryClass = "loadout-select__option-category";
        public const string LockedOptionClass = "loadout-select__option--locked";
        public const string OptionLockClass = "loadout-select__option-lock";
        public const string LockIconClass = "loadout-select__lock-icon";
        public const string LockShackleClass = "loadout-select__lock-shackle";
        public const string LockBodyClass = "loadout-select__lock-body";
        public const string LockLevelClass = "loadout-select__lock-level";
        public const string NoOptionClass = "loadout-select__no-option";
        public const string AbilitySlotClass = "loadout-select__ability";
        public const string AbilityPickerClass = "loadout-select__ability-picker";
        public const string AbilityStepClass = "loadout-select__ability-step";
        public const string AbilityNameClass = "loadout-select__ability-name";
        public const string NoAbilityClass = "loadout-select__ability-name--none";
        public const string ConfirmClass = "loadout-select__confirm";
        public const string FocusedConfirmClass = "loadout-select__confirm--focused";
        public const string StatusClass = "loadout-select__status";
        public const string ErrorStatusClass = "loadout-select__status--error";

        public const string PresetsClass = "loadout-select__presets";
        public const string FocusedPresetsClass = "loadout-select__presets--focused";
        public const string PresetTitleClass = "loadout-select__preset-title";
        public const string PresetPickerClass = "loadout-select__preset-picker";
        public const string PresetStepClass = "loadout-select__preset-step";
        public const string PresetNameClass = "loadout-select__preset-name";
        public const string PresetEditClass = "loadout-select__preset-edit";
        public const string PresetNameFieldClass = "loadout-select__preset-name-field";
        public const string PresetActionClass = "loadout-select__preset-action";

        public const string ConfirmButtonName = "loadout-confirm";
        public const string StatusName = "loadout-status";
        public const string PresetNameFieldName = "loadout-preset-name";

        // プリセットの行は武器の枠より上に出すが、行の番号は武器の枠を 0 から数えたまま変えないよう、その前の -1 に置く
        public const int PresetRow = -1;

        public const string StyleSheetResourcePath = "Loadout/LoadoutSelect";

        private static StyleSheet _styleSheet;
        private static bool _warnedMissingStyleSheet;

        private readonly VisualElement _container;
        private readonly LoadoutSelection _selection;
        private readonly AbilitySelection _abilities;
        private readonly ProgressionLedger _ledger;
        private readonly List<VisualElement> _rows = new List<VisualElement>();
        private readonly List<List<Button>> _options = new List<List<Button>>();
        private readonly AbilityCatalogAsset _abilityCatalog;
        private readonly List<VisualElement> _abilityRows = new List<VisualElement>();
        private readonly List<Label> _abilityNames = new List<Label>();
        private readonly List<Button> _abilityPrevious = new List<Button>();
        private readonly List<Button> _abilityNext = new List<Button>();
        private readonly Button _confirm;
        private readonly Label _status;
        private readonly VisualElement _navigationRoot;
        private readonly LoadoutPresetBook _presets;
        private VisualElement _presetRow;
        private Label _presetTitle;
        private Label _presetName;
        private Button _presetPrevious;
        private Button _presetNext;
        private TextField _presetNameField;
        private Button _renamePreset;
        private Button _saveAsNewPreset;
        private int _focusRow;
        private bool _busy;
        private bool _disposed;

        // 能力の枠は abilities が null なら出さない。abilityCatalog は名前を引くのにだけ使い、引けない能力は Id を出す。
        // presets が null ならプリセットの行を出さない。一覧を変えるのは呼ぶ側で、パネルは求めるイベントを出し、変わったら RefreshPresets で出し直す。
        public LoadoutSelectPanel(
            VisualElement container,
            LoadoutSelection selection,
            ProgressionLedger ledger = null,
            AbilitySelection abilities = null,
            AbilityCatalogAsset abilityCatalog = null,
            LoadoutPresetBook presets = null)
        {
            _container = container ?? throw new ArgumentNullException(nameof(container));
            _selection = selection ?? throw new ArgumentNullException(nameof(selection));
            _abilities = abilities;
            _abilityCatalog = abilityCatalog;
            _presets = presets;

            // 使えるかは selection の述語が決める。ledger から使えるかを引き直すと述語と食い違いうるので、鍵に添えるレベルを引くのにだけ使う
            _ledger = ledger;

            container.Clear();
            container.AddToClassList(PanelClass);

            // HUD とメニューは別の UXML なので、どちらにも同じ見た目を当てられるよう入れ物に直接付ける
            var styleSheet = LoadStyleSheet();
            if (styleSheet != null && !container.styleSheets.Contains(styleSheet))
            {
                container.styleSheets.Add(styleSheet);
            }

            var title = new Label(LoadoutFormat.Title);
            title.AddToClassList(TitleClass);
            container.Add(title);

            if (presets != null)
            {
                container.Add(BuildPresetRow());
            }

            for (var slot = 0; slot < selection.SlotCount; slot++)
            {
                container.Add(BuildRow(slot));
            }

            for (var slot = 0; slot < AbilitySlotCount; slot++)
            {
                container.Add(BuildAbilityRow(slot));
            }

            _confirm = new Button(() => Confirm()) { name = ConfirmButtonName, text = LoadoutFormat.Confirm };
            _confirm.AddToClassList(ConfirmClass);
            _confirm.RegisterCallback<PointerEnterEvent>(_ => SetFocusRow(ConfirmRow, false));
            container.Add(_confirm);

            _status = new Label(string.Empty) { name = StatusName };
            _status.AddToClassList(StatusClass);
            _status.style.display = DisplayStyle.None;
            container.Add(_status);

            // 何も選んでいないときの入力はパネルの根に届くので、根で受ける (パネルに付く前なら入れ物で受ける)
            _navigationRoot = container.panel?.visualTree ?? container;
            _navigationRoot.RegisterCallback<NavigationMoveEvent>(HandleMove, TrickleDown.TrickleDown);
            _navigationRoot.RegisterCallback<NavigationSubmitEvent>(HandleSubmit, TrickleDown.TrickleDown);
            _navigationRoot.RegisterCallback<NavigationCancelEvent>(HandleCancel, TrickleDown.TrickleDown);

            _focusRow = 0;
            RefreshAll();
        }

        public event Action<LoadoutChoice> Confirmed;

        public event Action Cancelled;

        // 選び直した組の番号。選択を組の中身で作り直すのは呼ぶ側
        public event Action<int> PresetSelected;

        // 入力欄の名前と今の選択で、新しい組を足してほしいとき
        public event Action<string, LoadoutChoice> PresetSaveAsNewRequested;

        // 入力欄の名前を、選んでいる組の名前にしてほしいとき
        public event Action<string> PresetRenameRequested;

        public LoadoutSelection Selection => _selection;

        public LoadoutPresetBook Presets => _presets;

        public bool HasPresets => _presets != null;

        public TextField PresetNameField => _presetNameField;

        public Button PresetPreviousButton => _presetPrevious;

        public Button PresetNextButton => _presetNext;

        public Button RenamePresetButton => _renamePreset;

        public Button SaveAsNewPresetButton => _saveAsNewPreset;

        public string PresetTitle => _presetTitle?.text;

        public string PresetName => _presetName?.text;

        public AbilitySelection Abilities => _abilities;

        public VisualElement Container => _container;

        public int FocusRow => _focusRow;

        public int AbilitySlotCount => _abilities?.SlotCount ?? 0;

        public AbilityCatalogAsset AbilityCatalog => _abilityCatalog;

        // 行は武器の枠、能力の枠、決定の順に並ぶ
        public int ConfirmRow => _selection.SlotCount + AbilitySlotCount;

        public Button ConfirmButton => _confirm;

        public string Status => _status.style.display == DisplayStyle.None ? null : _status.text;

        public bool IsStatusError => _status.ClassListContains(ErrorStatusClass);

        public bool IsBusy => _busy;

        public Button OptionButton(int slot, int option)
        {
            return _options[slot][option];
        }

        public int AbilityRow(int slot)
        {
            RequireAbilitySlot(slot);
            return _selection.SlotCount + slot;
        }

        public string AbilityName(int slot)
        {
            RequireAbilitySlot(slot);
            return _abilityNames[slot].text;
        }

        public Button AbilityPreviousButton(int slot)
        {
            RequireAbilitySlot(slot);
            return _abilityPrevious[slot];
        }

        public Button AbilityNextButton(int slot)
        {
            RequireAbilitySlot(slot);
            return _abilityNext[slot];
        }

        public void MoveFocus(int step)
        {
            if (_busy || step == 0)
            {
                return;
            }

            var first = HasPresets ? PresetRow : 0;
            var count = ConfirmRow + 1 - first;
            var position = _focusRow - first;
            SetFocusRow(((position + step) % count + count) % count + first, true);
        }

        public bool Cycle(int step)
        {
            if (_focusRow == PresetRow)
            {
                return CyclePreset(step);
            }

            if (_busy || _focusRow >= ConfirmRow)
            {
                return false;
            }

            if (_focusRow >= _selection.SlotCount)
            {
                if (!CycleAbilityAt(_focusRow - _selection.SlotCount, step))
                {
                    return false;
                }

                FocusCurrent();
                return true;
            }

            if (!_selection.Cycle(_focusRow, step))
            {
                return false;
            }

            ClearStatus();
            RefreshRow(_focusRow);
            FocusCurrent();
            return true;
        }

        // 前後のボタンを押したときの入口。押したボタンがフォーカスを持つので、ここではフォーカスを動かさない。
        public bool CycleAbility(int slot, int step)
        {
            if (_busy || slot < 0 || slot >= AbilitySlotCount)
            {
                return false;
            }

            SetFocusRow(_selection.SlotCount + slot, false);
            return CycleAbilityAt(slot, step);
        }

        public bool Select(int slot, int option)
        {
            if (_busy || slot < 0 || slot >= _selection.SlotCount || !_selection.Select(slot, option))
            {
                return false;
            }

            ClearStatus();
            RefreshRow(slot);
            SetFocusRow(slot, false);
            return true;
        }

        public bool Confirm()
        {
            if (_busy)
            {
                return false;
            }

            var reason = _selection.Validate();
            if (reason != null)
            {
                ShowStatus(LoadoutFormat.Rejected(reason), true);
                return false;
            }

            Confirmed?.Invoke(new LoadoutChoice(_selection.WeaponIds, _abilities?.AbilityIds));
            return true;
        }

        // 組が 1 つしか無ければ切り替える先が無い。最後の組の次は最初の組に戻る
        public bool CyclePreset(int step)
        {
            if (_busy || !HasPresets || step == 0 || _presets.Count < 2)
            {
                return false;
            }

            var count = _presets.Count;
            var current = Math.Max(0, _presets.ActiveIndex);
            var next = (int)(((current + (long)step) % count + count) % count);
            if (next == current)
            {
                return false;
            }

            SetFocusRow(PresetRow, false);
            PresetSelected?.Invoke(next);
            return true;
        }

        public bool SaveAsNewPreset()
        {
            if (_busy || !HasPresets)
            {
                return false;
            }

            if (_presets.IsFull)
            {
                ShowStatus(LoadoutFormat.PresetFull(_presets.MaxPresets), true);
                return false;
            }

            var reason = _selection.Validate();
            if (reason != null)
            {
                ShowStatus(LoadoutFormat.Rejected(reason), true);
                return false;
            }

            PresetSaveAsNewRequested?.Invoke(_presetNameField.value, new LoadoutChoice(_selection.WeaponIds, _abilities?.AbilityIds));
            return true;
        }

        public bool RenamePreset()
        {
            if (_busy || !HasPresets || _presets.Active == null)
            {
                return false;
            }

            PresetRenameRequested?.Invoke(_presetNameField.value);
            return true;
        }

        public void FocusPresets()
        {
            if (HasPresets)
            {
                SetFocusRow(PresetRow, true);
            }
        }

        // 呼ぶ側が組の一覧を変えた後に、選んでいる組の名前・番号と、ボタンを押せるかを出し直す
        public void RefreshPresets()
        {
            if (!HasPresets)
            {
                return;
            }

            var active = _presets.Active;
            var index = _presets.ActiveIndex;
            _presetTitle.text = active != null ? LoadoutFormat.PresetTitle(index, _presets.Count) : string.Empty;
            _presetName.text = active != null ? LoadoutFormat.PresetName(active, index) : string.Empty;
            _presetNameField.SetValueWithoutNotify(active?.Name ?? string.Empty);
            RefreshPresetButtons();
        }

        public void ShowStatus(string text, bool error)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                ClearStatus();
                return;
            }

            _status.text = text;
            _status.EnableInClassList(ErrorStatusClass, error);
            _status.style.display = DisplayStyle.Flex;
        }

        // 送った答えを待つ間の二度押しを防ぐため、busy の間はボタンも移動・選択・決定も止める。
        public void SetBusy(bool busy)
        {
            _busy = busy;
            for (var slot = 0; slot < _options.Count; slot++)
            {
                var row = _options[slot];
                for (var option = 0; option < row.Count; option++)
                {
                    row[option].SetEnabled(!busy && _selection.IsAvailableAt(slot, option));
                }
            }

            for (var slot = 0; slot < _abilityRows.Count; slot++)
            {
                var canCycle = !busy && HasAbilityChoices(slot);
                _abilityPrevious[slot].SetEnabled(canCycle);
                _abilityNext[slot].SetEnabled(canCycle);
            }

            _confirm.SetEnabled(!busy);
            RefreshPresetButtons();
            if (!busy)
            {
                FocusCurrent();
            }
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _navigationRoot.UnregisterCallback<NavigationMoveEvent>(HandleMove, TrickleDown.TrickleDown);
            _navigationRoot.UnregisterCallback<NavigationSubmitEvent>(HandleSubmit, TrickleDown.TrickleDown);
            _navigationRoot.UnregisterCallback<NavigationCancelEvent>(HandleCancel, TrickleDown.TrickleDown);
            _container.Clear();
            _container.RemoveFromClassList(PanelClass);
            if (_styleSheet != null)
            {
                _container.styleSheets.Remove(_styleSheet);
            }
            Confirmed = null;
            Cancelled = null;
            PresetSelected = null;
            PresetSaveAsNewRequested = null;
            PresetRenameRequested = null;
        }

        private static StyleSheet LoadStyleSheet()
        {
            if (_styleSheet == null)
            {
                _styleSheet = Resources.Load<StyleSheet>(StyleSheetResourcePath);
            }

            if (_styleSheet == null && !_warnedMissingStyleSheet)
            {
                _warnedMissingStyleSheet = true;
                Debug.LogWarning(
                    $"{LoadoutPreferenceStore.LogTag} style sheet 'Resources/{StyleSheetResourcePath}.uss' was not found; the loadout screen is unstyled");
            }

            return _styleSheet;
        }

        // 前後のボタンで組を切り替え、その下の入力欄で名前を変えるか、今の選択を新しい組として保存する
        private VisualElement BuildPresetRow()
        {
            _presetRow = new VisualElement();
            _presetRow.AddToClassList(PresetsClass);
            _presetRow.RegisterCallback<PointerEnterEvent>(_ => SetFocusRow(PresetRow, false));

            _presetTitle = new Label(string.Empty);
            _presetTitle.AddToClassList(PresetTitleClass);
            _presetRow.Add(_presetTitle);

            var picker = new VisualElement();
            picker.AddToClassList(PresetPickerClass);
            _presetRow.Add(picker);

            _presetPrevious = new Button(() => CyclePreset(-1)) { text = LoadoutFormat.PreviousPreset };
            _presetPrevious.AddToClassList(PresetStepClass);
            picker.Add(_presetPrevious);

            _presetName = new Label(string.Empty) { pickingMode = PickingMode.Ignore };
            _presetName.AddToClassList(PresetNameClass);
            picker.Add(_presetName);

            _presetNext = new Button(() => CyclePreset(1)) { text = LoadoutFormat.NextPreset };
            _presetNext.AddToClassList(PresetStepClass);
            picker.Add(_presetNext);

            var edit = new VisualElement();
            edit.AddToClassList(PresetEditClass);
            _presetRow.Add(edit);

            _presetNameField = new TextField(LoadoutFormat.PresetNameLabel)
            {
                name = PresetNameFieldName,
                maxLength = _presets.Spec.MaxNameLength,
            };
            _presetNameField.AddToClassList(PresetNameFieldClass);
            edit.Add(_presetNameField);

            _renamePreset = new Button(() => RenamePreset()) { text = LoadoutFormat.RenamePreset };
            _renamePreset.AddToClassList(PresetActionClass);
            edit.Add(_renamePreset);

            _saveAsNewPreset = new Button(() => SaveAsNewPreset()) { text = LoadoutFormat.SaveAsNewPreset };
            _saveAsNewPreset.AddToClassList(PresetActionClass);
            edit.Add(_saveAsNewPreset);

            RefreshPresets();
            return _presetRow;
        }

        private void RefreshPresetButtons()
        {
            if (!HasPresets)
            {
                return;
            }

            var canCycle = !_busy && _presets.Count > 1;
            _presetPrevious.SetEnabled(canCycle);
            _presetNext.SetEnabled(canCycle);
            _presetNameField.SetEnabled(!_busy);
            _renamePreset.SetEnabled(!_busy && _presets.Active != null);

            // 上限に達していても押せるままにして、押したときに上限を知らせる
            _saveAsNewPreset.SetEnabled(!_busy);
        }

        private bool IsInPresetNameField(EventBase evt)
        {
            return _presetNameField != null && evt.target is VisualElement target
                && (target == _presetNameField || _presetNameField.Contains(target));
        }

        private VisualElement BuildRow(int slot)
        {
            var row = new VisualElement();
            row.AddToClassList(SlotClass);

            var title = new Label(LoadoutFormat.SlotTitle(slot));
            title.AddToClassList(SlotTitleClass);
            row.Add(title);

            var list = new VisualElement();
            list.AddToClassList(OptionsClass);
            row.Add(list);

            var buttons = new List<Button>();
            var options = _selection.OptionsAt(slot);
            if (options.Count == 0)
            {
                var none = new Label(LoadoutFormat.NoOption);
                none.AddToClassList(NoOptionClass);
                list.Add(none);
            }

            for (var i = 0; i < options.Count; i++)
            {
                var option = i;
                var button = new Button(() => Select(slot, option)) { text = string.Empty };
                button.AddToClassList(OptionClass);

                var weaponName = new Label(GameText.Resolve(DisplayNames.Of(options[i]))) { pickingMode = PickingMode.Ignore };
                weaponName.AddToClassList(OptionNameClass);
                button.Add(weaponName);

                var category = new Label(CategoryText(options[i].Category)) { pickingMode = PickingMode.Ignore };
                category.AddToClassList(OptionCategoryClass);
                button.Add(category);

                if (!_selection.IsAvailableAt(slot, i))
                {
                    button.AddToClassList(LockedOptionClass);
                    button.Add(BuildLock(options[i].Id));
                    button.SetEnabled(false);
                }

                list.Add(button);
                buttons.Add(button);
            }

            _rows.Add(row);
            _options.Add(buttons);
            return row;
        }

        // 能力は候補が多くなりうるので、武器のように全部を並べず、前後のボタンで今の 1 つを替える。
        private VisualElement BuildAbilityRow(int slot)
        {
            var row = new VisualElement();
            row.AddToClassList(SlotClass);
            row.AddToClassList(AbilitySlotClass);

            var title = new Label(LoadoutFormat.AbilitySlotTitle(slot));
            title.AddToClassList(SlotTitleClass);
            row.Add(title);

            var picker = new VisualElement();
            picker.AddToClassList(AbilityPickerClass);
            picker.RegisterCallback<PointerEnterEvent>(_ => SetFocusRow(_selection.SlotCount + slot, false));
            row.Add(picker);

            var previous = new Button(() => CycleAbility(slot, -1)) { text = LoadoutFormat.PreviousAbility };
            previous.AddToClassList(AbilityStepClass);
            picker.Add(previous);

            var abilityName = new Label(string.Empty) { pickingMode = PickingMode.Ignore };
            abilityName.AddToClassList(AbilityNameClass);
            picker.Add(abilityName);

            var next = new Button(() => CycleAbility(slot, 1)) { text = LoadoutFormat.NextAbility };
            next.AddToClassList(AbilityStepClass);
            picker.Add(next);

            var canCycle = HasAbilityChoices(slot);
            previous.SetEnabled(canCycle);
            next.SetEnabled(canCycle);

            _abilityRows.Add(row);
            _abilityNames.Add(abilityName);
            _abilityPrevious.Add(previous);
            _abilityNext.Add(next);
            return row;
        }

        private bool HasAbilityChoices(int slot)
        {
            // 先頭の「なし」しか無い枠は回しても変わらない
            return _abilities.OptionsAt(slot).Count > 1;
        }

        private bool CycleAbilityAt(int slot, int step)
        {
            if (!_abilities.Cycle(slot, step))
            {
                return false;
            }

            ClearStatus();
            RefreshAbilityRow(slot);
            return true;
        }

        private void RefreshAbilityRow(int slot)
        {
            var id = _abilities.SelectedAt(slot);
            var none = string.IsNullOrEmpty(id);
            _abilityNames[slot].text = none ? LoadoutFormat.NoAbility : AbilityText(id);
            _abilityNames[slot].EnableInClassList(NoAbilityClass, none);
        }

        private string AbilityText(string abilityId)
        {
            if (_abilityCatalog == null)
            {
                return abilityId;
            }

            try
            {
                return _abilityCatalog.TryFind(abilityId, out var definition)
                    ? GameText.Resolve(DisplayNames.Of(definition))
                    : abilityId;
            }
            catch (AbilityDefinitionException)
            {
                // 開いた後にカタログが壊れても、選んでいる能力は分かるよう Id を出す
                return abilityId;
            }
        }

        private void RequireAbilitySlot(int slot)
        {
            if (slot < 0 || slot >= AbilitySlotCount)
            {
                throw new ArgumentOutOfRangeException(nameof(slot), slot, "slot must be in [0, " + AbilitySlotCount + ")");
            }
        }

        private VisualElement BuildLock(string weaponId)
        {
            var lockRow = new VisualElement { pickingMode = PickingMode.Ignore };
            lockRow.AddToClassList(OptionLockClass);

            var icon = new VisualElement { pickingMode = PickingMode.Ignore };
            icon.AddToClassList(LockIconClass);
            var shackle = new VisualElement { pickingMode = PickingMode.Ignore };
            shackle.AddToClassList(LockShackleClass);
            icon.Add(shackle);
            var body = new VisualElement { pickingMode = PickingMode.Ignore };
            body.AddToClassList(LockBodyClass);
            icon.Add(body);
            lockRow.Add(icon);

            var level = new Label(LockText(weaponId)) { pickingMode = PickingMode.Ignore };
            level.AddToClassList(LockLevelClass);
            lockRow.Add(level);
            return lockRow;
        }

        // 述語がアンロックの表とは別の理由で断った武器は表に無くレベル 1 が返るので、その武器には誤ったレベルを出さない
        private string LockText(string weaponId)
        {
            var level = _ledger?.UnlockLevelOf(weaponId) ?? 1;
            return level > 1 ? ProgressionFormat.LockedUntil(level) : ProgressionFormat.Locked;
        }

        // 新しい種別がまだ表示名を持たなくても画面ごと出せなくならないよう、列挙の名前を出す
        private static string CategoryText(WeaponCategory category)
        {
            try
            {
                return LoadoutFormat.Category(category);
            }
            catch (ArgumentOutOfRangeException)
            {
                return category.ToString();
            }
        }

        private void SetFocusRow(int row, bool focusElement)
        {
            if (_busy)
            {
                return;
            }

            _focusRow = row;
            RefreshFocusClasses();
            if (focusElement)
            {
                FocusCurrent();
            }
        }

        private void RefreshAll()
        {
            for (var slot = 0; slot < _rows.Count; slot++)
            {
                RefreshRow(slot);
            }

            for (var slot = 0; slot < _abilityRows.Count; slot++)
            {
                RefreshAbilityRow(slot);
            }

            RefreshFocusClasses();
            FocusCurrent();
        }

        private void RefreshRow(int slot)
        {
            var selected = _selection.SelectedIndexAt(slot);
            var buttons = _options[slot];
            for (var i = 0; i < buttons.Count; i++)
            {
                buttons[i].EnableInClassList(SelectedOptionClass, i == selected);
            }
        }

        private void RefreshFocusClasses()
        {
            for (var i = 0; i < _rows.Count; i++)
            {
                _rows[i].EnableInClassList(FocusedSlotClass, i == _focusRow);
            }

            for (var i = 0; i < _abilityRows.Count; i++)
            {
                _abilityRows[i].EnableInClassList(FocusedSlotClass, _selection.SlotCount + i == _focusRow);
            }

            _confirm.EnableInClassList(FocusedConfirmClass, _focusRow == ConfirmRow);
            _presetRow?.EnableInClassList(FocusedPresetsClass, _focusRow == PresetRow);
        }

        private void FocusCurrent()
        {
            // PauseMenu は開くときに 1 度しかフォーカスを取らないので、ここで奪うとポーズのボタンへ戻らなくなる
            if (PauseMenu.IsAnyOpen)
            {
                return;
            }

            var target = CurrentFocusTarget();
            if (target == null)
            {
                // 選べる武器が無い行では UI のフォーカスを外し、行の強調だけで選んでいることを示す
                _container.focusController?.focusedElement?.Blur();
                return;
            }

            target.Focus();

            // パネルに付いた直後は配置が終わっておらず Focus が効かないことがあるので、次の更新でもう一度選ぶ
            if (target.panel != null && target.focusController?.focusedElement != target)
            {
                var row = _focusRow;
                target.schedule.Execute(() =>
                {
                    if (!_disposed && _focusRow == row && !PauseMenu.IsAnyOpen)
                    {
                        target.Focus();
                    }
                });
            }
        }

        private Button CurrentFocusTarget()
        {
            if (_focusRow == PresetRow)
            {
                // 組の切り替えは左右の入力で済むので、決定は常に「新しく保存」に当てる
                return _saveAsNewPreset;
            }

            if (_focusRow >= ConfirmRow)
            {
                return _confirm;
            }

            if (_focusRow >= _selection.SlotCount)
            {
                // 左右の入力はパネルの根で受けて回すので、どちらのボタンにフォーカスを置いても同じに動く
                var slot = _focusRow - _selection.SlotCount;
                return HasAbilityChoices(slot) ? _abilityNext[slot] : null;
            }

            var selected = _selection.SelectedIndexAt(_focusRow);
            return selected >= 0 ? _options[_focusRow][selected] : null;
        }

        private void ClearStatus()
        {
            _status.text = string.Empty;
            _status.RemoveFromClassList(ErrorStatusClass);
            _status.style.display = DisplayStyle.None;
        }

        private void HandleMove(NavigationMoveEvent evt)
        {
            // 同じパネルの根を共有するポーズメニューが開いている間は、入力をそちらに任せる。名前の入力中の左右は文字の間を動かすのに使う
            if (PauseMenu.IsAnyOpen || IsInPresetNameField(evt))
            {
                return;
            }

            evt.StopPropagation();
            _navigationRoot.focusController?.IgnoreEvent(evt);
            switch (evt.direction)
            {
                case NavigationMoveEvent.Direction.Up:
                case NavigationMoveEvent.Direction.Previous:
                    MoveFocus(-1);
                    break;
                case NavigationMoveEvent.Direction.Down:
                case NavigationMoveEvent.Direction.Next:
                    MoveFocus(1);
                    break;
                case NavigationMoveEvent.Direction.Left:
                    Cycle(-1);
                    break;
                case NavigationMoveEvent.Direction.Right:
                    Cycle(1);
                    break;
            }
        }

        private void HandleSubmit(NavigationSubmitEvent evt)
        {
            if (PauseMenu.IsAnyOpen)
            {
                return;
            }

            // フォーカスしているボタンにも届くと、選び直しと決定が二重に走るので、ここで止めて行の順で扱う
            evt.StopPropagation();
            _navigationRoot.focusController?.IgnoreEvent(evt);

            // 名前の入力中に決定を押したら、打った名前で組の名前を変える
            if (IsInPresetNameField(evt))
            {
                RenamePreset();
                return;
            }

            if (_focusRow == PresetRow)
            {
                // フォーカス中のボタンへの決定は上で止めているので、押されたボタンの動作をここで呼ぶ。
                // それ以外は、文字を打たずに押せる「新しく保存」を割り当てる
                if (evt.target == _renamePreset)
                {
                    RenamePreset();
                }
                else if (evt.target == _presetPrevious)
                {
                    CyclePreset(-1);
                }
                else if (evt.target == _presetNext)
                {
                    CyclePreset(1);
                }
                else
                {
                    SaveAsNewPreset();
                }
            }
            else if (_focusRow >= ConfirmRow)
            {
                Confirm();
            }
            else
            {
                MoveFocus(1);
            }
        }

        private void HandleCancel(NavigationCancelEvent evt)
        {
            if (PauseMenu.IsAnyOpen)
            {
                return;
            }

            evt.StopPropagation();
            _navigationRoot.focusController?.IgnoreEvent(evt);
            Cancelled?.Invoke();
        }
    }
}
