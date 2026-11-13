using UnityEngine;

namespace Fps.Runtime
{
    /// <summary>
    /// キャラの動き (歩き・しゃがみ・撃つ・倒れる) を見た目のアニメーションに写す。プレイヤー・NetPlayer・敵の本体に付ける。
    /// 今は何もしない (Animator を持たせて状態を渡す処理は後で足す)。
    /// 専用サーバーでは <c>Fps.Net.HeadlessPresentation</c> が止める。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CharacterAnimator : MonoBehaviour
    {
    }
}
