using System.Globalization;
using Shooter.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace Shooter.Runtime
{
    // GameSignals.Wallet に値がある間 (経済のあるモードの試合中) だけ所持金を出す。
    [DisallowMultipleComponent]
    public sealed class WalletView : HudElementView
    {
        public const string UxmlName = "wallet";

        public const string MoneyName = "wallet-money";

        public const string MoneyClass = "wallet__money";

        // 準備の時間 (買える間) と、上限まで貯まって報酬を受け取れない間に、見た目を変えられるよう付ける。
        public const string BuyTimeClass = "wallet--buy-time";

        public const string FullClass = "wallet--full";

        public const string MoneyKey = "hud.wallet";

        private WalletStatus? _status;
        private Label _money;
        private bool _subscribed;

        public override string ElementName => UxmlName;

        public WalletStatus? Status => _status;

        public bool IsShown => _status.HasValue;

        public string MoneyText => _money != null && IsShown ? _money.text : string.Empty;

        public static string Money(int money)
        {
            return GameText.Format(MoneyKey, money.ToString(CultureInfo.InvariantCulture));
        }

        public void Show(WalletStatus? status)
        {
            _status = status;
            Refresh();
        }

        protected override void OnBound(VisualElement element)
        {
            _money = new Label(string.Empty) { name = MoneyName, pickingMode = PickingMode.Ignore };
            _money.AddToClassList(MoneyClass);
            element.Add(_money);
            Subscribe();
            Show(GameSignals.Wallet.Value);
        }

        protected override void OnLanguageChanged()
        {
            Refresh();
        }

        protected override void OnUnbinding(VisualElement element)
        {
            Unsubscribe();
            _money?.RemoveFromHierarchy();
            _money = null;
            element.RemoveFromClassList(BuyTimeClass);
            element.RemoveFromClassList(FullClass);
            element.style.display = DisplayStyle.None;
        }

        // Hud は木を作り直すときにしかビューへ要素を渡さないので、HUD を作った後に足されたときは今の木を自分で受ける
        private void Start()
        {
            if (Element != null)
            {
                return;
            }

            var document = GetComponent<UIDocument>();
            var root = document != null ? document.rootVisualElement : null;
            if (root != null && root.Q<VisualElement>(UxmlName) != null)
            {
                Bind(root);
            }
        }

        private void OnDestroy()
        {
            Unsubscribe();
        }

        private void Refresh()
        {
            var element = Element;
            if (element == null || _money == null)
            {
                return;
            }

            if (!_status.HasValue)
            {
                _money.text = string.Empty;
                element.RemoveFromClassList(BuyTimeClass);
                element.RemoveFromClassList(FullClass);
                element.style.display = DisplayStyle.None;
                return;
            }

            var status = _status.Value;
            _money.text = Money(status.Money);
            element.EnableInClassList(BuyTimeClass, status.CanBuy);
            element.EnableInClassList(FullClass, status.IsFull);
            element.style.display = DisplayStyle.Flex;
        }

        private void Subscribe()
        {
            if (_subscribed)
            {
                return;
            }

            GameSignals.Wallet.Changed += HandleWalletChanged;
            _subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!_subscribed)
            {
                return;
            }

            GameSignals.Wallet.Changed -= HandleWalletChanged;
            _subscribed = false;
        }

        private void HandleWalletChanged(WalletStatus? status)
        {
            // Edit Mode では OnDestroy が呼ばれずに消えることがあるので、消えた後に届いた通知で購読を外す
            if (this == null)
            {
                GameSignals.Wallet.Changed -= HandleWalletChanged;
                return;
            }

            Show(status);
        }
    }
}
