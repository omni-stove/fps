using UnityEngine;

namespace Fps.Runtime
{
    /// <summary>
    /// Pause の入力で開くメニュー。<see cref="Hud"/> と同じ GameObject に <see cref="Hud.Create"/> が付ける。
    /// 今は何もしない (入力の購読と、開いている間の操作の止め方は後で足す)。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Hud))]
    public sealed class PauseMenu : MonoBehaviour
    {
    }
}
