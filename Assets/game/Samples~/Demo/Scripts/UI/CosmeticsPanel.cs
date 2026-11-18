using System;
using System.Collections.Generic;
using Shooter.Core;
using UnityEngine.UIElements;

namespace Shooter.Runtime
{
    // 種類ごとに「なし」とカタログの見た目をボタンで並べ、押したものを着ける。まだ解けていないものは解けるレベルを添えて押せなくする。
    public sealed class CosmeticsPanel : IMainMenuPanel
    {
        public const string PanelClass = "cosmetics";
        public const string TitleClass = "cosmetics__title";
        public const string SectionClass = "cosmetics__section";
        public const string SectionTitleClass = "cosmetics__section-title";
        public const string OptionClass = "cosmetics__option";
        public const string OptionEquippedClass = "cosmetics__option--equipped";
        public const string OptionLockedClass = "cosmetics__option--locked";
        public const string EmptyClass = "cosmetics__empty";
        public const string StatusClass = "cosmetics__status";
        public const string BackClass = "cosmetics__back";

        public const string StatusName = "cosmetics-status";
        public const string BackButtonName = "cosmetics-back";

        private static readonly CosmeticKind[] _kinds = { CosmeticKind.Skin, CosmeticKind.Camo };

        private readonly VisualElement _container;
        private readonly ICosmeticLocker _locker;
        private readonly Label _title;
        private readonly Dictionary<CosmeticKind, Section> _sections = new Dictionary<CosmeticKind, Section>();
        private readonly Label _status;
        private readonly Button _back;
        private readonly VisualElement _navigationRoot;
        private bool _disposed;

        public CosmeticsPanel(VisualElement container, ICosmeticLocker locker)
        {
            _container = container ?? throw new ArgumentNullException(nameof(container));
            _locker = locker ?? throw new ArgumentNullException(nameof(locker));

            container.Clear();
            container.AddToClassList(PanelClass);

            _title = MakeLabel(TitleClass);
            container.Add(_title);

            // 見た目の数が増えても戻るのボタンが画面の外へ押し出されないよう、巻物の中に並べる
            var scroll = new ScrollView(ScrollViewMode.Vertical);
            container.Add(scroll);
            foreach (var kind in _kinds)
            {
                var section = new Section(kind);
                _sections[kind] = section;
                scroll.Add(section.Root);
            }

            _status = MakeLabel(StatusClass);
            _status.name = StatusName;
            container.Add(_status);

            _back = new Button(Cancel) { name = BackButtonName };
            _back.AddToClassList(BackClass);
            container.Add(_back);

            // 何も選んでいないときの入力はパネルの根に届くので、根で受ける (パネルに付く前なら入れ物で受ける)
            _navigationRoot = container.panel?.visualTree ?? container;
            _navigationRoot.RegisterCallback<NavigationCancelEvent>(HandleCancel, TrickleDown.TrickleDown);

            _locker.Changed += Refresh;
            Refresh();
            FocusFirst();
        }

        public event Action Cancelled;

        public static string Title => GameText.Get("cosmetics.title");

        public static string Back => GameText.Get("cosmetics.back");

        public static string None => GameText.Get("cosmetics.none");

        public static string Empty => GameText.Get("cosmetics.empty");

        public static string Refused => GameText.Get("cosmetics.refused");

        public VisualElement Container => _container;

        public ICosmeticLocker Locker => _locker;

        public Button BackButton => _back;

        public string TitleText => _title.text;

        public string StatusText => _status.text;

        // 記録係が出ていなければ開けない。メインメニューはこの例外を知らせに出す (MainMenuScreen.OpenPanel)
        public static CosmeticsPanel Open(VisualElement container)
        {
            var locker = GameSignals.Cosmetics.Value
                ?? throw new InvalidOperationException("no cosmetic locker is published (GameSignals.Cosmetics)");
            return new CosmeticsPanel(container, locker);
        }

        public static string SectionTitle(CosmeticKind kind)
        {
            return GameText.Get(kind == CosmeticKind.Skin ? "cosmetics.skins" : "cosmetics.camos");
        }

        public static string Equipped(string name)
        {
            return GameText.Format("cosmetics.equipped", name);
        }

        public static string Locked(string name, int level)
        {
            return GameText.Format("cosmetics.locked", name, level);
        }

        public static string OptionName(CosmeticKind kind, string cosmeticId)
        {
            var prefix = "cosmetics-" + (kind == CosmeticKind.Skin ? "skin" : "camo");
            return cosmeticId == null ? prefix + "-none" : prefix + "-item-" + cosmeticId;
        }

        public string SectionTitleText(CosmeticKind kind)
        {
            return SectionOf(kind).Title.text;
        }

        public IReadOnlyList<string> OptionLines(CosmeticKind kind)
        {
            var lines = new List<string>();
            SectionOf(kind).List.Query<Button>(className: OptionClass).ForEach(button => lines.Add(button.text));
            return lines;
        }

        public bool IsEmptyShown(CosmeticKind kind)
        {
            return SectionOf(kind).List.Q<Label>(className: EmptyClass) != null;
        }

        public Button Option(CosmeticKind kind, string cosmeticId)
        {
            return SectionOf(kind).List.Q<Button>(OptionName(kind, cosmeticId));
        }

        public bool Choose(CosmeticKind kind, string cosmeticId)
        {
            if (_disposed)
            {
                return false;
            }

            var equipped = _locker.Equip(kind, cosmeticId);
            _status.text = equipped ? string.Empty : Refused;

            // 着けると記録係の Changed で書き直されるが、記録係によっては同じものを着け直しても知らせないので、ここでも合わせる
            Refresh();
            return equipped;
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
            foreach (var kind in _kinds)
            {
                WriteSection(_sections[kind]);
            }

            _back.text = Back;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _locker.Changed -= Refresh;
            _navigationRoot.UnregisterCallback<NavigationCancelEvent>(HandleCancel, TrickleDown.TrickleDown);
            _container.Clear();
            _container.RemoveFromClassList(PanelClass);
            Cancelled = null;
        }

        // ボタンを毎回作り直すのは、装備中と解けているかの印と押せるかを行ごとに合わせ直すより簡単で、見た目の数は開いている間に変わらないため
        private void WriteSection(Section section)
        {
            var kind = section.Kind;
            var focused = section.List.panel?.focusController?.focusedElement as Button;
            var focusedName = focused != null && focused.parent == section.List ? focused.name : null;
            section.Title.text = SectionTitle(kind);
            section.List.Clear();

            var equippedId = _locker.EquippedId(kind);
            section.List.Add(MakeOption(kind, null, None, equippedId == null, true));

            var items = _locker.Catalog.OfKind(kind);
            foreach (var item in items)
            {
                var name = GameText.Resolve(DisplayNames.Of(item.DisplayNameKey, item.Id));
                var unlocked = _locker.IsUnlocked(item.Id);
                var equipped = string.Equals(item.Id, equippedId, StringComparison.Ordinal);
                var text = !unlocked ? Locked(name, item.UnlockLevel) : equipped ? Equipped(name) : name;
                section.List.Add(MakeOption(kind, item.Id, text, equipped, unlocked));
            }

            if (items.Count == 0)
            {
                section.List.Add(MakeLabel(EmptyClass, Empty));
            }

            if (focusedName != null)
            {
                section.List.Q<Button>(focusedName)?.Focus();
            }
        }

        private Button MakeOption(CosmeticKind kind, string cosmeticId, string text, bool equipped, bool unlocked)
        {
            var button = new Button(() => Choose(kind, cosmeticId)) { name = OptionName(kind, cosmeticId), text = text };
            button.AddToClassList(OptionClass);
            button.EnableInClassList(OptionEquippedClass, equipped);
            button.EnableInClassList(OptionLockedClass, !unlocked);
            button.SetEnabled(unlocked);
            return button;
        }

        private void FocusFirst()
        {
            var first = _sections[_kinds[0]].List.Q<Button>(className: OptionClass);
            (first ?? _back).Focus();
        }

        private Section SectionOf(CosmeticKind kind)
        {
            if (!_sections.TryGetValue(kind, out var section))
            {
                throw new ArgumentOutOfRangeException(nameof(kind), kind, "the cosmetics screen has no such section");
            }

            return section;
        }

        private void HandleCancel(NavigationCancelEvent evt)
        {
            evt.StopPropagation();
            _navigationRoot.focusController?.IgnoreEvent(evt);
            Cancel();
        }

        private static Label MakeLabel(string className, string text = "")
        {
            var label = new Label(text) { pickingMode = PickingMode.Ignore };
            label.AddToClassList(className);
            return label;
        }

        private sealed class Section
        {
            public Section(CosmeticKind kind)
            {
                Kind = kind;
                Root = new VisualElement();
                Root.AddToClassList(SectionClass);
                Title = MakeLabel(SectionTitleClass);
                Root.Add(Title);
                List = new VisualElement();
                Root.Add(List);
            }

            public CosmeticKind Kind { get; }

            public VisualElement Root { get; }

            public Label Title { get; }

            public VisualElement List { get; }
        }
    }
}
