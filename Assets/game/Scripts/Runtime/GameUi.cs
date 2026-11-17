using System;
using Shooter.Core;
using UnityEngine;

namespace Shooter.Runtime
{
    /// <summary>
    /// UI からゲーム進行への口の登録口。ポーズ (<see cref="Pause"/>)・ロードアウトの選択 (<see cref="LoadoutSelect"/>)・持ち物 (<see cref="InventoryScreen"/>)・
    /// メニューとの行き来 (<see cref="Menu"/>) は、UI のコードが UseDefault… で既定を登録し、プロパティに入れると差し替えられる (null を入れると既定に戻る)。
    /// HUD はゲーム進行からは作らず、UI が <see cref="GameSignals"/> を見て自分で作る。
    /// </summary>
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

        /// <summary>ポーズ画面の開閉の様子。差し替えていなければ既定。どちらも無ければ null。</summary>
        public static IPauseState Pause
        {
            get => _pause ?? _defaultPause;
            set => _pause = value;
        }

        /// <summary>ロードアウトを選ぶ画面の開閉の様子。差し替えていなければ既定。どちらも無ければ null。</summary>
        public static ILoadoutSelect LoadoutSelect
        {
            get => _loadoutSelect ?? _defaultLoadoutSelect;
            set => _loadoutSelect = value;
        }

        /// <summary>メインメニューとの行き来。差し替えていなければ既定。どちらも無ければ null。</summary>
        public static IMenuNavigator Menu
        {
            get => _menu ?? _defaultMenu;
            set => _menu = value;
        }

        /// <summary>どれかのポーズ画面が開いているか。<see cref="Pause"/> が無ければ false (開ける画面が無い)。</summary>
        public static bool IsPauseOpen
        {
            get
            {
                var pause = Pause;
                return pause != null && pause.IsAnyOpen;
            }
        }

        /// <summary>カーソルを解放して開いているロードアウトの画面があるか。<see cref="LoadoutSelect"/> が無ければ false。</summary>
        public static bool IsLoadoutSelectOpen
        {
            get
            {
                var loadoutSelect = LoadoutSelect;
                return loadoutSelect != null && loadoutSelect.IsAnyOpen;
            }
        }

        /// <summary>既定のポーズの様子を pause にする。<see cref="Pause"/> を差し替えていればそちらを使い続ける。</summary>
        /// <exception cref="ArgumentNullException">pause が null。</exception>
        public static void UseDefaultPause(IPauseState pause)
        {
            _defaultPause = pause ?? throw new ArgumentNullException(nameof(pause));
        }

        /// <summary>既定のロードアウトの選択の様子を loadoutSelect にする。<see cref="LoadoutSelect"/> を差し替えていればそちらを使い続ける。</summary>
        /// <exception cref="ArgumentNullException">loadoutSelect が null。</exception>
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

        // 通信の手順 (Shooter.NetV2) は UI のアセンブリを参照しないので、メニューが読むこの置き場に入れさせる。
        public static IOnlineMatchStarter OnlineStarter { get; set; }

        // ドメインの再読み込みを切った Play でも、前の Play のクライアントへメニューのボタンをつながない。
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnlineStarter()
        {
            OnlineStarter = null;
        }

        /// <summary>既定のメニューとの行き来を menu にする。<see cref="Menu"/> を差し替えていればそちらを使い続ける。</summary>
        /// <exception cref="ArgumentNullException">menu が null。</exception>
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
        }

        /// <summary>
        /// ui が使えるか。Unity のオブジェクトが実装しているときは破棄されていないかも見る
        /// (インターフェースの型のまま <c>!= null</c> で比べると、破棄済みでも true になるため)。
        /// </summary>
        public static bool IsAlive(object ui)
        {
            return ui is UnityEngine.Object unityObject ? unityObject != null : ui != null;
        }
    }
}
