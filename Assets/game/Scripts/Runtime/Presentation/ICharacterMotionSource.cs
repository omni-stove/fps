namespace Fps.Runtime
{
    /// <summary>
    /// キャラの本体が、接地・しゃがみ・ダッシュの今の状態を演出 (<see cref="CharacterAudio"/>) に見せる。
    /// オフラインのプレイヤー (<see cref="PlayerController"/>)・敵 (<see cref="EnemyAgent"/>)・ネット対戦のプレイヤー
    /// (<c>Fps.Net.NetworkPlayerMotor</c>) が実装し、演出は相手の種類を知らずに同じ規則で音を決める。
    /// 速さは演出の側が本体の位置の差から測るので、ここには持たせない。
    /// </summary>
    public interface ICharacterMotionSource
    {
        /// <summary>足が地面に付いているか。</summary>
        bool IsGrounded { get; }

        /// <summary>しゃがんでいるか。</summary>
        bool IsCrouched { get; }

        /// <summary>立って歩くときの水平の速さ (m/s)。ダッシュ中かが分からないときに速さから推すのに使う。</summary>
        float WalkSpeed { get; }

        /// <summary>
        /// ダッシュ中かが分かれば sprinting に入れて true を返す。同期されておらず分からなければ (ネット対戦の他人) false。
        /// </summary>
        bool TryGetSprinting(out bool sprinting);
    }
}
