using UnityEngine;

namespace Fps.Runtime
{
    /// <summary>
    /// <see cref="PlayerController"/> のうち、空中から接地に変わった (着地した) ときの処理。
    /// </summary>
    public sealed partial class PlayerController
    {
        /// <summary>
        /// 着地した <see cref="Tick"/> で呼ばれる。fallSpeed は着地したときの下向きの速さ (m/s、0 以上)。
        /// 初期化直後の最初の接地は、空中から降りたのか分からないので着地として扱わない。
        /// </summary>
        partial void OnLanded(float fallSpeed);

        // 接地したフレームの PlayerMotor.Velocity は、まだ接地中の吸着の速度に戻る前の落下速度を持っている
        private void ReportLanding(bool grounded)
        {
            if (_lastGrounded == false && grounded)
            {
                OnLanded(Mathf.Max(0f, -_motor.Velocity.Y));
            }
        }
    }
}
