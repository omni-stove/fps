using System;
using System.Collections.Generic;
using Fps.Core;
using UnityEngine.UIElements;

namespace Fps.Runtime
{
    /// <summary>
    /// キャンペーンのミッションと難易度を選び、続きから／最初から始める画面の要素を、渡された入れ物の中にコードで作って
    /// <see cref="CampaignMenu"/> につなぐ。<see cref="MainMenuScreen"/> がメニューの差し込み口に出す。見た目は MainMenu.uss の campaign-select。
    /// </summary>
    /// <remarks>
    /// ミッションを上から並べ、その下に難易度の行、選んだミッションの始め方 (セーブがあれば「続きから」「最初から」、無ければ「始める」) と「戻る」を置く。
    /// 「最初から」は確認の 2 つのボタンに入れ替え、確認してから <see cref="StartRequested"/> を呼ぶ。
    /// マウスではボタンを押して選ぶ。ゲームパッドとキーボードでは上下 (Tab) でフォーカスを動かし、難易度の行では左右で難易度を替え、
    /// Submit で決定、Cancel で確認を閉じるかメニューへ戻る (<see cref="Cancelled"/>)。
    /// 既定の移動は画面上の位置で次を選ぶので、パネルの根で移動・決定・取り消しを受けて止め、<see cref="CampaignMenu.Items"/> の順で動かす。
    /// </remarks>
    public sealed class CampaignSelectPanel : IDisposable
    {
        public const string PanelClass = "campaign-select";
        public const string TitleClass = "campaign-select__title";
        public const string SectionClass = "campaign-select__section";
        public const string MissionClass = "campaign-select__mission";
        public const string MissionNameClass = "campaign-select__mission-name";
        public const string SaveBadgeClass = "campaign-select__save-badge";
        public const string SelectedClass = "campaign-select__item--selected";
        public const string FocusedClass = "campaign-select__item--focused";
        public const string DifficultyRowClass = "campaign-select__difficulty";
        public const string DifficultyTitleClass = "campaign-select__difficulty-title";
        public const string DifficultyOptionsClass = "campaign-select__difficulty-options";
        public const string DifficultyOptionClass = "campaign-select__difficulty-option";
        public const string ActionClass = "campaign-select__action";
        public const string QuestionClass = "campaign-select__question";
        public const string StatusClass = "campaign-select__status";
        public const string ErrorStatusClass = "campaign-select__status--error";

        public const string SelectionName = "campaign-selection";
        public const string ConfirmName = "campaign-confirm";
        public const string QuestionName = "campaign-question";
        public const string StatusName = "campaign-status";

        private static readonly CampaignMenuItemKind[] _selectionActions =
        {
            CampaignMenuItemKind.Continue, CampaignMenuItemKind.Restart, CampaignMenuItemKind.Start, CampaignMenuItemKind.Back,
        };

        private static readonly CampaignMenuItemKind[] _confirmActions =
        {
            CampaignMenuItemKind.ConfirmRestart, CampaignMenuItemKind.CancelRestart,
        };

        private readonly VisualElement _container;
        private readonly CampaignMenu _menu;
        private readonly VisualElement _selection;
        private readonly VisualElement _confirm;
        private readonly Label _question;
        private readonly Label _status;
        private readonly List<Button> _missionButtons = new List<Button>();
        private readonly List<Label> _saveBadges = new List<Label>();
        private readonly VisualElement _difficultyRow;
        private readonly Dictionary<CampaignDifficulty, Button> _difficultyButtons = new Dictionary<CampaignDifficulty, Button>();
        private readonly Dictionary<CampaignMenuItemKind, Button> _actionButtons = new Dictionary<CampaignMenuItemKind, Button>();
        private readonly VisualElement _navigationRoot;
        private bool _busy;
        private bool _disposed;

        /// <summary>container を空にして menu の項目を作り、menu のフォーカスを選んだ状態にする。</summary>
        /// <exception cref="ArgumentNullException">container か menu が null。</exception>
        public CampaignSelectPanel(VisualElement container, CampaignMenu menu)
        {
            _container = container ?? throw new ArgumentNullException(nameof(container));
            _menu = menu ?? throw new ArgumentNullException(nameof(menu));

            container.Clear();
            container.AddToClassList(PanelClass);

            var title = new Label(CampaignMenuFormat.Title);
            title.AddToClassList(TitleClass);
            container.Add(title);

            _selection = new VisualElement { name = SelectionName };
            _selection.AddToClassList(SectionClass);
            container.Add(_selection);

            for (var i = 0; i < menu.Missions.Count; i++)
            {
                _selection.Add(BuildMission(i));
            }

            _difficultyRow = BuildDifficultyRow();
            _selection.Add(_difficultyRow);
            foreach (var kind in _selectionActions)
            {
                _selection.Add(BuildAction(kind));
            }

            _confirm = new VisualElement { name = ConfirmName };
            _confirm.AddToClassList(SectionClass);
            _question = new Label(string.Empty) { name = QuestionName };
            _question.AddToClassList(QuestionClass);
            _confirm.Add(_question);
            foreach (var kind in _confirmActions)
            {
                _confirm.Add(BuildAction(kind));
            }

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

            Refresh();
        }

        /// <summary>始め方を決めたとき。セーブを消す・シーンを読むのは受けた側。</summary>
        public event Action<CampaignMenuStart> StartRequested;

        /// <summary>「戻る」か、確認を出していないときの Cancel。閉じるかどうかは受けた側が決める。</summary>
        public event Action Cancelled;

        public CampaignMenu Menu => _menu;

        public VisualElement Container => _container;

        /// <summary>出している案内。出していなければ null。</summary>
        public string Status => _status.style.display == DisplayStyle.None ? null : _status.text;

        public bool IsStatusError => _status.ClassListContains(ErrorStatusClass);

        /// <summary>受けた側の処理 (シーンを読むなど) を待っていて、操作を受け付けていないか (<see cref="SetBusy"/>)。</summary>
        public bool IsBusy => _busy;

        /// <summary>確認の問いの文言。確認を出していなければ null。</summary>
        public string Question => _menu.IsConfirmingRestart ? _question.text : null;

        public Button MissionButton(int index)
        {
            return _missionButtons[index];
        }

        public bool HasSaveBadge(int index)
        {
            return _saveBadges[index].style.display != DisplayStyle.None;
        }

        public Button DifficultyButton(CampaignDifficulty difficulty)
        {
            return _difficultyButtons[difficulty];
        }

        /// <summary>kind のボタン (始め方・戻る・確認)。</summary>
        public Button ActionButton(CampaignMenuItemKind kind)
        {
            return _actionButtons[kind];
        }

        /// <summary>kind のボタンを出しているか。</summary>
        public bool IsShown(CampaignMenuItemKind kind)
        {
            var button = _actionButtons[kind];
            var section = Array.IndexOf(_confirmActions, kind) >= 0 ? _confirm : _selection;
            return section.style.display != DisplayStyle.None && button.style.display != DisplayStyle.None;
        }

        /// <summary>フォーカスを step だけ動かす。</summary>
        public void MoveFocus(int step)
        {
            if (_busy)
            {
                return;
            }

            _menu.MoveFocus(step);
            ClearStatus();
            Refresh();
        }

        /// <summary>難易度の行を選んでいれば、難易度を step だけ替える。替わったら true。</summary>
        public bool CycleDifficulty(int step)
        {
            if (_busy || _menu.FocusedItem.Kind != CampaignMenuItemKind.Difficulty || !_menu.CycleDifficulty(step))
            {
                return false;
            }

            Refresh();
            return true;
        }

        /// <summary>フォーカスしている項目を決定する。</summary>
        public void Submit()
        {
            if (_busy)
            {
                return;
            }

            Handle(_menu.Activate());
        }

        /// <summary>取り消し。確認を出していれば閉じ、そうでなければ <see cref="Cancelled"/> を呼ぶ。</summary>
        public void Cancel()
        {
            if (_busy)
            {
                return;
            }

            Handle(_menu.Cancel());
        }

        /// <summary>kind のボタンを押す (マウス)。今出ていなければ何もしない。</summary>
        public void Press(CampaignMenuItemKind kind)
        {
            if (_busy)
            {
                return;
            }

            Handle(_menu.Activate(kind));
        }

        /// <summary>index 番目のミッションを選び、その行にフォーカスを移す (マウス)。</summary>
        public void ChooseMission(int index)
        {
            if (_busy || !_menu.FocusOn(_menu.IndexOf(CampaignMenuItemKind.Mission, index)))
            {
                return;
            }

            ClearStatus();
            Refresh();
        }

        /// <summary>難易度を difficulty にし、難易度の行にフォーカスを移す (マウス)。</summary>
        public void ChooseDifficulty(CampaignDifficulty difficulty)
        {
            if (_busy || !_menu.SetDifficulty(difficulty))
            {
                return;
            }

            _menu.FocusOn(_menu.IndexOf(CampaignMenuItemKind.Difficulty));
            Refresh();
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

        /// <summary>busy の間はボタンを押せなくし、移動・決定・取り消しを受け付けない (シーンを読むまでの二度押しを防ぐ)。</summary>
        public void SetBusy(bool busy)
        {
            _busy = busy;
            foreach (var button in AllButtons())
            {
                button.SetEnabled(!busy);
            }

            if (!busy)
            {
                Refresh();
            }
        }

        /// <summary>メニューの状態 (セーブの有無・確認・選択・フォーカス) を画面に映し直す。</summary>
        public void Refresh()
        {
            if (_disposed)
            {
                return;
            }

            var confirming = _menu.IsConfirmingRestart;
            SetDisplayed(_selection, !confirming);
            SetDisplayed(_confirm, confirming);
            _question.text = confirming ? CampaignMenuFormat.ConfirmRestartQuestion(_menu.SelectedMission.DisplayName) : string.Empty;

            for (var i = 0; i < _missionButtons.Count; i++)
            {
                _missionButtons[i].EnableInClassList(SelectedClass, i == _menu.SelectedMissionIndex);
                SetDisplayed(_saveBadges[i], _menu.Missions[i].HasSave);
            }

            foreach (var pair in _difficultyButtons)
            {
                pair.Value.EnableInClassList(SelectedClass, pair.Key == _menu.Difficulty);
            }

            foreach (var pair in _actionButtons)
            {
                SetDisplayed(pair.Value, _menu.IndexOf(pair.Key) >= 0);
            }

            var focused = _menu.FocusedItem;
            for (var i = 0; i < _missionButtons.Count; i++)
            {
                _missionButtons[i].EnableInClassList(FocusedClass, focused.Kind == CampaignMenuItemKind.Mission && focused.MissionIndex == i);
            }

            _difficultyRow.EnableInClassList(FocusedClass, focused.Kind == CampaignMenuItemKind.Difficulty);
            foreach (var pair in _actionButtons)
            {
                pair.Value.EnableInClassList(FocusedClass, focused.Kind == pair.Key);
            }

            FocusCurrent();
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
            StartRequested = null;
            Cancelled = null;
        }

        private VisualElement BuildMission(int index)
        {
            var mission = _menu.Missions[index];
            var button = new Button(() => ChooseMission(index)) { text = string.Empty };
            button.AddToClassList(MissionClass);

            var missionName = new Label(mission.DisplayName) { pickingMode = PickingMode.Ignore };
            missionName.AddToClassList(MissionNameClass);
            button.Add(missionName);

            var badge = new Label(CampaignMenuFormat.SaveBadge) { pickingMode = PickingMode.Ignore };
            badge.AddToClassList(SaveBadgeClass);
            button.Add(badge);

            _missionButtons.Add(button);
            _saveBadges.Add(badge);
            return button;
        }

        private VisualElement BuildDifficultyRow()
        {
            var row = new VisualElement();
            row.AddToClassList(DifficultyRowClass);

            var title = new Label(CampaignMenuFormat.DifficultyTitle);
            title.AddToClassList(DifficultyTitleClass);
            row.Add(title);

            var options = new VisualElement();
            options.AddToClassList(DifficultyOptionsClass);
            row.Add(options);

            foreach (var difficulty in CampaignMenu.Difficulties)
            {
                var chosen = difficulty;
                var button = new Button(() => ChooseDifficulty(chosen)) { text = CampaignMenuFormat.DifficultyLabel(difficulty) };
                button.AddToClassList(DifficultyOptionClass);
                button.RegisterCallback<PointerEnterEvent>(_ => Hover(_menu.IndexOf(CampaignMenuItemKind.Difficulty)));
                options.Add(button);
                _difficultyButtons.Add(difficulty, button);
            }

            return row;
        }

        private Button BuildAction(CampaignMenuItemKind kind)
        {
            var button = new Button(() => Press(kind)) { text = CampaignMenuFormat.ItemLabel(kind) };
            button.AddToClassList(ActionClass);

            // ミッションの行は指しただけで選び直すと押す前に始め方が替わるので、始め方のボタンだけ指したときにフォーカスを移す
            button.RegisterCallback<PointerEnterEvent>(_ => Hover(_menu.IndexOf(kind)));
            _actionButtons.Add(kind, button);
            return button;
        }

        private void Hover(int index)
        {
            if (_busy || index < 0 || index == _menu.FocusIndex)
            {
                return;
            }

            _menu.FocusOn(index);
            Refresh();
        }

        private void Handle(CampaignMenuResultKind result)
        {
            ClearStatus();
            switch (result)
            {
                case CampaignMenuResultKind.Start:
                    Refresh();
                    StartRequested?.Invoke(_menu.LastStart);
                    break;
                case CampaignMenuResultKind.Back:
                    Cancelled?.Invoke();
                    break;
                default:
                    Refresh();
                    break;
            }
        }

        private IEnumerable<Button> AllButtons()
        {
            foreach (var button in _missionButtons)
            {
                yield return button;
            }

            foreach (var button in _difficultyButtons.Values)
            {
                yield return button;
            }

            foreach (var button in _actionButtons.Values)
            {
                yield return button;
            }
        }

        private Focusable CurrentFocusTarget()
        {
            var focused = _menu.FocusedItem;
            switch (focused.Kind)
            {
                case CampaignMenuItemKind.Mission:
                    return _missionButtons[focused.MissionIndex];
                case CampaignMenuItemKind.Difficulty:
                    return _difficultyButtons[_menu.Difficulty];
                default:
                    return _actionButtons[focused.Kind];
            }
        }

        private void FocusCurrent()
        {
            if (_busy)
            {
                return;
            }

            var target = CurrentFocusTarget();
            target.Focus();

            // パネルに付いた直後や表示を切り替えた直後は配置が終わっておらず Focus が効かないことがあるので、次の更新でもう一度選ぶ
            if (target.panel != null && target.focusController?.focusedElement != target)
            {
                var index = _menu.FocusIndex;
                target.schedule.Execute(() =>
                {
                    if (!_disposed && !_busy && _menu.FocusIndex == index)
                    {
                        CurrentFocusTarget().Focus();
                    }
                });
            }
        }

        private void ClearStatus()
        {
            _status.text = string.Empty;
            _status.RemoveFromClassList(ErrorStatusClass);
            _status.style.display = DisplayStyle.None;
        }

        private void HandleMove(NavigationMoveEvent evt)
        {
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
                    CycleDifficulty(-1);
                    break;
                case NavigationMoveEvent.Direction.Right:
                    CycleDifficulty(1);
                    break;
            }
        }

        private void HandleSubmit(NavigationSubmitEvent evt)
        {
            // フォーカスしているボタンにも届くと、押したことと決定が二重に走るので、ここで止めて項目の順で扱う
            evt.StopPropagation();
            _navigationRoot.focusController?.IgnoreEvent(evt);
            Submit();
        }

        private void HandleCancel(NavigationCancelEvent evt)
        {
            evt.StopPropagation();
            _navigationRoot.focusController?.IgnoreEvent(evt);
            Cancel();
        }

        private static void SetDisplayed(VisualElement element, bool displayed)
        {
            element.style.display = displayed ? DisplayStyle.Flex : DisplayStyle.None;
        }
    }
}
