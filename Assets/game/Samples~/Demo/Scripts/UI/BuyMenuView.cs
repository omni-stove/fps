using System;
using Shooter.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace Shooter.Runtime
{
    // 準備の時間 (GameSignals.Wallet.CanBuy) の間だけ開ける購入画面。開くキーと HUD への取り付けは置く側が決める。
    [DisallowMultipleComponent]
    public sealed class BuyMenuView : HudElementView
    {
        // Hud.uxml には自分の枠を足さず、HUD の根の中に枠をコードで作る。HUD を手で組み替えたゲームでも、根さえあれば出せるようにするため。
        public const string UxmlName = "hud";

        public const string PanelName = "buy-menu";

        [SerializeField, Min(0.1f), Tooltip("Seconds to wait for the wallet to drop after sending a purchase before saying it was not bought")]
        private float _answerSeconds = BuyMenuPanel.DefaultAnswerSeconds;

        private VisualElement _host;
        private BuyMenuPanel _panel;
        private BuyCatalog _catalog;
        private bool _wantsOpen;
        private bool _restoreCursor;
        private static int _openCursorCount;

        public static bool IsAnyOpen => _openCursorCount > 0;

        public override string ElementName => UxmlName;

        public bool IsOpen => _panel != null && _panel.IsOpen;

        public BuyMenuPanel Panel => _panel;

        public VisualElement Host => _host;

        public float AnswerSeconds
        {
            get => _answerSeconds;
            set
            {
                if (value <= 0f || float.IsNaN(value) || float.IsInfinity(value))
                {
                    throw new ArgumentOutOfRangeException(nameof(value), value, "answer seconds must be a positive number of seconds");
                }

                _answerSeconds = value;
            }
        }

        public event Action Closed;

        // 準備の時間でなければ開かずに false を返す。catalog が null なら調整値 (KitTuning.Economy) の一覧を出す。
        public bool Open(BuyCatalog catalog = null)
        {
            if (!BuyMenuPanel.IsBuyTime(GameSignals.Wallet.Value))
            {
                Debug.Log($"{BuyMenuPanel.LogTag} not opening: it is not the buy time", this);
                return false;
            }

            _catalog = catalog;
            _wantsOpen = true;
            Rebuild();
            return IsOpen;
        }

        public void Close()
        {
            if (!_wantsOpen)
            {
                return;
            }

            _wantsOpen = false;
            _catalog = null;
            DisposePanel();
            RestoreCursor();
            Closed?.Invoke();
        }

        // 開いていれば閉じ、閉じていれば開く。開くキーに割り当てる側が使う。
        public bool Toggle(BuyCatalog catalog = null)
        {
            if (IsOpen)
            {
                Close();
                return false;
            }

            return Open(catalog);
        }

        protected override void OnBound(VisualElement element)
        {
            _host = new VisualElement { name = PanelName };
            _host.style.display = DisplayStyle.None;
            element.Add(_host);
            Rebuild();
        }

        protected override void OnUnbinding(VisualElement element)
        {
            DisposePanel();
            _host?.RemoveFromHierarchy();
            _host = null;
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

        private void Update()
        {
            _panel?.Tick(Time.unscaledDeltaTime);
        }

        private void OnDisable()
        {
            Close();
        }

        private void OnDestroy()
        {
            DisposePanel();
            RestoreCursor();
        }

        private void Rebuild()
        {
            DisposePanel();
            if (_host == null)
            {
                return;
            }

            // HUD を作り直す間に準備の時間が終わっていれば、開き直さずに閉じる
            if (!_wantsOpen || !BuyMenuPanel.IsBuyTime(GameSignals.Wallet.Value))
            {
                if (_wantsOpen)
                {
                    Close();
                }

                return;
            }

            _panel = new BuyMenuPanel(_host, _catalog, _answerSeconds);
            _panel.Closed += HandlePanelClosed;
            _host.style.display = DisplayStyle.Flex;
            ReleaseCursor();
        }

        private void DisposePanel()
        {
            if (_host != null)
            {
                _host.style.display = DisplayStyle.None;
            }

            if (_panel == null)
            {
                return;
            }

            _panel.Dispose();
            _panel = null;
        }

        private void HandlePanelClosed()
        {
            Close();
        }

        private void ReleaseCursor()
        {
            // Edit Mode のテストでエディタのカーソルを書き換えない
            if (!Application.isPlaying || _restoreCursor)
            {
                return;
            }

            _restoreCursor = true;
            _openCursorCount++;
            UnityEngine.Cursor.lockState = CursorLockMode.None;
            UnityEngine.Cursor.visible = true;
        }

        private void RestoreCursor()
        {
            if (!_restoreCursor)
            {
                return;
            }

            _restoreCursor = false;
            _openCursorCount = Math.Max(0, _openCursorCount - 1);

            // 開いている間に視点操作が切り替わることがあるので、控えた値は戻さず決め直す。ポーズ中は閉じるときに PauseMenu が決め直す
            if (!PauseMenu.IsAnyOpen)
            {
                FirstPersonLook.RefreshCursorLock();
            }
        }
    }
}
