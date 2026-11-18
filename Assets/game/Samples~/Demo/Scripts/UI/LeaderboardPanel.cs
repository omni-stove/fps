using System;
using System.Collections.Generic;
using Shooter.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace Shooter.Runtime
{
    // 順位は GameSignals.Leaderboard から読み、ボードを選ぶのは GameSignals.LeaderboardControl に頼む。
    // MainMenuPanels.Register で MainMenuPanelKind.Leaderboard に登録して開く。
    public sealed class LeaderboardPanel : IMainMenuPanel
    {
        public const string LogTag = "[Leaderboard]";

        // ゲームが見た目を差し替えるときに置くレイアウト。無ければ同じ名前の要素をコードで組む
        public const string LayoutResourcePath = "Menu/Leaderboard";

        public const string PanelName = "leaderboard";
        public const string TitleName = "leaderboard-title";
        public const string BoardsName = "leaderboard-boards";
        public const string NoticeName = "leaderboard-notice";
        public const string RowsName = "leaderboard-rows";
        public const string RefreshButtonName = "leaderboard-refresh";
        public const string BackButtonName = "leaderboard-back";

        public const string PanelClass = "leaderboard";
        public const string BoardClass = "leaderboard__board";
        public const string SelectedBoardClass = "leaderboard__board--selected";
        public const string RowClass = "leaderboard__row";
        public const string LocalRowClass = "leaderboard__row--local";
        public const string RankClass = "leaderboard__rank";
        public const string NameClass = "leaderboard__name";
        public const string ScoreClass = "leaderboard__score";

        private readonly VisualElement _container;
        private readonly LeaderboardSpec _spec;
        private readonly Label _title;
        private readonly VisualElement _boards;
        private readonly Label _notice;
        private readonly VisualElement _rows;
        private readonly Button _refresh;
        private readonly Button _back;
        private readonly VisualElement _navigationRoot;
        private readonly List<Button> _boardButtons = new List<Button>();
        private readonly List<VisualElement> _rowElements = new List<VisualElement>();
        private readonly List<string> _rowTexts = new List<string>();
        private ILeaderboardControl _openedWith;
        private string _selected;
        private bool _disposed;

        public LeaderboardPanel(VisualElement container)
            : this(container, KitTuning.Leaderboards)
        {
        }

        public LeaderboardPanel(VisualElement container, LeaderboardSpec spec)
        {
            _container = container ?? throw new ArgumentNullException(nameof(container));
            _spec = spec ?? throw new ArgumentNullException(nameof(spec));

            container.Clear();
            var layout = Resources.Load<VisualTreeAsset>(LayoutResourcePath);
            if (layout != null)
            {
                layout.CloneTree(container);
            }
            else
            {
                BuildDefaultLayout(container);
            }

            _title = Require<Label>(TitleName);
            _boards = Require<VisualElement>(BoardsName);
            _notice = Require<Label>(NoticeName);
            _rows = Require<VisualElement>(RowsName);
            _refresh = Require<Button>(RefreshButtonName);
            _back = Require<Button>(BackButtonName);

            foreach (var board in spec.Boards)
            {
                var id = board.Id;
                var button = new Button(() => Select(id)) { name = BoardsName + "-" + id };
                button.AddToClassList(BoardClass);
                button.userData = board;
                _boards.Add(button);
                _boardButtons.Add(button);
            }

            _refresh.clicked += HandleRefreshClicked;
            _back.clicked += Cancel;

            // 何も選んでいないときの入力はパネルの根に届くので、根で受ける (パネルに付く前なら入れ物で受ける)
            _navigationRoot = container.panel?.visualTree ?? container;
            _navigationRoot.RegisterCallback<NavigationCancelEvent>(HandleCancel, TrickleDown.TrickleDown);

            GameSignals.Leaderboard.Changed += HandleStatusChanged;
            GameSignals.LeaderboardControl.Changed += HandleControlChanged;
            Localizer.Changed += HandleLanguageChanged;

            RefreshTexts();
            if (spec.Boards.Count > 0)
            {
                Select(spec.Boards[0].Id);
            }
            else
            {
                Render();
            }

            (_boardButtons.Count > 0 ? _boardButtons[0] : _back).Focus();
        }

        public event Action Cancelled;

        public VisualElement Container => _container;

        public ILeaderboardControl Control => GameSignals.LeaderboardControl.Value;

        public string SelectedBoardId => _selected;

        public IReadOnlyList<Button> BoardButtons => _boardButtons;

        public Button RefreshButton => _refresh;

        public Button BackButton => _back;

        public IReadOnlyList<VisualElement> RowElements => _rowElements;

        public IReadOnlyList<string> RowTexts => _rowTexts;

        public string Notice => _notice.style.display != DisplayStyle.None ? _notice.text : null;

        public bool Select(string boardId)
        {
            if (_disposed || _spec.Find(boardId) == null)
            {
                return false;
            }

            _selected = boardId;
            OpenSelected();
            Render();
            return true;
        }

        public bool Refresh()
        {
            var control = Control;
            if (_disposed || control == null || _selected == null)
            {
                return false;
            }

            return Call("refresh", () => control.Refresh());
        }

        public void Cancel()
        {
            if (!_disposed)
            {
                Cancelled?.Invoke();
            }
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            GameSignals.Leaderboard.Changed -= HandleStatusChanged;
            GameSignals.LeaderboardControl.Changed -= HandleControlChanged;
            Localizer.Changed -= HandleLanguageChanged;
            _navigationRoot.UnregisterCallback<NavigationCancelEvent>(HandleCancel, TrickleDown.TrickleDown);
            _refresh.clicked -= HandleRefreshClicked;
            _back.clicked -= Cancel;

            // 閉じた画面のために読み直し続けないよう、開いたボードを閉じてもらう
            var control = Control;
            if (control != null && ReferenceEquals(control, _openedWith))
            {
                Call("close", () =>
                {
                    control.Close();
                    return true;
                });
            }

            _openedWith = null;
            _container.Clear();
            Cancelled = null;
        }

        private static void BuildDefaultLayout(VisualElement container)
        {
            var panel = new VisualElement { name = PanelName };
            panel.AddToClassList(PanelClass);
            panel.Add(new Label { name = TitleName });
            panel.Add(new VisualElement { name = BoardsName });
            panel.Add(new Label { name = NoticeName });
            panel.Add(new ScrollView { name = RowsName });
            panel.Add(new Button { name = RefreshButtonName });
            panel.Add(new Button { name = BackButtonName });
            container.Add(panel);
        }

        private void OpenSelected()
        {
            var control = Control;
            _openedWith = control;
            if (control == null || _selected == null)
            {
                return;
            }

            var selected = _selected;
            Call("open " + selected, () => control.Open(selected));
        }

        private bool Call(string action, Func<bool> call)
        {
            try
            {
                var accepted = call();
                Debug.Log($"{LogTag} asked to {action}{(accepted ? string.Empty : " (refused)")}");
                return accepted;
            }
            catch (Exception e)
            {
                Debug.LogError($"{LogTag} could not {action}: {e.Message}");
                Debug.LogException(e);
                return false;
            }
        }

        private void Render()
        {
            if (_disposed)
            {
                return;
            }

            foreach (var button in _boardButtons)
            {
                var board = (LeaderboardDefinition)button.userData;
                button.EnableInClassList(SelectedBoardClass, board.Id == _selected);
            }

            var status = GameSignals.Leaderboard.Value;
            if (status.HasValue && status.Value.BoardId != _selected)
            {
                // 前に開いたボードの結果は、選んだボードの読み込み中として扱う
                status = null;
            }

            var control = Control;
            var notice = LeaderboardText.Notice(status, control != null, _spec.Boards.Count > 0);
            _notice.text = Localizer.Resolve(notice);
            _notice.style.display = notice.IsEmpty ? DisplayStyle.None : DisplayStyle.Flex;

            var showRows = status.HasValue && status.Value.Stage != LeaderboardStage.Unavailable && control != null;
            RenderRows(showRows ? status.Value.Rows : Array.Empty<LeaderboardRow>());
            _refresh.SetEnabled(control != null && _selected != null && (!status.HasValue || status.Value.Stage != LeaderboardStage.Loading));
        }

        // 行は 1 ページ (LeaderboardSpec.PageSize) までで、変わるのは知らせが届いたときと言語が変わったときだけなので、差分を取らずに作り直す。
        private void RenderRows(IReadOnlyList<LeaderboardRow> rows)
        {
            _rows.Clear();
            _rowElements.Clear();
            _rowTexts.Clear();
            foreach (var row in rows)
            {
                var element = new VisualElement { pickingMode = PickingMode.Ignore };
                element.AddToClassList(RowClass);
                element.EnableInClassList(LocalRowClass, row.IsLocal);

                var rank = Localizer.Resolve(LeaderboardText.Rank(row));
                var name = Localizer.Resolve(LeaderboardText.PlayerName(row));
                var score = Localizer.Resolve(LeaderboardText.Score(row));
                element.Add(Cell(rank, RankClass));
                element.Add(Cell(name, NameClass));
                element.Add(Cell(score, ScoreClass));

                _rows.Add(element);
                _rowElements.Add(element);
                _rowTexts.Add(rank + " " + name + " " + score);
            }
        }

        // 名前はプレイヤーが付けたものなので、タグを色や大きさとして読ませない。
        private static Label Cell(string text, string className)
        {
            var label = new Label(text) { pickingMode = PickingMode.Ignore, enableRichText = false };
            label.AddToClassList(className);
            return label;
        }

        private void RefreshTexts()
        {
            _title.text = Localizer.Resolve(LeaderboardText.Title);
            _refresh.text = Localizer.Resolve(LeaderboardText.Refresh);
            _back.text = Localizer.Resolve(LeaderboardText.Back);
            foreach (var button in _boardButtons)
            {
                button.text = Localizer.Resolve(LeaderboardText.BoardName((LeaderboardDefinition)button.userData));
            }
        }

        private void HandleRefreshClicked()
        {
            Refresh();
        }

        private void HandleCancel(NavigationCancelEvent evt)
        {
            evt.StopPropagation();
            _navigationRoot.focusController?.IgnoreEvent(evt);
            Cancel();
        }

        private void HandleStatusChanged(LeaderboardStatus? status)
        {
            Render();
        }

        // メニューより後に EOS へつながっても、選んでいるボードを開き直せるようにする
        private void HandleControlChanged(ILeaderboardControl control)
        {
            if (_disposed)
            {
                return;
            }

            if (!ReferenceEquals(control, _openedWith))
            {
                OpenSelected();
            }

            Render();
        }

        private void HandleLanguageChanged(GameLanguage language)
        {
            if (_disposed)
            {
                return;
            }

            RefreshTexts();
            Render();
        }

        private T Require<T>(string name)
            where T : VisualElement
        {
            var element = _container.Q<T>(name);
            if (element == null)
            {
                throw new InvalidOperationException(
                    $"leaderboard layout 'Resources/{LayoutResourcePath}.uxml' has no {typeof(T).Name} named '{name}'");
            }

            return element;
        }
    }
}
