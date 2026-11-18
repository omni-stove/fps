using System;
using System.Collections.Generic;
using Shooter.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace Shooter.Runtime
{
    // メインメニューの差し込み口 (MainMenuPanels) に置くリプレイの一覧。一覧と再生・削除は GameSignals.Replays の IReplayLibrary に任せる。
    // 削除は取り消せないので、同じ行の削除を 2 回押して消す。
    public sealed class ReplayPanel : IMainMenuPanel
    {
        public const string PanelClass = "replay";
        public const string TitleClass = "replay__title";
        public const string StatusClass = "replay__status";
        public const string ListClass = "replay__list";
        public const string RowClass = "replay__row";
        public const string EntryClass = "replay__entry";
        public const string ActionClass = "replay__action";
        public const string BackClass = "replay__back";

        public const string TitleName = "replay-title";
        public const string StatusName = "replay-status";
        public const string ListName = "replay-list";
        public const string BackButtonName = "replay-back";
        public const string PlayButtonPrefix = "replay-play-";
        public const string DeleteButtonPrefix = "replay-delete-";

        private readonly VisualElement _container;
        private readonly Label _title;
        private readonly Label _status;
        private readonly ScrollView _list;
        private readonly Button _back;
        private readonly VisualElement _navigationRoot;
        private IReplayLibrary _library;
        private string _confirmingDeleteId;
        private string _failure;
        private bool _disposed;

        public ReplayPanel(VisualElement container)
            : this(container, GameSignals.Replays.Value)
        {
        }

        // 一覧が後から出される (GameSignals.Replays) こともあるので、無くても開き、出されたら並べ直す。
        public ReplayPanel(VisualElement container, IReplayLibrary library)
        {
            _container = container ?? throw new ArgumentNullException(nameof(container));
            container.Clear();
            container.AddToClassList(PanelClass);

            _title = MakeLabel(TitleClass, TitleName);
            container.Add(_title);

            _status = MakeLabel(StatusClass, StatusName);
            _status.style.whiteSpace = WhiteSpace.Normal;
            container.Add(_status);

            _list = new ScrollView(ScrollViewMode.Vertical) { name = ListName };
            _list.AddToClassList(ListClass);
            container.Add(_list);

            _back = new Button(Cancel) { name = BackButtonName };
            _back.AddToClassList(BackClass);
            container.Add(_back);

            // 何も選んでいないときの入力はパネルの根に届くので、根で受ける (パネルに付く前なら入れ物で受ける)
            _navigationRoot = container.panel?.visualTree ?? container;
            _navigationRoot.RegisterCallback<NavigationCancelEvent>(HandleCancel, TrickleDown.TrickleDown);

            GameSignals.Replays.Changed += HandleLibraryPublished;
            Attach(library);
            Refresh();
        }

        public event Action Cancelled;

        public VisualElement Container => _container;

        public IReplayLibrary Library => _library;

        public Button BackButton => _back;

        public string TitleText => _title.text;

        public string StatusText => _status.text;

        public IReadOnlyList<string> EntryLines
        {
            get
            {
                var lines = new List<string>();
                _list.Query<Label>(className: EntryClass).ForEach(label => lines.Add(label.text));
                return lines;
            }
        }

        public Button PlayButtonOf(string replayId)
        {
            return _list.Q<Button>(PlayButtonPrefix + replayId);
        }

        public Button DeleteButtonOf(string replayId)
        {
            return _list.Q<Button>(DeleteButtonPrefix + replayId);
        }

        public void Cancel()
        {
            if (!_disposed)
            {
                Cancelled?.Invoke();
            }
        }

        public bool Play(string replayId)
        {
            if (_disposed || _library == null)
            {
                return false;
            }

            _confirmingDeleteId = null;
            if (_library.Play(replayId))
            {
                _failure = null;
                return true;
            }

            _failure = ReplayText.PlayFailed;
            Refresh();
            return false;
        }

        // 1 回目は確かめる表示にするだけで、同じ行をもう一度押したときに消す。
        public bool Delete(string replayId)
        {
            if (_disposed || _library == null)
            {
                return false;
            }

            if (!string.Equals(_confirmingDeleteId, replayId, StringComparison.Ordinal))
            {
                _confirmingDeleteId = replayId;
                _failure = null;
                Refresh();
                return false;
            }

            _confirmingDeleteId = null;
            var deleted = _library.Delete(replayId);
            _failure = deleted ? null : ReplayText.DeleteFailed;
            Refresh();
            return deleted;
        }

        public void Refresh()
        {
            if (_disposed)
            {
                return;
            }

            _title.text = ReplayText.Title;
            _back.text = ReplayText.Back;
            _list.Clear();

            var entries = _library?.Entries;
            if (entries != null)
            {
                foreach (var entry in entries)
                {
                    _list.Add(MakeRow(entry));
                }
            }

            _status.text = _failure ?? (_library == null ? ReplayText.Unavailable : entries == null || entries.Count == 0 ? ReplayText.Empty : string.Empty);
            SetDisplayed(_status, !string.IsNullOrEmpty(_status.text));
            FocusFirst();
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            GameSignals.Replays.Changed -= HandleLibraryPublished;
            Attach(null);
            _navigationRoot.UnregisterCallback<NavigationCancelEvent>(HandleCancel, TrickleDown.TrickleDown);
            _container.Clear();
            _container.RemoveFromClassList(PanelClass);
            Cancelled = null;
        }

        private VisualElement MakeRow(ReplayEntry entry)
        {
            var row = new VisualElement();
            row.AddToClassList(RowClass);
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;

            var label = MakeLabel(EntryClass);
            label.text = ReplayText.Entry(entry);
            label.style.flexGrow = 1f;
            row.Add(label);

            var id = entry.Id;
            var play = new Button(() => Play(id)) { name = PlayButtonPrefix + id, text = ReplayText.Play };
            play.AddToClassList(ActionClass);
            row.Add(play);

            var confirming = string.Equals(_confirmingDeleteId, id, StringComparison.Ordinal);
            var delete = new Button(() => Delete(id))
            {
                name = DeleteButtonPrefix + id,
                text = confirming ? ReplayText.ConfirmDelete : ReplayText.Delete,
            };
            delete.AddToClassList(ActionClass);
            row.Add(delete);
            return row;
        }

        private void Attach(IReplayLibrary library)
        {
            if (ReferenceEquals(_library, library))
            {
                return;
            }

            if (_library != null)
            {
                _library.Changed -= HandleLibraryChanged;
            }

            _library = library;
            if (_library != null)
            {
                _library.Changed += HandleLibraryChanged;
            }
        }

        private void HandleLibraryChanged()
        {
            Refresh();
        }

        private void HandleLibraryPublished(IReplayLibrary library)
        {
            if (_disposed)
            {
                return;
            }

            Debug.Log(ReplayLog.LibraryPublished(library != null));
            _confirmingDeleteId = null;
            _failure = null;
            Attach(library);
            Refresh();
        }

        // 確かめ中の削除のボタンを選んだままにし、2 回目をそのまま押せるようにする。
        private void FocusFirst()
        {
            Button target = null;
            if (_confirmingDeleteId != null)
            {
                target = DeleteButtonOf(_confirmingDeleteId);
            }

            target ??= _list.Q<Button>(className: ActionClass) ?? _back;
            target.Focus();
            if (target.panel != null && target.focusController?.focusedElement != target)
            {
                // パネルに付いた直後は配置が終わっておらず Focus が効かないことがあるので、次の更新でもう一度選ぶ
                target.schedule.Execute(() =>
                {
                    if (!_disposed && target.panel != null)
                    {
                        target.Focus();
                    }
                });
            }
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

        private static Label MakeLabel(string className, string name = null)
        {
            var label = new Label(string.Empty) { pickingMode = PickingMode.Ignore };
            if (name != null)
            {
                label.name = name;
            }

            label.AddToClassList(className);
            return label;
        }
    }
}
