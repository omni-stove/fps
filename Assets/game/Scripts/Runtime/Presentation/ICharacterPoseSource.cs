using Fps.Core;

namespace Fps.Runtime
{
    /// <summary>
    /// <see cref="CharacterAnimator"/> にキャラの状態を渡す部品。プレイヤー (PlayerController) でも敵 (EnemyAgent) でもないキャラ
    /// (ネット対戦の NetPlayer) が本体に付ける。Fps.Runtime は Fps.Net を参照できないので、この形で受け取る。
    /// 水平の速さは CharacterAnimator が本体の位置の変化から求めて渡す (速度が全員には同期されないため)。
    /// </summary>
    public interface ICharacterPoseSource
    {
        /// <summary>立って歩くときの水平速度 (m/s)。</summary>
        float WalkSpeed { get; }

        /// <summary>ダッシュ中の水平速度 (m/s)。</summary>
        float SprintSpeed { get; }

        /// <summary>しゃがみ中の水平速度 (m/s)。</summary>
        float CrouchSpeed { get; }

        /// <summary>今の状態を返す。</summary>
        /// <param name="horizontalSpeed">位置の変化から求めた水平の速さ (m/s)。0 以上の有限値。</param>
        CharacterPoseInput ReadPose(float horizontalSpeed);
    }
}
