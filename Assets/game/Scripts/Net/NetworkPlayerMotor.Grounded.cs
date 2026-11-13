using Fps.Runtime;

namespace Fps.Net
{
    /// <summary>
    /// <see cref="NetworkPlayerMotor"/> のうち、演出 (<see cref="CharacterAudio"/>) に見せる接地・しゃがみ・ダッシュ (<see cref="ICharacterMotionSource"/>)。
    /// 接地・しゃがみ・歩きの速さは本体の <see cref="IsGrounded"/> などをそのまま使う。
    /// ダッシュはオーナーの入力にしか無く同期していないので、他人では分からないと答える (演出が速さから推す)。
    /// </summary>
    public sealed partial class NetworkPlayerMotor : ICharacterMotionSource
    {
        /// <summary>オーナー (予測するクライアントとホスト) ならダッシュ中かを入れて true。それ以外は分からないので false。</summary>
        bool ICharacterMotionSource.TryGetSprinting(out bool sprinting)
        {
            sprinting = IsSprinting;
            return _role == NetworkPlayerRole.HostOwner || _role == NetworkPlayerRole.Predicted;
        }
    }
}
