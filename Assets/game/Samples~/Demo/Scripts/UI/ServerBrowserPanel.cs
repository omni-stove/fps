using System;
using System.Collections.Generic;
using Shooter.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace Shooter.Runtime
{
    // 一覧は GameSignals.ServerBrowser から読み、読み直しと入るのは GameSignals.ServerBrowserControl に頼む。
    // 絞り込みと並べ替えは ServerListQuery で行い、条件は開いている間だけ持つ。
    // MainMenuPanels で MainMenuPanelKind.ServerBrowser に登録して開く。
    public sealed class ServerBrowserPanel : IMainMenuPanel
    {
        public const string LogTag = "[ServerBrowser]";

        // ゲームが見た目を差し替えるときに置くレイアウト。無ければ同じ名前の要素をコードで組む
        public const string LayoutResourcePath = "Menu/ServerBrowser";

        public const string PanelName = "server-browser";
        public const string TitleName = "server-browser-title";
        public const string FiltersName = "server-browser-filters";
        public const string ColumnsName = "server-browser-columns";
        public const string NoticeName = "server-browser-notice";
        public const string RowsName = "server-browser-rows";
        public const string RefreshButtonName = "server-browser-refresh";
        public const string BackButtonName = "server-browser-back";

        public const string RegionFilterName = "server-browser-filter-region";
        public const string ModeFilterName = "server-browser-filter-mode";
        public const string MapFilterName = "server-browser-filter-map";
        public const string FullFilterName = "server-browser-filter-full";
        public const string EmptyFilterName = "server-browser-filter-empty";
        public const string SortButtonNamePrefix = "server-browser-sort-";
        public const string RowNamePrefix = "server-browser-row-";

        public const string PanelClass = "server-browser";
        public const string FilterClass = "server-browser__filter";
        public const string ActiveFilterClass = "server-browser__filter--active";
        public const string ColumnClass = "server-browser__column";
        public const string SortedColumnClass = "server-browser__column--sorted";
        public const string RowClass = "server-browser__row";
        public const string FullRowClass = "server-browser__row--full";
        public const string CellClass = "server-browser__cell";
        public const string ErrorNoticeClass = "server-browser__notice--error";

        // 見出しと行の列の並び。並べ替えの項目と同じ順にし、見出しを押すとその列で並べ替える。
        public static readonly IReadOnlyList<ServerListSort> Columns = new[]
        {
            ServerListSort.Region,
            ServerListSort.Mode,
            ServerListSort.Map,
            ServerListSort.Players,
            ServerListSort.Ping,
        };

        private readonly VisualElement _container;
        private readonly Label _title;
        private readonly VisualElement _filters;
        private readonly VisualElement _columns;
        private readonly Label _notice;
        private readonly VisualElement _rows;
        private readonly Button _refresh;
        private readonly Button _back;
        private readonly Button _regionFilter;
        private readonly Button _modeFilter;
        private readonly Button _mapFilter;
        private readonly Button _fullFilter;
        private readonly Button _emptyFilter;
        private readonly Dictionary<ServerListSort, Button> _columnButtons = new Dictionary<ServerListSort, Button>();
        private readonly List<Button> _rowButtons = new List<Button>();
        private readonly List<string> _rowIds = new List<string>();
        private readonly List<string> _rowTexts = new List<string>();
        private readonly VisualElement _navigationRoot;
        private ServerListQuery _query = ServerListQuery.Default;
        private IServerBrowserControl _openedWith;
        private LocalizedText _joinNotice;
        private bool _joinNoticeIsError;
        private int _request;
        private bool _joining;
        private bool _joined;
        private bool _disposed;

        public ServerBrowserPanel(VisualElement container)
        {
            _container = container ?? throw new ArgumentNullException(nameof(container));

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
            _filters = Require<VisualElement>(FiltersName);
            _columns = Require<VisualElement>(ColumnsName);
            _notice = Require<Label>(NoticeName);
            _rows = Require<VisualElement>(RowsName);
            _refresh = Require<Button>(RefreshButtonName);
            _back = Require<Button>(BackButtonName);

            _regionFilter = AddFilter(RegionFilterName, CycleRegion);
            _modeFilter = AddFilter(ModeFilterName, CycleMode);
            _mapFilter = AddFilter(MapFilterName, CycleMap);
            _fullFilter = AddFilter(FullFilterName, ToggleHideFull);
            _emptyFilter = AddFilter(EmptyFilterName, ToggleHideEmpty);
            foreach (var column in Columns)
            {
                var sort = column;
                var button = new Button(() => SortBy(sort)) { name = SortButtonNamePrefix + sort.ToString().ToLowerInvariant() };
                button.AddToClassList(ColumnClass);
                _columns.Add(button);
                _columnButtons.Add(sort, button);
            }

            _refresh.clicked += HandleRefreshClicked;
            _back.clicked += Cancel;

            // 何も選んでいないときの入力はパネルの根に届くので、根で受ける (パネルに付く前なら入れ物で受ける)
            _navigationRoot = container.panel?.visualTree ?? container;
            _navigationRoot.RegisterCallback<NavigationCancelEvent>(HandleCancel, TrickleDown.TrickleDown);

            GameSignals.ServerBrowser.Changed += HandleStatusChanged;
            GameSignals.ServerBrowserControl.Changed += HandleControlChanged;
            Localizer.Changed += HandleLanguageChanged;

            RefreshTexts();
            OpenWithControl();
            Render();
            _refresh.Focus();
        }

        public event Action Cancelled;

        public VisualElement Container => _container;

        public IServerBrowserControl Control => GameSignals.ServerBrowserControl.Value;

        public ServerListQuery Query => _query;

        public bool IsJoining => _joining;

        public bool HasJoined => _joined;

        public Button RefreshButton => _refresh;

        public Button BackButton => _back;

        public Button RegionFilterButton => _regionFilter;

        public Button ModeFilterButton => _modeFilter;

        public Button MapFilterButton => _mapFilter;

        public Button FullFilterButton => _fullFilter;

        public Button EmptyFilterButton => _emptyFilter;

        public IReadOnlyList<Button> RowButtons => _rowButtons;

        // 今出している行のサーバーの Id (ServerListing.Id) を上から順に。
        public IReadOnlyList<string> RowIds => _rowIds;

        public IReadOnlyList<string> RowTexts => _rowTexts;

        public string Notice => _notice.style.display != DisplayStyle.None ? _notice.text : null;

        public bool IsNoticeError => _notice.ClassListContains(ErrorNoticeClass);

        public bool CanJoin => !_disposed && !_joining && !_joined && Control != null;

        public Button ColumnButton(ServerListSort sort)
        {
            return _columnButtons.TryGetValue(sort, out var button) ? button : null;
        }

        public bool Refresh()
        {
            var control = Control;
            if (_disposed || _joined || control == null)
            {
                return false;
            }

            ClearJoinNotice();
            var accepted = Call("refresh", () => control.Refresh());
            Render();
            return accepted;
        }

        public void SetQuery(ServerListQuery query)
        {
            if (_disposed)
            {
                return;
            }

            _query = query ?? throw new ArgumentNullException(nameof(query));
            Render();
        }

        public void CycleRegion()
        {
            SetQuery(_query.WithRegion(Next(_query.Region, ServerListQuery.Regions(Servers()))));
        }

        public void CycleMode()
        {
            SetQuery(_query.WithMode(Next(_query.Mode, ServerListQuery.Modes(Servers()))));
        }

        public void CycleMap()
        {
            SetQuery(_query.WithMap(Next(_query.Map, ServerListQuery.Maps(Servers()))));
        }

        public void ToggleHideFull()
        {
            SetQuery(_query.WithHideFull(!_query.HideFull));
        }

        public void ToggleHideEmpty()
        {
            SetQuery(_query.WithHideEmpty(!_query.HideEmpty));
        }

        public void SortBy(ServerListSort sort)
        {
            SetQuery(_query.ToggleSort(sort));
        }

        public bool Join(string serverId)
        {
            if (!CanJoin || string.IsNullOrEmpty(serverId))
            {
                return false;
            }

            var control = Control;
            var request = ++_request;
            _joining = true;
            ShowJoinNotice(new LocalizedText(ServerBrowserText.JoiningKey), false);
            Render();
            Debug.Log($"{LogTag} joining server {serverId}");

            try
            {
                // 呼び戻しは呼んだその場で返ることもあるので、入っている状態にしてから呼ぶ。
                control.Join(serverId, outcome => HandleOutcome(request, serverId, outcome));
            }
            catch (Exception e)
            {
                Debug.LogError($"{LogTag} could not join server {serverId}: {e.Message}");
                Debug.LogException(e);
                if (request == _request)
                {
                    _request++;
                    _joining = false;
                    ShowJoinNotice(new LocalizedText(ServerBrowserText.FailedKey), true);
                    Render();
                    KeepFocus(null);
                }

                return false;
            }

            return true;
        }

        // 入った後は試合の読み込みを待つので、戻っても閉じない。
        public void Cancel()
        {
            if (!_disposed && !_joined)
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
            _request++;
            GameSignals.ServerBrowser.Changed -= HandleStatusChanged;
            GameSignals.ServerBrowserControl.Changed -= HandleControlChanged;
            Localizer.Changed -= HandleLanguageChanged;
            _navigationRoot.UnregisterCallback<NavigationCancelEvent>(HandleCancel, TrickleDown.TrickleDown);
            _refresh.clicked -= HandleRefreshClicked;
            _back.clicked -= Cancel;

            // 閉じた画面のために検索や ping を続けないよう、一覧を閉じてもらう。入った後はつないだ試合のために閉じない
            var control = Control;
            if (!_joined && control != null && ReferenceEquals(control, _openedWith))
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

        // 一覧に無い値 (読み直して消えた地域など) を選んでいても、押せば「すべて」の次 (一覧の先頭) へ進める。
        public static string Next(string current, IReadOnlyList<string> available)
        {
            if (available == null)
            {
                throw new ArgumentNullException(nameof(available));
            }

            if (available.Count == 0)
            {
                return null;
            }

            if (current == null)
            {
                return available[0];
            }

            for (var i = 0; i < available.Count; i++)
            {
                if (string.Equals(available[i], current, StringComparison.Ordinal))
                {
                    return i + 1 < available.Count ? available[i + 1] : null;
                }
            }

            return available[0];
        }

        private static void BuildDefaultLayout(VisualElement container)
        {
            var panel = new VisualElement { name = PanelName };
            panel.AddToClassList(PanelClass);
            panel.Add(new Label { name = TitleName });
            panel.Add(new VisualElement { name = FiltersName, style = { flexDirection = FlexDirection.Row, flexWrap = Wrap.Wrap } });
            panel.Add(new VisualElement { name = ColumnsName, style = { flexDirection = FlexDirection.Row } });
            panel.Add(new Label { name = NoticeName });
            panel.Add(new ScrollView { name = RowsName });
            panel.Add(new Button { name = RefreshButtonName });
            panel.Add(new Button { name = BackButtonName });
            container.Add(panel);
        }

        private Button AddFilter(string name, Action clicked)
        {
            var button = new Button(clicked) { name = name };
            button.AddToClassList(FilterClass);
            _filters.Add(button);
            return button;
        }

        private void OpenWithControl()
        {
            var control = Control;
            _openedWith = control;
            if (control != null)
            {
                Call("refresh", () => control.Refresh());
            }
        }

        private IReadOnlyList<ServerListing> Servers()
        {
            var status = GameSignals.ServerBrowser.Value;
            return status.HasValue ? status.Value.Servers : Array.Empty<ServerListing>();
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

        // 閉じた後や二度目に届いた答えは、今の画面の問い合わせのものではないので捨てる。
        private void HandleOutcome(int request, string serverId, ServerJoinOutcome outcome)
        {
            if (_disposed || request != _request || !_joining)
            {
                return;
            }

            _joining = false;
            Debug.Log($"{LogTag} server {serverId}: {outcome}");
            if (outcome == ServerJoinOutcome.Joined)
            {
                _joined = true;
                ShowJoinNotice(ServerBrowserText.Outcome(outcome), false);
                Render();
                return;
            }

            ShowJoinNotice(ServerBrowserText.Outcome(outcome), true);
            Render();
            KeepFocus(serverId);
        }

        private void Render()
        {
            if (_disposed)
            {
                return;
            }

            var control = Control;
            var status = GameSignals.ServerBrowser.Value;
            var showRows = control != null && status.HasValue && status.Value.Stage != ServerBrowserStage.Unavailable;
            var shown = showRows ? _query.Apply(status.Value.Servers) : Array.Empty<ServerListing>();
            var focusedId = FocusedRowId();
            RenderRows(shown);

            var notice = _joinNotice.IsEmpty ? ServerBrowserText.Notice(status, control != null, shown.Count) : _joinNotice;
            _notice.text = notice.IsEmpty ? string.Empty : Localizer.Resolve(notice);
            _notice.style.display = notice.IsEmpty ? DisplayStyle.None : DisplayStyle.Flex;
            _notice.EnableInClassList(ErrorNoticeClass, !_joinNotice.IsEmpty && _joinNoticeIsError);

            RenderFilters();
            var loading = status.HasValue && status.Value.Stage == ServerBrowserStage.Loading;
            _refresh.SetEnabled(control != null && !_joining && !_joined && !loading);
            var settled = !_joining && !_joined;
            _back.SetEnabled(!_joined);
            _filters.SetEnabled(settled);
            _columns.SetEnabled(settled);
            if (focusedId != null)
            {
                KeepFocus(focusedId);
            }
        }

        private void RenderFilters()
        {
            _regionFilter.text = Localizer.Resolve(ServerBrowserText.RegionFilter(_query.Region));
            _modeFilter.text = Localizer.Resolve(ServerBrowserText.ModeFilter(_query.Mode));
            _mapFilter.text = Localizer.Resolve(ServerBrowserText.MapFilter(_query.Map));
            _fullFilter.text = Localizer.Resolve(ServerBrowserText.FullFilter(_query.HideFull));
            _emptyFilter.text = Localizer.Resolve(ServerBrowserText.EmptyFilter(_query.HideEmpty));
            _regionFilter.EnableInClassList(ActiveFilterClass, _query.Region != null);
            _modeFilter.EnableInClassList(ActiveFilterClass, _query.Mode != null);
            _mapFilter.EnableInClassList(ActiveFilterClass, _query.Map != null);
            _fullFilter.EnableInClassList(ActiveFilterClass, _query.HideFull);
            _emptyFilter.EnableInClassList(ActiveFilterClass, _query.HideEmpty);
            foreach (var pair in _columnButtons)
            {
                pair.Value.text = Localizer.Resolve(ServerBrowserText.Column(pair.Key, _query));
                pair.Value.EnableInClassList(SortedColumnClass, pair.Key == _query.Sort);
            }
        }

        // 行は検索の上限 (LobbyLaunchOptions.MaxBrowserResults) までで、変わるのは知らせ・条件・言語が変わったときだけなので、差分を取らずに作り直す。
        private void RenderRows(IReadOnlyList<ServerListing> listings)
        {
            _rows.Clear();
            _rowButtons.Clear();
            _rowIds.Clear();
            _rowTexts.Clear();
            var joinable = CanJoin;
            foreach (var listing in listings)
            {
                var id = listing.Id;
                var row = new Button(() => Join(id)) { name = RowNamePrefix + id, userData = id };
                row.AddToClassList(RowClass);
                row.EnableInClassList(FullRowClass, listing.IsFull);

                var cells = new List<string>(Columns.Count);
                foreach (var column in Columns)
                {
                    var text = Localizer.Resolve(Cell(listing, column));
                    var label = new Label(text) { pickingMode = PickingMode.Ignore, enableRichText = false };
                    label.AddToClassList(CellClass);
                    label.AddToClassList(CellClass + "--" + column.ToString().ToLowerInvariant());
                    row.Add(label);
                    cells.Add(text);
                }

                row.SetEnabled(joinable && !listing.IsFull);
                _rows.Add(row);
                _rowButtons.Add(row);
                _rowIds.Add(id);
                _rowTexts.Add(string.Join(" ", cells));
            }
        }

        private static LocalizedText Cell(ServerListing listing, ServerListSort column)
        {
            switch (column)
            {
                case ServerListSort.Region:
                    return ServerBrowserText.Region(listing.Region);
                case ServerListSort.Mode:
                    return ServerBrowserText.Mode(listing.Mode);
                case ServerListSort.Map:
                    return ServerBrowserText.Map(listing.Map);
                case ServerListSort.Players:
                    return ServerBrowserText.Players(listing);
                default:
                    return ServerBrowserText.Ping(listing);
            }
        }

        private string FocusedRowId()
        {
            var focused = _container.focusController?.focusedElement as VisualElement;
            return focused != null && _rows.Contains(focused) ? focused.userData as string : null;
        }

        // 行を作り直すとフォーカスが消え、ゲームパッドで動かせなくなるので、同じサーバーの行か、使える要素へ移す。
        private void KeepFocus(string rowId)
        {
            if (rowId != null)
            {
                foreach (var row in _rowButtons)
                {
                    if (string.Equals(row.userData as string, rowId, StringComparison.Ordinal) && row.enabledInHierarchy)
                    {
                        row.Focus();
                        return;
                    }
                }
            }

            var focused = _container.focusController?.focusedElement as VisualElement;
            if (focused != null && _container.Contains(focused) && focused.enabledInHierarchy)
            {
                return;
            }

            if (focused == null || _container.Contains(focused))
            {
                (_refresh.enabledInHierarchy ? _refresh : _back).Focus();
            }
        }

        private void ShowJoinNotice(LocalizedText text, bool isError)
        {
            _joinNotice = text;
            _joinNoticeIsError = isError;
        }

        private void ClearJoinNotice()
        {
            _joinNotice = default;
            _joinNoticeIsError = false;
        }

        private void RefreshTexts()
        {
            _title.text = Localizer.Resolve(ServerBrowserText.Title);
            _refresh.text = Localizer.Resolve(ServerBrowserText.Refresh);
            _back.text = Localizer.Resolve(ServerBrowserText.Back);
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

        private void HandleStatusChanged(ServerBrowserStatus? status)
        {
            Render();
        }

        // メニューより後に EOS へつながっても、一覧を読み直せるようにする
        private void HandleControlChanged(IServerBrowserControl control)
        {
            if (_disposed)
            {
                return;
            }

            if (!ReferenceEquals(control, _openedWith) && !_joined)
            {
                OpenWithControl();
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
                    $"server browser layout 'Resources/{LayoutResourcePath}.uxml' has no {typeof(T).Name} named '{name}'");
            }

            return element;
        }
    }
}
