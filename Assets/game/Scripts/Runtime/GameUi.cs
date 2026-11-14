using System;
using Fps.Core;
using UnityEngine;

namespace Fps.Runtime
{
    /// <summary>
    /// ゲーム進行が画面を作るときの登録口。HUD は <see cref="HudFactory"/> の生成役が作る。
    /// 既定の生成役は UI のコードが起動時に <see cref="UseDefaultHudFactory"/> で登録し、使う側は <see cref="HudFactory"/> に
    /// 自分の生成役を入れると差し替えられる (null を入れると既定に戻る)。
    /// </summary>
    public static class GameUi
    {
        public const string LogTag = "[GameUi]";

        private static IHudFactory _hudFactory;
        private static IHudFactory _defaultHudFactory;

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

        /// <summary><see cref="HudFactory"/> で HUD を作る。</summary>
        /// <exception cref="InvalidOperationException">生成役が登録されていないか、生成役が null を返した。</exception>
        public static IHud CreateHud(string objectName, Damageable player, WeaponController weapon, Match match)
        {
            var factory = HudFactory;
            if (factory == null)
            {
                throw new InvalidOperationException(
                    "no HUD factory is registered; register one with GameUi.HudFactory (the default UI registers itself before the scene loads)");
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
