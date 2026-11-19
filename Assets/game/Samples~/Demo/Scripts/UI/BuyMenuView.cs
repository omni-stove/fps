using System;
using System.Collections.Generic;
using Shooter.Core;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace Shooter.Runtime
{
    // 準備の時間 (GameSignals.Wallet.CanBuy) の間だけ開ける購入画面。持ち物の画面と同じキー (ToggleActionPath) で開け閉めする。
    [DisallowMultipleComponent]
    public sealed class BuyMenuView : HudElementView
    {
        // Hud.uxml には自分の枠を足さず、HUD の根の中に枠をコードで作る。HUD を手で組み替えたゲームでも、根さえあれば出せるようにするため。
        public const string UxmlName = "hud";

        public const string PanelName = "buy-menu";

        // 購入専用のキーを足さないのは、準備の時間は持ち物を並べ替える場面ではなく、覚えるキーを増やさずに済むため。
        public const string ToggleActionPath = InputActionPaths.Inventory;

        private const string PauseMenuName = "pause-menu";

        [SerializeField, Min(0.1f), Tooltip("Seconds to wait for the answer to a purchase before saying it was not bought")]
        private float _answerSeconds = BuyMenuPanel.DefaultAnswerSeconds;

        private readonly List<InputAction> _suspended = new List<InputAction>();
        private VisualElement _host;
        private BuyMenuPanel _panel;
        private BuyCatalog _catalog;
        private bool _wantsOpen;
        private bool _restoreCursor;
        [NonSerialized]
        private InputAction _toggleAction;
        private bool _resolvedToggleAction;
        private bool _enabledToggleAction;
        private bool _listening;
        private bool _inventoryWasOpen;
        private static int _openCursorCount;
        private static int _listeningCount;

        public static bool IsAnyOpen => _openCursorCount > 0;

        // 同じキーで持ち物の画面まで重ねて開かないよう、持ち物の画面はこれが true の間キーを譲る
        public static bool TakesToggleKey => _listeningCount > 0 && BuyMenuPanel.IsBuyTime(GameSignals.Wallet.Value);

        public InputAction ToggleAction => _toggleAction;

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
            ResumePlayerActions();
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
            // 一時停止の画面より奥に置くため、末尾ではなくその直前に差し込む
            var pause = element.Q<VisualElement>(PauseMenuName);
            if (pause != null && pause.parent == element)
            {
                element.Insert(element.IndexOf(pause), _host);
            }
            else
            {
                element.Add(_host);
            }

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

        private void OnEnable()
        {
            AcquireToggleAction();
            if (!_listening)
            {
                _listening = true;
                _listeningCount++;
            }
        }

        private void Update()
        {
            // 一時停止を閉じると PauseMenu がカーソルをロックし直すので、開いたまま下に残さず閉じる (持ち物の画面と同じ)
            if (IsOpen && GameUi.IsPauseOpen)
            {
                Close();
            }

            _panel?.Tick(Time.unscaledDeltaTime);

            if (_toggleAction != null && _toggleAction.WasPressedThisFrame() && CanUseToggleKey())
            {
                Toggle();
            }
        }

        private void OnDisable()
        {
            Close();
            ReleaseToggleAction();
            StopListening();
        }

        private void OnDestroy()
        {
            DisposePanel();
            ResumePlayerActions();
            RestoreCursor();
            StopListening();
        }

        // 持ち物の画面と購入画面の Update の順は決まらないので、同じ押下で持ち物の画面を閉じた側が先に走っても開かないよう、前のフレームの終わりの状態を見る
        private void LateUpdate()
        {
            _inventoryWasOpen = GameUi.IsInventoryOpen;
        }

        private bool CanUseToggleKey()
        {
            if (GameUi.IsPauseOpen || GameUi.IsLoadoutSelectOpen || GameUi.IsInventoryOpen || _inventoryWasOpen || PlayerBindings.IsCapturingInput)
            {
                return false;
            }

            return IsOpen || BuyMenuPanel.IsBuyTime(GameSignals.Wallet.Value);
        }

        private void StopListening()
        {
            if (!_listening)
            {
                return;
            }

            _listening = false;
            _listeningCount = Math.Max(0, _listeningCount - 1);
        }

        private void AcquireToggleAction()
        {
            if (!Application.isPlaying)
            {
                return;
            }

            if (!_resolvedToggleAction)
            {
                _toggleAction = InputSystem.actions != null ? InputSystem.actions.FindAction(ToggleActionPath) : null;
                _resolvedToggleAction = true;
                if (_toggleAction == null)
                {
                    Debug.LogError($"{BuyMenuPanel.LogTag} the project actions have no '{ToggleActionPath}'; the buy menu cannot be opened with a key", this);
                }
            }

            if (_toggleAction == null || _toggleAction.enabled)
            {
                return;
            }

            _toggleAction.Enable();
            _enabledToggleAction = true;
        }

        private void ReleaseToggleAction()
        {
            if (_enabledToggleAction && _toggleAction != null)
            {
                _toggleAction.Disable();
            }

            _enabledToggleAction = false;
        }

        // 開いている間に品を押すクリックで撃ったり視点が回ったりしないよう、開け閉めと一時停止以外の Player のアクションを止める
        private void SuspendPlayerActions()
        {
            var actions = InputSystem.actions;
            var map = actions != null ? actions.FindActionMap(PlayerBindings.MapName) : null;
            if (!Application.isPlaying || map == null || _suspended.Count > 0)
            {
                return;
            }

            var pause = actions.FindAction(InputActionPaths.Pause);
            foreach (var action in map.actions)
            {
                if (!action.enabled || action == _toggleAction || action == pause)
                {
                    continue;
                }

                action.Disable();
                _suspended.Add(action);
            }
        }

        private void ResumePlayerActions()
        {
            foreach (var action in _suspended)
            {
                if (!action.enabled)
                {
                    action.Enable();
                }
            }

            _suspended.Clear();
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
            SuspendPlayerActions();
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
