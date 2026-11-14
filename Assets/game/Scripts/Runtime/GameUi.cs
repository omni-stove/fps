using System;
using Fps.Core;
using UnityEngine;

namespace Fps.Runtime
{
    /// <summary>
    /// ゲーム進行が画面を作るときの登録口。HUD は <see cref="HudFactory"/> の生成役が作る。
    /// 既定の生成役は UI のアセンブリ (Fps.Runtime.UI) が起動時に <see cref="UseDefaultHudFactory"/> で登録し、使う側は <see cref="HudFactory"/> に
    /// 自分の生成役を入れると差し替えられる (null を入れると既定に戻る)。
    /// ポーズ (<see cref="Pause"/>)・ロードアウトの選択 (<see cref="LoadoutSelect"/>)・メニューとの行き来 (<see cref="Menu"/>) も、
    /// 同じく UI のコードが UseDefault… で既定を登録し、プロパティに入れると差し替えられる。
    /// </summary>
    public static class GameUi
    {
        public const string LogTag = "[GameUi]";

        private static IHudFactory _hudFactory;
        private static IHudFactory _defaultHudFactory;
        private static IPauseState _pause;
        private static IPauseState _defaultPause;
        private static ILoadoutSelect _loadoutSelect;
        private static ILoadoutSelect _defaultLoadoutSelect;
        private static IMenuNavigator _menu;
        private static IMenuNavigator _defaultMenu;

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

        /// <summary>既定のメニューとの行き来を menu にする。<see cref="Menu"/> を差し替えていればそちらを使い続ける。</summary>
        /// <exception cref="ArgumentNullException">menu が null。</exception>
        public static void UseDefaultMenu(IMenuNavigator menu)
        {
            _defaultMenu = menu ?? throw new ArgumentNullException(nameof(menu));
        }

        // 既定の UI のアセンブリが無い状態は、テストで登録済みの既定を外さないと作れない。使う側は差し替えれば済むので公開しない
        internal static void ClearDefaults()
        {
            _defaultHudFactory = null;
            _defaultPause = null;
            _defaultLoadoutSelect = null;
            _defaultMenu = null;
        }

        /// <summary>HUD を作る生成役。差し替えていなければ既定の生成役。どちらも無ければ null。</summary>
        public static IHudFactory HudFactory
        {
            get => _hudFactory ?? _defaultHudFactory;
            set => _hudFactory = value;
        }

        /// <summary>
        /// 既定の生成役を factory にする。<see cref="HudFactory"/> を差し替えていればそちらを使い続ける
        /// (起動時の登録の順番に依らず、差し替えが既定に上書きされないよう、既定は別に持つ)。
        /// </summary>
        /// <exception cref="ArgumentNullException">factory が null。</exception>
        public static void UseDefaultHudFactory(IHudFactory factory)
        {
            _defaultHudFactory = factory ?? throw new ArgumentNullException(nameof(factory));
        }

        /// <summary>
        /// <see cref="HudFactory"/> で HUD を作る。生成役が登録されていなければ null を返す
        /// (既定の UI のアセンブリ Fps.Runtime.UI を入れずに使っても、例外で止めずに HUD 無しで進めるため)。
        /// </summary>
        /// <exception cref="InvalidOperationException">登録された生成役が null を返した。</exception>
        public static IHud CreateHud(string objectName, Damageable player, WeaponController weapon, Match match)
        {
            var factory = HudFactory;
            if (factory == null)
            {
                return null;
            }

            var hud = factory.Create(objectName, player, weapon, match);
            if (hud == null)
            {
                throw new InvalidOperationException($"{factory.GetType().Name}.Create returned no HUD");
            }

            return hud;
        }

        /// <summary>
        /// <see cref="HudFactory"/> で player にデバッグ表示を付ける。生成役が無ければ付けずに false
        /// (デバッグ用の表示なので、無くてもゲームは進める)。
        /// </summary>
        public static bool AttachActionDebug(GameObject player, PlayerActionReader reader)
        {
            var factory = HudFactory;
            if (factory == null)
            {
                return false;
            }

            factory.AttachActionDebug(player, reader);
            return true;
        }

        /// <summary>今のシーンで有効な HUD を 1 つ探す。無ければ null。</summary>
        public static IHud FindHud()
        {
            foreach (var behaviour in UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None))
            {
                if (behaviour is IHud hud)
                {
                    return hud;
                }
            }

            return null;
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
