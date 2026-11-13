using UnityEngine;

namespace Fps.Net
{
    /// <summary>
    /// <see cref="NetworkPlayerMotor"/> のうち、サーバーで確定した移動の着地 (空中から接地に変わった tick) の処理。
    /// </summary>
    public sealed partial class NetworkPlayerMotor
    {
        /// <summary>
        /// サーバー (ホスト自身のプレイヤーを含む) で、確定した tick の移動で着地したときに呼ばれる。
        /// fallSpeed は着地したときの下向きの速さ (m/s、0 以上)。オーナーの予測と再シミュレーションでは呼ばれない。
        /// 出現・テレポートの直後は接地していない状態から始まるので、最初の接地でもごく小さい fallSpeed で呼ばれる。
        /// </summary>
        partial void OnServerLanded(int tick, float fallSpeed);

        // 着地した tick の確定状態の速度は、まだ接地中の吸着の速度に戻る前の落下速度を持っている
        private void ReportServerLanding(int tick, bool wasGrounded, MotorState state)
        {
            if (!wasGrounded && state.IsGrounded)
            {
                OnServerLanded(tick, Mathf.Max(0f, -state.Velocity.y));
            }
        }
    }
}
