using UnityEngine;

namespace Fps.Runtime
{
    /// <summary>
    /// 武器の見た目の演出 (マズルフラッシュ・弾道・着弾の跡) を受け持つ。撃つ人 (プレイヤー・NetPlayer・敵) の本体に付ける。
    /// 今は何もしない (<see cref="CombatEvents.ShotFired"/> と <see cref="CombatEvents.Impact"/> の購読は後で足す)。
    /// 専用サーバーでは <c>Fps.Net.HeadlessPresentation</c> が止める。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class WeaponPresentation : MonoBehaviour
    {
    }
}
