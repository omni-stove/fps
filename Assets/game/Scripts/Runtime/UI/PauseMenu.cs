using UnityEngine;

namespace Fps.Runtime
{
    /// <summary>
    /// Pause の入力で開くメニュー。<see cref="Hud"/> と同じ GameObject に <see cref="Hud.Create"/> が付ける。
    /// 今はメインメニューへ戻る入口 (<see cref="ReturnToMainMenu"/>) だけを持つ (入力の購読と、開いている間の操作の止め方は後で足す)。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Hud))]
    public sealed class PauseMenu : MonoBehaviour
    {
        /// <summary>
        /// メインメニューへ戻る (<see cref="MenuNavigator.ReturnToMenu"/>)。オンラインなら接続は NetBootstrap が
        /// <see cref="MenuNavigator.ReturningToMenu"/> で切る。メニューのシーンがビルドに無ければ false で、今のシーンに留まる。
        /// </summary>
        public bool ReturnToMainMenu()
        {
            return MenuNavigator.ReturnToMenu();
        }
    }
}
