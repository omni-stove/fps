using System;
using System.Collections.Generic;
using System.Globalization;
using Shooter.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace Shooter.Runtime
{
    // 買えるか (値段・準備の時間・持てるか) を決めるのは送った先 (GameSignals.BuySender) なので、ここでの見積もりは、断られると分かっている品を送らずに理由を出すためだけに使う。
    // 送った先の答え (買えたか・断った理由) は CombatEvents.PurchaseAnswered で後から届くので、それを受けて出す。
    // 答えを出さない送り先 (ゲームが自前で足したもの) でも買えたことは分かるよう、財布が減ったら買えたとみなす。
    public sealed class BuyMenuPanel : IDisposable
    {
        public const string LogTag = "[BuyMenu]";

        public const string PanelClass = "buy-menu";
        public const string TitleClass = "buy-menu__title";
        public const string MoneyClass = "buy-menu__money";
        public const string ListClass = "buy-menu__list";
        public const string ItemClass = "buy-menu__item";
        public const string UnaffordableItemClass = "buy-menu__item--unaffordable";
        public const string PendingItemClass = "buy-menu__item--pending";
        public const string ItemNameClass = "buy-menu__item-name";
        public const string ItemDetailClass = "buy-menu__item-detail";
        public const string ItemPriceClass = "buy-menu__item-price";
        public const string EmptyClass = "buy-menu__empty";
        public const string StatusClass = "buy-menu__status";
        public const string ErrorStatusClass = "buy-menu__status--error";
        public const string CloseClass = "buy-menu__close";

        public const string ListName = "buy-menu-list";
        public const string MoneyName = "buy-menu-money";
        public const string EmptyName = "buy-menu-empty";
        public const string StatusName = "buy-menu-status";
        public const string CloseButtonName = "buy-menu-close";

        public const string StyleSheetResourcePath = "Hud/BuyMenu";

        public const string TitleKey = "economy.title";
        public const string MoneyKey = "economy.money";
        public const string PriceKey = "economy.price";
        public const string EmptyKey = "economy.empty";
        public const string CloseKey = "economy.close";
        public const string BuyingKey = "economy.buying";
        public const string NoAnswerKey = "economy.noAnswer";
        public const string NoSenderKey = "economy.noSender";
        public const string KindKeyPrefix = "economy.kind.";
        public const string VerdictKeyPrefix = "economy.verdict.";

        // 答えが届かないとき (つながっておらず送れなかった、答えを出さない送り先など) に「購入中」を出し続けないよう、この秒数で買えなかったと出す。
        // 秒が過ぎた後に答えが届けば、その答えで出し直す。
        public const float DefaultAnswerSeconds = 2f;

        private static StyleSheet _styleSheet;
        private static bool _warnedMissingStyleSheet;

        private readonly VisualElement _container;
        private readonly BuyCatalog _catalog;
        private readonly float _answerSeconds;
        private readonly Label _title;
        private readonly Label _money;
        private readonly Label _empty;
        private readonly Label _status;
        private readonly Button _close;
        private readonly List<Button> _itemButtons = new List<Button>();
        private readonly List<Label> _itemNames = new List<Label>();
        private readonly List<Label> _itemDetails = new List<Label>();
        private readonly List<Label> _itemPrices = new List<Label>();
        private readonly VisualElement _navigationRoot;
        private WalletStatus? _wallet;
        private LocalizedText? _statusSource;
        private bool _statusIsError;
        private BuyItem _pending;
        private int _moneyAtSend;
        private float _pendingSeconds;
        private bool _open;
        private bool _disposed;

        public BuyMenuPanel(VisualElement container, BuyCatalog catalog = null, float answerSeconds = DefaultAnswerSeconds)
        {
            _container = container ?? throw new ArgumentNullException(nameof(container));
            if (answerSeconds <= 0f || float.IsNaN(answerSeconds) || float.IsInfinity(answerSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(answerSeconds), answerSeconds, "answerSeconds must be a positive number of seconds");
            }

            if (!IsBuyTime(GameSignals.Wallet.Value))
            {
                throw new InvalidOperationException("the buy menu opens only while GameSignals.Wallet allows buying (the buy time)");
            }

            _catalog = catalog ?? KitTuning.Economy.Catalog;
            _answerSeconds = answerSeconds;
            _wallet = GameSignals.Wallet.Value;

            container.Clear();
            container.AddToClassList(PanelClass);

            // HUD の外 (メニューなど) に置いても同じ見た目になるよう、入れ物に直接付ける
            var styleSheet = LoadStyleSheet();
            if (styleSheet != null && !container.styleSheets.Contains(styleSheet))
            {
                container.styleSheets.Add(styleSheet);
            }

            _title = NewLabel(TitleClass);
            container.Add(_title);

            _money = NewLabel(MoneyClass);
            _money.name = MoneyName;
            container.Add(_money);

            var list = new VisualElement { name = ListName };
            list.AddToClassList(ListClass);
            container.Add(list);
            for (var i = 0; i < _catalog.Count; i++)
            {
                list.Add(BuildItem(i));
            }

            _empty = NewLabel(EmptyClass);
            _empty.name = EmptyName;
            _empty.style.display = _catalog.Count == 0 ? DisplayStyle.Flex : DisplayStyle.None;
            container.Add(_empty);

            _status = NewLabel(StatusClass);
            _status.name = StatusName;
            _status.style.display = DisplayStyle.None;
            container.Add(_status);

            _close = new Button { name = CloseButtonName, text = string.Empty };
            _close.clicked += Cancel;
            _close.AddToClassList(CloseClass);
            container.Add(_close);

            // 何も選んでいないときの入力はパネルの根に届くので、根で受ける (パネルに付く前なら入れ物で受ける)
            _navigationRoot = container.panel?.visualTree ?? container;
            _navigationRoot.RegisterCallback<NavigationCancelEvent>(HandleCancel, TrickleDown.TrickleDown);
            GameSignals.Wallet.Changed += HandleWalletChanged;
            CombatEvents.PurchaseAnswered += HandlePurchaseAnswered;
            Localizer.Changed += HandleLanguageChanged;

            _open = true;
            RefreshTexts();
            RefreshItems();
            FocusFirst();
        }

        // 準備の時間が終わったとき、閉じる操作をしたときに 1 度だけ呼ぶ。
        public event Action Closed;

        public VisualElement Container => _container;

        public BuyCatalog Catalog => _catalog;

        public bool IsOpen => _open && !_disposed;

        public int Count => _itemButtons.Count;

        public string Title => _title.text;

        public string MoneyText => _money.text;

        public string EmptyNotice => _empty.style.display == DisplayStyle.None ? null : _empty.text;

        public string Status => _status.style.display == DisplayStyle.None ? null : _status.text;

        public bool IsStatusError => _status.ClassListContains(ErrorStatusClass);

        public string PendingItemId => _pending?.Id;

        public Button CloseButton => _close;

        public static bool IsBuyTime(WalletStatus? wallet)
        {
            return wallet.HasValue && wallet.Value.CanBuy;
        }

        // 送る前に分かる断りだけを見る。持てるかは送った先の持ち物で決まるので、ここでは見ない。
        public static PurchaseVerdict Precheck(BuyItem item, WalletStatus? wallet)
        {
            if (item == null)
            {
                return PurchaseVerdict.UnknownItem;
            }

            if (!IsBuyTime(wallet))
            {
                return PurchaseVerdict.NotBuyTime;
            }

            return wallet.Value.CanAfford(item.Price) ? PurchaseVerdict.Bought : PurchaseVerdict.NotEnoughMoney;
        }

        public static string VerdictKeyOf(PurchaseVerdict verdict)
        {
            if (!Enum.IsDefined(typeof(PurchaseVerdict), verdict))
            {
                throw new ArgumentOutOfRangeException(nameof(verdict), verdict, "verdict is not a valid PurchaseVerdict");
            }

            return VerdictKeyPrefix + verdict;
        }

        public static string KindKeyOf(BuyItemKind kind)
        {
            if (!Enum.IsDefined(typeof(BuyItemKind), kind))
            {
                throw new ArgumentOutOfRangeException(nameof(kind), kind, "kind is not a valid BuyItemKind");
            }

            return KindKeyPrefix + kind;
        }

        public static LocalizedText NameOf(BuyItem item)
        {
            if (item == null)
            {
                throw new ArgumentNullException(nameof(item));
            }

            return DisplayNames.Of(item);
        }

        // 武器は入る枠 (1 から数える)、防具は足す量を添える。
        public static LocalizedText DetailOf(BuyItem item)
        {
            if (item == null)
            {
                throw new ArgumentNullException(nameof(item));
            }

            switch (item.Kind)
            {
                case BuyItemKind.Weapon:
                    return new LocalizedText(KindKeyOf(item.Kind), item.Slot + 1);
                case BuyItemKind.Armor:
                    return new LocalizedText(KindKeyOf(item.Kind), item.Amount);
                default:
                    return new LocalizedText(KindKeyOf(item.Kind));
            }
        }

        public static LocalizedText VerdictText(PurchaseVerdict verdict, BuyItem item)
        {
            if (item == null)
            {
                throw new ArgumentNullException(nameof(item));
            }

            return new LocalizedText(VerdictKeyOf(verdict), NameOf(item));
        }

        // 一覧に無い品の答えでも理由は出せるよう、名前の代わりに Id を出す。
        public static LocalizedText VerdictText(PurchaseVerdict verdict, string itemId)
        {
            return new LocalizedText(VerdictKeyOf(verdict), LocalizedText.Literal(itemId ?? string.Empty));
        }

        public static string PriceText(int price)
        {
            return GameText.Format(PriceKey, price.ToString(CultureInfo.InvariantCulture));
        }

        public Button ItemButton(int index)
        {
            return _itemButtons[index];
        }

        public string ItemName(int index)
        {
            return _itemNames[index].text;
        }

        public string ItemDetail(int index)
        {
            return _itemDetails[index].text;
        }

        public string ItemPrice(int index)
        {
            return _itemPrices[index].text;
        }

        public bool IsAffordable(int index)
        {
            return !_itemButtons[index].ClassListContains(UnaffordableItemClass);
        }

        // 送ったら true。断られると分かっている品は送らずに理由を出す。答えを待つ間は二度押しで同じ品を重ねて買わないよう受けない。
        public bool Buy(int index)
        {
            if (!IsOpen || _pending != null)
            {
                return false;
            }

            if (index < 0 || index >= _catalog.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(index), index, $"the buy menu lists {_catalog.Count} items");
            }

            var item = _catalog.Items[index];
            var verdict = Precheck(item, _wallet);
            if (verdict != PurchaseVerdict.Bought)
            {
                Debug.Log($"{LogTag} not sending '{item.Id}': {verdict}");
                ShowStatus(VerdictText(verdict, item), true);
                return false;
            }

            var sender = GameSignals.BuySender.Value;
            if (sender == null)
            {
                Debug.LogWarning($"{LogTag} '{item.Id}' was chosen but no buy sender is published (GameSignals.BuySender)");
                ShowStatus(new LocalizedText(NoSenderKey), true);
                return false;
            }

            // オフラインの送り先はその場で財布を出し直すので、送る前に待つ品を覚えておく
            _pending = item;
            _moneyAtSend = _wallet.Value.Money;
            _pendingSeconds = _answerSeconds;
            ShowStatus(new LocalizedText(BuyingKey, NameOf(item)), false);
            RefreshItems();
            Debug.Log($"{LogTag} buying '{item.Id}' price={item.Price} money={_moneyAtSend}");
            sender.Buy(item.Id);
            return true;
        }

        // CombatEvents.PurchaseAnswered で届いた答えを出す。itemId が待っている品と違えば、古い答えとして捨てる。
        public void ShowVerdict(string itemId, PurchaseVerdict verdict)
        {
            if (!IsOpen)
            {
                return;
            }

            _catalog.TryFind(itemId, out var item);
            if (_pending != null)
            {
                if (!string.Equals(_pending.Id, itemId, StringComparison.Ordinal))
                {
                    Debug.Log($"{LogTag} ignoring the answer {verdict} for '{itemId}' while waiting for '{_pending.Id}'");
                    return;
                }

                _pending = null;
                RefreshItems();
            }

            var text = item != null ? VerdictText(verdict, item) : VerdictText(verdict, itemId);
            ShowStatus(text, verdict != PurchaseVerdict.Bought);
        }

        // 画面の Update から呼ぶ。答えを待つ秒を数える。
        public void Tick(float deltaSeconds)
        {
            if (!IsOpen || _pending == null || deltaSeconds <= 0f)
            {
                return;
            }

            _pendingSeconds -= deltaSeconds;
            if (_pendingSeconds > 0f)
            {
                return;
            }

            var item = _pending;
            _pending = null;
            Debug.Log($"{LogTag} no purchase of '{item.Id}' within {_answerSeconds.ToString(CultureInfo.InvariantCulture)} s");
            ShowStatus(new LocalizedText(NoAnswerKey, NameOf(item)), true);
            RefreshItems();
        }

        public void Cancel()
        {
            Close();
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _open = false;
            Unsubscribe();
            _close.clicked -= Cancel;
            _container.Clear();
            _container.RemoveFromClassList(PanelClass);
            if (_styleSheet != null)
            {
                _container.styleSheets.Remove(_styleSheet);
            }

            Closed = null;
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
                Debug.LogWarning($"{LogTag} style sheet 'Resources/{StyleSheetResourcePath}.uss' was not found; the buy menu is unstyled");
            }

            return _styleSheet;
        }

        private static Label NewLabel(string className)
        {
            var label = new Label(string.Empty) { pickingMode = PickingMode.Ignore };
            label.AddToClassList(className);
            return label;
        }

        private VisualElement BuildItem(int index)
        {
            // 足りない品も押せるままにするのは、押したときに「お金が足りない」と理由を出すため。
            var button = new Button(() => Buy(index)) { text = string.Empty };
            button.AddToClassList(ItemClass);

            var texts = new VisualElement { pickingMode = PickingMode.Ignore };
            var name = NewLabel(ItemNameClass);
            texts.Add(name);
            var detail = NewLabel(ItemDetailClass);
            texts.Add(detail);
            button.Add(texts);

            var price = NewLabel(ItemPriceClass);
            button.Add(price);

            _itemButtons.Add(button);
            _itemNames.Add(name);
            _itemDetails.Add(detail);
            _itemPrices.Add(price);
            return button;
        }

        private void Close()
        {
            if (!IsOpen)
            {
                return;
            }

            _open = false;
            _pending = null;
            Unsubscribe();
            foreach (var button in _itemButtons)
            {
                button.SetEnabled(false);
            }

            _close.SetEnabled(false);
            var handlers = Closed;
            Closed = null;
            handlers?.Invoke();
        }

        private void Unsubscribe()
        {
            GameSignals.Wallet.Changed -= HandleWalletChanged;
            CombatEvents.PurchaseAnswered -= HandlePurchaseAnswered;
            Localizer.Changed -= HandleLanguageChanged;
            _navigationRoot.UnregisterCallback<NavigationCancelEvent>(HandleCancel, TrickleDown.TrickleDown);
        }

        private void RefreshTexts()
        {
            _title.text = GameText.Get(TitleKey);
            _empty.text = GameText.Get(EmptyKey);
            _close.text = GameText.Get(CloseKey);
            for (var i = 0; i < _itemButtons.Count; i++)
            {
                var item = _catalog.Items[i];
                _itemNames[i].text = GameText.Resolve(NameOf(item));
                _itemDetails[i].text = GameText.Resolve(DetailOf(item));
                _itemPrices[i].text = PriceText(item.Price);
            }

            RefreshMoney();
            RefreshStatus();
        }

        private void RefreshMoney()
        {
            var money = _wallet.HasValue ? _wallet.Value.Money : 0;
            _money.text = GameText.Format(MoneyKey, money.ToString(CultureInfo.InvariantCulture));
        }

        private void RefreshItems()
        {
            for (var i = 0; i < _itemButtons.Count; i++)
            {
                var item = _catalog.Items[i];
                var button = _itemButtons[i];
                var affordable = _wallet.HasValue && _wallet.Value.CanAfford(item.Price);
                button.EnableInClassList(UnaffordableItemClass, !affordable);
                button.EnableInClassList(PendingItemClass, ReferenceEquals(item, _pending));
                button.SetEnabled(IsOpen && _pending == null);
            }
        }

        private void ShowStatus(LocalizedText text, bool isError)
        {
            _statusSource = text;
            _statusIsError = isError;
            RefreshStatus();
        }

        private void RefreshStatus()
        {
            if (!_statusSource.HasValue)
            {
                _status.text = string.Empty;
                _status.RemoveFromClassList(ErrorStatusClass);
                _status.style.display = DisplayStyle.None;
                return;
            }

            _status.text = GameText.Resolve(_statusSource.Value);
            _status.EnableInClassList(ErrorStatusClass, _statusIsError);
            _status.style.display = DisplayStyle.Flex;
        }

        private void FocusFirst()
        {
            // PauseMenu は開くときに 1 度しかフォーカスを取らないので、ここで奪うとポーズのボタンへ戻らなくなる
            if (PauseMenu.IsAnyOpen)
            {
                return;
            }

            var target = _itemButtons.Count > 0 ? _itemButtons[0] : _close;
            target.Focus();

            // パネルに付いた直後は配置が終わっておらず Focus が効かないことがあるので、次の更新でもう一度選ぶ
            if (target.panel != null && target.focusController?.focusedElement != target)
            {
                target.schedule.Execute(() =>
                {
                    if (IsOpen && !PauseMenu.IsAnyOpen)
                    {
                        target.Focus();
                    }
                });
            }
        }

        private void HandleWalletChanged(WalletStatus? wallet)
        {
            if (!IsOpen)
            {
                return;
            }

            if (!IsBuyTime(wallet))
            {
                Debug.Log($"{LogTag} closing: the buy time is over");
                _wallet = wallet;
                Close();
                return;
            }

            _wallet = wallet;
            // 値段 0 の品は額が減らないので、送った後の財布の出し直しそのものを「買った」とみなす
            if (_pending != null && (wallet.Value.Money < _moneyAtSend || _pending.Price <= 0))
            {
                var item = _pending;
                _pending = null;
                ShowStatus(VerdictText(PurchaseVerdict.Bought, item), false);
            }

            RefreshMoney();
            RefreshItems();
        }

        private void HandlePurchaseAnswered(PurchaseAnswer answer)
        {
            ShowVerdict(answer.ItemId, answer.Verdict);
        }

        private void HandleLanguageChanged(GameLanguage language)
        {
            if (!_disposed)
            {
                RefreshTexts();
            }
        }

        private void HandleCancel(NavigationCancelEvent evt)
        {
            if (PauseMenu.IsAnyOpen || !IsOpen)
            {
                return;
            }

            evt.StopPropagation();
            _navigationRoot.focusController?.IgnoreEvent(evt);
            Cancel();
        }
    }
}
