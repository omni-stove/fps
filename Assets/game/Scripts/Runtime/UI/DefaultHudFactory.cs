using System;
using Fps.Core;
using UnityEngine;

namespace Fps.Runtime
{
    /// <summary>
    /// 既定の HUD の生成役。<see cref="Hud.Create"/> で HUD を作り、プレイヤーには <see cref="ActionDebugOverlay"/> を付ける。
    /// シーンを読み込む前 (エディターでは読み込み時にも) に <see cref="GameUi.UseDefaultHudFactory"/> で登録する。
    /// </summary>
    public sealed class DefaultHudFactory : IHudFactory
    {
        public IHud Create(string objectName, Damageable player, WeaponController weapon, Match match)
        {
            return Hud.Create(objectName, player, weapon, match);
        }

        public void AttachActionDebug(GameObject player, PlayerActionReader reader)
        {
            if (player == null)
            {
                throw new ArgumentNullException(nameof(player));
            }

            var overlay = player.GetComponent<ActionDebugOverlay>();
            if (overlay == null)
            {
                overlay = player.AddComponent<ActionDebugOverlay>();
            }

            overlay.Bind(reader);
        }

        // Edit Mode (テストやシーン生成) では Play の初期化が走らないので、エディターの読み込みでも登録する。
#if UNITY_EDITOR
        [UnityEditor.InitializeOnLoadMethod]
#endif
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Register()
        {
            GameUi.UseDefaultHudFactory(new DefaultHudFactory());
        }
    }
}
