using System;
using System.Collections.Generic;
using Fps.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace Fps.Runtime
{
    /// <summary>
    /// ロードアウトを選ぶ画面の要素を、渡された入れ物の中にコードで作って操作をつなぐ。HUD の <see cref="LoadoutSelectView"/> と
    /// メインメニューの <see cref="MainMenuScreen"/> が同じものを使う。
    /// </summary>
    /// <remarks>
    /// スロットごとに 1 行を作り、その行に選べる武器 (<see cref="LoadoutSelection.OptionsAt"/>) を武器名と種別のボタンで並べ、最後に決定のボタンを置く。
    /// マウスではボタンを押して選ぶ。ゲームパッドとキーボードでは上下 (Tab) で行を動かし、左右でその行の武器を替え、
    /// 決定の行で Submit を押すと決める。Cancel で <see cref="Cancelled"/> を呼ぶ。
    /// 既定の移動は画面上の位置で次を選ぶので、パネルの根で移動・決定・取り消しを受けて止め、行の順で動かす。
    /// </remarks>
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
        public const string NoOptionClass = "loadout-select__no-option";
        public const string ConfirmClass = "loadout-select__confirm";
        public const string FocusedConfirmClass = "loadout-select__confirm--focused";
        public const string StatusClass = "loadout-select__status";
        public const string ErrorStatusClass = "loadout-select__status--error";

        public const string ConfirmButtonName = "loadout-confirm";
        public const string StatusName = "loadout-status";

        /// <summary>Resources からの見た目 (Assets/game/UI/Resources/Loadout/LoadoutSelect.uss)。</summary>
        public const string StyleSheetResourcePath = "Loadout/LoadoutSelect";

        private static StyleSheet _styleSheet;
        private static bool _warnedMissingStyleSheet;

        private readonly VisualElement _container;
        private readonly LoadoutSelection _selection;
        private readonly List<VisualElement> _rows = new List<VisualElement>();
        private readonly List<List<Button>> _options = new List<List<Button>>();
        private readonly Button _confirm;
        private readonly Label _status;
        private readonly VisualElement _navigationRoot;
        private int _focusRow;
        private bool _busy;
        private bool _disposed;

        /// <summary>container を空にして selection の行を作り、最初の行を選んだ状態にする。</summary>
        /// <exception cref="ArgumentNullException">container か selection が null。</exception>
        public LoadoutSelectPanel(VisualElement container, LoadoutSelection selection)
        {
            _container = container ?? throw new ArgumentNullException(nameof(container));
            _selection = selection ?? throw new ArgumentNullException(nameof(selection));

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

            for (var slot = 0; slot < selection.SlotCount; slot++)
            {
                container.Add(BuildRow(slot));
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

        /// <summary>決定でルールに合う並びを選んでいたときに、スロット順の武器の Id を渡す。</summary>
        public event Action<IReadOnlyList<string>> Confirmed;

        /// <summary>Cancel を押したとき。閉じるかどうかは受けた側が決める。</summary>
        public event Action Cancelled;

        /// <summary>選んでいる中身。</summary>
        public LoadoutSelection Selection => _selection;

        /// <summary>行を作った入れ物。</summary>
        public VisualElement Container => _container;

        /// <summary>選んでいる行。0 から <see cref="LoadoutSelection.SlotCount"/> - 1 はスロット、<see cref="ConfirmRow"/> は決定のボタン。</summary>
        public int FocusRow => _focusRow;

        /// <summary>決定のボタンの行の番号。</summary>
        public int ConfirmRow => _selection.SlotCount;

        /// <summary>決定のボタン。</summary>
        public Button ConfirmButton => _confirm;

        /// <summary>出している案内。出していなければ null。</summary>
        public string Status => _status.style.display == DisplayStyle.None ? null : _status.text;

        /// <summary>出している案内がエラーか。</summary>
        public bool IsStatusError => _status.ClassListContains(ErrorStatusClass);

        /// <summary>答えを待っていて操作を受け付けていないか (<see cref="SetBusy"/>)。</summary>
        public bool IsBusy => _busy;

        /// <summary>slot 番のスロットの option 番目の武器のボタン。</summary>
        public Button OptionButton(int slot, int option)
        {
            return _options[slot][option];
        }

        /// <summary>選んでいる行を step だけ動かし、端では反対側へ回り込む。</summary>
        public void MoveFocus(int step)
        {
            if (_busy || step == 0)
            {
                return;
            }

            var count = ConfirmRow + 1;
            SetFocusRow(((_focusRow + step) % count + count) % count, true);
        }

        /// <summary>選んでいる行がスロットなら、その武器を step だけ替える (端では回り込む)。替わったら true。</summary>
        public bool Cycle(int step)
        {
            if (_busy || _focusRow >= ConfirmRow || !_selection.Cycle(_focusRow, step))
            {
                return false;
            }

            ClearStatus();
            RefreshRow(_focusRow);
            FocusCurrent();
            return true;
        }

        /// <summary>slot 番のスロットで option 番目の武器を選び、その行を選んでいる行にする。選べたら true。</summary>
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

        /// <summary>
        /// 選んでいる並びを確かめ (<see cref="LoadoutSelection.Validate"/>)、合えば <see cref="Confirmed"/> を呼んで true。
        /// 合わなければ理由をエラーの案内に出して false。答えを待っている間は何もせず false。
        /// </summary>
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

            Confirmed?.Invoke(_selection.WeaponIds);
            return true;
        }

        /// <summary>案内を出す。text が空なら消す。error なら赤くする。</summary>
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

        /// <summary>busy の間はボタンを押せなくし、移動・選択・決定を受け付けない (送った答えを待つ間の二度押しを防ぐ)。</summary>
        public void SetBusy(bool busy)
        {
            _busy = busy;
            foreach (var row in _options)
            {
                foreach (var button in row)
                {
                    button.SetEnabled(!busy);
                }
            }

            _confirm.SetEnabled(!busy);
            if (!busy)
            {
                FocusCurrent();
            }
        }

        /// <summary>根の入力の購読を外し、入れ物を空にする。2 度目からは何もしない。</summary>
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

                var weaponName = new Label(options[i].DisplayName) { pickingMode = PickingMode.Ignore };
                weaponName.AddToClassList(OptionNameClass);
                button.Add(weaponName);

                var category = new Label(CategoryText(options[i].Category)) { pickingMode = PickingMode.Ignore };
                category.AddToClassList(OptionCategoryClass);
                button.Add(category);

                list.Add(button);
                buttons.Add(button);
            }

            _rows.Add(row);
            _options.Add(buttons);
            return row;
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

            _confirm.EnableInClassList(FocusedConfirmClass, _focusRow == ConfirmRow);
        }

        private void FocusCurrent()
        {
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
                    if (!_disposed && _focusRow == row)
                    {
                        target.Focus();
                    }
                });
            }
        }

        private Focusable CurrentFocusTarget()
        {
            if (_focusRow >= ConfirmRow)
            {
                return _confirm;
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
            // 同じパネルの根を共有するポーズメニューが開いている間は、入力をそちらに任せる
            if (PauseMenu.IsAnyOpen)
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
            if (_focusRow >= ConfirmRow)
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
