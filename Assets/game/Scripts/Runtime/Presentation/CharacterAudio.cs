using UnityEngine;

namespace Fps.Runtime
{
    /// <summary>
    /// キャラの音 (足音・被弾の声) を受け持つ。プレイヤー・NetPlayer・敵の本体に付ける。
    /// 銃声・着弾・リロードの音は <see cref="WeaponPresentation"/> が鳴らす。
    /// 今は何もしない (AudioSource を持たせて鳴らす処理は後で足す)。
    /// 専用サーバーでは <c>Fps.Net.HeadlessPresentation</c> が止める。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CharacterAudio : MonoBehaviour
    {
    }
}
