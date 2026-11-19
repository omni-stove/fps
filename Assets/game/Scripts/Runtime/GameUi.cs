using System;
using Shooter.Core;
using UnityEngine;

namespace Shooter.Runtime
{
    public static class GameUi
    {
        public const string LogTag = "[GameUi]";

        private static IPauseState _pause;
        private static IPauseState _defaultPause;
        private static ILoadoutSelect _loadoutSelect;
        private static ILoadoutSelect _defaultLoadoutSelect;
        private static IMenuNavigator _menu;
        private static IMenuNavigator _defaultMenu;
        private static IInventoryScreen _inventoryScreen;
        private static IInventoryScreen _defaultInventoryScreen;
        private static IBuyMenuScreen _buyMenuScreen;
        private static IBuyMenuScreen _defaultBuyMenuScreen;

        public static IPauseState Pause
        {
            get => _pause ?? _defaultPause;
            set => _pause = value;
        }

        public static ILoadoutSelect LoadoutSelect
        {
            get => _loadoutSelect ?? _defaultLoadoutSelect;
            set => _loadoutSelect = value;
        }

        public static IMenuNavigator Menu
        {
            get => _menu ?? _defaultMenu;
            set => _menu = value;
        }

        public static bool IsPauseOpen
        {
            get
            {
                var pause = Pause;
                return pause != null && pause.IsAnyOpen;
            }
        }

        public static bool IsLoadoutSelectOpen
        {
            get
            {
                var loadoutSelect = LoadoutSelect;
                return loadoutSelect != null && loadoutSelect.IsAnyOpen;
            }
        }

        public static void UseDefaultPause(IPauseState pause)
        {
            _defaultPause = pause ?? throw new ArgumentNullException(nameof(pause));
        }

        public static void UseDefaultLoadoutSelect(ILoadoutSelect loadoutSelect)
        {
            _defaultLoadoutSelect = loadoutSelect ?? throw new ArgumentNullException(nameof(loadoutSelect));
        }

        public static IInventoryScreen InventoryScreen
        {
            get => _inventoryScreen ?? _defaultInventoryScreen;
            set => _inventoryScreen = value;
        }

        public static bool IsInventoryOpen
        {
            get
            {
                var screen = InventoryScreen;
                return screen != null && screen.IsAnyOpen;
            }
        }

        public static void UseDefaultInventoryScreen(IInventoryScreen screen)
        {
            _defaultInventoryScreen = screen ?? throw new ArgumentNullException(nameof(screen));
        }

        public static IBuyMenuScreen BuyMenuScreen
        {
            get => _buyMenuScreen ?? _defaultBuyMenuScreen;
            set => _buyMenuScreen = value;
        }

        public static bool IsBuyMenuOpen
        {
            get
            {
                var screen = BuyMenuScreen;
                return screen != null && screen.IsAnyOpen;
            }
        }

        public static void UseDefaultBuyMenuScreen(IBuyMenuScreen screen)
        {
            _defaultBuyMenuScreen = screen ?? throw new ArgumentNullException(nameof(screen));
        }

        // 通信の手順 (Shooter.NetV2) は UI のアセンブリを参照しないので、メニューが読むこの置き場に入れさせる。
        public static IOnlineMatchStarter OnlineStarter { get; set; }

        // ドメインの再読み込みを切った Play でも、前の Play のクライアントへメニューのボタンをつながない。
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnlineStarter()
        {
            OnlineStarter = null;
        }

        public static void UseDefaultMenu(IMenuNavigator menu)
        {
            _defaultMenu = menu ?? throw new ArgumentNullException(nameof(menu));
        }

        // 既定の UI のアセンブリが無い状態は、テストで登録済みの既定を外さないと作れない。使う側は差し替えれば済むので公開しない
        internal static void ClearDefaults()
        {
            _defaultPause = null;
            _defaultLoadoutSelect = null;
            _defaultMenu = null;
            _defaultInventoryScreen = null;
            _defaultBuyMenuScreen = null;
        }

        // インターフェースの型のまま != null で比べると破棄済みでも true になるので、Unity のオブジェクトなら破棄されていないかも見る。
        public static bool IsAlive(object ui)
        {
            return ui is UnityEngine.Object unityObject ? unityObject != null : ui != null;
        }
    }
}
