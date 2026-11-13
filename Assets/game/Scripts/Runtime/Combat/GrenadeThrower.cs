using UnityEngine;

namespace Fps.Runtime
{
    /// <summary>
    /// Grenade の入力で手榴弾を投げる。プレイヤーと NetPlayer の本体に付ける。
    /// 今は何もしない (入力の購読と、投げた手榴弾の爆発は後で足す)。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GrenadeThrower : MonoBehaviour
    {
    }
}
