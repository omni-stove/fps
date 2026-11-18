using System;
using System.Collections.Generic;
using Shooter.Core;
using UnityEngine.UIElements;

namespace Shooter.Runtime
{
    public sealed class StashPanel : IMainMenuPanel
    {
        public const string PanelClass = "stash";
        public const string TitleClass = "stash__title";
        public const string SlotsClass = "stash__slots";
        public const string ListClass = "stash__list";
        public const string ItemClass = "stash__item";
        public const string BackClass = "stash__back";

        public const string SlotsName = "stash-slots";
        public const string ItemsName = "stash-items";
        public const string BackButtonName = "stash-back";

        private readonly VisualElement _container;
        private readonly ItemCatalog _items;
        private readonly Label _title;
        private readonly Label _slots;
        private readonly VisualElement _list;
        private readonly Button _back;
        private readonly VisualElement _navigationRoot;
        private StashStatus _status;
        private bool _disposed;

        // 表から外した持ち物も持ち物庫には残る (Stash.Restore) ので、名前が引けなければ空にせず Id を出す
        public StashPanel(VisualElement container, StashStatus status, ItemCatalog items = null)
        {
            _container = container ?? throw new ArgumentNullException(nameof(container));
            _items = items;
            _status = status;

            container.Clear();
            container.AddToClassList(PanelClass);

            _title = MakeLabel(TitleClass);
            container.Add(_title);

            _slots = MakeLabel(SlotsClass, SlotsName);
            container.Add(_slots);

            _list = new VisualElement { name = ItemsName };
            _list.AddToClassList(ListClass);
            container.Add(_list);

            _back = new Button(Cancel) { name = BackButtonName };
            _back.AddToClassList(BackClass);
            container.Add(_back);

            // 何も選んでいないときの入力はパネルの根に届くので、根で受ける (パネルに付く前なら入れ物で受ける)
            _navigationRoot = container.panel?.visualTree ?? container;
            _navigationRoot.RegisterCallback<NavigationSubmitEvent>(HandleSubmit, TrickleDown.TrickleDown);
            _navigationRoot.RegisterCallback<NavigationCancelEvent>(HandleCancel, TrickleDown.TrickleDown);

            GameSignals.Stash.Changed += OnStashChanged;
            Refresh();
        }

        public event Action Cancelled;

        public static string Title => GameText.Get("extraction.stash.title");

        public static string Back => GameText.Get("extraction.stash.back");

        public static string Empty => GameText.Get("extraction.stash.empty");

        public VisualElement Container => _container;

        public StashStatus Status => _status;

        public Button BackButton => _back;

        public string TitleText => _title.text;

        public string SlotsText => _slots.text;

        public IReadOnlyList<string> ItemLines
        {
            get
            {
                var lines = new List<string>();
                _list.Query<Label>(className: ItemClass).ForEach(label => lines.Add(label.text));
                return lines;
            }
        }

        public static string Slots(int used, int capacity)
        {
            return GameText.Format("extraction.stash.slots", used, capacity);
        }

        public static string Item(string name, int count)
        {
            return GameText.Format("extraction.stash.item", name, count);
        }

        // 保存に失敗しても試合の持ち物庫は今の Play に残るので、保存したものより試合が出したものを先に見る。
        public static StashPanel Open(VisualElement container)
        {
            var items = KitTuning.Items;
            var status = GameSignals.Stash.Value ?? new StashStore().Load(ExtractionRunner.StashTuning, items).Status;
            return new StashPanel(container, status, items);
        }

        public void Show(StashStatus status)
        {
            _status = status;
            Refresh();
        }

        public void Cancel()
        {
            if (!_disposed)
            {
                Cancelled?.Invoke();
            }
        }

        public void Refresh()
        {
            if (_disposed)
            {
                return;
            }

            _title.text = Title;
            _slots.text = Slots(_status.Stacks.Count, _status.Capacity);

            var lines = new List<string>();
            foreach (var stack in _status.Stacks)
            {
                lines.Add(Item(NameOf(stack.ItemId), stack.Count));
            }

            if (lines.Count == 0)
            {
                lines.Add(Empty);
            }

            WriteLines(lines);
            _back.text = Back;
            FocusBack();
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            GameSignals.Stash.Changed -= OnStashChanged;
            _navigationRoot.UnregisterCallback<NavigationSubmitEvent>(HandleSubmit, TrickleDown.TrickleDown);
            _navigationRoot.UnregisterCallback<NavigationCancelEvent>(HandleCancel, TrickleDown.TrickleDown);
            _container.Clear();
            _container.RemoveFromClassList(PanelClass);
            Cancelled = null;
        }

        // 出した側が片付けて消したときは、見ていた中身を残す (空の持ち物庫に見せない)
        private void OnStashChanged(StashStatus? status)
        {
            if (status.HasValue)
            {
                Show(status.Value);
            }
        }

        private string NameOf(string itemId)
        {
            if (_items != null && _items.TryFind(itemId, out var item))
            {
                return GameText.Resolve(DisplayNames.Of(item.NameKey, item.Id));
            }

            return itemId;
        }

        private void WriteLines(IReadOnlyList<string> lines)
        {
            var rows = _list.Query<Label>(className: ItemClass).ToList();
            for (var i = rows.Count; i < lines.Count; i++)
            {
                var row = MakeLabel(ItemClass);
                _list.Add(row);
                rows.Add(row);
            }

            for (var i = rows.Count - 1; i >= lines.Count; i--)
            {
                rows[i].RemoveFromHierarchy();
                rows.RemoveAt(i);
            }

            for (var i = 0; i < lines.Count; i++)
            {
                rows[i].text = lines[i];
            }
        }

        private void FocusBack()
        {
            _back.Focus();

            // パネルに付いた直後は配置が終わっておらず Focus が効かないことがあるので、次の更新でもう一度選ぶ
            if (_back.panel != null && _back.focusController?.focusedElement != _back)
            {
                _back.schedule.Execute(() =>
                {
                    if (!_disposed)
                    {
                        _back.Focus();
                    }
                });
            }
        }

        private void HandleSubmit(NavigationSubmitEvent evt)
        {
            // フォーカスしている戻るのボタンにも届くと Cancelled が二重に走るので、ここで止める
            evt.StopPropagation();
            _navigationRoot.focusController?.IgnoreEvent(evt);
            Cancel();
        }

        private void HandleCancel(NavigationCancelEvent evt)
        {
            evt.StopPropagation();
            _navigationRoot.focusController?.IgnoreEvent(evt);
            Cancel();
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
